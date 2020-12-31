using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Windows.Forms.DataVisualization.Charting;
using MathNet.Numerics.Statistics;

namespace SupportResistence
{
    public class CandlesProcessor
    {
        static ConcurrentDictionary<string, Candles> dictionary = new ConcurrentDictionary<string, Candles>();

        public void Run(string path)
        {
            string[] allfiles = Directory.GetFiles(path, "*.csv");
            var dictionary = FillDataDictionary(allfiles);
        }
        static int winSize = 50;


        public static ConcurrentDictionary<string, Candles> FillDataDictionary(string[] allfiles1)
        {
            int dCol = 0;
            int oCol = 1;
            int hCol = 2;
            int lCol = 3;
            int cCol = 4;
            int vCol = 5;

            if (dictionary != null && dictionary.Count > 0) return dictionary;

            var allfiles = allfiles1.ToList();

            allfiles.Sort();

            //Parallel.ForEach(allfiles, (file) =>
            foreach (string file in allfiles)
            {
                var sym = Path.GetFileNameWithoutExtension(file);

                var lines = File.ReadAllLines(file);

                Candles dic = new Candles(winSize);

                for (var i = 1; i < lines.Length; i++)
                {
                    var line = lines[i];

                    var splitted = line.Split(';');

                    if (splitted[4].Equals("null", StringComparison.InvariantCultureIgnoreCase)) continue;

                    //var dateTime = DateTime.Parse(splitted[dCol]);
                    var dateTime = DateTime.ParseExact(splitted[dCol], "yyyyMMdd HHmmss", null);

                    if (dic.ContainsKey(dateTime)) continue;

                    var candle = new Candle
                    {
                        DateTime = dateTime,
                        O = double.Parse(splitted[oCol]),
                        H = double.Parse(splitted[hCol]),
                        L = double.Parse(splitted[lCol]),
                        C = double.Parse(splitted[cCol]),
                        V = double.Parse(splitted[vCol]),
                    };

                    dic.Add(dateTime, candle);
                }

                var dicAry = dic.ToArray();
                //for (var i = 0; i < dicAry.Length-winSize; i++)
                //{
                //    var candle = dicAry[i];

                //    if (candle.Value.Alert)
                //    {
                //        var inSample = dic.ToList().GetRange(i - winSize, winSize * 2).ToDictionary(pair => pair.Key, pair => pair.Value);

                //        GraphCandles($@"{sym}_{i}", inSample);

                //    }

                //}


                dictionary.TryAdd(sym, dic);
            }//);

            return dictionary;
        }

        ChartArea GetChartArea(string name)
        {
            var chartArea = new ChartArea(name);

            chartArea.AxisX.MajorGrid.LineWidth = 1;
            chartArea.AxisY.MajorGrid.LineWidth = 1;

            chartArea.AxisX.LabelAutoFitStyle = LabelAutoFitStyles.WordWrap;
            chartArea.AxisX.IsLabelAutoFit = true;
            chartArea.AxisX.LabelStyle.Enabled = true;
            chartArea.AxisY.IsStartedFromZero = false;
            chartArea.AlignmentOrientation = AreaAlignmentOrientations.All;

            return chartArea;
        }

        public void GraphCandles(string path, Dictionary<DateTime, Candle> candles)
        {
            using (var chartV = new Chart())
            {
                using (var chartC = new Chart())
                {
                    chartC.Height = 600;
                    chartC.Width = 1800;

                    chartV.Height = chartC.Height / 2;
                    chartV.Width = chartC.Width;

                    chartC.ChartAreas.Add(GetChartArea("ChartArea1"));
                    chartV.ChartAreas.Add(GetChartArea("ChartArea2"));

                    var barSeries = new Series("Bar")
                    {
                        ChartType = SeriesChartType.Column,
                        XValueMember = "Day",
                        YValueMembers = "Volume",
                        XValueType = ChartValueType.DateTime,
                        YValueType = ChartValueType.Double,
                        ["PixelPointWidth"] = "3",
                    };

                    chartV.Series.Add(barSeries);

                    var candleSeries = new Series("Candle")
                    {
                        ChartType = SeriesChartType.Candlestick,
                        YValuesPerPoint = 4,
                        XValueMember = "DateTime",
                        YValueMembers = "H,L,O,C",
                        XValueType = ChartValueType.DateTime,

                        CustomProperties = "PriceDownColor=Blue,PriceUpColor=Red",
                        ["OpenCloseStyle"] = "Triangle",
                        ["ShowOpenClose"] = "Both",
                    };

                    chartC.Series.Add(candleSeries);

                    foreach (var candle in candles)
                    {
                        int i = candleSeries.Points.Count;
                        // adding date and high
                        candleSeries.Points.AddXY(candle.Key, candle.Value.H);
                        // adding low
                        candleSeries.Points[i].YValues[1] = candle.Value.L;
                        //adding open
                        candleSeries.Points[i].YValues[2] = candle.Value.C;
                        // adding close
                        candleSeries.Points[i].YValues[3] = candle.Value.O;

                        chartV.Series["Bar"].Points.AddXY(candle.Key, candle.Value.V);

                        if (i == winSize)
                        {
                            AddMarker(candleSeries.Points[i]);
                            AddMarker(chartV.Series["Bar"].Points[i]);
                        }
                    }

                    //chart2.SaveImage(path + ".v.jpg", ChartImageFormat.Png);

                    //chart1.SaveImage(path + ".c.jpg", ChartImageFormat.Png);
                    MemoryStream chartVStream = new MemoryStream();
                    chartV.SaveImage(chartVStream, ChartImageFormat.Png);

                    MemoryStream chartCStream = new MemoryStream();
                    chartC.SaveImage(chartCStream, ChartImageFormat.Png);

                    System.Drawing.Bitmap bitmap = new Bitmap(chartC.Width, chartC.Height + chartV.Height);
                    using (Graphics g = Graphics.FromImage(bitmap))
                    {
                        g.DrawImage(Image.FromStream(chartCStream), 0, 0);
                        g.DrawImage(Image.FromStream(chartVStream), 0, chartC.Height);
                    }

                    bitmap.Save(path + ".jpg", ImageFormat.Png);
                }
            }
        }

