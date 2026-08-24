using It.Unimi.Dsi.Fastutil.Ints;
using Org.Apache.Commons.Math3.Stat.Descriptive;
using Org.Puffinbasic.Domain;
using Org.Puffinbasic.Domain.STObjects;
using Org.Puffinbasic.Error;
using Org.Puffinbasic.Parser.PuffinBasicIR;
using Java.Util;
using Org.Puffinbasic.Error.PuffinBasicRuntimeError.ErrorCode;
using Org.Puffinbasic.Runtime.Functions;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;

namespace Org.Puffinbasic.Runtime
{
    public sealed class ArraysUtil
    {
        public sealed class ArrayState
        {
            private int dimIndex;
            public int GetAndIncrement()
            {
                return dimIndex++;
            }

            public void Reset()
            {
                dimIndex = 0;
            }
        }

        public static void Dim(PuffinBasicSymbolTable symbolTable, IList<Instruction> @params, Instruction instruction)
        {
            List<int> dims = new List<int>(@params.Count);
            foreach (var param in @params)
            {
                dims.Add(symbolTable[param.op1].GetValue().GetInt32());
            }

            symbolTable[instruction.op1].GetValue().SetArrayDimensions(dims);
        }

        public static void ResetIndex(ArrayState state, PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            state.Reset();
            symbolTable[instruction.op1].GetValue().ResetArrayIndex();
        }

        public static void AllocArray(PuffinBasicSymbolTable symbolTable, IList<Instruction> @params, Instruction instruction)
        {
            List<int> dims = new List<int>(@params.Count);
            foreach (var param in @params)
            {
                dims.Add(symbolTable[param.op1].GetValue().GetInt32());
            }

            var arrayEntry = symbolTable[instruction.result];
            var arrayType = (ArrayType)arrayEntry.GetType();
            arrayType.SetArrayDimensions(dims);
            arrayEntry.GetValue().SetArrayDimensions(dims);
        }

        public static void ReallocArray(PuffinBasicSymbolTable symbolTable, IList<Instruction> @params, Instruction instruction)
        {
            List<int> dims = new List<int>(@params.Count);
            foreach (var param in @params)
            {
                dims.Add(symbolTable[param.op1].GetValue().GetInt32());
            }

            var arrayEntry = symbolTable[instruction.op1];
            var arrayType = (ArrayType)arrayEntry.GetType();
            arrayType.SetArrayDimensions(dims);

            // Create new value
            ((AbstractSTEntry)arrayEntry).CreateAndSetInstance(symbolTable);
        }

        public static void SetIndex(ArrayState state, PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            int index = symbolTable[instruction.op2].GetValue().GetInt32();
            symbolTable[instruction.op1].GetValue().SetArrayIndex(state.GetAndIncrement(), index);
        }

        public static void Arrayref(PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            var index = symbolTable[instruction.op1].GetValue().GetArrayIndex1D();
            symbolTable[instruction.result].GetValue().SetArrayReferenceIndex1D(index);
        }

        public static void Arrayfill(PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            var array = symbolTable[instruction.op1].GetValue();
            var fillEntry = symbolTable[instruction.op2];
            var fill = fillEntry.GetValue();
            switch (fillEntry.GetType().GetAtomTypeId())
            {
                case INT32:
                    array.Fill(fill.GetInt32());
                    break;
                case INT64:
                    array.Fill(fill.GetInt64());
                    break;
                case FLOAT:
                    array.Fill(fill.GetFloat32());
                    break;
                case DOUBLE:
                    array.Fill(fill.GetFloat64());
                    break;
                case STRING:
                    array.FillString(fill.GetString());
                    break;
                default:
                    ThrowUnsupportedType(fillEntry.GetType().GetAtomTypeId());
                    break;
            }
        }

