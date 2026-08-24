using It.Unimi.Dsi.Fastutil.Objects;

//using Java.Text;
//using Java.Util;

using Org.Puffinbasic.Error;
using Org.Puffinbasic.Error.PuffinBasicRuntimeError.ErrorCode;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;

namespace Org.Puffinbasic.Runtime
{
    public class Formatter
    {
        public sealed class FormatterCache
        {
            private readonly Dictionary<string, IFormatter> cache;
            public FormatterCache()
            {
                cache = new Dictionary<string, IFormatter>();
            }

            public IFormatter Get(string format)
            {
                if (!cache.TryGetValue(format, out IFormatter formatter))
                    cache[format] = Formatter.GetFormatter();
                return formatter;
    
                //return cache.ComputeIfAbsent(format, Formatter.GetFormatter());
            }
        }

        public interface IIFormatter
        {
            string Format(object o);
            bool SupportsNumeric();
            bool SupportsString();
        }

        /// <summary>
        /// Examples:
        /// <pre>
        ///     **$##.## formats -21.2 to -$**21.20
        /// </pre>
        /// <pre>
        /// '#' specifies 1 digit position.
        /// 
        /// '.' specifies decimal point.
        /// 
        /// ',' adds comma in formatted number.
        /// 
        /// First optional prefix:
        /// '+' prefix will add a sign prefix.
        /// '-' prefix will add a minus prefix for negative number.
        /// 
        /// Next optional prefix:
        /// '**' causes leading spaces to be filled with '*' and specifies 2 more digit positions.
        /// '**$' adds dollar prefix, causes leading spaces to be filled with '*' and
        ///       specifies 2 more digit positions.
        /// '$$' add dollar prefix and specifies 1 more difit position.
        /// 
        /// First optional suffix:
        /// '+' suffix will add a sign suffix.
        /// '-' suffix will add a minus suffix for negative number.
        /// 
        /// Next optional suffix:
        /// '^^^^' suffix indicates scientific notation.
        /// 
        /// </pre>
        /// </summary>
        public sealed class NumberFormatter : IIFormatter
        {
            private readonly DecimalFormat decimalFormat;
            private readonly bool scientific;
            private readonly bool signPrefix;
            private readonly bool signSuffix;
            private readonly bool minusSuffix;
            private readonly bool shouldFill;
            private readonly bool dollar;
            public NumberFormatter(string format)
            {

                // Handle prefix '+' or '-'
                if (format.StartsWith("+"))
                {
                    signPrefix = true;
                    format = format.Substring(1);
                }
                else if (format.StartsWith("-"))
                {
                    signPrefix = false;
                    format = format.Substring(1);
                }
                else
                {
                    signPrefix = false;
                }


                // Handle suffix '+' or '-'
                if (format.EndsWith("+"))
                {
                    signSuffix = true;
                    minusSuffix = false;
                    format = format.Substring(0, format.Length - 1);
                }
                else if (format.EndsWith("-"))
                {
                    signSuffix = false;
                    minusSuffix = true;
                    format = format.Substring(0, format.Length - 1);
                }
                else
                {
                    signSuffix = false;
                    minusSuffix = false;
                }


                // Handle scientific notation
                if (format.EndsWith("^^^^"))
                {
                    format = format.Replace("^^^^", "E00");
                    scientific = true;
                }
                else
                {
                    scientific = false;
                }


                // Replace # with 0 so that result is filled with 0 prefixes
                format = format.Replace("#", "0");

                // Handle **, **$, and $$ prefix
                bool dollar = false;
                int numToFill = 0;
                int removeFromFormat = 0;
                if (format.StartsWith("**$"))
                {
                    numToFill = 2;
                    removeFromFormat = 3;
                    dollar = true;
                }
                else if (format.StartsWith("**"))
                {
                    numToFill = 2;
                    removeFromFormat = 2;
                }
                else if (format.StartsWith("$$"))
                {
                    numToFill = 1;
                    removeFromFormat = 2;
                    dollar = true;
                }

                if (removeFromFormat > 0)
                {
                    format = format.Substring(removeFromFormat);
                }

                this.dollar = dollar;
                this.shouldFill = numToFill > 0;
                if (numToFill > 0)
                {
                    var b = new byte[numToFill];
                    Arrays.Fill(b, 0, numToFill, (byte)'0');
                    format = new string (b) + format;
                }

                this.decimalFormat = new DecimalFormat(format);
            }

