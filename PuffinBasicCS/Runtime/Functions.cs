//using Com.Google.Common.Base;
//using It.Unimi.Dsi.Fastutil.Doubles;
namespace PuffinBasicCS.Runtime
{
    using PuffinBasicCS.Domain;
    using PuffinBasicCS.Error;
    using static PuffinBasicCS.Domain.STObjects;
    using static PuffinBasicCS.Parser.PuffinBasicIR;
    //using Java.Nio;
    //using Java.Nio.Charset;
    //using Java.Time;
    //using Java.Util;
    //using Java.Util.Concurrent;
    using static PuffinBasicCS.Domain.PuffinBasicSymbolTable;
    using static PuffinBasicCS.Domain.STObjects.PuffinBasicAtomTypeId;
    using static PuffinBasicCS.Domain.STObjects.PuffinBasicTypeId;
    using static PuffinBasicCS.Error.PuffinBasicRuntimeError.ErrorCode;
    using System;
    using System.Linq;
    using System.Text;
    using System.Text.RegularExpressions;
    using PuffinBasicCS.File;
    using PuffinBasicCS.Common;

    public class Functions
    {
        public static void Abs(PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            var op1Entry = symbolTable[instruction.op1];
            var op1 = op1Entry.Value;
            var result = symbolTable[instruction.result].Value;
            switch (op1Entry.Type.AtomTypeId)
            {
                case INT32:
                    result.SetInt32(Math.Abs(op1.GetInt32()));
                    break;
                case INT64:
                    result.SetInt64(Math.Abs(op1.GetInt64()));
                    break;
                case FLOAT:
                    result.SetFloat32(Math.Abs(op1.GetFloat32()));
                    break;
                case DOUBLE:
                    result.SetFloat64(Math.Abs(op1.GetFloat64()));
                    break;
                default:
                    ThrowUnsupportedType(op1Entry.Type.AtomTypeId);
                    break;
            }
        }

        public static void Asc(PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            var value = symbolTable[instruction.op1].Value.GetString();
            if (String.IsNullOrEmpty(value))
            {
                throw new PuffinBasicRuntimeError(ILLEGAL_FUNCTION_PARAM, $"IllegalFunctionCall: null/empty string: '{value}'");
            }

            var ascii = (int)value[0];
            symbolTable[instruction.result].Value.SetInt32(ascii);
        }

        public static void Sin(PuffinBasicSymbolTable symbolTable, Instruction instruction) => ApplyDoubleFunction(symbolTable, instruction, d => Math.Sin(d));

        public static void Cos(PuffinBasicSymbolTable symbolTable, Instruction instruction) => ApplyDoubleFunction(symbolTable, instruction, d => Math.Cos(d));

        public static void Tan(PuffinBasicSymbolTable symbolTable, Instruction instruction) => ApplyDoubleFunction(symbolTable, instruction, d => Math.Tan(d));

        public static void Asin(PuffinBasicSymbolTable symbolTable, Instruction instruction) => ApplyDoubleFunction(symbolTable, instruction, d => Math.Asin(d));

        public static void Acos(PuffinBasicSymbolTable symbolTable, Instruction instruction) => ApplyDoubleFunction(symbolTable, instruction, d => Math.Acos(d));

        public static void Atn(PuffinBasicSymbolTable symbolTable, Instruction instruction) => ApplyDoubleFunction(symbolTable, instruction, d => Math.Atan(d));

        public static void Sinh(PuffinBasicSymbolTable symbolTable, Instruction instruction) => ApplyDoubleFunction(symbolTable, instruction, d => Math.Sinh(d));

        public static void Cosh(PuffinBasicSymbolTable symbolTable, Instruction instruction) => ApplyDoubleFunction(symbolTable, instruction, d => Math.Cosh(d));

        public static void Tanh(PuffinBasicSymbolTable symbolTable, Instruction instruction) => ApplyDoubleFunction(symbolTable, instruction, d => Math.Tanh(d));

        public static void Sqr(PuffinBasicSymbolTable symbolTable, Instruction instruction) => ApplyDoubleFunction(symbolTable, instruction, d => Math.Sqrt(d));

        public static void Log(PuffinBasicSymbolTable symbolTable, Instruction instruction) => ApplyDoubleFunction(symbolTable, instruction, d => Math.Log(d));