        public static void ArrayCopy(PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            var array1Entry = symbolTable[instruction.op1];
            var array1 = array1Entry.GetValue();
            var array2Entry = symbolTable[instruction.op2];
            var array2 = array2Entry.GetValue();
            if (array1Entry.GetType().GetAtomTypeId() != array2Entry.GetType().GetAtomTypeId())
            {
                throw new PuffinBasicRuntimeError(DATA_TYPE_MISMATCH, "Array data type mismatch: " + array1Entry.GetType().GetAtomTypeId() + " is not compatible with " + array2Entry.GetType().GetAtomTypeId());
            }

            if (array1.GetTotalLength() != array2.GetTotalLength())
            {
                throw new PuffinBasicRuntimeError(ILLEGAL_FUNCTION_PARAM, "Array length mismatch: " + array1.GetTotalLength() + " is not compatible with " + array2.GetTotalLength());
            }

            switch (array1Entry.GetType().GetAtomTypeId())
            {
                case INT32:
                {
                    int[] value = ((STInt32ArrayValue)array1).GetValue();
                    System.Arraycopy(value, 0, ((STInt32ArrayValue)array2).GetValue(), 0, value.length);
                }

                    break;
                case INT64:
                {
                    long[] value = ((STInt64ArrayValue)array1).GetValue();
                    System.Arraycopy(value, 0, ((STInt64ArrayValue)array2).GetValue(), 0, value.length);
                }

                    break;
                case FLOAT:
                {
                    float[] value = ((STFloat32ArrayValue)array1).GetValue();
                    System.Arraycopy(value, 0, ((STFloat32ArrayValue)array2).GetValue(), 0, value.length);
                }

                    break;
                case DOUBLE:
                {
                    double[] value = ((STFloat64ArrayValue)array1).GetValue();
                    System.Arraycopy(value, 0, ((STFloat64ArrayValue)array2).GetValue(), 0, value.length);
                }

                    break;
                case STRING:
                {
                    String[] value = ((STStringArrayValue)array1).GetValue();
                    System.Arraycopy(value, 0, ((STStringArrayValue)array2).GetValue(), 0, value.length);
                }

                    break;
                default:
                    ThrowUnsupportedType(array1Entry.GetType().GetAtomTypeId());
                    break;
            }
        }

        public static void Array2dShiftVertical(PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            var arrayEntry = symbolTable[instruction.op1];
            var array = arrayEntry.GetValue();
            var shift = symbolTable[instruction.op2].GetValue().GetInt32();
            var dims = array.GetArrayDimensions();

            // Arrays are row-major.
            var dim1 = dims.GetInt(0);
            var dim2 = dims.GetInt(1);
            var n = array.GetTotalLength();
            var delta = (Math.Abs(shift) % dim1) * dim2;
            int src0, dst0, len = n - delta, fillSrc0;
            if (shift > 0)
            {
                src0 = 0;
                dst0 = delta;
                fillSrc0 = 0;
            }
            else
            {
                src0 = delta;
                dst0 = 0;
                fillSrc0 = n - delta;
            }

            switch (arrayEntry.GetType().GetAtomTypeId())
            {
                case INT32:
                {
                    int[] value = ((STInt32ArrayValue)array).GetValue();
                    System.Arraycopy(value, src0, value, dst0, len);
                    Arrays.Fill(value, fillSrc0, fillSrc0 + delta, 0);
                }

                    break;
                case INT64:
                {
                    long[] value = ((STInt64ArrayValue)array).GetValue();
                    System.Arraycopy(value, src0, value, dst0, len);
                    Arrays.Fill(value, fillSrc0, fillSrc0 + delta, 0);
                }

                    break;
                case FLOAT:
                {
                    float[] value = ((STFloat32ArrayValue)array).GetValue();
                    System.Arraycopy(value, src0, value, dst0, len);
                    Arrays.Fill(value, fillSrc0, fillSrc0 + delta, 0);
                }

                    break;
                case DOUBLE:
                {
                    double[] value = ((STFloat64ArrayValue)array).GetValue();
                    System.Arraycopy(value, src0, value, dst0, len);
                    Arrays.Fill(value, fillSrc0, fillSrc0 + delta, 0);
                }

                    break;
                case STRING:
                {
                    String[] value = ((STStringArrayValue)array).GetValue();
                    System.Arraycopy(value, src0, value, dst0, len);
                    Arrays.Fill(value, fillSrc0, fillSrc0 + delta, "");
                }

                    break;
                default:
                    ThrowUnsupportedType(arrayEntry.GetType().GetAtomTypeId());
                    break;
            }
        }