        void AddMarker(DataPoint p)
        {
            p.MarkerColor = System.Drawing.Color.GreenYellow;
            p.MarkerSize = 10;
            p.MarkerStyle = MarkerStyle.Diamond;
        }
    }


    public class Candles : IDictionary<DateTime, Candle>
    {
        public Candles(int period)
        {
            Period = period;
        }

        public Candles(IEnumerable<KeyValuePair<DateTime, Candle>> candles)
        {
            foreach (var item in candles)
            {
                this.Add(item);
            }

        }

        public int Period;
        private SortedDictionary<DateTime, Candle> dictionaryImplementation = new SortedDictionary<DateTime, Candle>();
        public IEnumerator<KeyValuePair<DateTime, Candle>> GetEnumerator()
        {
            return dictionaryImplementation.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable)dictionaryImplementation).GetEnumerator();
        }

        public void Add(KeyValuePair<DateTime, Candle> item)
        {
            //if (dictionaryImplementation.Count > Period - 1)
            //{
            //    var slice = dictionaryImplementation.Skip(dictionaryImplementation.Count - Period).Take(Period).ToDictionary(pair => pair.Key, pair => pair.Value);
            //    item.Value.HistorySlice = slice;
            //}

            dictionaryImplementation.Add(item.Key, item.Value);

        }

        public void Clear()
        {
            dictionaryImplementation.Clear();
        }

        public bool Contains(KeyValuePair<DateTime, Candle> item)
        {
            return dictionaryImplementation.Contains(item);
        }

        public void CopyTo(KeyValuePair<DateTime, Candle>[] array, int arrayIndex)
        {
            dictionaryImplementation.CopyTo(array, arrayIndex);
        }

        public bool Remove(KeyValuePair<DateTime, Candle> item)
        {
            return dictionaryImplementation.Remove(item.Key);
        }

        public int Count => dictionaryImplementation.Count;

        public bool IsReadOnly => false;

        public bool ContainsKey(DateTime key)
        {
            return dictionaryImplementation.ContainsKey(key);
        }

        public void Add(DateTime key, Candle value)
        {
            Add(new KeyValuePair<DateTime, Candle>(key, value));
        }

        public bool Remove(DateTime key)
        {
            return dictionaryImplementation.Remove(key);
        }

        public bool TryGetValue(DateTime key, out Candle value)
        {
            return dictionaryImplementation.TryGetValue(key, out value);
        }

        public Candle this[DateTime key]
        {
            get => dictionaryImplementation[key];
            set => dictionaryImplementation[key] = value;
        }

        public ICollection<DateTime> Keys => dictionaryImplementation.Keys;

        public ICollection<Candle> Values => dictionaryImplementation.Values;
    }

    public class Candle
    {
        public DateTime DateTime;
        public double O, H, L, C, V;

        public double Body => Math.Abs(O - C);
        public double Tail => Math.Abs(Math.Min(O, C) - L);
        public double Wick => Math.Abs(Math.Max(O, C) - H);

        public double GrossBody => H - L;

        public Dictionary<DateTime, Candle> HistorySlice { get; set; } = new Dictionary<DateTime, Candle>();

        public Tuple<double, double> AvgStdBody => HistorySlice.Select(kvp => kvp.Value.Body).MeanStandardDeviation();
        public Tuple<double, double> AvgStdTail => HistorySlice.Select(kvp => kvp.Value.Tail).MeanStandardDeviation();
        public Tuple<double, double> AvgStdWick => HistorySlice.Select(kvp => kvp.Value.Wick).MeanStandardDeviation();
        public Tuple<double, double> AvgStdVol => HistorySlice.Select(kvp => kvp.Value.V).MeanStandardDeviation();
        public Tuple<double, double> AvgStdGBody => HistorySlice.Select(kvp => kvp.Value.GrossBody).MeanStandardDeviation();

        public double SigmaBody => Math.Abs(Math.Abs(AvgStdBody.Item1) - Math.Abs(Body)) / AvgStdBody.Item2;
        public double SigmaTail => Math.Abs(Math.Abs(AvgStdTail.Item1) - Math.Abs(Tail)) / AvgStdTail.Item2;
        public double SigmaWick => Math.Abs(Math.Abs(AvgStdWick.Item1) - Math.Abs(Wick)) / AvgStdWick.Item2;
        public double SigmaVol => Math.Abs(Math.Abs(AvgStdVol.Item1) - Math.Abs(V)) / AvgStdVol.Item2;
        public double SigmaGBody => Math.Abs(Math.Abs(AvgStdGBody.Item1) - Math.Abs(GrossBody)) / AvgStdGBody.Item2;

        public double SigmaAlertThreshold { get; set; } = 5;

        public bool Alert => //SigmaGBody >= SigmaAlertThreshold &&
        SigmaBody >= SigmaAlertThreshold &&
        SigmaVol >= SigmaAlertThreshold;
        //(SigmaTail >= SigmaAlertThreshold || SigmaWick >= SigmaAlertThreshold);
    }
}
