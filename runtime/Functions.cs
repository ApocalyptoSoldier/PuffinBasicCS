//using Com.Google.Common.Base;
//using It.Unimi.Dsi.Fastutil.Doubles;
using Org.Puffinbasic.Domain;
using static Org.Puffinbasic.Domain.STObjects;
using Org.Puffinbasic.Error;
using Org.Puffinbasic.File;
using static Org.Puffinbasic.Parser.PuffinBasicIR;
//using Java.Nio;
//using Java.Nio.Charset;
//using Java.Time;
//using Java.Util;
//using Java.Util.Concurrent;
using static Org.Puffinbasic.Domain.PuffinBasicSymbolTable;
using static Org.Puffinbasic.Domain.STObjects.PuffinBasicAtomTypeId;
using static Org.Puffinbasic.Domain.STObjects.PuffinBasicTypeId;
using static Org.Puffinbasic.Error.PuffinBasicRuntimeError.ErrorCode;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.IO;
using Org.Puffinbasic.Common;

namespace Org.Puffinbasic.Runtime
{
    public class Functions
    {
        public static void Abs(PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            var op1Entry = symbolTable[instruction.op1];
            var op1 = op1Entry.GetValue();
            var result = symbolTable[instruction.result].GetValue();
            switch (op1Entry.GetType().GetAtomTypeId())
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
                    ThrowUnsupportedType(op1Entry.GetType().GetAtomTypeId());
                    break;
            }
        }

        public static void Asc(PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            var value = symbolTable[instruction.op1].GetValue().GetString();
            if (String.IsNullOrEmpty(value))
            {
                throw new PuffinBasicRuntimeError(ILLEGAL_FUNCTION_PARAM, "IllegalFunctionCall: null/empty string: '" + value + "'");
            }

            var ascii = (int)value[0];
            symbolTable[instruction.result].GetValue().SetInt32(ascii);
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
            var result = symbolTable[instruction.result].GetValue();
            result.SetFloat64(Math.E);
        }

        public static void Pi(PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            var result = symbolTable[instruction.result].GetValue();
            result.SetFloat64(Math.PI);
        }