        public static void Array2dShiftHorizontal(PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            var arrayEntry = symbolTable[instruction.op1];
            var array = arrayEntry.GetValue();
            var shift = symbolTable[instruction.op2].GetValue().GetInt32();
            var dims = array.GetArrayDimensions();

            // Arrays are row-major.
            var dim1 = dims.GetInt(0);
            var dim2 = dims.GetInt(1);
            var n = array.GetTotalLength();
            var delta = Math.Abs(shift) % dim2;
            int src0, dst0, len = dim2 - delta, fillSrc0;
            if (shift > 0)
            {
                src0 = 0;
                dst0 = delta;
                fillSrc0 = 0;
            }
            else
            {
                src0 = delta;
                dst0 = 0;
                fillSrc0 = dim2 - delta;
            }

            switch (arrayEntry.GetType().GetAtomTypeId())
            {
                case INT32:
                {
                    int[] value = ((STInt32ArrayValue)array).GetValue();
                    if (shift >= 0)
                    {
                        for (int dc = dst0 + len - 1, sc = src0 + len - 1; dc >= dst0; dc--, sc--)
                        {
                            for (int r = 0; r < dim1; r++)
                            {
                                int dr = r * dim2;
                                value[dr + dc] = value[dr + sc];
                            }
                        }
                    }
                    else
                    {
                        for (int dc = dst0, sc = src0; dc < dst0 + len; dc++, sc++)
                        {
                            for (int r = 0; r < dim1; r++)
                            {
                                int dr = r * dim2;
                                value[dr + dc] = value[dr + sc];
                            }
                        }
                    }

                    for (int c = fillSrc0; c < fillSrc0 + delta; c++)
                    {
                        for (int r = 0; r < dim1; r++)
                        {
                            value[r * dim2 + c] = 0;
                        }
                    }
                }

                    break;
                case INT64:
                {
                    long[] value = ((STInt64ArrayValue)array).GetValue();
                    if (shift >= 0)
                    {
                        for (int dc = dst0 + len - 1, sc = src0 + len - 1; dc >= dst0; dc--, sc--)
                        {
                            for (int r = 0; r < dim1; r++)
                            {
                                int dr = r * dim2;
                                value[dr + dc] = value[dr + sc];
                            }
                        }
                    }
                    else
                    {
                        for (int dc = dst0, sc = src0; dc < dst0 + len; dc++, sc++)
                        {
                            for (int r = 0; r < dim1; r++)
                            {
                                int dr = r * dim2;
                                value[dr + dc] = value[dr + sc];
                            }
                        }
                    }

                    for (int c = fillSrc0; c < fillSrc0 + delta; c++)
                    {
                        for (int r = 0; r < dim1; r++)
                        {
                            value[r * dim2 + c] = 0;
                        }
                    }
                }

                    break;
                case FLOAT:
                {
                    float[] value = ((STFloat32ArrayValue)array).GetValue();
                    if (shift >= 0)
                    {
                        for (int dc = dst0 + len - 1, sc = src0 + len - 1; dc >= dst0; dc--, sc--)
                        {
                            for (int r = 0; r < dim1; r++)
                            {
                                int dr = r * dim2;
                                value[dr + dc] = value[dr + sc];
                            }
                        }
                    }
                    else
                    {
                        for (int dc = dst0, sc = src0; dc < dst0 + len; dc++, sc++)
                        {
                            for (int r = 0; r < dim1; r++)
                            {
                                int dr = r * dim2;
                                value[dr + dc] = value[dr + sc];
                            }
                        }
                    }

                    for (int c = fillSrc0; c < fillSrc0 + delta; c++)
                    {
                        for (int r = 0; r < dim1; r++)
                        {
                            value[r * dim2 + c] = 0;
                        }
                    }
                }

                    break;
                case DOUBLE:
                {
                    double[] value = ((STFloat64ArrayValue)array).GetValue();
                    if (shift >= 0)
                    {
                        for (int dc = dst0 + len - 1, sc = src0 + len - 1; dc >= dst0; dc--, sc--)
                        {
                            for (int r = 0; r < dim1; r++)
                            {
                                int dr = r * dim2;
                                value[dr + dc] = value[dr + sc];
                            }
                        }
                    }
                    else
                    {
                        for (int dc = dst0, sc = src0; dc < dst0 + len; dc++, sc++)
                        {
                            for (int r = 0; r < dim1; r++)
                            {
                                int dr = r * dim2;
                                value[dr + dc] = value[dr + sc];
                            }
                        }
                    }

                    for (int c = fillSrc0; c < fillSrc0 + delta; c++)
                    {
                        for (int r = 0; r < dim1; r++)
                        {
                            value[r * dim2 + c] = 0;
                        }
                    }
                }

                    break;
                case STRING:
                {
                    String[] value = ((STStringArrayValue)array).GetValue();
                    if (shift >= 0)
                    {
                        for (int dc = dst0 + len - 1, sc = src0 + len - 1; dc >= dst0; dc--, sc--)
                        {
                            for (int r = 0; r < dim1; r++)
                            {
                                int dr = r * dim2;
                                value[dr + dc] = value[dr + sc];
                            }
                        }
                    }
                    else
                    {
                        for (int dc = dst0, sc = src0; dc < dst0 + len; dc++, sc++)
                        {
                            for (int r = 0; r < dim1; r++)
                            {
                                int dr = r * dim2;
                                value[dr + dc] = value[dr + sc];
                            }
                        }
                    }

                    for (int c = fillSrc0; c < fillSrc0 + delta; c++)
                    {
                        for (int r = 0; r < dim1; r++)
                        {
                            value[r * dim2 + c] = "";
                        }
                    }
                }

                    break;
                default:
                    ThrowUnsupportedType(arrayEntry.GetType().GetAtomTypeId());
                    break;
            }
        }