            // Handle prefix '+' or '-'
            // Handle suffix '+' or '-'
            // Handle scientific notation
            // Replace # with 0 so that result is filled with 0 prefixes
            // Handle **, **$, and $$ prefix
            public string Format(object o)
            {
                if (o is long)
                {
                    return Format((long)o);
                }
                else if (o is Double)
                {
                    return Format((double)o);
                }
                else
                {
                    throw new PuffinBasicInternalError(typeof(NumberFormatter).GetSimpleName() + ": data type mismatch: " + o.GetType());
                }
            }

            // Handle prefix '+' or '-'
            // Handle suffix '+' or '-'
            // Handle scientific notation
            // Replace # with 0 so that result is filled with 0 prefixes
            // Handle **, **$, and $$ prefix
            public bool SupportsNumeric()
            {
                return true;
            }

            // Handle prefix '+' or '-'
            // Handle suffix '+' or '-'
            // Handle scientific notation
            // Replace # with 0 so that result is filled with 0 prefixes
            // Handle **, **$, and $$ prefix
            public bool SupportsString()
            {
                return false;
            }

            // Handle prefix '+' or '-'
            // Handle suffix '+' or '-'
            // Handle scientific notation
            // Replace # with 0 so that result is filled with 0 prefixes
            // Handle **, **$, and $$ prefix
            public string Format(long value)
            {
                bool isNegative = value < 0;
                if (isNegative)
                {
                    value = -value;
                }

                return Format(decimalFormat.Format(value), isNegative);
            }

            // Handle prefix '+' or '-'
            // Handle suffix '+' or '-'
            // Handle scientific notation
            // Replace # with 0 so that result is filled with 0 prefixes
            // Handle **, **$, and $$ prefix
            public string Format(double value)
            {
                bool isNegative = value < 0;
                if (isNegative)
                {
                    value = -value;
                }

                return Format(decimalFormat.Format(value), isNegative);
            }

            // Handle prefix '+' or '-'
            // Handle suffix '+' or '-'
            // Handle scientific notation
            // Replace # with 0 so that result is filled with 0 prefixes
            // Handle **, **$, and $$ prefix
            private string Format(string result, bool isNegative)
            {

                // Handle scientific
                if (scientific)
                {
                    if (!result.Contains("E-"))
                    {
                        result = result.Replace("E", "E+");
                    }
                }


                // If ** or **$ is set, replace leading 0s with *s.
                // If ** or **$ is not set, remove leading 0s.
                var dest = new char[result.Length];
                bool checkForLeadingZero = true;
                int fillToLoc = -1;
                for (int i = 0; i < result.Length; i++)
                {
                    var c = result[i];
                    if ((c >= '1' && c <= '9'))
                    {
                        checkForLeadingZero = false;
                    }

                    if (checkForLeadingZero)
                    {
                        if (c == '0')
                        {
                            c = '*';
                            fillToLoc = i;
                        }
                    }


                    // Copy char to dest
                    dest[i] = (char)c;
                }

                if (fillToLoc >= 0)
                {
                    result = shouldFill ? new string (dest) : new string (dest).Substring(fillToLoc + 1);
                    if (result.StartsWith(","))
                    {
                        result = result.Substring(1);
                    }
                }


                // Add $ prefix
                if (dollar)
                {
                    result = '$' + result;
                }


                // Add sign prefix
                if (signPrefix)
                {
                    result = (isNegative ? '-' : '+') + result;
                }


                // Add minus prefix
                if (isNegative && !minusSuffix && !result.StartsWith("-"))
                {
                    result = '-' + result;
                }


                // Add sign suffix
                if (signSuffix)
                {
                    result = result + (isNegative ? '-' : '+');
                }


                // Add minus suffix
                if (isNegative && minusSuffix)
                {
                    result = result + '-';
                }

                return result;
            }
        }

