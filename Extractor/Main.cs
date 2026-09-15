using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using SewerSystem;

namespace Extractor
{
    internal static class Regexes
    {
        internal static readonly Regex Basin = new Regex(@"^BÁCIA: .*$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        internal static readonly Regex Collection = new Regex(@"^COLETOR: \d+$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    }
    
    
    public class Main : IExtractor
    {
        private static readonly Dictionary<string, string> Headers = new Dictionary<string, string>()
        {
            { "Manhole", "PVM" },
            { "TerrainLevel", "CTM" },
            { "InvertLevel", "CCM" },
            { "Depth", "PRFM" },
            { "CollectionSegment", "COL-TRE" },
            { "Slope", "DECLIV" },
            { "ProgressiveLength", "ACUMUL." },
            { "FlowRate", "VAZÃO (l/s)" },
            { "Material", "MATERIAL" },
            { "Station", "ESTACAS" },
        };

        private static readonly HashSet<string> Ignore = new HashSet<string>(){ "OBS", "CTJ", "CCJ", "PRFJ" };

        private (TextEntity[], TextEntity[]) SplitData(TextEntity[] data)
        {
            var sectionData = new List<TextEntity>();
            var stationData = new List<TextEntity>();
            var shouldCapture = false;

            foreach (var item in data)
            {
                if (!shouldCapture && item.Content.Equals(Headers["CollectionSegment"],
                        StringComparison.InvariantCultureIgnoreCase))
                {
                    shouldCapture = true;
                }

                if (shouldCapture)
                {
                    if (item.Content.Equals(Headers["Station"], StringComparison.InvariantCultureIgnoreCase))
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
            
            return (sectionData.ToArray(), stationData.ToArray());
        }
        
        public void Entry(SewerSystem.TextEntity[] textEntity)
        {
            var data = textEntity
                .OrderByDescending(x => x.Coordinates.Y)
                .ThenBy(x => x.Coordinates.X)
                .SkipWhile(text =>
                    text.Content.Trim().Equals(Headers["Manhole"], StringComparison.InvariantCultureIgnoreCase))
                .TakeWhile(text => !Regexes.Basin.IsMatch(text.Content))
                .Where(text => !Ignore.Contains(text.Content))
                .ToArray();

            var (sectionData, stationData) = SplitData(data);

            stationData = stationData
                .OrderBy(x => x.Coordinates.X)
                .ThenByDescending(x => x.Coordinates.Y).ToArray();
        }
    }
}