        public static void Array1DCopy(PuffinBasicSymbolTable symbolTable, Instruction i0, Instruction i1, Instruction instruction)
        {
            var srcEntry = symbolTable[i0.op1];
            var src = srcEntry.GetValue();
            var src0 = symbolTable[i0.op2].GetValue().GetInt32();
            var dstEntry = symbolTable[i1.op1];
            var dst = dstEntry.GetValue();
            var dst0 = symbolTable[i1.op2].GetValue().GetInt32();
            var len = symbolTable[instruction.op1].GetValue().GetInt32();
            if (srcEntry.GetType().GetAtomTypeId() != dstEntry.GetType().GetAtomTypeId())
            {
                throw new PuffinBasicRuntimeError(DATA_TYPE_MISMATCH, "Array data type mismatch: " + srcEntry.GetType().GetAtomTypeId() + " is not compatible with " + dstEntry.GetType().GetAtomTypeId());
            }

            if (src.GetNumArrayDimensions() != 1 && dst.GetNumArrayDimensions() != 1)
            {
                throw new PuffinBasicRuntimeError(ILLEGAL_FUNCTION_PARAM, "Array #dim!=1 : src=" + src.GetNumArrayDimensions() + " and dst=" + dst.GetNumArrayDimensions());
            }

            if (src0 < 0 || src0 >= src.GetTotalLength() || dst0 < 0 || len < 0 || dst0 + len > dst.GetTotalLength())
            {
                throw new PuffinBasicRuntimeError(ILLEGAL_FUNCTION_PARAM, "Bad params: srcOrigin=" + src0 + " dstOrigin=" + dst0 + " len=" + len + " srcArraySize=" + src.GetTotalLength() + " dstArraySize=" + dst.GetTotalLength());
            }

            switch (srcEntry.GetType().GetAtomTypeId())
            {
                case INT32:
                {
                    int[] value = ((STInt32ArrayValue)src).GetValue();
                    System.Arraycopy(value, src0, ((STInt32ArrayValue)dst).GetValue(), dst0, len);
                }

                    break;
                case INT64:
                {
                    long[] value = ((STInt64ArrayValue)src).GetValue();
                    System.Arraycopy(value, src0, ((STInt64ArrayValue)dst).GetValue(), dst0, len);
                }

                    break;
                case FLOAT:
                {
                    float[] value = ((STFloat32ArrayValue)src).GetValue();
                    System.Arraycopy(value, src0, ((STFloat32ArrayValue)dst).GetValue(), dst0, len);
                }

                    break;
                case DOUBLE:
                {
                    double[] value = ((STFloat64ArrayValue)src).GetValue();
                    System.Arraycopy(value, src0, ((STFloat64ArrayValue)dst).GetValue(), dst0, len);
                }

                    break;
                case STRING:
                {
                    String[] value = ((STStringArrayValue)src).GetValue();
                    System.Arraycopy(value, src0, ((STStringArrayValue)dst).GetValue(), dst0, len);
                }

                    break;
                default:
                    ThrowUnsupportedType(srcEntry.GetType().GetAtomTypeId());
                    break;
            }
        }