        public static void Log10(PuffinBasicSymbolTable symbolTable, Instruction instruction) => ApplyDoubleFunction(symbolTable, instruction, d => Math.Log10(d));

        public static void Log2(PuffinBasicSymbolTable symbolTable, Instruction instruction) => ApplyDoubleFunction(symbolTable, instruction, d => Math.Log(d) / Math.Log(2));

        public static void Exp(PuffinBasicSymbolTable symbolTable, Instruction instruction) => ApplyDoubleFunction(symbolTable, instruction, d => Math.Exp(d));

        public static void ToRad(PuffinBasicSymbolTable symbolTable, Instruction instruction) => ApplyDoubleFunction(symbolTable, instruction, d => d * (Math.PI / 180.0));

        public static void ToDeg(PuffinBasicSymbolTable symbolTable, Instruction instruction) => ApplyDoubleFunction(symbolTable, instruction, d => d * (180.0 / Math.PI));

        public static void Floor(PuffinBasicSymbolTable symbolTable, Instruction instruction) => ApplyDoubleFunction(symbolTable, instruction, d => Math.Floor(d));

        public static void Ceil(PuffinBasicSymbolTable symbolTable, Instruction instruction) => ApplyDoubleFunction(symbolTable, instruction, d => Math.Ceiling(d));

        public static void Round(PuffinBasicSymbolTable symbolTable, Instruction instruction) => ApplyDoubleFunction(symbolTable, instruction, d => Math.Round(d));

        public static void E(PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            var result = symbolTable[instruction.result].Value;
            result.SetFloat64(Math.E);
        }

        public static void Pi(PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            var result = symbolTable[instruction.result].Value;
            result.SetFloat64(Math.PI);
        }

        public static void Min(PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            var v1 = symbolTable[instruction.op1].Value;
            var v2 = symbolTable[instruction.op2].Value;
            var resultEntry = symbolTable[instruction.result];
            var result = resultEntry.Value;
            switch (resultEntry.Type.AtomTypeId)
            {
                case INT32:
                    result.SetInt32(Math.Min(v1.GetInt32(), v2.GetInt32()));
                    break;
                case INT64:
                    result.SetInt64(Math.Min(v1.GetInt64(), v2.GetInt64()));
                    break;
                case FLOAT:
                    result.SetFloat32(Math.Min(v1.GetFloat32(), v2.GetFloat32()));
                    break;
                case DOUBLE:
                    result.SetFloat64(Math.Min(v1.GetFloat64(), v2.GetFloat64()));
                    break;
                default:
                    ThrowUnsupportedType(resultEntry.Type.AtomTypeId);
                    break;
            }
        }

        public static void Max(PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            var v1 = symbolTable[instruction.op1].Value;
            var v2 = symbolTable[instruction.op2].Value;
            var resultEntry = symbolTable[instruction.result];
            var result = resultEntry.Value;
            switch (resultEntry.Type.AtomTypeId)
            {
                case INT32:
                    result.SetInt32(Math.Max(v1.GetInt32(), v2.GetInt32()));
                    break;
                case INT64:
                    result.SetInt64(Math.Max(v1.GetInt64(), v2.GetInt64()));
                    break;
                case FLOAT:
                    result.SetFloat32(Math.Max(v1.GetFloat32(), v2.GetFloat32()));
                    break;
                case DOUBLE:
                    result.SetFloat64(Math.Max(v1.GetFloat64(), v2.GetFloat64()));
                    break;
                default:
                    ThrowUnsupportedType(resultEntry.Type.AtomTypeId);
                    break;
            }
        }

        private static void ApplyDoubleFunction(PuffinBasicSymbolTable symbolTable, Instruction instruction, Func<double, double> function)
        {
            var value = symbolTable[instruction.op1].Value.GetFloat64();
            var result = symbolTable[instruction.result].Value;
            result.SetFloat64(function.Invoke(value));
        }

        public static void Cint(PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            var entry = symbolTable[instruction.op1].Value;
            double value = entry.GetFloat64();
            if (value < int.MinValue || value > int.MaxValue)
            {
                throw new PuffinBasicRuntimeError(DATA_OUT_OF_RANGE, "CINT: value: " + value + " overflows an int32");
            }

            symbolTable[instruction.result].Value.SetInt32(entry.GetRoundedInt32());
        }

