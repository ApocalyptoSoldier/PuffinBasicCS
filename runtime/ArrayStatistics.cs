//using It.Unimi.Dsi.Fastutil.Ints;
//using Org.Apache.Commons.Math3.Stat.Descriptive;
namespace Org.Puffinbasic.Runtime
{
    using System;
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

        public double GetMean() => values.Sum() / values.Count;

        // https://stackoverflow.com/a/57862581
        public double GetVariance() { 
            double mean = GetMean();

            double variance = 0.0;
            foreach (var value in values)
                variance += Math.Pow(value - mean, 2.0);
            
            return variance / (values.Count - 1);
        }

        // https://stackoverflow.com/a/8137455
        public double GetPercentile(double percentile)
        {
            values.Sort();

            double realIndex = (percentile / 100) * (values.Count - 1);
            int index = (int)realIndex; // If we ceil this then PCT matches the original test case, but MEDIAN does not, and vice versa
            double frac = realIndex - index;
            if (index + 1 < values.Count)
                return values[index] * (1 - frac) + values[index + 1] * frac;
            else
                return values[index];
        }
    }
}