        public static void Array1dSort(PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            var entry = symbolTable[instruction.op1];
            var array = entry.GetValue();
            switch (entry.GetType().GetAtomTypeId())
            {
                case INT32:
                    Arrays.Sort(((STInt32ArrayValue)array).GetValue());
                    break;
                case INT64:
                    Arrays.Sort(((STInt64ArrayValue)array).GetValue());
                    break;
                case FLOAT:
                    Arrays.Sort(((STFloat32ArrayValue)array).GetValue());
                    break;
                case DOUBLE:
                    Arrays.Sort(((STFloat64ArrayValue)array).GetValue());
                    break;
                case STRING:
                    Arrays.Sort(((STStringArrayValue)array).GetValue());
                    break;
                default:
                    ThrowUnsupportedType(entry.GetType().GetAtomTypeId());
                    break;
            }
        }

        public static void Array1dBinSearch(PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            var arrayEntry = symbolTable[instruction.op1];
            var array = arrayEntry.GetValue();
            var search = symbolTable[instruction.op2].GetValue();
            var result = symbolTable[instruction.result].GetValue();
            var index = -1;
            switch (arrayEntry.GetType().GetAtomTypeId())
            {
                case INT32:
                    index = Arrays.BinarySearch(((STInt32ArrayValue)array).GetValue(), search.GetInt32());
                    break;
                case INT64:
                    index = Arrays.BinarySearch(((STInt64ArrayValue)array).GetValue(), search.GetInt64());
                    break;
                case FLOAT:
                    index = Arrays.BinarySearch(((STFloat32ArrayValue)array).GetValue(), search.GetFloat32());
                    break;
                case DOUBLE:
                    index = Arrays.BinarySearch(((STFloat64ArrayValue)array).GetValue(), search.GetFloat64());
                    break;
                case STRING:
                    index = Arrays.BinarySearch(((STStringArrayValue)array).GetValue(), search.GetString());
                    break;
                default:
                    ThrowUnsupportedType(arrayEntry.GetType().GetAtomTypeId());
                    break;
            }

            result.SetInt32(index);
        }

        public static void Array1dMin(PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            var arrayEntry = symbolTable[instruction.op1];
            var array = arrayEntry.GetValue();
            var result = symbolTable[instruction.result].GetValue();
            switch (arrayEntry.GetType().GetAtomTypeId())
            {
                case INT32:
                {
                    int[] value = ((STInt32ArrayValue)array).GetValue();
                    var min = Integer.MAX_VALUE;
                    foreach (var v in value)
                    {
                        if (v < min)
                        {
                            min = v;
                        }
                    }

                    result.SetInt32(min);
                }

                    break;
                case INT64:
                {
                    long[] value = ((STInt64ArrayValue)array).GetValue();
                    var min = Long.MAX_VALUE;
                    foreach (var v in value)
                    {
                        if (v < min)
                        {
                            min = v;
                        }
                    }

                    result.SetInt64(min);
                }

                    break;
                case FLOAT:
                {
                    float[] value = ((STFloat32ArrayValue)array).GetValue();
                    var min = Float.MAX_VALUE;
                    foreach (var v in value)
                    {
                        if (v < min)
                        {
                            min = v;
                        }
                    }

                    result.SetFloat32(min);
                }

                    break;
                case DOUBLE:
                {
                    double[] value = ((STFloat64ArrayValue)array).GetValue();
                    var min = Double.MAX_VALUE;
                    foreach (var v in value)
                    {
                        if (v < min)
                        {
                            min = v;
                        }
                    }

                    result.SetFloat64(min);
                }

                    break;
                default:
                    ThrowUnsupportedType(arrayEntry.GetType().GetAtomTypeId());
                    break;
            }
        }