        public sealed class FirstCharFormatter : IIFormatter
        {
            public string Format(object o)
            {
                if (o is string)
                {
                    var str = (string)o;
                    return String.IsNullOrEmpty(str) ? "" : str.Substring(0, 1);
                }
                else
                {
                    throw new PuffinBasicInternalError(typeof(FirstCharFormatter).GetSimpleName() + ": data type mismatch: " + o.GetType());
                }
            }

            public bool SupportsNumeric()
            {
                return false;
            }

            public bool SupportsString()
            {
                return true;
            }
        }

        public sealed class NSpacesFormatter : IIFormatter
        {
            private readonly int length;
            public NSpacesFormatter(string format)
            {
                if (format.Length >= 2)
                {
                    length = format.Length;
                    var spaces = format.Substring(1, format.Length - 1);
                    for (int i = 0; i < spaces.Length; i++)
                    {
                        if (spaces[i] != ' ')
                        {
                            throw new PuffinBasicRuntimeError(ILLEGAL_FUNCTION_PARAM, "Expected spaces in n+2 spaces formatter, but found: " + spaces[i]);
                        }
                    }
                }
                else
                {
                    throw new PuffinBasicRuntimeError(ILLEGAL_FUNCTION_PARAM, "Bad n+2 formatter string: " + format);
                }
            }

            public string Format(object o)
            {
                if (o is string)
                {
                    var str = (string)o;
                    var strlen = str.Length;
                    if (strlen > this.length)
                    {
                        return str.Substring(0, this.length);
                    }
                    else
                    {
                        byte[] bytes = new byte[this.length];
                        System.Arraycopy(str.GetBytes(), 0, bytes, 0, str.Length);
                        java.util.Arrays.Fill(bytes, str.Length, length, (byte)' ');
                        return new string (bytes);
                    }
                }
                else
                {
                    throw new PuffinBasicInternalError(typeof(FirstCharFormatter).GetSimpleName() + ": data type mismatch: " + o.GetType());
                }
            }

            public bool SupportsNumeric()
            {
                return false;
            }

            public bool SupportsString()
            {
                return true;
            }
        }

        public sealed class VarLenStringFormatter : IIFormatter
        {
            public string Format(object o)
            {
                if (o is string)
                {
                    return (string)o;
                }
                else
                {
                    throw new PuffinBasicInternalError(typeof(FirstCharFormatter).GetSimpleName() + ": data type mismatch: " + o.GetType());
                }
            }

            public bool SupportsNumeric()
            {
                return false;
            }

            public bool SupportsString()
            {
                return true;
            }
        }

        public static IIFormatter GetFormatter(string format)
        {
            if (format.Equals("!"))
            {
                return new FirstCharFormatter();
            }
            else if (format.Equals("&"))
            {
                return new VarLenStringFormatter();
            }
            else if (format.StartsWith("\\") && format.EndsWith("\\"))
            {
                return new NSpacesFormatter(format);
            }
            else
            {
                return new NumberFormatter(format);
            }
        }

        public static string PrintFormatInt32(int value)
        {
            return value < 0 ? value + " " : " " + value + " ";
        }

        public static string PrintFormatInt64(long value)
        {
            return value < 0 ? value + " " : " " + value + " ";
        }

        public static string PrintFormatFloat32(float value)
        {
            return value < 0 ? value + " " : " " + value + " ";
        }

        public static string PrintFormatFloat64(double value)
        {
            return value < 0 ? value + " " : " " + value + " ";
        }

        public static string PrintFormatString(string value)
        {
            return value;
        }

        public static string WriteFormatInt32(int value)
        {
            return value.ToString();
        }

        public static string WriteFormatInt64(long value)
        {
            return value.ToString();
        }

        public static string WriteFormatFloat32(float value)
        {
            return value.ToString();
        }

        public static string WriteFormatFloat64(double value)
        {
            return value.ToString();
        }

        public static string WriteFormatString(string value)
        {
            if (value.Contains("\""))
            {

                // Expects unescaped quotes
                value = value.Replace("\"", "\\\"");
            }

            return "\"" + value + "\"";
        }
    }
}

