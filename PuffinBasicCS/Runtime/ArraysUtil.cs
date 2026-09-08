//using It.Unimi.Dsi.Fastutil.Ints;
//using Org.Apache.Commons.Math3.Stat.Descriptive;
namespace PuffinBasicCS.Runtime
{
    using PuffinBasicCS.Error;
    using static PuffinBasicCS.Domain.STObjects;
    using static PuffinBasicCS.Parser.PuffinBasicIR;
    //using Java.Util;
    using static PuffinBasicCS.Error.PuffinBasicRuntimeError.ErrorCode;
    using static PuffinBasicCS.Runtime.Functions;

    using System;
    using System.Collections.Generic;

    using PuffinBasicCS.Domain;

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
                dims.Add(symbolTable[param.op1].Value.GetInt32());
            }

            symbolTable[instruction.op1].Value.SetArrayDimensions(dims);
        }

        public static void ResetIndex(ArrayState state, PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            state.Reset();
            symbolTable[instruction.op1].Value.ResetArrayIndex();
        }

        public static void AllocArray(PuffinBasicSymbolTable symbolTable, IList<Instruction> @params, Instruction instruction)
        {
            List<int> dims = new List<int>(@params.Count);
            foreach (var param in @params)
            {
                dims.Add(symbolTable[param.op1].Value.GetInt32());
            }

            var arrayEntry = symbolTable[instruction.result];
            var arrayType = (ArrayType)arrayEntry.Type;
            arrayType.SetArrayDimensions(dims);
            arrayEntry.Value.SetArrayDimensions(dims);
        }

        public static void ReallocArray(PuffinBasicSymbolTable symbolTable, IList<Instruction> @params, Instruction instruction)
        {
            List<int> dims = new List<int>(@params.Count);
            foreach (var param in @params)
            {
                dims.Add(symbolTable[param.op1].Value.GetInt32());
            }

            var arrayEntry = symbolTable[instruction.op1];
            var arrayType = (ArrayType)arrayEntry.Type;
            arrayType.SetArrayDimensions(dims);

            // Create new value
            // TODO: check if we actually need to cast this to AbstractSTEntity
            ((AbstractSTEntry)arrayEntry).CreateAndSetInstance(symbolTable);
        }

        public static void SetIndex(ArrayState state, PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            int index = symbolTable[instruction.op2].Value.GetInt32();
            symbolTable[instruction.op1].Value.SetArrayIndex(state.GetAndIncrement(), index);
        }

        public static void Arrayref(PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            var index = symbolTable[instruction.op1].Value.GetArrayIndex1D();
            symbolTable[instruction.result].Value.SetArrayReferenceIndex1D(index);
        }

        public static void Arrayfill(PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            var array = symbolTable[instruction.op1].Value;
            var fillEntry = symbolTable[instruction.op2];
            var fill = fillEntry.Value;
            switch (fillEntry.Type.AtomTypeId)
            {
                case PuffinBasicAtomTypeId.INT32:
                    array.Fill(fill.GetInt32());
                    break;
                case PuffinBasicAtomTypeId.INT64:
                    array.Fill(fill.GetInt64());
                    break;
                case PuffinBasicAtomTypeId.FLOAT:
                    array.Fill(fill.GetFloat32());
                    break;
                case PuffinBasicAtomTypeId.DOUBLE:
                    array.Fill(fill.GetFloat64());
                    break;
                case PuffinBasicAtomTypeId.STRING:
                    array.FillString(fill.GetString());
                    break;
                default:
                    ThrowUnsupportedType(fillEntry.Type.AtomTypeId);
                    break;
            }
        }

        public static void ArrayCopy(PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            var array1Entry = symbolTable[instruction.op1];
            var array1 = array1Entry.Value;
            var array2Entry = symbolTable[instruction.op2];
            var array2 = array2Entry.Value;
            if (array1Entry.Type.AtomTypeId != array2Entry.Type.AtomTypeId)
            {
                throw new PuffinBasicRuntimeError(DATA_TYPE_MISMATCH, 
                    $"Array data type mismatch: {array1Entry.Type.AtomTypeId} is not compatible with {array2Entry.Type.AtomTypeId}");
            }

            if (array1.GetTotalLength() != array2.GetTotalLength())
            {
                throw new PuffinBasicRuntimeError(ILLEGAL_FUNCTION_PARAM,
                    $"Array length mismatch: {array1.GetTotalLength()} is not compatible with {array2.GetTotalLength()}");
            }

            switch (array1Entry.Type.AtomTypeId)
            {
                case PuffinBasicAtomTypeId.INT32:
                {
                    int[] value = ((STInt32ArrayValue)array1).Value;
                    Array.Copy(value, ((STInt32ArrayValue)array2).Value, value.Length);
                }

                    break;
                case PuffinBasicAtomTypeId.INT64:
                {
                    long[] value = ((STInt64ArrayValue)array1).Value;
                    Array.Copy(value, ((STInt64ArrayValue)array2).Value, value.Length);
                }

                    break;
                case PuffinBasicAtomTypeId.FLOAT:
                {
                    float[] value = ((STFloat32ArrayValue)array1).Value;
                    Array.Copy(value, ((STFloat32ArrayValue)array2).Value, value.Length);
                }

                    break;
                case PuffinBasicAtomTypeId.DOUBLE:
                {
                    double[] value = ((STFloat64ArrayValue)array1).Value;
                    Array.Copy(value, ((STFloat64ArrayValue)array2).Value, value.Length);
                }

                    break;
                case PuffinBasicAtomTypeId.STRING:
                {
                    String[] value = ((STStringArrayValue)array1).Value;
                    Array.Copy(value, ((STStringArrayValue)array2).Value, value.Length);
                }

                    break;
                default:
                    ThrowUnsupportedType(array1Entry.Type.AtomTypeId);
                    break;
            }
        }

        public static void Array2dShiftVertical(PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            var arrayEntry = symbolTable[instruction.op1];
            var array = arrayEntry.Value;
            var shift = symbolTable[instruction.op2].Value.GetInt32();
            var dims = array.GetArrayDimensions();

            // Arrays are row-major.
            var dim1 = dims[0];
            var dim2 = dims[1];
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

            switch (arrayEntry.Type.AtomTypeId)
            {
                case PuffinBasicAtomTypeId.INT32:
                {
                    int[] value = ((STInt32ArrayValue)array).Value;
                    Array.Copy(value, src0, value, dst0, len);
                    ArrayUtil.Fill(value, 0, fillSrc0, delta);
                }

                    break;
                case PuffinBasicAtomTypeId.INT64:
                {
                    long[] value = ((STInt64ArrayValue)array).Value;
                    Array.Copy(value, src0, value, dst0, len);
                    ArrayUtil.Fill(value, 0, fillSrc0, delta);
                }

                    break;
                case PuffinBasicAtomTypeId.FLOAT:
                {
                    float[] value = ((STFloat32ArrayValue)array).Value;
                    Array.Copy(value, src0, value, dst0, len);
                    ArrayUtil.Fill(value, 0, fillSrc0, delta);
                }

                    break;
                case PuffinBasicAtomTypeId.DOUBLE:
                {
                    double[] value = ((STFloat64ArrayValue)array).Value;
                    Array.Copy(value, src0, value, dst0, len);
                    ArrayUtil.Fill(value, 0, fillSrc0, delta);
                }

                    break;
                case PuffinBasicAtomTypeId.STRING:
                {
                    string[] value = ((STStringArrayValue)array).Value;
                    Array.Copy(value, src0, value, dst0, len);
                    ArrayUtil.Fill(value, "", fillSrc0, delta);
                }

                    break;
                default:
                    ThrowUnsupportedType(arrayEntry.Type.AtomTypeId);
                    break;
            }
        }

        public static void Array2dShiftHorizontal(PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            var arrayEntry = symbolTable[instruction.op1];
            var array = arrayEntry.Value;
            var shift = symbolTable[instruction.op2].Value.GetInt32();
            var dims = array.GetArrayDimensions();

            // Arrays are row-major.
            var dim1 = dims[0];
            var dim2 = dims[1];
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

            switch (arrayEntry.Type.AtomTypeId)
            {
                case PuffinBasicAtomTypeId.INT32:
                {
                    int[] value = ((STInt32ArrayValue)array).Value;
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
                case PuffinBasicAtomTypeId.INT64:
                {
                    long[] value = ((STInt64ArrayValue)array).Value;
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
                case PuffinBasicAtomTypeId.FLOAT:
                {
                    float[] value = ((STFloat32ArrayValue)array).Value;
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
                case PuffinBasicAtomTypeId.DOUBLE:
                {
                    double[] value = ((STFloat64ArrayValue)array).Value;
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
                case PuffinBasicAtomTypeId.STRING:
                {
                    String[] value = ((STStringArrayValue)array).Value;
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
                    ThrowUnsupportedType(arrayEntry.Type.AtomTypeId);
                    break;
            }
        }

        public static void Array1DCopy(PuffinBasicSymbolTable symbolTable, Instruction i0, Instruction i1, Instruction instruction)
        {
            var srcEntry = symbolTable[i0.op1];
            var src = srcEntry.Value;
            var src0 = symbolTable[i0.op2].Value.GetInt32();
            var dstEntry = symbolTable[i1.op1];
            var dst = dstEntry.Value;
            var dst0 = symbolTable[i1.op2].Value.GetInt32();
            var len = symbolTable[instruction.op1].Value.GetInt32();
            if (srcEntry.Type.AtomTypeId != dstEntry.Type.AtomTypeId)
            {
                throw new PuffinBasicRuntimeError(DATA_TYPE_MISMATCH, 
                    $"Array data type mismatch: {srcEntry.Type.AtomTypeId} is not compatible with {dstEntry.Type.AtomTypeId}");
            }

            if (src.GetNumArrayDimensions() != 1 && dst.GetNumArrayDimensions() != 1)
            {
                throw new PuffinBasicRuntimeError(ILLEGAL_FUNCTION_PARAM, 
                    $"Array #dim!=1 : src={src.GetNumArrayDimensions()} and dst={dst.GetNumArrayDimensions()}");
            }

            if (src0 < 0 || src0 >= src.GetTotalLength() || dst0 < 0 || len < 0 || dst0 + len > dst.GetTotalLength())
            {
                throw new PuffinBasicRuntimeError(ILLEGAL_FUNCTION_PARAM, 
                    $"Bad params: srcOrigin={src0} dstOrigin={dst0} len={len} srcArraySize={src.GetTotalLength()} dstArraySize={dst.GetTotalLength()}");
            }

            switch (srcEntry.Type.AtomTypeId)
            {
                case PuffinBasicAtomTypeId.INT32:
                {
                    int[] value = ((STInt32ArrayValue)src).Value;
                    Array.Copy(value, src0, ((STInt32ArrayValue)dst).Value, dst0, len);
                }

                    break;
                case PuffinBasicAtomTypeId.INT64:
                {
                    long[] value = ((STInt64ArrayValue)src).Value;
                    Array.Copy(value, src0, ((STInt64ArrayValue)dst).Value, dst0, len);
                }

                    break;
                case PuffinBasicAtomTypeId.FLOAT:
                {
                    float[] value = ((STFloat32ArrayValue)src).Value;
                    Array.Copy(value, src0, ((STFloat32ArrayValue)dst).Value, dst0, len);
                }

                    break;
                case PuffinBasicAtomTypeId.DOUBLE:
                {
                    double[] value = ((STFloat64ArrayValue)src).Value;
                    Array.Copy(value, src0, ((STFloat64ArrayValue)dst).Value, dst0, len);
                }

                    break;
                case PuffinBasicAtomTypeId.STRING:
                {
                    String[] value = ((STStringArrayValue)src).Value;
                    Array.Copy(value, src0, ((STStringArrayValue)dst).Value, dst0, len);
                }

                    break;
                default:
                    ThrowUnsupportedType(srcEntry.Type.AtomTypeId);
                    break;
            }
        }

        public static void Array1dSort(PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            var entry = symbolTable[instruction.op1];
            var array = entry.Value;
            switch (entry.Type.AtomTypeId)
            {
                case PuffinBasicAtomTypeId.INT32:
                    Array.Sort(((STInt32ArrayValue)array).Value);
                    break;
                case PuffinBasicAtomTypeId.INT64:
                    Array.Sort(((STInt64ArrayValue)array).Value);
                    break;
                case PuffinBasicAtomTypeId.FLOAT:
                    Array.Sort(((STFloat32ArrayValue)array).Value);
                    break;
                case PuffinBasicAtomTypeId.DOUBLE:
                    Array.Sort(((STFloat64ArrayValue)array).Value);
                    break;
                case PuffinBasicAtomTypeId.STRING:
                    Array.Sort(((STStringArrayValue)array).Value);
                    break;
                default:
                    ThrowUnsupportedType(entry.Type.AtomTypeId);
                    break;
            }
        }

        public static void Array1dBinSearch(PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            var arrayEntry = symbolTable[instruction.op1];
            var array = arrayEntry.Value;
            var search = symbolTable[instruction.op2].Value;
            var result = symbolTable[instruction.result].Value;
            var index = -1;
            switch (arrayEntry.Type.AtomTypeId)
            {
                case PuffinBasicAtomTypeId.INT32:
                    index = Array.BinarySearch(((STInt32ArrayValue)array).Value, search.GetInt32());
                    break;
                case PuffinBasicAtomTypeId.INT64:
                    index = Array.BinarySearch(((STInt64ArrayValue)array).Value, search.GetInt64());
                    break;
                case PuffinBasicAtomTypeId.FLOAT:
                    index = Array.BinarySearch(((STFloat32ArrayValue)array).Value, search.GetFloat32());
                    break;
                case PuffinBasicAtomTypeId.DOUBLE:
                    index = Array.BinarySearch(((STFloat64ArrayValue)array).Value, search.GetFloat64());
                    break;
                case PuffinBasicAtomTypeId.STRING:
                    index = Array.BinarySearch(((STStringArrayValue)array).Value, search.GetString());
                    break;
                default:
                    ThrowUnsupportedType(arrayEntry.Type.AtomTypeId);
                    break;
            }

            result.SetInt32(index);
        }

        public static void Array1dMin(PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            var arrayEntry = symbolTable[instruction.op1];
            var array = arrayEntry.Value;
            var result = symbolTable[instruction.result].Value;
            switch (arrayEntry.Type.AtomTypeId)
            {
                case PuffinBasicAtomTypeId.INT32:
                {
                    int[] value = ((STInt32ArrayValue)array).Value;
                    var min = int.MaxValue;
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
                case PuffinBasicAtomTypeId.INT64:
                {
                    long[] value = ((STInt64ArrayValue)array).Value;
                    var min = long.MaxValue;
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
                case PuffinBasicAtomTypeId.FLOAT:
                {
                    float[] value = ((STFloat32ArrayValue)array).Value;
                    var min = float.MaxValue;
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
                case PuffinBasicAtomTypeId.DOUBLE:
                {
                    double[] value = ((STFloat64ArrayValue)array).Value;
                    var min = Double.MaxValue;
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
                    ThrowUnsupportedType(arrayEntry.Type.AtomTypeId);
                    break;
            }
        }

        public static void Array1dMax(PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            var arrayEntry = symbolTable[instruction.op1];
            var array = arrayEntry.Value;
            var result = symbolTable[instruction.result].Value;
            switch (arrayEntry.Type.AtomTypeId)
            {
                case PuffinBasicAtomTypeId.INT32:
                {
                    int[] value = ((STInt32ArrayValue)array).Value;
                    var max = int.MinValue;
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
                case PuffinBasicAtomTypeId.INT64:
                {
                    long[] value = ((STInt64ArrayValue)array).Value;
                    var max = long.MinValue;
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
                case PuffinBasicAtomTypeId.FLOAT:
                {
                    float[] value = ((STFloat32ArrayValue)array).Value;
                    var max = float.MinValue;
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
                case PuffinBasicAtomTypeId.DOUBLE:
                {
                    double[] value = ((STFloat64ArrayValue)array).Value;
                    var max = Double.MinValue;
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
                    ThrowUnsupportedType(arrayEntry.Type.AtomTypeId);
                    break;
            }
        }

        public static void Array1dMean(PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            var arrayEntry = symbolTable[instruction.op1];
            var result = symbolTable[instruction.result].Value;
            var stats = Array1dStats(arrayEntry);
            result.SetFloat64(stats.GetMean());
        }

        public static void Array1dStddev(PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            var arrayEntry = symbolTable[instruction.op1];
            var array = arrayEntry.Value;
            var result = symbolTable[instruction.result].Value;
            var stats = Array1dStats(arrayEntry);
            result.SetFloat64(Math.Sqrt(stats.GetVariance()));
        }

        public static void Array1dSum(PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            var arrayEntry = symbolTable[instruction.op1];
            var result = symbolTable[instruction.result].Value;
            var stats = Array1dStats(arrayEntry);
            result.SetFloat64(stats.GetSum());
        }

        public static void Array1dMedian(PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            var arrayEntry = symbolTable[instruction.op1];
            var result = symbolTable[instruction.result].Value;
            var stats = Array1dStats(arrayEntry);
            result.SetFloat64(stats.GetPercentile(50));
        }

        public static void Array1dPercentile(PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            var arrayEntry = symbolTable[instruction.op1];
            var pct = symbolTable[instruction.op2].Value.GetFloat64();
            if (pct < 0 || pct > 100)
            {
                throw new PuffinBasicRuntimeError(DATA_OUT_OF_RANGE, $"Percentile value out of range: {pct}");
            }

            var result = symbolTable[instruction.result].Value;
            var stats = Array1dStats(arrayEntry);
            result.SetFloat64(stats.GetPercentile(pct));
        }

        private static ArrayStatistics Array1dStats(ISTEntry array)
        {
            var stats = new ArrayStatistics();
            switch (array.Type.AtomTypeId)
            {
                case PuffinBasicAtomTypeId.INT32:
                {
                    int[] value = ((STInt32ArrayValue)array.Value).Value;
                    foreach (int v in value)
                    {
                        stats.AddValue(v);
                    }
                }

                    break;
                case PuffinBasicAtomTypeId.INT64:
                {
                    long[] value = ((STInt64ArrayValue)array.Value).Value;
                    foreach (long v in value)
                    {
                        stats.AddValue(v);
                    }
                }

                    break;
                case PuffinBasicAtomTypeId.FLOAT:
                {
                    float[] value = ((STFloat32ArrayValue)array.Value).Value;
                    foreach (float v in value)
                    {
                        stats.AddValue(v);
                    }
                }

                    break;
                case PuffinBasicAtomTypeId.DOUBLE:
                {
                    double[] value = ((STFloat64ArrayValue)array.Value).Value;
                    foreach (double v in value)
                    {
                        stats.AddValue(v);
                    }
                }

                    break;
                default:
                    ThrowUnsupportedType(array.Type.AtomTypeId);
                    break;
            }

            return stats;
        }

        public static void Array2dFindRow(PuffinBasicSymbolTable symbolTable, IList<Instruction> @params, Instruction instruction)
        {
            var i1 = @params[0];
            var i2 = @params[1];
            var arrayEntry = symbolTable[instruction.op1];
            var array = arrayEntry.Value;
            var search = symbolTable[instruction.op2].Value;
            var result = symbolTable[instruction.result].Value;
            var dims = array.GetArrayDimensions();

            // Arrays are row-major.
            var numRows = dims[0];
            var numCols = dims[1];
            var n = array.GetTotalLength();
            var x1 = Math.Min(Math.Max(0, symbolTable[i1.op1].Value.GetInt32()), numCols - 1);
            var y1 = Math.Min(Math.Max(0, symbolTable[i1.op2].Value.GetInt32()), numRows - 1);
            var x2 = Math.Min(Math.Max(0, symbolTable[i2.op1].Value.GetInt32()), numCols - 1);
            var y2 = Math.Min(Math.Max(0, symbolTable[i2.op2].Value.GetInt32()), numRows - 1);
            if (y1 * numCols + x1 >= n || y2 * numCols + x2 >= n)
            {
                throw new PuffinBasicRuntimeError(INDEX_OUT_OF_BOUNDS, $"x1={x1}/y1={y1}/x2={x2}/y2={y2} is out of bounds, array length={n}");
            }

            switch (arrayEntry.Type.AtomTypeId)
            {
                case PuffinBasicAtomTypeId.INT32:
                {
                    int[] int32Array = ((STInt32ArrayValue)array).Value;
                    result.SetInt32(FindRowWithValue(int32Array, numCols, x1, y1, x2, y2, search.GetInt32()));
                }

                    break;
                case PuffinBasicAtomTypeId.INT64:
                {
                    long[] int64Array = ((STInt64ArrayValue)array).Value;
                    result.SetInt32(FindRowWithValue(int64Array, numCols, x1, y1, x2, y2, search.GetInt64()));
                }

                    break;
                default:
                    ThrowUnsupportedType(arrayEntry.Type.AtomTypeId);
                    break;
            }
        }

        public static void Array2dFindColumn(PuffinBasicSymbolTable symbolTable, IList<Instruction> @params, Instruction instruction)
        {
            var i1 = @params[0];
            var i2 = @params[1];
            var arrayEntry = symbolTable[instruction.op1];
            var array = arrayEntry.Value;
            var search = symbolTable[instruction.op2].Value;
            var result = symbolTable[instruction.result].Value;
            var dims = array.GetArrayDimensions();

            // Arrays are row-major.
            var numRows = dims[0];
            var numCols = dims[1];
            var n = array.GetTotalLength();
            var x1 = Math.Min(Math.Max(0, symbolTable[i1.op1].Value.GetInt32()), numCols - 1);
            var y1 = Math.Min(Math.Max(0, symbolTable[i1.op2].Value.GetInt32()), numRows - 1);
            var x2 = Math.Min(Math.Max(0, symbolTable[i2.op1].Value.GetInt32()), numCols - 1);
            var y2 = Math.Min(Math.Max(0, symbolTable[i2.op2].Value.GetInt32()), numRows - 1);
            if (y1 * numCols + x1 >= n || y2 * numCols + x2 >= n)
            {
                throw new PuffinBasicRuntimeError(INDEX_OUT_OF_BOUNDS, $"x1={x1}/y1={y1}/x2={x2}/y2={y2} is out of bounds, array length={n}");
            }

            switch (arrayEntry.Type.AtomTypeId)
            {
                case PuffinBasicAtomTypeId.INT32:
                {
                    int[] int32Array = ((STInt32ArrayValue)array).Value;
                    result.SetInt32(FindColumnWithValue(int32Array, numCols, x1, y1, x2, y2, search.GetInt32()));
                }

                    break;
                case PuffinBasicAtomTypeId.INT64:
                {
                    long[] int64Array = ((STInt64ArrayValue)array).Value;
                    result.SetInt32(FindColumnWithValue(int64Array, numCols, x1, y1, x2, y2, search.GetInt64()));
                }

                    break;
                default:
                    ThrowUnsupportedType(arrayEntry.Type.AtomTypeId);
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

    public static class ArrayUtil
    {
        public static void Fill<T>(T[] array, T value, int startIndex, int count)
        {
            if (array.Length < (startIndex + count))
                Array.Resize(ref array, (startIndex + count));
            Array.Fill(array, value, startIndex, count);
        }
    }
}