        public static void Array1dMax(PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            var arrayEntry = symbolTable[instruction.op1];
            var array = arrayEntry.GetValue();
            var result = symbolTable[instruction.result].GetValue();
            switch (arrayEntry.GetType().GetAtomTypeId())
            {
                case INT32:
                {
                    int[] value = ((STInt32ArrayValue)array).GetValue();
                    var max = Integer.MIN_VALUE;
                    foreach (var v in value)
                    {
                        if (v > max)
                        {
                            max = v;
                        }
                    }

                    result.SetInt32(max);
                }

                    break;
                case INT64:
                {
                    long[] value = ((STInt64ArrayValue)array).GetValue();
                    var max = Long.MIN_VALUE;
                    foreach (var v in value)
                    {
                        if (v > max)
                        {
                            max = v;
                        }
                    }

                    result.SetInt64(max);
                }

                    break;
                case FLOAT:
                {
                    float[] value = ((STFloat32ArrayValue)array).GetValue();
                    var max = Float.MIN_VALUE;
                    foreach (var v in value)
                    {
                        if (v > max)
                        {
                            max = v;
                        }
                    }

                    result.SetFloat32(max);
                }

                    break;
                case DOUBLE:
                {
                    double[] value = ((STFloat64ArrayValue)array).GetValue();
                    var max = Double.MIN_VALUE;
                    foreach (var v in value)
                    {
                        if (v > max)
                        {
                            max = v;
                        }
                    }

                    result.SetFloat64(max);
                }

                    break;
                default:
                    ThrowUnsupportedType(arrayEntry.GetType().GetAtomTypeId());
                    break;
            }
        }

        public static void Array1dMean(PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            var arrayEntry = symbolTable[instruction.op1];
            var result = symbolTable[instruction.result].GetValue();
            var stats = Array1dSummaryStats(arrayEntry);
            result.SetFloat64(stats.GetMean());
        }

        public static void Array1dStddev(PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            var arrayEntry = symbolTable[instruction.op1];
            var array = arrayEntry.GetValue();
            var result = symbolTable[instruction.result].GetValue();
            var stats = Array1dSummaryStats(arrayEntry);
            result.SetFloat64(Math.Sqrt(stats.GetVariance()));
        }

        public static void Array1dSum(PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            var arrayEntry = symbolTable[instruction.op1];
            var result = symbolTable[instruction.result].GetValue();
            var stats = Array1dSummaryStats(arrayEntry);
            result.SetFloat64(stats.GetSum());
        }

        public static void Array1dMedian(PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            var arrayEntry = symbolTable[instruction.op1];
            var result = symbolTable[instruction.result].GetValue();
            var stats = Array1dDescriptiveStats(arrayEntry);
            result.SetFloat64(stats.GetPercentile(50));
        }

        public static void Array1dPercentile(PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            var arrayEntry = symbolTable[instruction.op1];
            var pct = symbolTable[instruction.op2].GetValue().GetFloat64();
            if (pct < 0 || pct > 100)
            {
                throw new PuffinBasicRuntimeError(PuffinBasicRuntimeError.ErrorCode.DATA_OUT_OF_RANGE, "Percentile value out of range: " + pct);
            }

            var result = symbolTable[instruction.result].GetValue();
            var stats = Array1dDescriptiveStats(arrayEntry);
            result.SetFloat64(stats.GetPercentile(pct));
        }

        private static SummaryStatistics Array1dSummaryStats(ISTEntry array)
        {
            var stats = new SummaryStatistics();
            switch (array.GetType().GetAtomTypeId())
            {
                case INT32:
                {
                    int[] value = ((STInt32ArrayValue)array.GetValue()).GetValue();
                    foreach (int v in value)
                    {
                        stats.AddValue(v);
                    }
                }

                    break;
                case INT64:
                {
                    long[] value = ((STInt64ArrayValue)array.GetValue()).GetValue();
                    foreach (long v in value)
                    {
                        stats.AddValue(v);
                    }
                }

                    break;
                case FLOAT:
                {
                    float[] value = ((STFloat32ArrayValue)array.GetValue()).GetValue();
                    foreach (float v in value)
                    {
                        stats.AddValue(v);
                    }
                }

                    break;
                case DOUBLE:
                {
                    double[] value = ((STFloat64ArrayValue)array.GetValue()).GetValue();
                    foreach (double v in value)
                    {
                        stats.AddValue(v);
                    }
                }

                    break;
                default:
                    ThrowUnsupportedType(array.GetType().GetAtomTypeId());
                    break;
            }

            return stats;
        }