        public static void Clng(PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            var entry = symbolTable[instruction.op1].Value;
            double value = entry.GetFloat64();
            if (value < long.MinValue || value > long.MaxValue)
            {
                throw new PuffinBasicRuntimeError(DATA_OUT_OF_RANGE, $"CLONG: value: {value} overflows an int64");
            }

            symbolTable[instruction.result].Value.SetInt64(entry.GetRoundedInt64());
        }

        public static void Csng(PuffinBasicSymbolTable symbolTable, Instruction instruction) => symbolTable[instruction.result].Value.SetFloat32(symbolTable[instruction.op1].Value.GetFloat32());

        public static void Cdbl(PuffinBasicSymbolTable symbolTable, Instruction instruction) => symbolTable[instruction.result].Value.SetFloat64(symbolTable[instruction.op1].Value.GetFloat64());

        public static void Chrdlr(PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            int intValue = symbolTable[instruction.op1].Value.GetInt32();
            char charValue = (char)intValue;
            symbolTable[instruction.result].Value.SetString(new string (new char[] { charValue }));
        }

        public static void Mkidlr(PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            var value = symbolTable[instruction.op1].Value.GetInt32();
            byte[] b = new byte[4];
            BitConverter.GetBytes(value).CopyTo(b, 0);

            symbolTable[instruction.result].Value.SetString(ISOEncoding.GetString(b));
        }

        public static void Mkldlr(PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            var value = symbolTable[instruction.op1].Value.GetInt64();
            byte[] b = new byte[8];
            BitConverter.GetBytes(value).CopyTo(b, 0);

            symbolTable[instruction.result].Value.SetString(ISOEncoding.GetString(b));
        }

        public static void Mksdlr(PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            var value = symbolTable[instruction.op1].Value.GetFloat32();
            byte[] b = new byte[4];
            BitConverter.GetBytes(value).CopyTo(b, 0);

            symbolTable[instruction.result].Value.SetString(ISOEncoding.GetString(b));
        }

        public static void Mkddlr(PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            var value = symbolTable[instruction.op1].Value.GetFloat64();
            byte[] b = new byte[8];
            BitConverter.GetBytes(value).CopyTo(b, 0);

            symbolTable[instruction.result].Value.SetString(ISOEncoding.GetString(b));
        }

        public static void Cvi(PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            string value = symbolTable[instruction.op1].Value.GetString();
            if (value.Length != 4)
            {
                throw new PuffinBasicRuntimeError(DATA_OUT_OF_RANGE, $"CVI$: value: {value} length must be 4, found: {value.Length}");
            }

            int intValue = BitConverter.ToInt32(ISOEncoding.GetBytes(value), 0);
            symbolTable[instruction.result].Value.SetInt32(intValue);
        }

        public static void Cvl(PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            string value = symbolTable[instruction.op1].Value.GetString();
            if (value.Length != 8)
            {
                throw new PuffinBasicRuntimeError(DATA_OUT_OF_RANGE, $"CVL$: value: {value} length must be 8, found: {value.Length}");
            }

            long longValue = BitConverter.ToInt64(ISOEncoding.GetBytes(value), 0);
            symbolTable[instruction.result].Value.SetInt64(longValue);
        }

        public static void Cvs(PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            string value = symbolTable[instruction.op1].Value.GetString();
            if (value.Length != 4)
            {
                throw new PuffinBasicRuntimeError(DATA_OUT_OF_RANGE, $"CVS$: value: {value} length must be 4, found: {value.Length}");
            }

            float floatValue = BitConverter.ToSingle(ISOEncoding.GetBytes(value), 0);

            symbolTable[instruction.result].Value.SetFloat32(floatValue);
        }

        public static void Cvd(PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            string value = symbolTable[instruction.op1].Value.GetString();
            if (value.Length != 8)
            {
                throw new PuffinBasicRuntimeError(DATA_OUT_OF_RANGE, $"CVD$: value: {value} length must be 8, found: {value.Length}");
            }

            double doubleValue = BitConverter.ToDouble(ISOEncoding.GetBytes(value), 0);
            symbolTable[instruction.result].Value.SetFloat64(doubleValue);
        }

        public static void Spacedlr(PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            int len = symbolTable[instruction.op1].Value.GetInt32();
            byte[] bytes = new byte[len];
            for (int i = 0; i < len; i++)
            {
                bytes[i] = (byte)' ';
            }

            string str = ISOEncoding.GetString(bytes);
            symbolTable[instruction.result].Value.SetString(str);
        }

