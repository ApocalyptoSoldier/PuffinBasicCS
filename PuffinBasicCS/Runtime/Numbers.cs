namespace PuffinBasicCS.Runtime
{
    using PuffinBasicCS.Error;
    //using Java.Util.Function;
    using static PuffinBasicCS.Error.PuffinBasicSemanticError.ErrorCode;

    using System;
    using System.Globalization;

    public class Numbers
    {
        public static int ParseInt32(string value, string line)
        {
            try
            {
                return Int32.Parse(value);
            }
            catch (FormatException)
            {
                throw new PuffinBasicSemanticError(BAD_NUMBER, line, "Failed to parse number as int32: " + value);
            }
        }

        public static int ParseInt32(string value, int @base, string line)
        {
            try
            {
                return Convert.ToInt32(value, @base);
            }
            catch (FormatException)
            {
                throw new PuffinBasicSemanticError(BAD_NUMBER, line, "Failed to parse number as int32: " + value);
            }
        }

        public static long ParseInt64(string value, string line)
        {
            try
            {
                return Int64.Parse(value);
            }
            catch (FormatException)
            {
                throw new PuffinBasicSemanticError(BAD_NUMBER, line, "Failed to parse number as int64: " + value);
            }
        }

        public static long ParseInt64(string value, int @base, string line)
        {
            try
            {
                return Convert.ToInt64(value, @base);
            }
            catch (FormatException)
            {
                throw new PuffinBasicSemanticError(BAD_NUMBER, line, "Failed to parse number as int64: " + value);
            }
        }

        public static float ParseFloat32(string value, string line)
        {
            try
            {
                return Single.Parse(value, CultureInfo.InvariantCulture);
            }
            catch (FormatException)
            {
                throw new PuffinBasicSemanticError(BAD_NUMBER, line, "Failed to parse number as float32: " + value);
            }
        }

        public static double ParseFloat64(string value, string line)
        {
            try
            {
                return Double.Parse(value);
            }
            catch (FormatException)
            {
                throw new PuffinBasicSemanticError(BAD_NUMBER, line, "Failed to parse number as float64: " + value);
            }
        }
    }
}

