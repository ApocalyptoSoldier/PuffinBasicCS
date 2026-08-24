using Org.Puffinbasic.Error;
using Java.Util.Function;
using Org.Puffinbasic.Error.PuffinBasicSemanticError.ErrorCode;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;

namespace Org.Puffinbasic.Runtime
{
    public class Numbers
    {
        public static int ParseInt32(string value, Supplier<string> lineSupplier)
        {
            try
            {
                return int.Parse(value);
            }
            catch (FormatException e)
            {
                throw new PuffinBasicSemanticError(BAD_NUMBER, lineSupplier.Get(), "Failed to parse number as int32: " + value);
            }
        }

        public static int ParseInt32(string value, int @base, Supplier<string> lineSupplier)
        {
            try
            {
                return Convert.ToInt32(value, @base);
            }
            catch (FormatException e)
            {
                throw new PuffinBasicSemanticError(BAD_NUMBER, lineSupplier.Get(), "Failed to parse number as int32: " + value);
            }
        }

        public static long ParseInt64(string value, Supplier<string> lineSupplier)
        {
            try
            {
                return long.Parse(value);
            }
            catch (FormatException e)
            {
                throw new PuffinBasicSemanticError(BAD_NUMBER, lineSupplier.Get(), "Failed to parse number as int64: " + value);
            }
        }

        public static long ParseInt64(string value, int @base, Supplier<string> lineSupplier)
        {
            try
            {
                return Convert.ToInt64(value, @base);
            }
            catch (FormatException e)
            {
                throw new PuffinBasicSemanticError(BAD_NUMBER, lineSupplier.Get(), "Failed to parse number as int64: " + value);
            }
        }

        public static float ParseFloat32(string value, Supplier<string> lineSupplier)
        {
            try
            {
                return float.Parse(value);
            }
            catch (FormatException e)
            {
                throw new PuffinBasicSemanticError(BAD_NUMBER, lineSupplier.Get(), "Failed to parse number as float32: " + value);
            }
        }

        public static double ParseFloat64(string value, Supplier<string> lineSupplier)
        {
            try
            {
                return Double.Parse(value);
            }
            catch (FormatException e)
            {
                throw new PuffinBasicSemanticError(BAD_NUMBER, lineSupplier.Get(), "Failed to parse number as float64: " + value);
            }
        }
    }
}

