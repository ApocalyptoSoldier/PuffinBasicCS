//using It.Unimi.Dsi.Fastutil.Ints;
//using Org.Apache.Commons.Math3.Stat.Descriptive;
namespace Org.Puffinbasic.Runtime
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;

    internal class ArrayStatistics
    {
        List<double> values = new List<double>();

        public void AddValue(int value) { values.Add(value); }
        public void AddValue(long value) { values.Add(value); }
        public void AddValue(double value) { values.Add(value); }
        public void AddValue(float value) { values.Add(value); }

        public double GetSum() => values.Sum();

        public double GetMean() => values.Average();

        // https://stackoverflow.com/a/57862581
        public double GetVariance() { 
            double variance = 0.0;

            double mean = GetMean();

            double variance = 0.0;
            foreach (int value in values)
                variance += Math.Pow(value - mean, 2.0);
            
            return variance / values.Length;
        }

        // https://stackoverflow.com/a/8137455
        public double GetPercentile(double percentile)
        {
            values.Sort();
            int N = values.Count;
            double n = (N - 1) * percentile + 1;
            // Another method: double n = (N + 1) * percentile;
            if (n == 1d) return values[0];
            else if (n == N) return values[N - 1];
            else
            {
                int k = (int)n;
                double d = n - k;
                return values[k - 1] + d * (values[k] - values[k - 1]);
            }
        }
    }
}