        public static void Val(PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            var str = symbolTable[instruction.op1].Value.GetString();
            var result = symbolTable[instruction.result].Value;
            try
            {
                result.SetFloat64(Double.Parse(str));
            }
            catch (FormatException)
            {
                throw new PuffinBasicRuntimeError(DATA_OUT_OF_RANGE, $"Failed to parse string: {str} as numeric");
            }
        }

        public static void Fnint(PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            var vEntry = symbolTable[instruction.op1];
            var v = vEntry.Value;
            var result = symbolTable[instruction.result].Value;
            switch (vEntry.Type.AtomTypeId)
            {
                case INT32:
                    result.SetInt32(v.GetInt32());
                    break;
                case INT64:
                    result.SetInt64(v.GetInt64());
                    break;
                case FLOAT:
                    result.SetFloat32((float)Math.Floor(v.GetFloat32()));
                    break;
                case DOUBLE:
                    result.SetFloat64(Math.Floor(v.GetFloat64()));
                    break;
                default:
                    ThrowUnsupportedType(vEntry.Type.AtomTypeId);
                    break;
            }
        }

        public static void Fix(PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            var vEntry = symbolTable[instruction.op1];
            var v = vEntry.Value;
            var result = symbolTable[instruction.result].Value;
            switch (vEntry.Type.AtomTypeId)
            {
                case INT32:
                    result.SetInt32(v.GetInt32());
                    break;
                case INT64:
                    result.SetInt64(v.GetInt64());
                    break;
                case FLOAT:
                    result.SetFloat32((float)(v.GetFloat32() < 0 ? Math.Ceiling(v.GetFloat32()) : Math.Floor(v.GetFloat32())));
                    break;
                case DOUBLE:
                    result.SetFloat64(v.GetFloat64() < 0 ? Math.Ceiling(v.GetFloat64()) : Math.Floor(v.GetFloat64()));
                    break;
                default:
                    ThrowUnsupportedType(vEntry.Type.AtomTypeId);
                    break;
            }
        }

        public static void Len(PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            var stEntry = symbolTable[instruction.op1];
            var value = stEntry.Value;
            int len;
            if (stEntry.Type.TypeId == ARRAY)
            {
                int axis = instruction.op2 != NULL_ID ? symbolTable[instruction.op2].Value.GetInt32() : 0;
                if (axis < 0 || axis >= value.GetNumArrayDimensions())
                {
                    throw new PuffinBasicRuntimeError(ILLEGAL_FUNCTION_PARAM, $"Bad axis={axis}, #dims={value.GetNumArrayDimensions()}");
                }

                len = value.GetArrayDimensions()[axis];
            }
            else if (stEntry.Value.HasLen)
            {
                len = value.Len;
            }
            else
            {
                throw new PuffinBasicRuntimeError(ILLEGAL_FUNCTION_PARAM, "Bad LEN() call!");
            }

            symbolTable[instruction.result].Value.SetInt32(len);
        }

        public static void Strdlr(PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            var numericEntry = symbolTable[instruction.op1];
            var numeric = numericEntry.Value;
            var dt = numericEntry.Type.AtomTypeId;
            string str;
            if (dt == INT32)
                str = numeric.GetInt32().ToString();
            else if (dt == INT64)
                str = numeric.GetInt64().ToString();
            else if (dt == FLOAT)
                str = numeric.GetFloat32().ToString();
            else
                str = numeric.GetFloat64().ToString();

            symbolTable[instruction.result].Value.SetString(str);
        }

        public static void Hexdlr(PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            var numericEntry = symbolTable[instruction.op1];
            var numeric = numericEntry.Value;
            var dt = numericEntry.Type.AtomTypeId;
            string str;
            if (dt == INT32)
            {
                str = numeric.GetInt32().ToString("X");
            }
            else if (dt == INT64)
            {
                str = numeric.GetInt64().ToString("X");
            }
            else if (dt == FLOAT)
            {
                str = numeric.GetFloat32().ToString("X");
            }
            else
            {
                str = numeric.GetFloat64().ToString("X");
            }

            symbolTable[instruction.result].Value.SetString(str);
        }