        public static void Min(PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            var v1 = symbolTable[instruction.op1].GetValue();
            var v2 = symbolTable[instruction.op2].GetValue();
            var resultEntry = symbolTable[instruction.result];
            var result = resultEntry.GetValue();
            switch (resultEntry.GetType().GetAtomTypeId())
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
                    ThrowUnsupportedType(resultEntry.GetType().GetAtomTypeId());
                    break;
            }
        }

        public static void Max(PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            var v1 = symbolTable[instruction.op1].GetValue();
            var v2 = symbolTable[instruction.op2].GetValue();
            var resultEntry = symbolTable[instruction.result];
            var result = resultEntry.GetValue();
            switch (resultEntry.GetType().GetAtomTypeId())
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
                    ThrowUnsupportedType(resultEntry.GetType().GetAtomTypeId());
                    break;
            }
        }

        private static void ApplyDoubleFunction(PuffinBasicSymbolTable symbolTable, Instruction instruction, Func<double, double> function)
        {
            var value = symbolTable[instruction.op1].GetValue().GetFloat64();
            var result = symbolTable[instruction.result].GetValue();
            result.SetFloat64(function.Invoke(value));
        }

        public static void Cint(PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            var entry = symbolTable[instruction.op1].GetValue();
            double value = entry.GetFloat64();
            if (value < int.MinValue || value > int.MaxValue)
            {
                throw new PuffinBasicRuntimeError(DATA_OUT_OF_RANGE, "CINT: value: " + value + " overflows an int32");
            }

            symbolTable[instruction.result].GetValue().SetInt32(entry.GetRoundedInt32());
        }

        public static void Clng(PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            var entry = symbolTable[instruction.op1].GetValue();
            double value = entry.GetFloat64();
            if (value < long.MinValue || value > long.MaxValue)
            {
                throw new PuffinBasicRuntimeError(DATA_OUT_OF_RANGE, "CLONG: value: " + value + " overflows an int64");
            }

            symbolTable[instruction.result].GetValue().SetInt64(entry.GetRoundedInt64());
        }

        public static void Csng(PuffinBasicSymbolTable symbolTable, Instruction instruction) => symbolTable[instruction.result].GetValue().SetFloat32(symbolTable[instruction.op1].GetValue().GetFloat32());

        public static void Cdbl(PuffinBasicSymbolTable symbolTable, Instruction instruction) => symbolTable[instruction.result].GetValue().SetFloat64(symbolTable[instruction.op1].GetValue().GetFloat64());

        public static void Chrdlr(PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            int intValue = symbolTable[instruction.op1].GetValue().GetInt32();
            char charValue = (char)intValue;
            symbolTable[instruction.result].GetValue().SetString(new string (new char[] { charValue }));
        }

        public static void Mkidlr(PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            var value = symbolTable[instruction.op1].GetValue().GetInt32();
            byte[] b = new byte[4];
            BitConverter.GetBytes(value).CopyTo(b, 0);

            symbolTable[instruction.result].GetValue().SetString(ISOEncoding.GetString(b));


            //int value = symbolTable[instruction.op1].GetValue().GetInt32();
            //string str = new string (ByteBuffer.Allocate(4).PutInt(value).Array(), StandardCharsets.ISO_8859_1);
            //symbolTable[instruction.result].GetValue().SetString(str);
        }

        public static void Mkldlr(PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            var value = symbolTable[instruction.op1].GetValue().GetInt64();
            byte[] b = new byte[8];
            BitConverter.GetBytes(value).CopyTo(b, 0);

            symbolTable[instruction.result].GetValue().SetString(ISOEncoding.GetString(b));

            //long value = symbolTable[instruction.op1].GetValue().GetInt64();
            //string str = new string (ByteBuffer.Allocate(8).PutLong(value).Array(), StandardCharsets.ISO_8859_1);
            //symbolTable[instruction.result].GetValue().SetString(str);
        }

        public static void Mksdlr(PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            var value = symbolTable[instruction.op1].GetValue().GetFloat32();
            byte[] b = new byte[4];
            BitConverter.GetBytes(value).CopyTo(b, 0);

            symbolTable[instruction.result].GetValue().SetString(ISOEncoding.GetString(b));

            //float value = symbolTable[instruction.op1].GetValue().GetFloat32();
            //string str = new string (ByteBuffer.Allocate(4).PutFloat(value).Array(), StandardCharsets.ISO_8859_1);
            //symbolTable[instruction.result].GetValue().SetString(str);
        }

        public static void Mkddlr(PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            var value = symbolTable[instruction.op1].GetValue().GetFloat64();
            byte[] b = new byte[8];
            BitConverter.GetBytes(value).CopyTo(b, 0);

            symbolTable[instruction.result].GetValue().SetString(ISOEncoding.GetString(b));

            //double value = symbolTable[instruction.op1].GetValue().GetFloat64();
            //string str = new string (ByteBuffer.Allocate(8).PutDouble(value).Array(), StandardCharsets.ISO_8859_1);
            //symbolTable[instruction.result].GetValue().SetString(str);
        }

        public static void Cvi(PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            string value = symbolTable[instruction.op1].GetValue().GetString();
            if (value.Length != 4)
            {
                throw new PuffinBasicRuntimeError(DATA_OUT_OF_RANGE, "CVI$: value: " + value + " length must be 4, found: " + value.Length);
            }

            int intValue = BitConverter.ToInt32(ISOEncoding.GetBytes(value), 0);
            symbolTable[instruction.result].GetValue().SetInt32(intValue);

            //int intValue = ByteBuffer.Wrap(value.GetBytes(StandardCharsets.ISO_8859_1), 0, 4).GetInt();
            //symbolTable[instruction.result].GetValue().SetInt32(intValue);
        }

        public static void Cvl(PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            string value = symbolTable[instruction.op1].GetValue().GetString();
            if (value.Length != 8)
            {
                throw new PuffinBasicRuntimeError(DATA_OUT_OF_RANGE, "CVL$: value: " + value + " length must be 8, found: " + value.Length);
            }

            long longValue = BitConverter.ToInt64(ISOEncoding.GetBytes(value), 0);

            //long longValue = ByteBuffer.Wrap(value.GetBytes(StandardCharsets.ISO_8859_1), 0, 8).GetLong();
            symbolTable[instruction.result].GetValue().SetInt64(longValue);
        }

        public static void Cvs(PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            string value = symbolTable[instruction.op1].GetValue().GetString();
            if (value.Length != 4)
            {
                throw new PuffinBasicRuntimeError(DATA_OUT_OF_RANGE, "CVS$: value: " + value + " length must be 4, found: " + value.Length);
            }

            float floatValue = BitConverter.ToSingle(ISOEncoding.GetBytes(value), 0);

            //float floatValue = ByteBuffer.Wrap(value.GetBytes(StandardCharsets.ISO_8859_1), 0, 4).GetFloat();
            symbolTable[instruction.result].GetValue().SetFloat32(floatValue);
        }

        public static void Cvd(PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            string value = symbolTable[instruction.op1].GetValue().GetString();
            if (value.Length != 8)
            {
                throw new PuffinBasicRuntimeError(DATA_OUT_OF_RANGE, "CVD$: value: " + value + " length must be 8, found: " + value.Length);
            }

            double doubleValue = BitConverter.ToDouble(ISOEncoding.GetBytes(value), 0);
            //double doubleValue = ByteBuffer.Wrap(value.GetBytes(StandardCharsets.ISO_8859_1), 0, 8).GetDouble();
            symbolTable[instruction.result].GetValue().SetFloat64(doubleValue);
        }

        public static void Spacedlr(PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            int len = symbolTable[instruction.op1].GetValue().GetInt32();
            byte[] bytes = new byte[len];
            for (int i = 0; i < len; i++)
            {
                bytes[i] = (byte)' ';
            }

            string str = new string(bytes.Select(b => (char)b).ToArray());
            symbolTable[instruction.result].GetValue().SetString(str);
        }

        public static void Val(PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            var str = symbolTable[instruction.op1].GetValue().GetString();
            var result = symbolTable[instruction.result].GetValue();
            try
            {
                result.SetFloat64(Double.Parse(str));
            }
            catch (FormatException e)
            {
                throw new PuffinBasicRuntimeError(DATA_OUT_OF_RANGE, "Failed to parse string: " + str + " as numeric");
            }
        }

        public static void Fnint(PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            var vEntry = symbolTable[instruction.op1];
            var v = vEntry.GetValue();
            var result = symbolTable[instruction.result].GetValue();
            switch (vEntry.GetType().GetAtomTypeId())
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
                    ThrowUnsupportedType(vEntry.GetType().GetAtomTypeId());
                    break;
            }
        }

        public static void Fix(PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            var vEntry = symbolTable[instruction.op1];
            var v = vEntry.GetValue();
            var result = symbolTable[instruction.result].GetValue();
            switch (vEntry.GetType().GetAtomTypeId())
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
                    ThrowUnsupportedType(vEntry.GetType().GetAtomTypeId());
                    break;
            }
        }

        public static void Len(PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            var stEntry = symbolTable[instruction.op1];
            var value = stEntry.GetValue();
            int len;
            if (stEntry.GetType().GetTypeId() == ARRAY)
            {
                int axis = instruction.op2 != NULL_ID ? symbolTable[instruction.op2].GetValue().GetInt32() : 0;
                if (axis < 0 || axis >= value.GetNumArrayDimensions())
                {
                    throw new PuffinBasicRuntimeError(ILLEGAL_FUNCTION_PARAM, "Bad axis=" + axis + ", #dims=" + value.GetNumArrayDimensions());
                }

                len = value.GetArrayDimensions()[axis];
            }
            else if (stEntry.GetValue().HasLen())
            {
                len = value.Len();
            }
            else
            {
                throw new PuffinBasicRuntimeError(ILLEGAL_FUNCTION_PARAM, "Bad LEN() call!");
            }

            symbolTable[instruction.result].GetValue().SetInt32(len);
        }

        public static void Strdlr(PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            var numericEntry = symbolTable[instruction.op1];
            var numeric = numericEntry.GetValue();
            var dt = numericEntry.GetType().GetAtomTypeId();
            string str;
            if (dt == INT32)
                str = numeric.GetInt32().ToString();
            else if (dt == INT64)
                str = numeric.GetInt64().ToString();
            else if (dt == FLOAT)
                str = numeric.GetFloat32().ToString();
            else
                str = numeric.GetFloat64().ToString();

            //if (dt == INT32)
            //{
            //    str = int.ToString(numeric.GetInt32());
            //}
            //else if (dt == INT64)
            //{
            //    str = long.ToString(numeric.GetInt64());
            //}
            //else if (dt == FLOAT)
            //{
            //    str = float.ToString(numeric.GetFloat32());
            //}
            //else
            //{
            //    str = Double.ToString(numeric.GetFloat64());
            //}

            symbolTable[instruction.result].GetValue().SetString(str);
        }

        public static void Hexdlr(PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            var numericEntry = symbolTable[instruction.op1];
            var numeric = numericEntry.GetValue();
            var dt = numericEntry.GetType().GetAtomTypeId();
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

            symbolTable[instruction.result].GetValue().SetString(str);
        }

        public static void Octdlr(PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            var numericEntry = symbolTable[instruction.op1];
            var numeric = numericEntry.GetValue();
            var dt = numericEntry.GetType().GetAtomTypeId();
            string str;
            if (dt == INT32 || dt == FLOAT)
            {
                str = Convert.ToString(numeric.GetInt32(), 8);
            }
            else
            {
                str = Convert.ToString(numeric.GetInt64(), 8);
            }

            symbolTable[instruction.result].GetValue().SetString(str);
        }

        public static void Leftdlr(PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            var x = symbolTable[instruction.op1].GetValue().GetString();
            var n = symbolTable[instruction.op2].GetValue().GetInt32();
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

            symbolTable[instruction.result].GetValue().SetString(result);
        }

        public static void Rightdlr(PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            var x = symbolTable[instruction.op1].GetValue().GetString();
            var n = symbolTable[instruction.op2].GetValue().GetInt32();
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

            symbolTable[instruction.result].GetValue().SetString(result);
        }

        public static void Instr(PuffinBasicSymbolTable symbolTable, Instruction instr0, Instruction instr)
        {
            var x = symbolTable[instr0.op1].GetValue().GetString();
            var y = symbolTable[instr0.op2].GetValue().GetString();
            var n = symbolTable[instr.op1].GetValue().GetInt32();
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

            symbolTable[instr.result].GetValue().SetInt32(result);
        }

        public static void Middlr(PuffinBasicSymbolTable symbolTable, Instruction instr0, Instruction instr)
        {
            var x = symbolTable[instr0.op1].GetValue().GetString();
            var n = symbolTable[instr0.op2].GetValue().GetInt32();
            var m = symbolTable[instr.op1].GetValue().GetInt32();
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
                result = x.Substring(n - 1, Math.Min(xlen, n + m - 1));
            }

            symbolTable[instr.result].GetValue().SetString(result);
        }

        public static void Rnd(Random random, PuffinBasicSymbolTable symbolTable, Instruction instruction) => symbolTable[instruction.result].GetValue().SetFloat64(random.NextDouble());

        public static void Sgn(PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            var entry = symbolTable[instruction.op1];
            var numeric = entry.GetValue();
            var dt = entry.GetType().GetAtomTypeId();
            int result;
            if (dt == INT32)
                result = Math.Sign(numeric.GetInt32());
            else if (dt == INT64)
                result = Math.Sign(numeric.GetInt64());
            else if (dt == FLOAT)
                result = Math.Sign(numeric.GetFloat32());
            else
                result = Math.Sign(numeric.GetFloat64());

            symbolTable[instruction.result].GetValue().SetInt32(result);
        }

        public static void Timer(PuffinBasicSymbolTable symbolTable, Instruction instruction) =>
            //var nowZoned = ZonedDateTime.Now();
            //var midnight = nowZoned.ToLocalDate().AtStartOfDay(nowZoned.GetZone()).ToInstant();
            //var duration = Duration.Between(midnight, DateTime.Now());
            //var seconds = duration.GetSeconds() + duration.GetNano() / 1000000000;

            symbolTable[instruction.result].GetValue().SetFloat64((DateTime.Now - DateTime.Now.Date).TotalMilliseconds);

        public static void TimerMillis(PuffinBasicSymbolTable symbolTable, Instruction instruction) =>
            //var nowZoned = ZonedDateTime.Now();
            //var midnight = nowZoned.ToLocalDate().AtStartOfDay(nowZoned.GetZone()).ToInstant();
            //var duration = Duration.Between(midnight, DateTime.Now());
            //var millis = TimeUnit.SECONDS.ToMillis(duration.GetSeconds()) + TimeUnit.NANOSECONDS.ToMillis(duration.GetNano());

            symbolTable[instruction.result].GetValue().SetInt64((long)(DateTime.Now - DateTime.Now.Date).TotalMilliseconds);

        public static void Stringdlr(PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            var n = symbolTable[instruction.op1].GetValue().GetInt32();
            var jOrxdlrEntry = symbolTable[instruction.op2];
            var jOrxdlr = jOrxdlrEntry.GetValue();
            string c;
            if (jOrxdlrEntry.GetType().GetAtomTypeId() == STRING)
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
            //    result = Strings.Repeat(c, n);
                result = new StringBuilder(c.Length * n)
                    .Insert(0, c, n)
                    .ToString();
            }

            symbolTable[instruction.result].GetValue().SetString(result);
        }

        public static void Loc(PuffinBasicFiles files, PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            var fileNumber = symbolTable[instruction.op1].GetValue().GetInt32();
            var loc = files[fileNumber].GetCurrentRecordNumber();
            symbolTable[instruction.result].GetValue().SetInt32(loc);
        }

        public static void Lof(PuffinBasicFiles files, PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            var fileNumber = symbolTable[instruction.op1].GetValue().GetInt32();
            var lof = files[fileNumber].GetFileSizeInBytes();
            symbolTable[instruction.result].GetValue().SetInt64(lof);
        }

        public static void Eof(PuffinBasicFiles files, PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            var fileNumber = symbolTable[instruction.op1].GetValue().GetInt32();
            var eof = files[fileNumber].Eof();
            symbolTable[instruction.result].GetValue().SetInt32(eof ? -1 : 0);
        }

        public static void Inputdlr(PuffinBasicFiles files, PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            var x = symbolTable[instruction.op1].GetValue().GetInt32();
            var fileNumber = symbolTable[instruction.op2].GetValue().GetInt32();
            byte[] read;
            if (fileNumber < 0)
            {
                read = files.sys.ReadBytes(x);
            }
            else
            {
                throw new NotSupportedException();
            }

            symbolTable[instruction.result].GetValue()
                .SetString(System.Text.Encoding.UTF8.GetString(read, 0, read.Length));
                //.SetString(new string (read));
        }

        public static void Environdlr(IEnvironment env, PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            var envvar = symbolTable[instruction.op1].GetValue().GetString();
            var result = env[envvar];
            symbolTable[instruction.result].GetValue().SetString(result);
        }

        public static void Splitdlr(PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            var str = symbolTable[instruction.op1].GetValue().GetString();
            var regex = symbolTable[instruction.op2].GetValue().GetString();
            String[] tokens = (new Regex(regex)).Split(str);
            STRING.CopyArray(tokens, symbolTable[instruction.result].GetValue());
        }

        public static void ThrowUnsupportedType(PuffinBasicAtomTypeId type) => throw new PuffinBasicInternalError("Data type " + type + " is not supported");
    }
}

