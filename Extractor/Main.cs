using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using MoreLinq.Extensions;
using SewerSystem.Contracts;

[assembly: InternalsVisibleTo("Extractor.Tests")]

namespace Extractor
{
    internal static class Regexes
    {
        internal static readonly Regex Basin = new Regex(@"^BÁCIA: .*$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        internal static readonly Regex Collection = new Regex(@"^COLETOR: \d+$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        
        #region StationNames
        internal static readonly Regex Manhole = new Regex(@"^SB\d+-\d$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        internal static readonly Regex Intermediate = new Regex(@"^\d+-\d+E\d+$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        internal static readonly Regex PipeDrop = new Regex(@"^TQ\s\d(?:[.,]\d+)?$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        internal static readonly Regex DropPipe = new Regex(@"^DG\s\d(?:[.,]\d+)?$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        #endregion
    }
    public class Main : IExtractor
    {
        private enum Headers
        {
            Manhole,
            TerrainLevel,
            InvertLevel,
            Depth,
            CollectionSegment,
            PipeLengthDiameter,
            Slope,
            ProgressiveLength,
            FlowRate,
            Material,
            Intermediates
        }

        private static readonly Dictionary<Headers, string> StationHeaders = new()
        {
            { Headers.Manhole, "PVM" },
            { Headers.TerrainLevel, "CTM" },
            { Headers.InvertLevel, "CCM" },
            { Headers.Depth, "PRFM" },
        };

        private static readonly Dictionary<Headers, string> MixHeaders = new()
        {
            { Headers.Intermediates, "ESTACAS" },
        };

    private static readonly Dictionary<Headers, string> SegmentHeaders = new()
        {
            { Headers.CollectionSegment, "COL-TRE" },
            { Headers.PipeLengthDiameter, "COMP-DIAM" },
            { Headers.Slope, "DECLIV" },
            { Headers.ProgressiveLength, "ACUMUL." },
            { Headers.FlowRate, "VAZÃO (l/s)" },
            { Headers.Material, "MATERIAL" },
        };

        private static readonly HashSet<string> Ignore = new(){"OBS", "CTJ", "CCJ", "PRFJ"};

        /// <summary>
        /// This functions splits a HORIZONTAL table (Header -> Data -> Header -> Data -> ...)
        /// into two separate lists, a Section table and a Station table
        /// </summary>
        /// <param name="data"></param>
        /// <returns>A Section and Station table respectively in horizontal order.</returns>
        private static (List<TextEntity>, List<TextEntity>) SplitData(TextEntity[] data)
        {
            var sectionData = new List<TextEntity>(data.Length);
            var stationData = new List<TextEntity>(data.Length);
            var shouldCapture = false;

            foreach (var item in data)
            {
                if (!shouldCapture && item.Content.Equals(SegmentHeaders[Headers.CollectionSegment],
                        StringComparison.InvariantCultureIgnoreCase))
                {
                    shouldCapture = true;
                }

                if (shouldCapture)
                {
                    if (item.Content.Equals(SegmentHeaders[Headers.Intermediates], StringComparison.InvariantCultureIgnoreCase))
                    {
                        shouldCapture = false;
                        stationData.Add(item);
                    }
                    else
                    {
                        sectionData.Add(item);
                    }
                }
                else
                {
                    stationData.Add(item);
                }
            }
            
            return (sectionData, stationData);
        }

        
        /// <summary>
        /// This function takes the raw data from SewerSystem and separates them
        /// into SectionData and StationData.
        /// </summary>
        /// <param name="rawData"></param>
        /// <returns></returns>
        /// <exception cref="System.IndexOutOfRangeException">
        /// Should the Station Table be incomplete, the Batch will fail
        /// </exception>
        internal static (TextEntity[][], TextEntity[][]) GetSectionStationData(TextEntity[] rawData)
        {
            var data = rawData
                .OrderByDescending(x => x.Coordinates.Y)
                .ThenBy(x => x.Coordinates.X)
                .SkipWhile(text =>
                    text.Content.Equals(StationHeaders[Headers.Manhole], StringComparison.InvariantCultureIgnoreCase))
                .TakeWhile(text => !Regexes.Basin.IsMatch(text.Content))
                .Where(text => !Ignore.Contains(text.Content))
                .ToArray();

            var (sectionData, stationData) = SplitData(data);

            var sectionDataVertical = stationData
                .OrderBy(x => x.Coordinates.X)
                .ThenByDescending(x => x.Coordinates.Y)
                .Skip(SegmentHeaders.Count)
                .Batch(SegmentHeaders.Count);
            
            if (sectionDataVertical.Any(batch => batch.Length != SegmentHeaders.Count))
            {
                throw new InvalidOperationException($"Not all sections have {SegmentHeaders.Count} elements.");
            }

            var stationDataVertical = stationData
                .OrderBy(x => x.Coordinates.X)
                .ThenByDescending(x => x.Coordinates.Y)
                .Skip(StationHeaders.Count + MixHeaders.Count)
                .Batch(StationHeaders.Count)
                .Select(t =>
                {
                    if (t.Length >= StationHeaders.Count)
                    {
                        return Regexes.Intermediate.IsMatch(t[^1].Content) ? [t[^1], t[0], t[1], t[2]] : t;
                    }

                    throw new InvalidOperationException(
                        $"The function expected {StationHeaders.Count} elements, but got {t.Length}.");
                });

            return (stationDataVertical.ToArray(), sectionDataVertical.ToArray());
        }

        /// <summary>
        /// This should check for the following errors:
        ///
        /// 1. Incorrect Station Data Length, this uses the StationHeaders.Count for validation
        /// 2. Unknown station type. It checks if the ID is one of the following: Manhole, Intermediate, StepDrop, PipeDrop
        /// 3. Incorrect Level format (for both Terrain level and Invert level) it accepts both "," and "." (e.g. 123.123 or 123,123)
        /// 4. Depth mismatch. If the depth from the table is different from the subtraction of Terrain Level and invert level, it's considered and error.
        /// 5. Incorrect start or end. The First Station and the Last Station have to be Manholes.
        ///
        /// These tests and meant to be simple, just checking the data structure, not if the manholes are connected probably or not, example, if
        /// a manhole is connecting above the downstream manhole, this is not checked here.
        ///
        /// The only check that is kinda different is the Depth mismatch, this might be removed from here and join another set of tests that
        /// checks the connections.
        /// </summary>
        /// <param name="stationData">The section data table</param>
        /// <returns>A list of found errors</returns>
        private static List<string> ValidateStationData(TextEntity[][] stationData)
        {
            List<string> errors = new List<string>();
            
            string errorMessage;

            stationData.ForEach(x =>
            {
                var curStationLen = x.Length;   
                if (curStationLen != StationHeaders.Count)
                {
                    errorMessage = $"The station data should have {StationHeaders.Count}, but got  {x.Length}.\nDetected data:";

                    for (var i = 0; i < curStationLen; i++)
                    {
                        errorMessage += $"\n{x[i]}";
                    }
                    
                    errorMessage += $"We can't check for other errors related to this Station since its incomplete. Please, review it.";
                    errors.Add(errorMessage);
                    return;
                }
                
                var (id, terrainLevel, invertLevel, depth) = (x[0].Content, x[1].Content, x[2].Content, x[3].Content);

                if (!(Regexes.Manhole.IsMatch(id) || Regexes.Intermediate.IsMatch(id) || Regexes.DropPipe.IsMatch(id) ||
                      Regexes.PipeDrop.IsMatch(id)))
                {
                    errorMessage = $"Unknown Station type Found: {x[0].Content}";
                    errors.Add(errorMessage);
                }

                var failed = false;
                
                if (!decimal.TryParse(terrainLevel, NumberStyles.Number, CultureInfo.InvariantCulture, out var convertedTerrain) && !decimal.TryParse(terrainLevel, NumberStyles.Number, CultureInfo.GetCultureInfo("pt-BR"), out convertedTerrain))
                {
                    errorMessage =$"Invalid terrain level: {terrainLevel}";
                    errors.Add(errorMessage);
                    failed = true;
                }
                
                if (!decimal.TryParse(invertLevel, NumberStyles.Number, CultureInfo.InvariantCulture, out var convertedInvert) && !decimal.TryParse(invertLevel, NumberStyles.Number, CultureInfo.GetCultureInfo("pt-BR"), out  convertedInvert))
                {
                    errorMessage =$"Invalid invert level: {invertLevel}";
                    errors.Add(errorMessage);
                    failed = true;
                }
                
                if (!decimal.TryParse(depth, NumberStyles.Number, CultureInfo.InvariantCulture, out var convertedDepth) && !decimal.TryParse(depth, NumberStyles.Number, CultureInfo.GetCultureInfo("pt-BR"), out convertedDepth))
                {
                    errorMessage = $"Invalid depth level: {depth}";
                    errors.Add(errorMessage);
                    failed = true;
                }

                if (!failed)
                {
                    var expectedDepth = convertedTerrain - convertedInvert;
                
                    if (expectedDepth != convertedDepth)
                    {
                        errorMessage = $"The depth is said to be {depth} but should be {expectedDepth}";
                        errors.Add(errorMessage);
                    }
                }
            });

            var firstStationId = stationData[1][0].Content;
            var lastStationId = stationData[^1][0].Content;

            if (!(Regexes.Manhole.IsMatch(firstStationId) && Regexes.Manhole.IsMatch(stationData[^1][0].Content)))
            {
                errorMessage =
                    $"The first and Last Station should be Manholes, but the first Station is {firstStationId} and the last is {lastStationId}";
                
                errors.Add(errorMessage);
            }

            return errors;
        }
        
        public Manhole Entry(TextEntity[] textEntity)
        {
            var (stationData, sectionData) = GetSectionStationData(textEntity);

            var stationDataErrors = ValidateStationData(stationData);
            var sectionDataErros = ValidateSectionData(sectionData);
        }

        private List<string> ValidateSectionData(TextEntity[][] sectionData)
        {
            throw new NotImplementedException();
        }
    }
}