        public static void Octdlr(PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            var numericEntry = symbolTable[instruction.op1];
            var numeric = numericEntry.Value;
            var dt = numericEntry.Type.AtomTypeId;
            string str;
            if (dt == INT32 || dt == FLOAT)
            {
                str = Convert.ToString(numeric.GetInt32(), 8);
            }
            else
            {
                str = Convert.ToString(numeric.GetInt64(), 8);
            }

            symbolTable[instruction.result].Value.SetString(str);
        }

        public static void Leftdlr(PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            var x = symbolTable[instruction.op1].Value.GetString();
            var n = symbolTable[instruction.op2].Value.GetInt32();
            string result;
            if (n < 0)
            {
                throw new PuffinBasicRuntimeError(INDEX_OUT_OF_BOUNDS, "LEFT$: expected n >= 0, actual=" + n);
            }
            else if (n == 0)
            {
                result = "";
            }
            else if (n >= x.Length)
            {
                result = x;
            }
            else
            {
                result = x.Substring(0, n);
            }

            symbolTable[instruction.result].Value.SetString(result);
        }

        public static void Rightdlr(PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            var x = symbolTable[instruction.op1].Value.GetString();
            var n = symbolTable[instruction.op2].Value.GetInt32();
            var xlen = x.Length;
            string result;
            if (n < 0)
            {
                throw new PuffinBasicRuntimeError(INDEX_OUT_OF_BOUNDS, "RIGHT$: expected n >= 0, actual=" + n);
            }
            else if (n == 0)
            {
                result = "";
            }
            else if (n >= xlen)
            {
                result = x;
            }
            else
            {
                result = x.Substring(xlen - n, n);
            }

            symbolTable[instruction.result].Value.SetString(result);
        }

        public static void Instr(PuffinBasicSymbolTable symbolTable, Instruction instr0, Instruction instr)
        {
            var x = symbolTable[instr0.op1].Value.GetString();
            var y = symbolTable[instr0.op2].Value.GetString();
            var n = symbolTable[instr.op1].Value.GetInt32();
            var xlen = x.Length;
            var ylen = y.Length;
            int result;
            if (n <= 0)
            {
                throw new PuffinBasicRuntimeError(INDEX_OUT_OF_BOUNDS, "INSTR: expected n > 0, actual=" + n);
            }
            else if (n > xlen)
            {
                result = 0;
            }
            else if (ylen == 0)
            {
                result = n;
            }
            else
            {
                result = x.IndexOf(y, n - 1) + 1;
            }

            symbolTable[instr.result].Value.SetInt32(result);
        }

        public static void Middlr(PuffinBasicSymbolTable symbolTable, Instruction instr0, Instruction instr)
        {
            var x = symbolTable[instr0.op1].Value.GetString();
            var n = symbolTable[instr0.op2].Value.GetInt32();
            var m = symbolTable[instr.op1].Value.GetInt32();
            var xlen = x.Length;
            string result;
            if (n <= 0)
            {
                throw new PuffinBasicRuntimeError(INDEX_OUT_OF_BOUNDS, "INSTR: expected n > 0, actual=" + n);
            }
            else if (n > xlen || m == 0)
            {
                result = "";
            }
            else
            {
                // Take m (or all remaining if not specified) characters starting at n
                int length = Math.Min(xlen - n, xlen - m - n);
                result = x.Substring(n - 1, xlen - m - n);
            }

            symbolTable[instr.result].Value.SetString(result);
        }

        public static void Rnd(Random random, PuffinBasicSymbolTable symbolTable, Instruction instruction) => symbolTable[instruction.result].Value.SetFloat64(random.NextDouble());

        public static void Sgn(PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            var entry = symbolTable[instruction.op1];
            var numeric = entry.Value;
            var dt = entry.Type.AtomTypeId;
            int result;
            if (dt == INT32)
                result = Math.Sign(numeric.GetInt32());
            else if (dt == INT64)
                result = Math.Sign(numeric.GetInt64());
            else if (dt == FLOAT)
                result = Math.Sign(numeric.GetFloat32());
            else
                result = Math.Sign(numeric.GetFloat64());

            symbolTable[instruction.result].Value.SetInt32(result);
        }

        public static void Timer(PuffinBasicSymbolTable symbolTable, Instruction instruction) =>
            //var nowZoned = ZonedDateTime.Now();
            //var midnight = nowZoned.ToLocalDate().AtStartOfDay(nowZoned.GetZone()).ToInstant();
            //var duration = Duration.Between(midnight, DateTime.Now());
            //var seconds = duration.GetSeconds() + duration.GetNano() / 1000000000;

