using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SupportResistence
{
    public class SupportResistenceAlgo
    {

        public void Run()
        {
            var path = $@"C:\temp\{DateTime.UtcNow.ToString("yyyyMMdd")}";


            var directories = Directory.GetDirectories(path);

            foreach (var directory in directories)
            {
                var ss = directory.Split(Path.DirectorySeparatorChar).LastOrDefault();
                var ss2 = Path.GetDirectoryName(directory);

                Run2(directory);
            }

        }

        public void Run2(string path)
        {
            //var path = $@"C:\temp\{DateTime.UtcNow.ToString("yyyyMMdd")}\Minute5";
            var files = Directory.GetFiles(path, "**.csv");
            //var candles = CandlesProcessor.FillDataDictionary(new string[] { @"C:\temp\20200614\Minute15\20200614_EURUSD.csv" });
            ConcurrentDictionary<string, Candles> candles = CandlesProcessor.FillDataDictionary(files);

            foreach (var candle in candles)
            {
                //Console.WriteLine(candle.Key);

                List<(DateTime DateTime, decimal)> highs = candle.Value.Values.Select(v => (v.DateTime, decimal.Parse(v.H.ToString()))).ToList();
                List<(DateTime DateTime, decimal)> lows = candle.Value.Values.Select(v => (v.DateTime, decimal.Parse(v.L.ToString()))).ToList();

                //var calc = new SupportResistanceCalculator();
                //Tuple<List<Level>, List<Level>> sr = calc.GetSupportResistance(closes, 0, closes.Count, 1, 5);

                decimal variation = 0.0001M;

                if (candle.Key.Contains("JPY"))
                    variation = 0.01M;

                if (candle.Key.Contains("AUS200"))
                    variation = 1.0M;

                var mostCommonGap = Quant.MostCommonGap(highs);

                SortedDictionary<decimal, List<(DateTime DateTime, decimal)>> resists = Quant.GetResistances2(highs, variation, 2, mostCommonGap);
                Print(resists, candle.Key);

                SortedDictionary<decimal, List<(DateTime DateTime, decimal)>> supports = Quant.GetSupport2(lows, variation, 2, mostCommonGap);
                Print(supports, candle.Key);

                var symbol = candle.Key.Split('_').Last();


                if (resists.Count > 0)
                    File.WriteAllText(Path.Combine(path, $"{symbol}.resist"), string.Join(",", resists.Select(x => x.Key.ToString())));

                if (supports.Count > 0)
                    File.WriteAllText(Path.Combine(path, $"{symbol}.support"), string.Join(",", supports.Select(x => x.Key.ToString())));

                //if(supports.Count > 0)
                //{
                //    FinancialChartType chart = new FinancialChartType(string.Empty);

                //    var trendLine = new TrendLine();
                //    //trendLine.BuyPrice = double.Parse(supports.First().Item2.ToString());
                //    //trendLine.StopLossTracking = supports.ToDictionary(t => t.DateTime, t => (double)t.Item2);
                //    trendLine.StopLossTracking = supports.ToDictionary(t => t.DateTime, t => (double)supports.Select(x => x.Item2).Min());
                //    trendLine.StartDate = supports.Select(x => x.DateTime).Min();
                //    trendLine.EndDate = supports.Select(x => x.DateTime).Max();
                //    trendLine.Code = "usd";

                //    IEnumerable<KeyValuePair<DateTime, Candle>> subCandles = candle.Value.Where(c => c.Key >= trendLine.StartDate); //.ToDictionary(t => t.Key, t => t.Value); ;

                //    chart.ChartWithTrendline(new Candles(subCandles), trendLine);
                //    var image1 = $"aa.png";


                //    chart.SaveImage(image1);

                //}

                //supports.Sort();

                //var minValue = supports.Min(c => c.Item2);
                //var minV = supports.Where(c => c.Item2 == minValue).ToList();

                //resists.Sort();

            }

        }

        private void Print(SortedDictionary<decimal, List<(DateTime DateTime, decimal)>> resists, string key)
        {
            if (resists.Count > 0)
            {
                Console.WriteLine();
                Console.WriteLine(key);

            }
            //foreach (var item in supports)
            //    Console.WriteLine($"{item.ToString()}");

            foreach (var item in resists)
            {
                //Console.WriteLine();

                Console.Write(item.Key);
                //Console.WriteLine();
                var dt = item.Value.Select(x => x.DateTime).Distinct().ToList();
                dt.Sort();

                foreach (DateTime kvp in dt)
                {
                    Console.Write(" | " + kvp.ToString("dd HH:mm"));
                }
                Console.WriteLine();

            }
        }

    }

    public static class Quant
    {
        public static List<(DateTime DateTime, decimal)> GetSupports(List<(DateTime DateTime, decimal)> series, double variation = 0.0005, int h = 1)
        {
            List<(DateTime DateTime, decimal)> minima = new List<(DateTime DateTime, decimal)>();
            List<(DateTime DateTime, decimal)> supports = new List<(DateTime DateTime, decimal)>();

            for (int i = h; i < series.Count - h; i++)
            {
                if (series[i].Item2 < series[i - h].Item2 && series[i].Item2 < series[i + h].Item2)
                {
                    minima.Add(series[i]);
                }
            }


            minima.Reverse();
            foreach (var low1 in minima)
            {
                if (supports.Count() > 0) break;

                var r = low1.Item2 * (decimal)variation;
                List<(DateTime DateTime, decimal)> commonLevel = new List<(DateTime DateTime, decimal)>();

                foreach (var low2 in minima)
                {
                    if (low2.Item2 > low1.Item2 - r && low2.Item2 < low1.Item2 + r)
                    {
                        commonLevel.Add(low2);
                    }
                }

                if (commonLevel.Count > 1)
                {
                    for (int i = 1; i < commonLevel.Count; i++)
                    {
                        var common1 = commonLevel[i - 1];
                        var common2 = commonLevel[i];

                        var betweenBars = series.Where(s => common1.DateTime > s.DateTime && s.DateTime > common2.DateTime).ToList();
                        var belowSupport = betweenBars.Where(b => b.Item2 < Math.Max(common1.Item2, common2.Item2)).ToList();

                        if (belowSupport.Count() == 0)
                        {
                            var barsToCurrent = series.Where(s => common1.DateTime < s.DateTime).ToList();

                            var belowSupportToCurrent = barsToCurrent.Where(b => b.Item2 < Math.Max(common1.Item2, common2.Item2)).ToList();

                            if (belowSupportToCurrent.Count() == 0)
                            {
                                supports.AddRange(commonLevel);
                                //supports.Add(common2);
                                break;
                            }
                        }
                    }

                    //var minValue = commonLevel.Min(c => c.Item2);
                    //var level = commonLevel.Last(c => c.Item2 == minValue);
                    //if (!supports.Contains(level))
                    //{
                    //    supports.Add(level);
                    //}
                }
            }


            return supports;
        }

        public static SortedDictionary<decimal, List<(DateTime DateTime, decimal)>> GetSupport2(List<(DateTime DateTime, decimal)> series,
            decimal variation, int h, TimeSpan mostCommonGap = default)
        {
            List<(DateTime DateTime, decimal)> minimas = new List<(DateTime DateTime, decimal)>();

            for (int i = h; i < series.Count - h; i++)
            {
                if (series[i].Item2 < series[i - h].Item2 && series[i].Item2 < series[i + h].Item2)
                {
                    minimas.Add(series[i]);
                }
            }

            minimas.Reverse();

            return ProcessMaxMinimas(minimas, variation, mostCommonGap, series, "support");
        }

        public static TimeSpan MostCommonGap(List<(DateTime DateTime, decimal)> series)
        {
            var dateTimes = series.Select(x => x.DateTime).ToList();

            var timeGaps = new List<TimeSpan>();

            for (int i = 1; i < dateTimes.Count; i++)
                timeGaps.Add(dateTimes[i] - dateTimes[i - 1]);

            TimeSpan mostCommon = timeGaps.GroupBy(i => i).OrderByDescending(grp => grp.Count())
                        .Select(grp => grp.Key).First();

            return mostCommon;
        }

        public static SortedDictionary<decimal, List<(DateTime DateTime, decimal)>> GetResistances2(List<(DateTime DateTime, decimal)> series,
            decimal variation, int h, TimeSpan mostCommon)
        {
            //List<(DateTime DateTime, decimal)> resistances = new List<(DateTime DateTime, decimal)>();

            //var avgPrice = series.Select(x => x.Item2).Average();

            //var r = variation; // avgPrice * 0.001m * 0.5m; // maxima1.Item2 * (decimal)variation;


            List<(DateTime DateTime, decimal)> maximas = new List<(DateTime DateTime, decimal)>();

            for (int i = h; i < series.Count - h; i++)
            {
                if (series[i].Item2 > series[i - h].Item2 && series[i].Item2 > series[i + h].Item2)
                {
                    maximas.Add(series[i]);
                }
            }

            maximas.Reverse();

            //int count = BitConverter.GetBytes(decimal.GetBits(series[1].Item2)[3])[2];

            return ProcessMaxMinimas(maximas, variation, mostCommon, series, "resist");

        }

        private static SortedDictionary<decimal, List<(DateTime DateTime, decimal)>> ProcessMaxMinimas(List<(DateTime DateTime, decimal)> maximas,
            decimal variation, TimeSpan mostCommon, List<(DateTime DateTime, decimal)> series, string mode)
        {

            SortedDictionary<decimal, List<(DateTime DateTime, decimal)>> resistDic = new SortedDictionary<decimal, List<(DateTime DateTime, decimal)>>();

            foreach (var maxima1 in maximas)
            {
                if (resistDic.ContainsKey(maxima1.Item2))
                    continue;


                var highCommonLevel = maxima1.Item2 + variation;
                var lowCommonLevel = maxima1.Item2 - variation;


                List<(DateTime DateTime, decimal)> commonLevelsUncompressed = maximas.Where(m => lowCommonLevel <= m.Item2 && m.Item2 <= highCommonLevel).ToList();

                List<(DateTime DateTime, decimal)> commonLevels = new List<(DateTime DateTime, decimal)>();

                for (int i = 1; i < commonLevelsUncompressed.Count; i++)
                {
                    var commonLevel1 = commonLevelsUncompressed[i - 1];
                    var commonLevel2 = commonLevelsUncompressed[i];

                    if ((commonLevel1.DateTime - commonLevel2.DateTime).TotalMinutes == mostCommon.TotalMinutes)
                    {
                        commonLevels.Add((commonLevel1.DateTime, (commonLevel1.Item2 + commonLevel2.Item2) / 2));
                        i++;
                    }
                    else
                    {
                        commonLevels.Add(commonLevel1);
                    }

                }


                if (commonLevels.Count >= 2)
                {
                    var maxCommonLevel = commonLevels.Select(x => x.Item2).Max();

                    var maxDateTime = series.Select(x => x.DateTime).Max();

                    commonLevels.Insert(0, (maxDateTime, maxCommonLevel));

                    int resistSegmentCounter = 0;
                    for (int i = 1; i < commonLevels.Count; i++)
                    {
                        var common1 = commonLevels[i - 1];
                        var common2 = commonLevels[i];


                        var betweenBars = series.Where(s => common1.DateTime > s.DateTime && s.DateTime > common2.DateTime).ToList();

                        List<(DateTime DateTime, decimal)> aboveResist;

                        if (mode == "resist")
                        {
                            var highestCommon = Math.Max(common1.Item2, common2.Item2);
                            aboveResist = betweenBars.Where(b => b.Item2 > highestCommon).ToList();
                        }
                        else
                        {
                            var lowestCommon = Math.Min(common1.Item2, common2.Item2);
                            aboveResist = betweenBars.Where(b => b.Item2 < lowestCommon).ToList();
                        }

                        if (aboveResist.Count() == 0)
                            resistSegmentCounter++;
                        else
                        {
                            break;
                        }
                    }

                    if (resistSegmentCounter >= 3)
                    {
                        var subCommonLevel = commonLevels.Take(resistSegmentCounter + 1).ToList();

                        var span = subCommonLevel.First().DateTime - subCommonLevel.Last().DateTime;

                        if (span.TotalHours >= 8)
                            resistDic.Add(maxima1.Item2, subCommonLevel);
                    }
                }
            }

            if (resistDic.Count > 2)
            {
                return CompressDictionary(resistDic);
            }


            return resistDic;
        }

        private static SortedDictionary<decimal, List<(DateTime DateTime, decimal)>> CompressDictionary(SortedDictionary<decimal, List<(DateTime DateTime, decimal)>> resistDic)
        {
            var resistDic2 = new SortedDictionary<decimal, List<(DateTime DateTime, decimal)>>();

            for (int i = 1; i < resistDic.Count; i++)
            {
                var resist1 = resistDic.ToArray()[i - 1];
                var resist2 = resistDic.ToArray()[i];

                if ((resist2.Key - resist1.Key) <= (resist1.Key * (decimal)0.0002m))
                {
                    var l = new List<(DateTime DateTime, decimal)>();
                    l.AddRange(resist1.Value);
                    l.AddRange(resist2.Value);

                    var l2 = l.Distinct().ToList();
                    l2.Sort();

                    resistDic2.Add((resist1.Key + resist2.Key) / 2, l2);
                    i++;
                }
                else
                {
                    if (i == resistDic.Count - 1)
                        resistDic2.Add(resist2.Key, resist2.Value);

                    resistDic2.Add(resist1.Key, resist1.Value);
                }
            }

            if (resistDic.Count != resistDic2.Count)
                return CompressDictionary(resistDic2);
            else
                return resistDic2;

        }

        public static List<(DateTime DateTime, decimal)> GetResistances(List<(DateTime DateTime, decimal)> series, double variation = 0.0005, int h = 1)
        {
            List<(DateTime DateTime, decimal)> maximas = new List<(DateTime DateTime, decimal)>();
            List<(DateTime DateTime, decimal)> resistances = new List<(DateTime DateTime, decimal)>();

            for (int i = h; i < series.Count - h; i++)
            {
                if (series[i].Item2 > series[i - h].Item2 && series[i].Item2 > series[i + h].Item2)
                {
                    maximas.Add(series[i]);
                }
            }

            maximas.Reverse();

            foreach (var maxima in maximas)
            {
                if (resistances.Count() > 0) break;

                var r = maxima.Item2 * (decimal)variation;
                List<(DateTime DateTime, decimal)> commonLevel = new List<(DateTime DateTime, decimal)>();

                foreach (var high2 in maximas)
                {
                    if (high2.Item2 > maxima.Item2 - r && high2.Item2 < maxima.Item2 + r)
                    {
                        commonLevel.Add(high2);
                    }
                }

                if (commonLevel.Count > 1)
                {
                    for (int i = 1; i < commonLevel.Count; i++)
                    {
                        var common1 = commonLevel[i - 1];
                        var common2 = commonLevel[i];

                        var betweenBars = series.Where(s => common1.DateTime > s.DateTime && s.DateTime > common2.DateTime).ToList();
                        var aboveResist = betweenBars.Where(b => b.Item2 > Math.Min(common1.Item2, common2.Item2)).ToList();

                        if (aboveResist.Count() == 0)
                        {
                            var barsToCurrent = series.Where(s => common1.DateTime < s.DateTime).ToList();

                            var aboveResistToCurrent = barsToCurrent.Where(b => b.Item2 > Math.Max(common1.Item2, common2.Item2)).ToList();

                            if (aboveResistToCurrent.Count() == 0)
                            {
                                resistances.Add(common1);
                                resistances.Add(common2);
                                break;
                            }
                        }
                    }
                    //var maxVal = commonLevel.Max(c => c.Item2);
                    //var level = commonLevel.Last(c => c.Item2 == maxVal);
                    //if (!resistances.Contains(level))
                    //{
                    //    resistances.Add(level);
                    //}
                }
            }


            return resistances;
        }

    }

    public interface ISupportResistanceCalculator
    {
        Tuple<List<Level>, List<Level>> GetSupportResistance(List<float> timeseries,
            int beginIndex, int endIndex, int segmentSize, float rangePct);
    }

    public enum LevelType
    {

        Support, Resistance

    }

    //public class Tuple<TA, TB>
    //{

    //    private readonly TA a;

    //    private readonly TB b;

    //    public Tuple(TA a, TB b)
    //    {
    //        this.a = a;
    //        this.b = b;
    //    }

    //    public TA GetA()
    //    {
    //        return this.a;
    //    }

    //    public TB GetB()
    //    {
    //        return this.b;
    //    }

    //    public String ToString()
    //    {
    //        return "Tuple [a=" + this.a + ", b=" + this.b + "]";
    //    }

    //}

    public abstract class CollectionUtils
    {

        public static void Remove<T>(List<T> list,
            List<int> indexes)
        {
            int i = 0;
            for (int j = 0; j < indexes.Count; j++)
            {
                list.RemoveAt(j - i++);
            }
        }

        public static IEnumerable<List<T>> IntoBatches<T>(IEnumerable<T> list, int size)
        {
            if (size < 1)
                throw new ArgumentException();

            var rest = list;

            while (rest.Any())
            {
                yield return rest.Take(size).ToList();
                rest = rest.Skip(size);
            }
        }
    }


    public class Level
    {

        private long serialVersionUID = -7561265699198045328L;

        private LevelType type;

        private readonly float level;
        private readonly float strength;


        public Level(LevelType type, float level, float strength)
        {
            this.type = type;
            this.level = level;
            this.strength = strength;
        }

        public new LevelType GetType()
        {
            return this.type;
        }

        public float GetLevel()
        {
            return this.level;
        }

        public float GetStrength()
        {
            return this.strength;
        }

        public override String ToString()
        {
            return "Level [type=" + this.type + ", level=" + this.level
                    + ", strength=" + this.strength + "]";
        }

    }


    public interface ILevelHelper
    {

        float Aggregate(List<float> data);

        LevelType Type(float level, float priceAsOfDate, float rangePct);

        bool WithinRange(float node, float rangePct, float val);

    }

    public class Support : ILevelHelper
    {
        public float Aggregate(List<float> data)
        {
            return data.Min();
        }

        public LevelType Type(float level, float priceAsOfDate,
            float rangePct)
        {
            float threshold = level * (1 - (rangePct / 100));
            return (priceAsOfDate < threshold) ? LevelType.Resistance : LevelType.Support;
        }

        public bool WithinRange(float node, float rangePct,
            float val)
        {
            float threshold = node * (1 + (rangePct / 100f));
            if (val < threshold)
                return true;
            return false;
        }

    }

    public class Resistance : ILevelHelper
    {
        public float Aggregate(List<float> data)
        {
            return data.Max();
        }

        public LevelType Type(float level, float priceAsOfDate,
            float rangePct)
        {
            float threshold = level * (1 + (rangePct / 100));
            return (priceAsOfDate > threshold) ? LevelType.Resistance : LevelType.Support;
        }

        public bool WithinRange(float node, float rangePct,
            float val)
        {
            float threshold = node * (1 - (rangePct / 100f));
            if (val > threshold)
                return true;
            return false;
        }

    }

    public class SupportResistanceCalculator : ISupportResistanceCalculator
    {

        private static readonly int SMOOTHEN_COUNT = 2;

        private static readonly ILevelHelper SupportHelper = new Support();

        private static readonly ILevelHelper ResistanceHelper = new Resistance();


        public Tuple<List<Level>, List<Level>> GetSupportResistance(
            List<float> timeseries, int beginIndex,
            int endIndex, int segmentSize, float rangePct)
        {

            List<float> series = this.SeriesToWorkWith(timeseries,
                beginIndex, endIndex);
            // Split the timeseries into chunks
            List<List<float>> segments = this.SplitList(series, segmentSize);
            float priceAsOfDate = series[series.Count - 1];

            List<Level> levels = new List<Level>();
            this.IdentifySRLevel(levels, segments, rangePct, priceAsOfDate,
                SupportHelper);

            this.IdentifySRLevel(levels, segments, rangePct, priceAsOfDate,
                ResistanceHelper);

            List<Level> support = new List<Level>();
            List<Level> resistance = new List<Level>();
            this.SeparateLevels(support, resistance, levels);

            // Smoothen the levels
            this.Smoothen(support, resistance, rangePct);

            return new Tuple<List<Level>, List<Level>>(support, resistance);
        }

        private void IdentifySRLevel(List<Level> levels,
            List<List<float>> segments, float rangePct,
            float priceAsOfDate, ILevelHelper helper)
        {

            List<float> aggregateVals = new List<float>();

            // Find min/max of each segment
            foreach (var segment in segments)
            {
                aggregateVals.Add(helper.Aggregate(segment));
            }

            while (aggregateVals.Any())
            {
                List<float> withinRange = new List<float>();
                HashSet<int> withinRangeIdx = new HashSet<int>();

                // Support/resistance level node
                float node = helper.Aggregate(aggregateVals);

                // Find elements within range
                for (int i = 0; i < aggregateVals.Count; ++i)
                {
                    float f = aggregateVals[i];
                    if (helper.WithinRange(node, rangePct, f))
                    {
                        withinRangeIdx.Add(i);
                        withinRange.Add(f);
                    }
                }

                // Remove elements within range
                CollectionUtils.Remove(aggregateVals, withinRangeIdx.ToList());

                // Take an average
                float level = withinRange.Average();
                float strength = withinRange.Count;

                levels.Add(new Level(helper.Type(level, priceAsOfDate, rangePct),
                    level, strength));

            }

        }

        private List<List<float>> SplitList(List<float> series,
            int segmentSize)
        {
            List<List<float>> splitList = CollectionUtils.IntoBatches(series, segmentSize).ToList();

            if (splitList.Count > 1)
            {
                // If last segment it too small
                int lastIdx = splitList.Count - 1;
                List<float> last = splitList[lastIdx].ToList();
                if (last.Count <= (segmentSize / 1.5f))
                {
                    // Remove last segment
                    splitList.Remove(last);
                    // Move all elements from removed last segment to new last
                    // segment
                    foreach (var l in last)
                    {
                        splitList[lastIdx - 1].Add(l);
                    }
                }
            }

            return splitList.ToList();
        }

        private void SeparateLevels(List<Level> support,
            List<Level> resistance, List<Level> levels)
        {
            foreach (var level in levels)
            {
                if (level.GetType() == LevelType.Support)
                {
                    support.Add(level);
                }
                else
                {
                    resistance.Add(level);
                }
            }
        }

        private void Smoothen(List<Level> support,
            List<Level> resistance, float rangePct)
        {
            for (int i = 0; i < SMOOTHEN_COUNT; ++i)
            {
                this.Smoothen(support, rangePct);
                this.Smoothen(resistance, rangePct);
            }
        }

        /**
         * Removes one of the adjacent levels which are close to each other.
         */
        private void Smoothen(List<Level> levels, float rangePct)
        {
            if (levels.Count < 2)
                return;

            List<int> removeIdx = new List<int>();
            //levels.Sort();

            for (int i = 0; i < (levels.Count - 1); i++)
            {
                Level currentLevel = levels[i];
                Level nextLevel = levels[i + 1];
                float current = currentLevel.GetLevel();
                float next = nextLevel.GetLevel();
                float difference = Math.Abs(next - current);
                float threshold = (current * rangePct) / 100;

                if (difference < threshold)
                {
                    int remove = currentLevel.GetStrength() >= nextLevel
                                     .GetStrength()
                        ? i
                        : i + 1;
                    removeIdx.Add(remove);
                    i++; // start with next pair
                }
            }

            CollectionUtils.Remove(levels, removeIdx);
        }

        private List<float> SeriesToWorkWith(List<float> timeseries,
            int beginIndex, int endIndex)
        {

            if ((beginIndex == 0) && (endIndex == timeseries.Count))
                return timeseries;
            return timeseries.GetRange(beginIndex, endIndex);

        }
    }
}