        private static DescriptiveStatistics Array1dDescriptiveStats(ISTEntry array)
        {
            var stats = new DescriptiveStatistics();
            switch (array.GetType().GetAtomTypeId())
            {
                case INT32:
                {
                    int[] value = ((STInt32ArrayValue)array.GetValue()).GetValue();
                    foreach (int v in value)
                    {
                        stats.AddValue(v);
                    }
                }

                    break;
                case INT64:
                {
                    long[] value = ((STInt64ArrayValue)array.GetValue()).GetValue();
                    foreach (long v in value)
                    {
                        stats.AddValue(v);
                    }
                }

                    break;
                case FLOAT:
                {
                    float[] value = ((STFloat32ArrayValue)array.GetValue()).GetValue();
                    foreach (float v in value)
                    {
                        stats.AddValue(v);
                    }
                }

                    break;
                case DOUBLE:
                {
                    double[] value = ((STFloat64ArrayValue)array.GetValue()).GetValue();
                    foreach (double v in value)
                    {
                        stats.AddValue(v);
                    }
                }

                    break;
                default:
                    ThrowUnsupportedType(array.GetType().GetAtomTypeId());
                    break;
            }

            return stats;
        }

        public static void Array2dFindRow(PuffinBasicSymbolTable symbolTable, IList<Instruction> @params, Instruction instruction)
        {
            var i1 = @params[0];
            var i2 = @params[1];
            var arrayEntry = symbolTable[instruction.op1];
            var array = arrayEntry.GetValue();
            var search = symbolTable[instruction.op2].GetValue();
            var result = symbolTable[instruction.result].GetValue();
            var dims = array.GetArrayDimensions();

            // Arrays are row-major.
            var numRows = dims.GetInt(0);
            var numCols = dims.GetInt(1);
            var n = array.GetTotalLength();
            var x1 = Math.Min(Math.Max(0, symbolTable[i1.op1].GetValue().GetInt32()), numCols - 1);
            var y1 = Math.Min(Math.Max(0, symbolTable[i1.op2].GetValue().GetInt32()), numRows - 1);
            var x2 = Math.Min(Math.Max(0, symbolTable[i2.op1].GetValue().GetInt32()), numCols - 1);
            var y2 = Math.Min(Math.Max(0, symbolTable[i2.op2].GetValue().GetInt32()), numRows - 1);
            if (y1 * numCols + x1 >= n || y2 * numCols + x2 >= n)
            {
                throw new PuffinBasicRuntimeError(PuffinBasicRuntimeError.ErrorCode.INDEX_OUT_OF_BOUNDS, "x1=" + x1 + "/y1=" + y1 + "/x2=" + x2 + "/y2=" + y2 + " is out of bounds, array length=" + n);
            }

            switch (arrayEntry.GetType().GetAtomTypeId())
            {
                case INT32:
                {
                    int[] int32Array = ((STInt32ArrayValue)array).GetValue();
                    result.SetInt32(FindRowWithValue(int32Array, numCols, x1, y1, x2, y2, search.GetInt32()));
                }

                    break;
                case INT64:
                {
                    long[] int64Array = ((STInt64ArrayValue)array).GetValue();
                    result.SetInt32(FindRowWithValue(int64Array, numCols, x1, y1, x2, y2, search.GetInt64()));
                }

                    break;
                default:
                    ThrowUnsupportedType(arrayEntry.GetType().GetAtomTypeId());
                    break;
            }
        }

        public static void Array2dFindColumn(PuffinBasicSymbolTable symbolTable, IList<Instruction> @params, Instruction instruction)
        {
            var i1 = @params[0];
            var i2 = @params[1];
            var arrayEntry = symbolTable[instruction.op1];
            var array = arrayEntry.GetValue();
            var search = symbolTable[instruction.op2].GetValue();
            var result = symbolTable[instruction.result].GetValue();
            var dims = array.GetArrayDimensions();

            // Arrays are row-major.
            var numRows = dims.GetInt(0);
            var numCols = dims.GetInt(1);
            var n = array.GetTotalLength();
            var x1 = Math.Min(Math.Max(0, symbolTable[i1.op1].GetValue().GetInt32()), numCols - 1);
            var y1 = Math.Min(Math.Max(0, symbolTable[i1.op2].GetValue().GetInt32()), numRows - 1);
            var x2 = Math.Min(Math.Max(0, symbolTable[i2.op1].GetValue().GetInt32()), numCols - 1);
            var y2 = Math.Min(Math.Max(0, symbolTable[i2.op2].GetValue().GetInt32()), numRows - 1);
            if (y1 * numCols + x1 >= n || y2 * numCols + x2 >= n)
            {
                throw new PuffinBasicRuntimeError(PuffinBasicRuntimeError.ErrorCode.INDEX_OUT_OF_BOUNDS, "x1=" + x1 + "/y1=" + y1 + "/x2=" + x2 + "/y2=" + y2 + " is out of bounds, array length=" + n);
            }

            switch (arrayEntry.GetType().GetAtomTypeId())
            {
                case INT32:
                {
                    int[] int32Array = ((STInt32ArrayValue)array).GetValue();
                    result.SetInt32(FindColumnWithValue(int32Array, numCols, x1, y1, x2, y2, search.GetInt32()));
                }

                    break;
                case INT64:
                {
                    long[] int64Array = ((STInt64ArrayValue)array).GetValue();
                    result.SetInt32(FindColumnWithValue(int64Array, numCols, x1, y1, x2, y2, search.GetInt64()));
                }

                    break;
                default:
                    ThrowUnsupportedType(arrayEntry.GetType().GetAtomTypeId());
                    break;
            }
        }