            symbolTable[instruction.result].Value.SetFloat64((DateTime.Now - DateTime.Now.Date).TotalMilliseconds);

        public static void TimerMillis(PuffinBasicSymbolTable symbolTable, Instruction instruction) =>
            //var nowZoned = ZonedDateTime.Now();
            //var midnight = nowZoned.ToLocalDate().AtStartOfDay(nowZoned.GetZone()).ToInstant();
            //var duration = Duration.Between(midnight, DateTime.Now());
            //var millis = TimeUnit.SECONDS.ToMillis(duration.GetSeconds()) + TimeUnit.NANOSECONDS.ToMillis(duration.GetNano());

            symbolTable[instruction.result].Value.SetInt64((long)(DateTime.Now - DateTime.Now.Date).TotalMilliseconds);

        public static void Stringdlr(PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            var n = symbolTable[instruction.op1].Value.GetInt32();
            var jOrxdlrEntry = symbolTable[instruction.op2];
            var jOrxdlr = jOrxdlrEntry.Value;
            string c;
            if (jOrxdlrEntry.Type.AtomTypeId == STRING)
            {
                if (String.IsNullOrEmpty(jOrxdlr.GetString()))
                {
                    throw new PuffinBasicRuntimeError(ILLEGAL_FUNCTION_PARAM, "STRING$: expected len(x%) > 0, actual=0");
                }

                c = jOrxdlr.GetString().Substring(0, 1);
            }
            else
            {
                int j = jOrxdlr.GetInt32();
                if (j < 0 || j > 255)
                {
                    throw new PuffinBasicRuntimeError(ILLEGAL_FUNCTION_PARAM, "STRING$: expected 0 <= j <= 255, actual=" + j);
                }

                c = ((char)jOrxdlr.GetInt32()).ToString();
            }

            string result;
            if (n < 0)
            {
                throw new PuffinBasicRuntimeError(INDEX_OUT_OF_BOUNDS, "STRING$: expected n >= 0, actual=" + n);
            }
            else if (n == 0)
            {
                result = "";
            }
            else
            {
                result = new StringBuilder(c.Length * n)
                    .Insert(0, c, n)
                    .ToString();
            }

            symbolTable[instruction.result].Value.SetString(result);
        }

        public static void Loc(PuffinBasicFiles files, PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            var fileNumber = symbolTable[instruction.op1].Value.GetInt32();
            var loc = files[fileNumber].GetCurrentRecordNumber();
            symbolTable[instruction.result].Value.SetInt32(loc);
        }

        public static void Lof(PuffinBasicFiles files, PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            var fileNumber = symbolTable[instruction.op1].Value.GetInt32();
            var lof = files[fileNumber].GetFileSizeInBytes();
            symbolTable[instruction.result].Value.SetInt64(lof);
        }

        public static void Eof(PuffinBasicFiles files, PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            var fileNumber = symbolTable[instruction.op1].Value.GetInt32();
            var eof = files[fileNumber].Eof();
            symbolTable[instruction.result].Value.SetInt32(eof ? -1 : 0);
        }

        public static void Inputdlr(PuffinBasicFiles files, PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            var x = symbolTable[instruction.op1].Value.GetInt32();
            var fileNumber = symbolTable[instruction.op2].Value.GetInt32();
            byte[] read;
            if (fileNumber < 0)
            {
                read = files.sys.ReadBytes(x);
            }
            else
            {
                throw new NotSupportedException();
            }

            symbolTable[instruction.result].Value.SetString(ISOEncoding.GetString(read));
        }

        public static void Environdlr(IEnvironment env, PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            var envvar = symbolTable[instruction.op1].Value.GetString();
            var result = env[envvar];
            symbolTable[instruction.result].Value.SetString(result);
        }

        public static void Splitdlr(PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            var str = symbolTable[instruction.op1].Value.GetString();
            var regex = symbolTable[instruction.op2].Value.GetString();
            String[] tokens = (new Regex(regex)).Split(str);
            STRING.CopyArray(tokens, symbolTable[instruction.result].Value);
        }

        public static void ThrowUnsupportedType(PuffinBasicAtomTypeId type) => throw new PuffinBasicInternalError("Data type " + type + " is not supported");
    }
}

