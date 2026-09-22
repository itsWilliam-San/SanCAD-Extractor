using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using MoreLinq.Extensions;
using SewerSystem.Contracts;

[assembly: InternalsVisibleTo("SancadExtractor.Tests")]

namespace Extractor
{
    internal static class Regexes
    {
        internal static readonly Regex Basin = new Regex(@"^BÁCIA: .*$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        internal static readonly Regex Collection = new Regex(@"^COLETOR: \d+$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        
        #region StationNames
        internal static readonly Regex Manhole = new Regex(@"^SB\d+-\d$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        internal static readonly Regex Intermediate = new Regex(@"^\d+-\d+E\d+$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        internal static readonly Regex StepDrop = new Regex(@"^TQ\s\d(?:[.,]\d+)?$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        internal static readonly Regex DropPipe = new Regex(@"^DG\s\d(?:[.,]\d+)?$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        #endregion
        
        #region SegmentsSyntaxe
        internal static readonly Regex ColTre = new Regex(@"^(\d+)-(\d+)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        internal static readonly Regex CompDiam = ColTre;
        #endregion
    }
    
    [PluginInfo("Unofficial SANCAD extractor", "Use this extractor if you have a table generated from SANCAD software", "0.1.0", "William S. Santos")]
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
        internal static (string[][], string[][]) GetSectionStationData(TextEntity[] rawData)
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

            var sectionDataVertical = sectionData 
                .OrderBy(x => x.Coordinates.X)
                .ThenByDescending(x => x.Coordinates.Y)
                .Skip(SegmentHeaders.Count)
                .Select(x => x.Content)
                .Batch(SegmentHeaders.Count);
            
            if (sectionDataVertical.Any(batch => batch.Length != SegmentHeaders.Count))
            {
                throw new InvalidOperationException($"Not all sections have {SegmentHeaders.Count} elements.");
            }

            var stationDataVertical = stationData
                .OrderBy(x => x.Coordinates.X)
                .ThenByDescending(x => x.Coordinates.Y)
                .Skip(StationHeaders.Count + MixHeaders.Count)
                .Select(x => x.Content)
                .Batch(StationHeaders.Count)
                .Select(t =>
                {
                    if (t.Length >= StationHeaders.Count)
                    {
                        return Regexes.Intermediate.IsMatch(t[^1]) ? [t[^1], t[0], t[1], t[2]] : t;
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
        private static List<string> ValidateStationData(string[][] stationData)
        {
            List<string> errors = new List<string>();
            
            string errorMessage;

            stationData.ForEach(x =>
            {
                var curStationLen = x.Length;   
                if (curStationLen != StationHeaders.Count)
                {
                    errorMessage = $"The station data should have {StationHeaders.Count}, but got  {x.Length}.\nDetected data:";

                    x.Aggregate(errorMessage, (current, data) => current + $" {data}");
                    
                    errorMessage += $"Please, fix your station data according to the following headers:";

                    StationHeaders.Aggregate(errorMessage, (current, header) => current + $" {header.Value}");
                    
                    errorMessage += $"We can't check for other errors related to this Station since its incomplete. Please, review it.";
                    errors.Add(errorMessage);
                    return;
                }
                
                var (id, terrainLevel, invertLevel, depth) = (x[0], x[1], x[2], x[3]);

                if (!(Regexes.Manhole.IsMatch(id) || Regexes.Intermediate.IsMatch(id) || Regexes.DropPipe.IsMatch(id) ||
                      Regexes.StepDrop.IsMatch(id)))
                {
                    errorMessage = $"Unknown Station type Found: {x[0]}";
                    errors.Add(errorMessage);
                }

                var failed = false;

                if (!TryConvertToDecimal(terrainLevel, out var convertedTerrain))
                {
                    errorMessage =$"Invalid terrain level: {terrainLevel}";
                    errors.Add(errorMessage);
                    failed = true;
                }

                if (!TryConvertToDecimal(invertLevel, out var convertedInvert))
                {
                    errorMessage =$"Invalid invert level: {invertLevel}";
                    errors.Add(errorMessage);
                    failed = true;
                }

                if (!TryConvertToDecimal(depth, out var convertedDepth))
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

            var firstStationId = stationData[1][0];
            var lastStationId = stationData[^1][0];

            if (!(Regexes.Manhole.IsMatch(firstStationId) && Regexes.Manhole.IsMatch(stationData[^1][0])))
            {
                errorMessage =
                    $"The first and Last Station should be Manholes, but the first Station is {firstStationId} and the last is {lastStationId}";
                
                errors.Add(errorMessage);
            }

            return errors;
        }

        private string? CheckForErrors(string[][] stationData, string[][] sectionData)
        {
            string? message = null;
            var stationDataErrors = ValidateStationData(stationData);

            var manholesCount = stationData.Count(data => Regexes.Manhole.IsMatch(data[0]));

            var sectionDataErros = ValidateSectionData(sectionData, manholesCount);
            
            var totalErrors = stationDataErrors.Count + sectionDataErros.Count;

            if (totalErrors > 0)
            {
                message = $"There's been found a total of {totalErrors} errors in the provided data set.";

                if (stationDataErrors.Count > 0)
                {
                    message += $"\nA total of {stationDataErrors.Count} Station Data errors have been found";
                }

                if (sectionDataErros.Count > 0)
                {
                    message += $"\nA total of {sectionDataErros.Count} Section Data errors have been found";
                }

                message += $"Station Data errors:" + stationDataErrors;
                message += $"Segment Data errors:" + sectionDataErros;

                message += $"Please fix them first, and try again.";
            }

            return message;
        }

        private List<(List<string[]>, string[])> JoinStationSection(string[][] stationData, string[][] sectionData)
        {
            var groupedStationData = new List<List<string[]>>(sectionData.Length);
            var currentGroup = new List<string[]>();
            
            foreach (var station in stationData)
            {
                currentGroup.Add(station);
                
                if (Regexes.Manhole.IsMatch(station[0]) && currentGroup.Count > 1)
                {
                    groupedStationData.Add(new List<string[]>(currentGroup));
                    currentGroup.Clear();
                    currentGroup.Add(station);
                }
            }

            if (currentGroup.Count > 2)
            {
                var message = $"Couldn't group the station correctly. The last station must be a manhole. But found {currentGroup.Last()[0]}";
                throw new InvalidOperationException(message);
            }

            return groupedStationData.Zip(sectionData, (station, section) =>  (station, section)).ToList();
        }

        private T ToStation<T>(string[] station, Func<string, decimal, decimal, decimal, T> factory) where T : Station
        {
            TryConvertToDecimal(station[1], out var terrain);
            TryConvertToDecimal(station[2], out var invert);
            TryConvertToDecimal(station[3], out var depth);
            
            return factory(station[0], terrain, invert, depth);
        }

        private Segment ToSegment(string[] segment)
        {
            TryConvertToDecimal(segment[3], out var incrementalLength);
            TryConvertToDecimal(segment[2], out var slope);

            var groups = Regexes.CompDiam.Match(segment[1]).Groups;
            var (length, diameter) = (groups[1].Value, groups[2].Value);
            
            var pipe = new Pipe(segment[5], int.Parse(length), ushort.Parse(diameter));

            return new Segment(segment[0], incrementalLength, slope, pipe);
        }
        
        private Manhole ToManhole(string[] station) => ToStation(station, (id, terrain, invert, depth) => new Manhole(id, terrain, invert, depth));
        private Intermediate ToIntermediate(string[] station) => ToStation(station, (id, terrain, invert, depth) => new Intermediate(id, terrain, invert, depth));

        private Manhole ToManhole(List<(List<string[]>, string[])> data)
        {
            Manhole? root = null;

            foreach (var stationSegment in data)
            {
                var curSegment = stationSegment.Item1;
                var startStation = curSegment.First();
                var endStation = curSegment.Last();
                
                var start = ToManhole(startStation); 
                var end = ToManhole(endStation);
                
                var intermediates = curSegment
                    .Skip(1)
                    .Take(curSegment.Count - 1)
                    .Where(x => !(Regexes.DropPipe.IsMatch(x[0]) || Regexes.StepDrop.IsMatch(x[0])))
                    .Select(ToIntermediate)
                    .ToList();

                var segment = ToSegment(stationSegment.Item2);

                var dropPipe = curSegment.Find(x => Regexes.DropPipe.IsMatch(x[0]));

                if (dropPipe is null)
                {
                    var stepDrop = curSegment.Find(x => Regexes.StepDrop.IsMatch(x[0]));

                    if (stepDrop is null)
                    {
                        var downstreamConnection = new DownstreamConnection(end, null, intermediates, segment);
                        start.DownstreamConnection = downstreamConnection;
                        end.UpstreamManholes.Add(start);
                    }
                    else
                    {
                        TryConvertToDecimal(stepDrop[2], out var invert);
                        
                        var downstreamConnection = new DownstreamConnection(end, new StepDrop(invert), intermediates, segment);
                        start.DownstreamConnection = downstreamConnection;
                    }
                }
                else
                {
                    TryConvertToDecimal(dropPipe[2], out var invert);
                    
                    var downstreamConnection = new DownstreamConnection(end, new DropPipe(invert), intermediates, segment);
                    start.DownstreamConnection = downstreamConnection;
                }
                
                end.UpstreamManholes.Add(start);

                root ??= start;
            }

            if (root is null) throw new InvalidOperationException("This shouldn't happen");
            
            return root;
        }
        
        public Manhole Entry(TextEntity[] textEntity)
        {
            var (stationData, sectionData) = GetSectionStationData(textEntity);
            
            string? errorMessage = CheckForErrors(stationData, sectionData);

            if (errorMessage != null)
            {
                throw new InvalidOperationException(errorMessage);
            }
            
            var data = JoinStationSection(stationData, sectionData);
            return ToManhole(data);
        }

        private List<string> ValidateSectionData(string[][] sectionData, int numberOfManholes)
        {
            var errors = new List<string>();
            string message;

            sectionData.ForEach(x =>
            {
                if (x.Length != SegmentHeaders.Count)
                {
                    message = $"Expected {SegmentHeaders.Count} elements, but got {x.Length}. Detected Data:";
                    x.Aggregate(message, (current, data) => current + $" {data}");
                    message += $"\nPlease fix your table according to the following headers:";

                    message = SegmentHeaders.Aggregate(message, (current, header) => current + $" {header.Value}");
                    message += $"\nNo more validations will be done for the table is incomplete.";

                    errors.Add(message);
                    return;
                }

                if (!Regexes.ColTre.IsMatch(x[0]))
                {
                    message = $"{x[0]} is not a valid {SegmentHeaders[Headers.CollectionSegment]} syntax.";
                    errors.Add(message);
                }

                if (!Regexes.CompDiam.IsMatch(x[1]))
                {
                    message = $"{x[1]} is not a valid {SegmentHeaders[Headers.PipeLengthDiameter]} syntax.";
                    errors.Add(message);
                }


                var slope = x[2];
                var progressiveLength = x[3];
                var flowRate = x[4];

                if (!TryConvertToDecimal(slope, out _))
                {
                    message = $"{slope} is not a valid slope.";
                    errors.Add(message);
                }

                if (!TryConvertToDecimal(progressiveLength, out _))
                {
                    message = $"{progressiveLength} is not a valid progressive length.";
                    errors.Add(message);
                }

                if (!TryConvertToDecimal(flowRate, out _))
                {
                    message = $"{flowRate} is not a valid flow rate.";
                    errors.Add(message);
                }
                
                // There's no check for material, we'll accept anything even if it's not a real material for now.
            });

            var expectedSectionLength = numberOfManholes - 1;
            
            if (sectionData.Length != expectedSectionLength)
            {
                message = $"It was detected only {sectionData.Length} segments. The number of manholes is {numberOfManholes}, so we should have {expectedSectionLength} segments.";
                errors.Add(message); 
            } 
            
            return errors;
        }

        private static bool TryConvertToDecimal(string data, out decimal value)
        {
            return decimal.TryParse(data, NumberStyles.Number, CultureInfo.InvariantCulture, out value) || decimal.TryParse(data, NumberStyles.Number, CultureInfo.GetCultureInfo("pt-BR"), out value);
        }
    }
}