        private static int FindRowWithValue(int[] array, int w, int x1, int y1, int x2, int y2, int search)
        {
            if (y1 <= y2)
            {
                for (int r = y1; r <= y2; r++)
                {
                    for (int c = x1; c <= x2; c++)
                    {
                        var v = array[r * w + c];
                        if (v == search)
                        {
                            return r;
                        }
                    }
                }
            }
            else
            {
                for (int r = y1; r >= y2; r--)
                {
                    for (int c = x1; c <= x2; c++)
                    {
                        var v = array[r * w + c];
                        if (v == search)
                        {
                            return r;
                        }
                    }
                }
            }

            return -1;
        }

        private static int FindColumnWithValue(int[] array, int w, int x1, int y1, int x2, int y2, int search)
        {
            if (x1 <= x2)
            {
                for (int c = x1; c <= x2; c++)
                {
                    for (int r = y1; r <= y2; r++)
                    {
                        var v = array[r * w + c];
                        if (v == search)
                        {
                            return c;
                        }
                    }
                }
            }
            else
            {
                for (int c = x1; c >= x2; c--)
                {
                    for (int r = y1; r <= y2; r++)
                    {
                        var v = array[r * w + c];
                        if (v == search)
                        {
                            return c;
                        }
                    }
                }
            }

            return -1;
        }

        private static int FindRowWithValue(long[] array, int w, int x1, int y1, int x2, int y2, long search)
        {
            if (y1 <= y2)
            {
                for (int r = y1; r <= y2; r++)
                {
                    for (int c = x1; c <= x2; c++)
                    {
                        var v = array[r * w + c];
                        if (v == search)
                        {
                            return r;
                        }
                    }
                }
            }
            else
            {
                for (int r = y1; r >= y2; r--)
                {
                    for (int c = x1; c <= x2; c++)
                    {
                        var v = array[r * w + c];
                        if (v == search)
                        {
                            return r;
                        }
                    }
                }
            }

            return -1;
        }

        private static int FindColumnWithValue(long[] array, int w, int x1, int y1, int x2, int y2, long search)
        {
            if (x1 <= x2)
            {
                for (int c = x1; c <= x2; c++)
                {
                    for (int r = y1; r <= y2; r++)
                    {
                        var v = array[r * w + c];
                        if (v == search)
                        {
                            return c;
                        }
                    }
                }
            }
            else
            {
                for (int c = x1; c >= x2; c--)
                {
                    for (int r = y1; r <= y2; r++)
                    {
                        var v = array[r * w + c];
                        if (v == search)
                        {
                            return c;
                        }
                    }
                }
            }

            return -1;
        }

        private int FindRowWithValue(double[] array, int w, int x1, int y1, int x2, int y2, int search)
        {
            if (y1 <= y2)
            {
                for (int r = y1; r <= y2; r++)
                {
                    for (int c = x1; c <= x2; c++)
                    {
                        var v = array[r * w + c];
                        if (v == search)
                        {
                            return r;
                        }
                    }
                }
            }
            else
            {
                for (int r = y1; r >= y2; r--)
                {
                    for (int c = x1; c <= x2; c++)
                    {
                        var v = array[r * w + c];
                        if (v == search)
                        {
                            return r;
                        }
                    }
                }
            }

            return -1;
        }
    }
}

