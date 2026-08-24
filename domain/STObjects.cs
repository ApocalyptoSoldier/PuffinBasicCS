//using Com.Google.Common.Collect;

//using It.Unimi.Dsi.Fastutil.Ints;
//using It.Unimi.Dsi.Fastutil.Objects;

//using Java.Time;
//using Java.Time.Format;
//using Java.Util;

using static Org.Puffinbasic.Domain.PuffinBasicSymbolTable;
using static Org.Puffinbasic.Domain.STObjects.PuffinBasicAtomTypeId;
using static Org.Puffinbasic.Domain.Variable;
using Org.Puffinbasic.Error;
using static Org.Puffinbasic.Error.PuffinBasicRuntimeError.ErrorCode;
using Org.Puffinbasic.Runtime;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;

using static Org.Puffinbasic.Domain.Variable;
using System.Collections;

namespace Org.Puffinbasic.Domain
{
    public class STObjects
    {
        public enum PuffinBasicTypeId
        {
            SCALAR,
            ARRAY,
            UDF,
            STRUCT,
            LIST,
            SET,
            DICT
        }

        public enum PuffinBasicAtomTypeId
        {
            // INT32('%') {
            // 
            //     @Override
            //     public STVariable createVariableEntry(Variable variable) {
            //         if (variable.isArray()) {
            //             return new STVariable(new STInt32ArrayValue(), variable);
            //         } else if (variable.isUDF()) {
            //             return new STUDF(new STInt32ScalarValue(), variable);
            //         } else if (variable.isScalar()) {
            //             return new STVariable(new STInt32ScalarValue(), variable);
            //         } else {
            //             throw new PuffinBasicInternalError("Variable type not supported: " + variable);
            //         }
            //     }
            // 
            //     @Override
            //     public STTmp createTmpEntry() {
            //         return new STTmp(new STInt32ScalarValue(), ScalarType.INT32);
            //     }
            // 
            //     @Override
            //     public STTmp createArrayEntry() {
            //         return new STTmp(new STInt32ArrayValue(), ScalarType.INT32);
            //     }
            // 
            //     @Override
            //     public STValue createValue() {
            //         return new STInt32ScalarValue();
            //     }
            // 
            //     @Override
            //     public Object getValueFrom(STValue src) {
            //         return src.getInt32();
            //     }
            // 
            //     @Override
            //     public void setValueIn(Object value, STValue dst) {
            //         dst.setInt32((int) value);
            //     }
            // 
            //     @Override
            //     public void copyArray(Collection<?> src, STValue dst) {
            //         var dims = new List<int>(1);
            //         dims.add(src.size());
            //         dst.setArrayDimensions(dims);
            //         int[] array = ((STInt32ArrayValue) dst).getValue();
            //         int i = 0;
            //         for (Object o : src) {
            //             array[i++] = (int) o;
            //         }
            //     }
            // 
            //     @Override
            //     public void copyArray(Object[] src, STValue dst) {
            //         Integer[] srcList = (Integer[]) src;
            //         var dims = new List<int>(1);
            //         dims.add(src.length);
            //         dst.setArrayDimensions(dims);
            //         int[] array = ((STInt32ArrayValue) dst).getValue();
            //         int i = 0;
            //         for (Integer o : srcList) {
            //             array[i++] = o;
            //         }
            //     }
            // 
            //     @Override
            //     public boolean isCompatibleWith(PuffinBasicAtomTypeId other) {
            //         return other == INT32 || other == INT64 || other == FLOAT || other == DOUBLE;
            //     }
            // }
            // 
            INT32,
            // INT64('@') {
            // 
            //     @Override
            //     public STVariable createVariableEntry(Variable variable) {
            //         if (variable.isArray()) {
            //             return new STVariable(new STInt64ArrayValue(), variable);
            //         } else if (variable.isUDF()) {
            //             return new STUDF(new STInt64ScalarValue(), variable);
            //         } else if (variable.isScalar()) {
            //             return new STVariable(new STInt64ScalarValue(), variable);
            //         } else {
            //             throw new PuffinBasicInternalError("Variable type not supported: " + variable);
            //         }
            //     }
            // 
            //     @Override
            //     public STTmp createTmpEntry() {
            //         return new STTmp(new STInt64ScalarValue(), ScalarType.INT64);
            //     }
            // 
            //     @Override
            //     public STTmp createArrayEntry() {
            //         return new STTmp(new STInt64ArrayValue(), ScalarType.INT64);
            //     }
            // 
            //     @Override
            //     public STValue createValue() {
            //         return new STInt64ScalarValue();
            //     }
            // 
            //     @Override
            //     public Object getValueFrom(STValue src) {
            //         return src.getInt64();
            //     }
            // 
            //     @Override
            //     public void setValueIn(Object value, STValue dst) {
            //         dst.setInt64((long) value);
            //     }
            // 
            //     @Override
            //     public void copyArray(Collection<?> src, STValue dst) {
            //         var dims = new List<int>(1);
            //         dims.add(src.size());
            //         dst.setArrayDimensions(dims);
            //         long[] array = ((STInt64ArrayValue) dst).getValue();
            //         int i = 0;
            //         for (Object o : src) {
            //             array[i++] = (long) o;
            //         }
            //     }
            // 
            //     @Override
            //     public void copyArray(Object[] src, STValue dst) {
            //         Long[] srcList = (Long[]) src;
            //         var dims = new List<int>(1);
            //         dims.add(src.length);
            //         dst.setArrayDimensions(dims);
            //         long[] array = ((STInt64ArrayValue) dst).getValue();
            //         int i = 0;
            //         for (long o : srcList) {
            //             array[i++] = o;
            //         }
            //     }
            // 
            //     @Override
            //     public boolean isCompatibleWith(PuffinBasicAtomTypeId other) {
            //         return other == INT32 || other == INT64 || other == FLOAT || other == DOUBLE;
            //     }
            // }
            // 
            INT64,
            // FLOAT('!') {
            // 
            //     @Override
            //     public STVariable createVariableEntry(Variable variable) {
            //         if (variable.isArray()) {
            //             return new STVariable(new STFloat32ArrayValue(), variable);
            //         } else if (variable.isUDF()) {
            //             return new STUDF(new STFloat32ScalarValue(), variable);
            //         } else if (variable.isScalar()) {
            //             return new STVariable(new STFloat32ScalarValue(), variable);
            //         } else {
            //             throw new PuffinBasicInternalError("Variable type not supported: " + variable);
            //         }
            //     }
            // 
            //     @Override
            //     public STTmp createTmpEntry() {
            //         return new STTmp(new STFloat32ScalarValue(), ScalarType.FLOAT32);
            //     }
            // 
            //     @Override
            //     public STTmp createArrayEntry() {
            //         return new STTmp(new STFloat32ArrayValue(), ScalarType.FLOAT32);
            //     }
            // 
            //     @Override
            //     public STValue createValue() {
            //         return new STFloat32ScalarValue();
            //     }
            // 
            //     @Override
            //     public Object getValueFrom(STValue src) {
            //         return src.getFloat32();
            //     }
            // 
            //     @Override
            //     public void setValueIn(Object value, STValue dst) {
            //         dst.setFloat32((float) value);
            //     }
            // 
            //     @Override
            //     public void copyArray(Collection<?> src, STValue dst) {
            //         var dims = new List<int>(1);
            //         dims.add(src.size());
            //         dst.setArrayDimensions(dims);
            //         float[] array = ((STFloat32ArrayValue) dst).getValue();
            //         int i = 0;
            //         for (Object o : src) {
            //             array[i++] = (float) o;
            //         }
            //     }
            // 
            //     @Override
            //     public void copyArray(Object[] src, STValue dst) {
            //         Float[] srcList = (Float[]) src;
            //         var dims = new List<int>(1);
            //         dims.add(src.length);
            //         dst.setArrayDimensions(dims);
            //         float[] array = ((STFloat32ArrayValue) dst).getValue();
            //         int i = 0;
            //         for (Float o : srcList) {
            //             array[i++] = o;
            //         }
            //     }
            // 
            //     @Override
            //     public boolean isCompatibleWith(PuffinBasicAtomTypeId other) {
            //         return other == INT32 || other == INT64 || other == FLOAT || other == DOUBLE;
            //     }
            // }
            // 
            FLOAT,
            // DOUBLE('#') {
            // 
            //     @Override
            //     public STVariable createVariableEntry(Variable variable) {
            //         if (variable.isArray()) {
            //             return new STVariable(new STFloat64ArrayValue(), variable);
            //         } else if (variable.isUDF()) {
            //             return new STUDF(new STFloat64ScalarValue(), variable);
            //         } else if (variable.isScalar()) {
            //             return new STVariable(new STFloat64ScalarValue(), variable);
            //         } else {
            //             throw new PuffinBasicInternalError("Variable type not supported: " + variable);
            //         }
            //     }
            // 
            //     @Override
            //     public STTmp createTmpEntry() {
            //         return new STTmp(new STFloat64ScalarValue(), ScalarType.FLOAT64);
            //     }
            // 
            //     @Override
            //     public STTmp createArrayEntry() {
            //         return new STTmp(new STFloat64ArrayValue(), ScalarType.FLOAT64);
            //     }
            // 
            //     @Override
            //     public STValue createValue() {
            //         return new STFloat64ScalarValue();
            //     }
            // 
            //     @Override
            //     public Object getValueFrom(STValue src) {
            //         return src.getFloat64();
            //     }
            // 
            //     @Override
            //     public void setValueIn(Object value, STValue dst) {
            //         dst.setFloat64((double) value);
            //     }
            // 
            //     @Override
            //     public void copyArray(Collection<?> src, STValue dst) {
            //         var dims = new List<int>(1);
            //         dims.add(src.size());
            //         dst.setArrayDimensions(dims);
            //         double[] array = ((STFloat64ArrayValue) dst).getValue();
            //         int i = 0;
            //         for (Object o : src) {
            //             array[i++] = (double) o;
            //         }
            //     }
            // 
            //     @Override
            //     public void copyArray(Object[] src, STValue dst) {
            //         Double[] srcList = (Double[]) src;
            //         var dims = new List<int>(1);
            //         dims.add(src.length);
            //         dst.setArrayDimensions(dims);
            //         double[] array = ((STFloat64ArrayValue) dst).getValue();
            //         int i = 0;
            //         for (Double o : srcList) {
            //             array[i++] = o;
            //         }
            //     }
            // 
            //     @Override
            //     public boolean isCompatibleWith(PuffinBasicAtomTypeId other) {
            //         return other == INT32 || other == INT64 || other == FLOAT || other == DOUBLE;
            //     }
            // }
            // 
            DOUBLE,
            // STRING('$') {
            // 
            //     @Override
            //     public STVariable createVariableEntry(Variable variable) {
            //         if (variable.isArray()) {
            //             return new STVariable(new STStringArrayValue(), variable);
            //         } else if (variable.isUDF()) {
            //             return new STUDF(new STStringScalarValue(), variable);
            //         } else if (variable.isScalar()) {
            //             String varname = variable.getVariableName().getVarname();
            //             if (varname.equalsIgnoreCase("date")) {
            //                 return new STVariable(new STStringScalarDateValue(), variable);
            //             } else if (varname.equalsIgnoreCase("time")) {
            //                 return new STVariable(new STStringScalarTimeValue(), variable);
            //             } else {
            //                 return new STVariable(new STStringScalarValue(), variable);
            //             }
            //         } else {
            //             throw new PuffinBasicInternalError("Variable type not supported: " + variable);
            //         }
            //     }
            // 
            //     @Override
            //     public STTmp createTmpEntry() {
            //         return new STTmp(new STStringScalarValue(), ScalarType.STRING);
            //     }
            // 
            //     @Override
            //     public STTmp createArrayEntry() {
            //         return new STTmp(new STStringArrayValue(), ScalarType.STRING);
            //     }
            // 
            //     @Override
            //     public STValue createValue() {
            //         return new STStringScalarValue();
            //     }
            // 
            //     @Override
            //     public Object getValueFrom(STValue src) {
            //         return src.getString();
            //     }
            // 
            //     @Override
            //     public void setValueIn(Object value, STValue dst) {
            //         dst.setString((String) value);
            //     }
            // 
            //     @Override
            //     public void copyArray(Collection<?> src, STValue dst) {
            //         var dims = new List<int>(1);
            //         dims.add(src.size());
            //         dst.setArrayDimensions(dims);
            //         String[] array = ((STStringArrayValue) dst).getValue();
            //         int i = 0;
            //         for (Object o : src) {
            //             array[i++] = (String) o;
            //         }
            //     }
            // 
            //     @Override
            //     public void copyArray(Object[] src, STValue dst) {
            //         String[] srcList = (String[]) src;
            //         var dims = new List<int>(1);
            //         dims.add(src.length);
            //         dst.setArrayDimensions(dims);
            //         String[] array = ((STStringArrayValue) dst).getValue();
            //         int i = 0;
            //         for (String o : srcList) {
            //             array[i++] = o;
            //         }
            //     }
            // 
            //     @Override
            //     public boolean isCompatibleWith(PuffinBasicAtomTypeId other) {
            //         return other == STRING;
            //     }
            // }
            // 
            STRING,
            // COMPOSITE(null) {
            // 
            //     @Override
            //     public STVariable createVariableEntry(Variable variable) {
            //         return new STVariable(null, variable);
            //     }
            // 
            //     @Override
            //     public STTmp createTmpEntry() {
            //         throw new PuffinBasicInternalError("Not implemented");
            //     }
            // 
            //     @Override
            //     public STTmp createArrayEntry() {
            //         throw new PuffinBasicInternalError("Not implemented");
            //     }
            // 
            //     @Override
            //     public STValue createValue() {
            //         throw new PuffinBasicInternalError("Not implemented");
            //     }
            // 
            //     @Override
            //     public Object getValueFrom(STValue src) {
            //         throw new PuffinBasicInternalError("Not implemented");
            //     }
            // 
            //     @Override
            //     public void setValueIn(Object value, STValue dst) {
            //         throw new PuffinBasicInternalError("Not implemented");
            //     }
            // 
            //     @Override
            //     public void copyArray(Collection<?> src, STValue dst) {
            //         throw new PuffinBasicInternalError("Not implemented");
            //     }
            // 
            //     @Override
            //     public void copyArray(Object[] src, STValue dst) {
            //         throw new PuffinBasicInternalError("Not implemented");
            //     }
            // 
            //     @Override
            //     public boolean isCompatibleWith(PuffinBasicAtomTypeId other) {
            //         return other == COMPOSITE;
            //     }
            // }
            // 
            COMPOSITE 

            // --------------------
            // TODO enum body members
            // private static final Dictionary<int, PuffinBasicAtomTypeId> mapping;
            // static {
            //     mapping = new Int2ObjectOpenHashMap<>();
            //     for (PuffinBasicAtomTypeId value : PuffinBasicAtomTypeId.values()) {
            //         if (value.repr != null) {
            //             mapping.put(value.repr, value);
            //         }
            //     }
            // }
            // public final Character repr;
            // PuffinBasicAtomTypeId(Character repr) {
            //     this.repr = repr;
            // }
            // public String getRepr() {
            //     return repr != null ? String.valueOf(repr) : null;
            // }
            // public abstract STVariable createVariableEntry(Variable variable);
            // public abstract STTmp createTmpEntry();
            // public abstract STTmp createArrayEntry();
            // public abstract STValue createValue();
            // public abstract boolean isCompatibleWith(PuffinBasicAtomTypeId other);
            // public abstract Object getValueFrom(STValue src);
            // public abstract void setValueIn(Object value, STValue dst);
            // public abstract void copyArray(Collection<?> src, STValue dst);
            // public abstract void copyArray(Object[] src, STValue dst);
            // public static PuffinBasicAtomTypeId lookup(String repr) {
            //     if (repr == null || repr.length() != 1) {
            //         throw new PuffinBasicInternalError("Variable suffix: '" + repr + "' is null or length != 1");
            //     }
            //     var dataType = mapping.get(repr.charAt(0));
            //     if (dataType == null) {
            //         throw new PuffinBasicInternalError("Variable suffix '" + repr + "' is invalid");
            //     }
            //     return dataType;
            // }
            // --------------------
        }

        public abstract class PuffinBasicType
        {
            public abstract PuffinBasicTypeId GetTypeId();
            public abstract PuffinBasicAtomTypeId GetAtomTypeId();
            public abstract STValue NewInstance(PuffinBasicSymbolTable symbolTable);

            public bool CanBeLValue()
            {
                return false;
            }

            PuffinBasicType GetFuncCallReturnType(string funcName)
            {
                throw new PuffinBasicRuntimeError(BAD_FIELD, "Unsupported function: " + funcName + " in type: " + this);
            }

            void CheckFuncCallArguments(string funcName, IList<PuffinBasicType> paramTypes)
            {
            }

            public bool IsCompatibleWith(PuffinBasicType other)
            {
                return this.Equals(other);
            }

            StructType AsStruct()
            {
                if (GetTypeId() != PuffinBasicTypeId.STRUCT)
                {
                    throw new PuffinBasicRuntimeError(BAD_FIELD, "Type is not struct!");
                }

                return (StructType)this;
            }
        }

        public class ScalarType : PuffinBasicType
        {
            public static readonly ScalarType INT32 = new ScalarType(PuffinBasicAtomTypeId.INT32);
            public static readonly ScalarType INT64 = new ScalarType(PuffinBasicAtomTypeId.INT64);
            public static readonly ScalarType FLOAT32 = new ScalarType(FLOAT);
            public static readonly ScalarType FLOAT64 = new ScalarType(DOUBLE);
            public static readonly ScalarType STRING = new ScalarType(PuffinBasicAtomTypeId.STRING);
            private readonly PuffinBasicAtomTypeId atomType;
            public ScalarType(PuffinBasicAtomTypeId atomType)
            {
                this.atomType = atomType;
            }

            public override PuffinBasicTypeId GetTypeId()
            {
                return PuffinBasicTypeId.SCALAR;
            }

            public override PuffinBasicAtomTypeId GetAtomTypeId()
            {
                return atomType;
            }

            public override STValue NewInstance(PuffinBasicSymbolTable symbolTable)
            {
                return atomType.CreateValue();
            }

            public override bool Equals(object obj)
            {
                if (this == obj)
                {
                    return true;
                }

                if (obj == null || obj.GetType() != typeof(ScalarType))
                {
                    return false;
                }

                ScalarType o = (ScalarType)obj;
                return GetTypeId() == o.GetTypeId() && GetAtomTypeId() == o.GetAtomTypeId();
            }

            public override int GetHashCode()
            {
                return Objects.Hash(GetTypeId(), GetAtomTypeId());
            }

            public override bool IsCompatibleWith(PuffinBasicType other)
            {
                return GetAtomTypeId().IsCompatibleWith(other.GetAtomTypeId());
            }
        }

        public class ArrayType : PuffinBasicType
        {
            private readonly PuffinBasicAtomTypeId atomType;
            private readonly List<int> dims;
            private readonly bool canBeLValue;
            public ArrayType(PuffinBasicAtomTypeId atomType)
            {
                this.atomType = atomType;
                this.dims = new List<int>();
                this.canBeLValue = false;
            }

            public ArrayType(PuffinBasicAtomTypeId atomType, List<int> dims, bool canBeLValue)
            {
                this.atomType = atomType;
                this.dims = dims;
                this.canBeLValue = canBeLValue;
            }

            public override void SetArrayDimensions(List<int> dims)
            {
                this.dims.Clear();
                this.dims.AddAll(dims);
            }

            public override bool IsNDArray(int n)
            {
                return dims != null && dims.Count == n;
            }

            public override bool CanBeLValue()
            {
                return canBeLValue;
            }

            public override PuffinBasicTypeId GetTypeId()
            {
                return PuffinBasicTypeId.ARRAY;
            }

            public override PuffinBasicAtomTypeId GetAtomTypeId()
            {
                return atomType;
            }

            public override STValue NewInstance(PuffinBasicSymbolTable symbolTable)
            {
                var entry = atomType.CreateArrayEntry();
                var value = entry.GetValue();
                if (dims != null)
                {
                    value.SetArrayDimensions(dims);
                }

                return value;
            }

            public override bool Equals(object obj)
            {
                if (this == obj)
                {
                    return true;
                }

                if (obj == null || obj.GetType() != typeof(ArrayType))
                {
                    return false;
                }

                ArrayType o = (ArrayType)obj;
                return GetTypeId() == o.GetTypeId() && GetAtomTypeId() == o.GetAtomTypeId();
            }

            public override int GetHashCode()
            {
                return Objects.Hash(GetTypeId(), GetAtomTypeId());
            }

            public override bool IsCompatibleWith(PuffinBasicType other)
            {
                return atomType.IsCompatibleWith(other.GetAtomTypeId());
            }
        }

        public class UDFType : PuffinBasicType
        {
            private readonly PuffinBasicAtomTypeId atomType;
            UDFType(PuffinBasicAtomTypeId atomType)
            {
                this.atomType = atomType;
            }

            public override PuffinBasicTypeId GetTypeId()
            {
                return PuffinBasicTypeId.UDF;
            }

            public override PuffinBasicAtomTypeId GetAtomTypeId()
            {
                return atomType;
            }

            public override STValue NewInstance(PuffinBasicSymbolTable symbolTable)
            {
                throw new PuffinBasicInternalError("Not implemented!");
            }

            public override bool Equals(object obj)
            {
                if (this == obj)
                {
                    return true;
                }

                if (obj == null || obj.GetType() != typeof(ArrayType))
                {
                    return false;
                }

                ArrayType o = (ArrayType)obj;
                return GetTypeId() == o.GetTypeId() && GetAtomTypeId() == o.GetAtomTypeId();
            }

            public override int GetHashCode()
            {
                return Objects.Hash(GetTypeId(), GetAtomTypeId());
            }

            public override bool IsCompatibleWith(PuffinBasicType other)
            {
                return GetTypeId() == other.GetTypeId() && GetAtomTypeId().IsCompatibleWith(other.GetAtomTypeId());
            }
        }

        public sealed class StructType : PuffinBasicType
        {
            private readonly string typeName;
            internal readonly Dictionary<int, PuffinBasicType> refIdToTypeMap;
            internal readonly Dictionary<VariableName, int> nameToRefIdMap;
            private int counter;
            public StructType(string typeName)
            {
                this.typeName = typeName;
                this.refIdToTypeMap = new Dictionary<int, PuffinBasicType>();
                this.nameToRefIdMap = new Dictionary<VariableName, int>();
            }

            public string GetTypeName()
            {
                return typeName;
            }

            public PuffinBasicType GetMemberType(VariableName memberName)
            {
                return refIdToTypeMap[GetMemberRefId(memberName)];
            }

            public bool ContainsMember(VariableName memberName)
            {
                return nameToRefIdMap.ContainsKey(memberName);
            }

            public int GetMemberRefId(VariableName memberName)
            {
                if (!nameToRefIdMap.TryGetValue(memberName, out int memberRefId))
                    throw new PuffinBasicRuntimeError(BAD_FIELD, "Missing field " + typeName + "." + memberName);

                return memberRefId;
            }

            public void DeclareField(VariableName memberName, PuffinBasicType type)
            {
                int refId = counter++;
                refIdToTypeMap.Add(refId, type);
                nameToRefIdMap.Add(memberName, refId);
            }

            public PuffinBasicTypeId GetTypeId()
            {
                return PuffinBasicTypeId.STRUCT;
            }

            public PuffinBasicAtomTypeId GetAtomTypeId()
            {
                return PuffinBasicAtomTypeId.COMPOSITE;
            }

            public STValue NewInstance(PuffinBasicSymbolTable symbolTable)
            {
                return new STStruct(symbolTable, this);
            }

            public bool Equals(object obj)
            {
                if (this == obj)
                {
                    return true;
                }

                if (obj == null || obj.GetType() != typeof(StructType))
                {
                    return false;
                }

                StructType o = (StructType)obj;
                return GetTypeId() == o.GetTypeId() && GetAtomTypeId() == o.GetAtomTypeId() && GetTypeName().Equals(o.GetTypeName());
            }

            public int GetHashCode()
            {
                return Objects.Hash(GetTypeId(), GetAtomTypeId());
            }
        }

        private interface IMemberCallHandler
        {
            void Call(object o, STValue[] @params, STValue result);
        }

        private sealed class MemberFunction
        {
            internal readonly string functionName;
            internal readonly PuffinBasicType[] paramTypes;
            internal readonly PuffinBasicType returnType;
            internal readonly IMemberCallHandler callHandler;
            MemberFunction(string functionName, PuffinBasicType[] paramTypes, PuffinBasicType returnType, IMemberCallHandler callHandler)
            {
                this.functionName = functionName;
                this.paramTypes = paramTypes;
                this.returnType = returnType;
                this.callHandler = callHandler;
            }
        }

        private sealed class MemberFunctions
        {
            private readonly Dictionary<string, MemberFunction> memberFunctions;
            MemberFunctions(IList<MemberFunction> memberFunctions)
            {
                this.memberFunctions = new Dictionary<string, MemberFunction>();
                foreach (MemberFunction mf  in memberFunctions)
                    this.memberFunctions.Add(mf.functionName, mf);
            }

            public MemberFunction this[string funcName]
            {
                get => Get(funcName);
            }

            public MemberFunction Get(string funcName)
            {
                var mf = memberFunctions[funcName];
                if (mf == null)
                {
                    throw new PuffinBasicRuntimeError(BAD_FIELD, "Unknown member function: " + funcName);
                }

                return mf;
            }

            public void CheckFuncCallArguments(string funcName, IList<PuffinBasicType> paramTypes)
            {
                PuffinBasicType[] expectedParamTypes = Get(funcName).paramTypes;
                if (expectedParamTypes.Length != paramTypes.Count)
                {
                    throw new PuffinBasicRuntimeError(BAD_FUNCTION_CALL, "Function " + funcName + " expects " + expectedParamTypes.length + " params, but called with " + paramTypes.Count + " params");
                }

                for (int i = 0; i < expectedParamTypes.Length; i++)
                {
                    if (!expectedParamTypes[i].IsCompatibleWith(paramTypes[i]))
                    {
                        throw new PuffinBasicRuntimeError(BAD_FUNCTION_CALL, "Function " + funcName + " called with wrong param type for param#" + (i + 1) + ", expected type " + expectedParamTypes[i] + ", actual " + paramTypes[i]);
                    }
                }
            }
        }

        public sealed class ListType : PuffinBasicType
        {
            private readonly PuffinBasicType type;
            private readonly MemberFunctions memberFunctions;
            public ListType(PuffinBasicType type)
            {
                this.type = type;
                ArrayType valuesType = new ArrayType(type.GetAtomTypeId());
                this.memberFunctions = new MemberFunctions(ImmutableList.Builder().Add(new MemberFunction("append", new PuffinBasicType[] { type }, ScalarType.INT32, (obj, @params, result) =>
                {
                    var list = (IList<object>)obj;
                    if (type.GetTypeId() == PuffinBasicTypeId.SCALAR)
                    {
                        list.Add(type.GetAtomTypeId().GetValueFrom(@params[0]));
                    }
                    else
                    {
                        list.Add(@params[0]);
                    }

                    result.SetInt32(0);
                })).Add(new MemberFunction("insert", new PuffinBasicType[] { ScalarType.INT32, type }, ScalarType.INT32, (obj, @params, result) =>
                {
                    var list = (IList<object>)obj;
                    int index = @params[0].GetInt32();
                    if (type.GetTypeId() == PuffinBasicTypeId.SCALAR)
                    {
                        var value = type.GetAtomTypeId().GetValueFrom(@params[1]);
                        list.Add(index, value);
                    }
                    else
                    {
                        list.Add(index, @params[1]);
                    }

                    result.SetInt32(0);
                })).Add(new MemberFunction("get", new PuffinBasicType[] { ScalarType.INT32 }, type, (obj, @params, result) =>
                {
                    var list = (IList<object>)obj;
                    int index = @params[0].GetInt32();
                    if (index < 0 || index >= list.Count)
                    {
                        throw new PuffinBasicRuntimeError(ARRAY_INDEX_OUT_OF_BOUNDS, "List index: " + index + " is out of bounds, list size: " + list.Count);
                    }

                    if (type.GetTypeId() == PuffinBasicTypeId.SCALAR)
                    {
                        type.GetAtomTypeId().SetValueIn(list[index], result);
                    }
                    else
                    {
                        STValue item = (STValue)list[index];
                        if (item == null)
                        {
                            throw new PuffinBasicRuntimeError(NOT_INITIALIZED, "Value at list index: " + index + " is not set!");
                        }

                        result.Replace(item);
                    }
                })).Add(new MemberFunction("values", new PuffinBasicType[] { }, valuesType, (obj, @params, result) =>
                {
                    var list = (IList<object>)obj;
                    if (type.GetTypeId() == PuffinBasicTypeId.SCALAR)
                    {
                        type.GetAtomTypeId().CopyArray(list, result);
                    }
                    else
                    {
                        throw new PuffinBasicRuntimeError(BAD_FUNCTION_CALL, "values() not supported for non-scalar type!");
                    }
                })).Add(new MemberFunction("clear", new PuffinBasicType[] { }, ScalarType.INT32, (obj, @params, result) =>
                {
                    var list = (IList<object>)obj;
                    list.Clear();
                    result.SetInt32(0);
                })).Build());
            }

            public PuffinBasicTypeId GetTypeId()
            {
                return PuffinBasicTypeId.LIST;
            }

            public PuffinBasicAtomTypeId GetAtomTypeId()
            {
                return COMPOSITE;
            }

            public STValue NewInstance(PuffinBasicSymbolTable symbolTable)
            {
                return new STList(type, memberFunctions);
            }

            public PuffinBasicType GetFuncCallReturnType(string funcName)
            {
                return memberFunctions[funcName].returnType;
            }

            public void CheckFuncCallArguments(string funcName, IList<PuffinBasicType> paramTypes)
            {
                memberFunctions.CheckFuncCallArguments(funcName, paramTypes);
            }

            public bool Equals(object obj)
            {
                if (this == obj)
                {
                    return true;
                }

                if (obj == null || obj.GetType() != typeof(ListType))
                {
                    return false;
                }

                ListType o = (ListType)obj;
                return GetTypeId() == o.GetTypeId() && GetAtomTypeId() == o.GetAtomTypeId();
            }

            public int GetHashCode()
            {
                return Objects.Hash(GetTypeId(), GetAtomTypeId());
            }
        }

        public sealed class SetType : PuffinBasicType
        {
            private readonly PuffinBasicType type;
            private readonly MemberFunctions memberFunctions;
            public SetType(PuffinBasicType type)
            {
                this.type = type;
                ArrayType valuesType = new ArrayType(type.GetAtomTypeId());
                this.memberFunctions = new MemberFunctions(ImmutableList.Builder().Add(new MemberFunction("add", new PuffinBasicType[] { type }, ScalarType.INT32, (obj, @params, result) =>
                {
                    var set = (HashSet<object>)obj;
                    var value = type.GetAtomTypeId().GetValueFrom(@params[0]);
                    set.Add(value);
                    result.SetInt32(0);
                })).Add(new MemberFunction("remove", new PuffinBasicType[] { type }, ScalarType.INT32, (obj, @params, result) =>
                {
                    var set = (HashSet<object>)obj;
                    var value = type.GetAtomTypeId().GetValueFrom(@params[0]);
                    var removeRes = set.Remove(value);
                    result.SetInt32(removeRes ? -1 : 0);
                })).Add(new MemberFunction("contains", new PuffinBasicType[] { type }, ScalarType.INT32, (obj, @params, result) =>
                {
                    var set = (HashSet<object>)obj;
                    var value = type.GetAtomTypeId().GetValueFrom(@params[0]);
                    result.SetInt32(set.Contains(value) ? -1 : 0);
                })).Add(new MemberFunction("values", new PuffinBasicType[] { }, valuesType, (obj, @params, result) =>
                {
                    var set = (HashSet<object>)obj;
                    type.GetAtomTypeId().CopyArray(set, result);
                })).Add(new MemberFunction("clear", new PuffinBasicType[] { }, ScalarType.INT32, (obj, @params, result) =>
                {
                    var set = (HashSet<object>)obj;
                    set.Clear();
                    result.SetInt32(0);
                })).Build());
            }

            public PuffinBasicTypeId GetTypeId()
            {
                return PuffinBasicTypeId.SET;
            }

            public PuffinBasicAtomTypeId GetAtomTypeId()
            {
                return COMPOSITE;
            }

            public STValue NewInstance(PuffinBasicSymbolTable symbolTable)
            {
                return new STSet(type, memberFunctions);
            }

            public PuffinBasicType GetFuncCallReturnType(string funcName)
            {
                return memberFunctions[funcName].returnType;
            }

            public void CheckFuncCallArguments(string funcName, IList<PuffinBasicType> paramTypes)
            {
                memberFunctions.CheckFuncCallArguments(funcName, paramTypes);
            }

            public bool Equals(object obj)
            {
                if (this == obj)
                {
                    return true;
                }

                if (obj == null || obj.GetType() != typeof(SetType))
                {
                    return false;
                }

                SetType o = (SetType)obj;
                return GetTypeId() == o.GetTypeId() && GetAtomTypeId() == o.GetAtomTypeId();
            }

            public int GetHashCode()
            {
                return Objects.Hash(GetTypeId(), GetAtomTypeId());
            }
        }

        public sealed class DictType : PuffinBasicType
        {
            private readonly PuffinBasicType keyType;
            private readonly PuffinBasicType valueType;
            private readonly MemberFunctions memberFunctions;
            public DictType(PuffinBasicType keyType, PuffinBasicType valueType)
            {
                this.keyType = keyType;
                this.valueType = valueType;
                ArrayType valuesType = new ArrayType(keyType.GetAtomTypeId());
                this.memberFunctions = new MemberFunctions(ImmutableList.Builder().Add(new MemberFunction("put", new PuffinBasicType[] { keyType, valueType }, ScalarType.INT32, (obj, @params, result) =>
                {
                    var dict = (Dictionary<object, object>)obj;
                    var key = keyType.GetAtomTypeId().GetValueFrom(@params[0]);
                    var value = valueType.GetAtomTypeId().GetValueFrom(@params[1]);
                    dict.Put(key, value);
                    result.SetInt32(0);
                })).Add(new MemberFunction("removeKey", new PuffinBasicType[] { keyType }, ScalarType.INT32, (obj, @params, result) =>
                {
                    var dict = (Dictionary<object, object>)obj;
                    var key = keyType.GetAtomTypeId().GetValueFrom(@params[0]);
                    var removeRes = dict.Remove(key);
                    result.SetInt32(removeRes != null ? -1 : 0);
                })).Add(new MemberFunction("getOrDefault", new PuffinBasicType[] { keyType, valueType }, valueType, (obj, @params, result) =>
                {
                    var dict = (Dictionary<object, object>)obj;
                    var key = keyType.GetAtomTypeId().GetValueFrom(@params[0]);
                    var value = valueType.GetAtomTypeId().GetValueFrom(@params[1]);
                    if (!dict.TryGetValue(key, out var getRes))
                        getRes = value;
                    valueType.GetAtomTypeId().SetValueIn(getRes, result);
                })).Add(new MemberFunction("containsKey", new PuffinBasicType[] { keyType }, ScalarType.INT32, (obj, @params, result) =>
                {
                    var dict = (Dictionary<object, object>)obj;
                    var key = keyType.GetAtomTypeId().GetValueFrom(@params[0]);
                    result.SetInt32(dict.ContainsKey(key) ? -1 : 0);
                })).Add(new MemberFunction("keys", new PuffinBasicType[] { }, valuesType, (obj, @params, result) =>
                {
                    var dict = (Dictionary<object, object>)obj;
                    keyType.GetAtomTypeId().CopyArray(dict.KeySet(), result);
                })).Add(new MemberFunction("clear", new PuffinBasicType[] { }, ScalarType.INT32, (obj, @params, result) =>
                {
                    var dict = (Dictionary<object, object>)obj;
                    dict.Clear();
                    result.SetInt32(0);
                })).Build());
            }

            public PuffinBasicTypeId GetTypeId()
            {
                return PuffinBasicTypeId.DICT;
            }

            public PuffinBasicAtomTypeId GetAtomTypeId()
            {
                return COMPOSITE;
            }

            public STValue NewInstance(PuffinBasicSymbolTable symbolTable)
            {
                return new STDict(valueType, memberFunctions);
            }

            public PuffinBasicType GetFuncCallReturnType(string funcName)
            {
                return memberFunctions[funcName].returnType;
            }

            public void CheckFuncCallArguments(string funcName, IList<PuffinBasicType> paramTypes)
            {
                memberFunctions.CheckFuncCallArguments(funcName, paramTypes);
            }

            public bool Equals(object obj)
            {
                if (this == obj)
                {
                    return true;
                }

                if (obj == null || obj.GetType() != typeof(DictType))
                {
                    return false;
                }

                DictType o = (DictType)obj;
                return GetTypeId() == o.GetTypeId() && keyType == o.keyType && valueType == o.valueType;
            }

            public int GetHashCode()
            {
                return Objects.Hash(GetTypeId(), keyType, valueType);
            }
        }

        public interface ISTEntry
        {
            bool IsLValue()
            {
                return false;
            }

            STValue GetValue();
            PuffinBasicType GetType();
        }

        public abstract class AbstractSTEntry : ISTEntry
        {
            private readonly PuffinBasicType type;
            private STValue value;
            public AbstractSTEntry(STValue value, PuffinBasicType type)
            {
                this.value = value;
                this.type = type;
            }

            public new PuffinBasicType GetType()
            {
                return type;
            }

            public void SetValue(STValue value)
            {
                this.value = value;
            }

            public STValue GetValue()
            {
                if (value == null)
                {
                    throw new PuffinBasicInternalError("Value is not set for type: " + GetType());
                }

                return value;
            }

            public void CreateAndSetInstance(PuffinBasicSymbolTable symbolTable)
            {
                SetValue(GetType().NewInstance(symbolTable));
            }
        }

        public class STLValue : AbstractSTEntry
        {
            public STLValue(STValue value, PuffinBasicType type) : base(value, type)
            {
            }

            public bool IsLValue()
            {
                return true;
            }
        }

        public class STVariable : STLValue
        {
            private readonly Variable variable;
            public STVariable(STValue value, Variable variable) : base(value, variable.GetType())
            {
                this.variable = variable;
            }

            public Variable GetVariable()
            {
                return variable;
            }
        }

        public class STRef : STLValue
        {
            private ISTEntry @ref;
            public STRef(PuffinBasicType type) : base(null, type)
            {
            }

            public void SetRef(ISTEntry @ref)
            {
                if (!@ref.GetType().Equals(GetType()))
                {
                    throw new PuffinBasicRuntimeError(DATA_TYPE_MISMATCH, "Expected " + GetType() + " got " + @ref.GetType());
                }

                this.@ref = @ref;
            }

            private ISTEntry GetRef()
            {
                if (@ref == null)
                {
                    throw new PuffinBasicInternalError("Ref is null");
                }

                return @ref;
            }

            public override STValue GetValue()
            {
                return GetRef().GetValue();
            }
        }

        public sealed class STTmp : AbstractSTEntry
        {
            public STTmp(STValue value, PuffinBasicType type) : base(value, type)
            {
            }
        }

        public sealed class STUDF : STVariable
        {
            private readonly List<int> paramIds;
            STUDF(STValue value, Variable variable) : base(value, variable)
            {
                this.paramIds = new List<int>();
            }

            public void DeclareParam(int paramId)
            {
                paramIds.Add(paramId);
            }

            public int GetNumDeclaredParams()
            {
                return paramIds.Count;
            }

            public int GetDeclaredParam(int i)
            {
                return paramIds.ElementAt(i);
            }
        }

        public sealed class STLabel : AbstractSTEntry
        {
            public STLabel() : base(new STInt32ScalarValue(), null)
            {
            }

            public override PuffinBasicType GetType()
            {
                throw new PuffinBasicInternalError("Labels don't have a type!");
            }
        }

        public interface ISTValue
        {
            public string PrintFormat();
            public string WriteFormat();
            public void Assign(ISTValue entry);
            public void Replace(ISTValue entry);

            public int GetInt32();
            public long GetInt64();
            public float GetFloat32();
            public double GetFloat64();
            public int GetRoundedInt32();
            public long GetRoundedInt64();
            public string GetString();
            public void SetInt32(int value);
            public void SetInt64(long value);
            public void SetFloat32(float value);
            public void SetFloat64(double value);
            public void SetString(string value);
            public int GetFieldLength();

            public void SetFieldLength(int fieldLength);

            public void SetArrayDimensions(List<int> dims);

            public List<int> GetArrayDimensions();

            public int GetTotalLength();

            public int GetNumArrayDimensions();

            public void SetArrayIndex(int dim, int index);

            public void ResetArrayIndex();

            public int GetArrayIndex1D();

            public void SetArrayReferenceIndex1D(int index1d);

            public int[] GetInt32Array1D();

            public void Fill(Number fill);

            public void FillString(string fill);

            public bool IsInitialized();

            public void CheckInitialized();

            public void SetInitialized();

            public void Call(string funcName, STValue[] @params, ISTValue result);

            public bool HasLen();

            public int Len();
        }

        public abstract class STValue : ISTValue
        {
            public void Replace(ISTValue entry)
            {
                Assign(entry);
            }

            public int GetFieldLength()
            {
                return 0;
            }

            public void SetFieldLength(int fieldLength)
            {
            }

            public void SetArrayDimensions(List<int> dims)
            {
            }

            public List<int> GetArrayDimensions()
            {
                return new List<int>();
            }

            public int GetTotalLength()
            {
                return 0;
            }

            public int GetNumArrayDimensions()
            {
                return 0;
            }

            public void SetArrayIndex(int dim, int index)
            {
            }

            public void ResetArrayIndex()
            {
            }

            public int GetArrayIndex1D()
            {
                return 0;
            }

            public void SetArrayReferenceIndex1D(int index1d)
            {
                throw new PuffinBasicInternalError("Unsupported");
            }

            public int[] GetInt32Array1D()
            {
                throw new PuffinBasicInternalError("Unsupported");
            }

            public void Fill(Number fill)
            {
                throw new PuffinBasicInternalError("Unsupported");
            }

            public void FillString(string fill)
            {
                throw new PuffinBasicInternalError("Unsupported");
            }

            public bool IsInitialized()
            {
                return true;
            }

            public void CheckInitialized()
            {
                if (!IsInitialized())
                {
                    throw new PuffinBasicRuntimeError(NOT_INITIALIZED, "Value cannot be read without initializing");
                }
            }

            public void SetInitialized()
            {
            }

            public void Call(string funcName, STValue[] @params, ISTValue result)
            {
                throw new PuffinBasicRuntimeError(BAD_FIELD, "Function call is not supported: " + funcName);
            }

            public bool HasLen()
            {
                return false;
            }

            public int Len()
            {
                throw new PuffinBasicInternalError("Not implemented");
            }

            public abstract string PrintFormat();
            public abstract string WriteFormat();
            public abstract void Assign(ISTValue entry);
            public abstract int GetInt32();
            public abstract long GetInt64();
            public abstract float GetFloat32();
            public abstract double GetFloat64();
            public abstract int GetRoundedInt32();
            public abstract long GetRoundedInt64();
            public abstract string GetString();
            public abstract void SetInt32(int value);
            public abstract void SetInt64(long value);
            public abstract void SetFloat32(float value);
            public abstract void SetFloat64(double value);
            public abstract void SetString(string value);
        }

        private sealed class STInt32ScalarValue : STValue
        {
            private bool isSet;
            private int value;
            public new bool IsInitialized()
            {
                return isSet;
            }

            public new void SetInitialized()
            {
                isSet = true;
            }

            public override string PrintFormat()
            {
                CheckInitialized();
                return Formatter.PrintFormatInt32(value);
            }

            public override string WriteFormat()
            {
                CheckInitialized();
                return Formatter.WriteFormatInt32(value);
            }

            public override void Assign(ISTValue entry)
            {
                SetInitialized();
                this.value = entry.GetInt32();
            }

            public override int GetInt32()
            {
                CheckInitialized();
                return value;
            }

            public override long GetInt64()
            {
                CheckInitialized();
                return value;
            }

            public override float GetFloat32()
            {
                CheckInitialized();
                return value;
            }

            public override double GetFloat64()
            {
                CheckInitialized();
                return value;
            }

            public override int GetRoundedInt32()
            {
                CheckInitialized();
                return value;
            }

            public override long GetRoundedInt64()
            {
                CheckInitialized();
                return value;
            }

            public override string GetString()
            {
                throw new PuffinBasicInternalError("Can't cast int32 to String");
            }

            public override void SetInt32(int value)
            {
                SetInitialized();
                this.value = value;
            }

            public override void SetInt64(long value)
            {
                this.isSet = true;
                this.value = (int)value;
            }

            public override void SetFloat32(float value)
            {
                this.isSet = true;
                this.value = (int)value;
            }

            public override void SetFloat64(double value)
            {
                this.isSet = true;
                this.value = (int)value;
            }

            public override void SetString(string value)
            {
                throw new PuffinBasicInternalError("Can't cast String to int32: '" + value + "'");
            }
        }

        private sealed class STInt64ScalarValue : STValue
        {
            private bool isSet;
            private long value;
            public new bool IsInitialized()
            {
                return isSet;
            }

            public new void SetInitialized()
            {
                isSet = true;
            }

            public override string PrintFormat()
            {
                CheckInitialized();
                return Formatter.PrintFormatInt64(value);
            }

            public override string WriteFormat()
            {
                CheckInitialized();
                return Formatter.WriteFormatInt64(value);
            }

            public override void Assign(ISTValue entry)
            {
                this.isSet = true;
                this.value = entry.GetInt64();
            }

            public override int GetInt32()
            {
                CheckInitialized();
                return (int)value;
            }

            public override long GetInt64()
            {
                CheckInitialized();
                return value;
            }

            public override float GetFloat32()
            {
                CheckInitialized();
                return value;
            }

            public override double GetFloat64()
            {
                CheckInitialized();
                return value;
            }

            public override int GetRoundedInt32()
            {
                CheckInitialized();
                return (int)value;
            }

            public override long GetRoundedInt64()
            {
                CheckInitialized();
                return value;
            }

            public override string GetString()
            {
                throw new PuffinBasicInternalError("Can't cast int64 to String");
            }

            public override void SetInt32(int value)
            {
                this.isSet = true;
                this.value = value;
            }

            public override void SetInt64(long value)
            {
                this.isSet = true;
                this.value = value;
            }

            public override void SetFloat32(float value)
            {
                this.isSet = true;
                this.value = (long)value;
            }

            public override void SetFloat64(double value)
            {
                this.isSet = true;
                this.value = (long)value;
            }

            public override void SetString(string value)
            {
                throw new PuffinBasicInternalError("Can't cast String to int64: '" + value + "'");
            }
        }

        private sealed class STFloat32ScalarValue : STValue
        {
            private bool isSet;
            private float value;
            public new bool IsInitialized()
            {
                return isSet;
            }

            public new void SetInitialized()
            {
                isSet = true;
            }

            public override string PrintFormat()
            {
                CheckInitialized();
                return Formatter.PrintFormatFloat32(value);
            }

            public override string WriteFormat()
            {
                CheckInitialized();
                return Formatter.WriteFormatFloat32(value);
            }

            public override void Assign(ISTValue entry)
            {
                this.isSet = true;
                this.value = entry.GetFloat32();
            }

            public override int GetInt32()
            {
                CheckInitialized();
                return (int)value;
            }

            public override long GetInt64()
            {
                CheckInitialized();
                return (long)value;
            }

            public override float GetFloat32()
            {
                CheckInitialized();
                return value;
            }

            public override double GetFloat64()
            {
                CheckInitialized();
                return value;
            }

            public override int GetRoundedInt32()
            {
                CheckInitialized();
                return (int)Math.Round(value);
            }

            public override long GetRoundedInt64()
            {
                CheckInitialized();
                return (long)Math.Round(value);
            }

            public override string GetString()
            {
                throw new PuffinBasicInternalError("Can't cast float32 to String");
            }

            public override void SetInt32(int value)
            {
                this.isSet = true;
                this.value = value;
            }

            public override void SetInt64(long value)
            {
                this.isSet = true;
                this.value = value;
            }

            public override void SetFloat32(float value)
            {
                this.isSet = true;
                this.value = value;
            }

            public override void SetFloat64(double value)
            {
                this.isSet = true;
                this.value = (float)value;
            }

            public override void SetString(string value)
            {
                throw new PuffinBasicInternalError("Can't cast String to float32: '" + value + "'");
            }
        }

        private sealed class STFloat64ScalarValue : STValue
        {
            private bool isSet;
            private double value;
            public new bool IsInitialized()
            {
                return isSet;
            }

            public new void SetInitialized()
            {
                isSet = true;
            }

            public override string PrintFormat()
            {
                CheckInitialized();
                return Formatter.PrintFormatFloat64(value);
            }

            public override string WriteFormat()
            {
                CheckInitialized();
                return Formatter.WriteFormatFloat64(value);
            }

            public override void Assign(ISTValue entry)
            {
                this.isSet = true;
                this.value = entry.GetFloat64();
            }

            public override int GetInt32()
            {
                CheckInitialized();
                return (int)value;
            }

            public override long GetInt64()
            {
                CheckInitialized();
                return (long)value;
            }

            public override float GetFloat32()
            {
                CheckInitialized();
                return (float)value;
            }

            public override double GetFloat64()
            {
                CheckInitialized();
                return value;
            }

            public override int GetRoundedInt32()
            {
                CheckInitialized();
                return (int)Math.Round(value);
            }

            public override long GetRoundedInt64()
            {
                CheckInitialized();
                return Math.Round(value);
            }

            public override string GetString()
            {
                throw new PuffinBasicInternalError("Can't cast float64 to String");
            }

            public override void SetInt32(int value)
            {
                this.isSet = true;
                this.value = value;
            }

            public override void SetInt64(long value)
            {
                this.isSet = true;
                this.value = value;
            }

            public override void SetFloat32(float value)
            {
                this.isSet = true;
                this.value = value;
            }

            public override void SetFloat64(double value)
            {
                this.isSet = true;
                this.value = value;
            }

            public override void SetString(string value)
            {
                throw new PuffinBasicInternalError("Can't cast String to float64: '" + value + "'");
            }
        }

        private sealed class STStringScalarValue : STValue
        {
            private bool isSet;
            private int fieldLength;
            private string value = "";
            public new bool IsInitialized()
            {
                return isSet;
            }

            public new void SetInitialized()
            {
                isSet = true;
            }

            public override string PrintFormat()
            {
                CheckInitialized();
                return Formatter.PrintFormatString(value);
            }

            public override string WriteFormat()
            {
                CheckInitialized();
                return Formatter.WriteFormatString(value);
            }

            public override void Assign(ISTValue entry)
            {
                this.isSet = true;
                this.value = entry.GetString();
            }

            public override int GetInt32()
            {
                throw new PuffinBasicInternalError("Can't cast String to int32");
            }

            public override long GetInt64()
            {
                throw new PuffinBasicInternalError("Can't cast String to int64");
            }

            public override float GetFloat32()
            {
                throw new PuffinBasicInternalError("Can't cast String to float32");
            }

            public override double GetFloat64()
            {
                throw new PuffinBasicInternalError("Can't cast String to float64");
            }

            public override int GetRoundedInt32()
            {
                throw new PuffinBasicInternalError("Can't cast String to int32");
            }

            public override long GetRoundedInt64()
            {
                throw new PuffinBasicInternalError("Can't cast String to int64");
            }

            public override string GetString()
            {
                CheckInitialized();
                return value;
            }

            public override void SetInt32(int value)
            {
                throw new PuffinBasicInternalError("Can't cast int32 to String");
            }

            public override void SetInt64(long value)
            {
                throw new PuffinBasicInternalError("Can't cast int64 to String");
            }

            public override void SetFloat32(float value)
            {
                throw new PuffinBasicInternalError("Can't cast float32 to String");
            }

            public override void SetFloat64(double value)
            {
                throw new PuffinBasicInternalError("Can't cast float64 to String");
            }

            public override void SetString(string value)
            {
                this.isSet = true;
                this.value = value;
            }

            public new int GetFieldLength()
            {
                return fieldLength;
            }

            public new void SetFieldLength(int fieldLength)
            {
                this.fieldLength = fieldLength;
            }

            public new bool HasLen()
            {
                return true;
            }

            public new int Len()
            {
                return GetString().Length;
            }
        }

        private sealed class STStringScalarTimeValue : STValue
        {
            private static readonly DateTimeFormatter FORMATTER = DateTimeFormatter.ISO_LOCAL_TIME;
            private LocalTime time;
            public override string PrintFormat()
            {
                return GetString();
            }

            public override string WriteFormat()
            {
                return GetString();
            }

            public override void Assign(ISTValue entry)
            {
                SetString(entry.GetString());
            }

            public override int GetInt32()
            {
                throw new PuffinBasicInternalError("Can't cast String to int32");
            }

            public override long GetInt64()
            {
                throw new PuffinBasicInternalError("Can't cast String to int64");
            }

            public override float GetFloat32()
            {
                throw new PuffinBasicInternalError("Can't cast String to float32");
            }

            public override double GetFloat64()
            {
                throw new PuffinBasicInternalError("Can't cast String to float64");
            }

            public override int GetRoundedInt32()
            {
                throw new PuffinBasicInternalError("Can't cast String to int32");
            }

            public override long GetRoundedInt64()
            {
                throw new PuffinBasicInternalError("Can't cast String to int64");
            }

            public override string GetString()
            {
                return FormatLocalTime(time != null ? time : LocalTime.Now());
            }

            private string FormatLocalTime(LocalTime time)
            {
                return time.Format(FORMATTER);
            }

            public override void SetInt32(int value)
            {
                throw new PuffinBasicInternalError("Can't cast int32 to String");
            }

            public override void SetInt64(long value)
            {
                throw new PuffinBasicInternalError("Can't cast int64 to String");
            }

            public override void SetFloat32(float value)
            {
                throw new PuffinBasicInternalError("Can't cast float32 to String");
            }

            public override void SetFloat64(double value)
            {
                throw new PuffinBasicInternalError("Can't cast float64 to String");
            }

            public override void SetString(string value)
            {
                this.time = LocalTime.Parse(value, FORMATTER);
            }

            public new int GetFieldLength()
            {
                return 0;
            }

            public new void SetFieldLength(int fieldLength)
            {
                throw new PuffinBasicRuntimeError(ILLEGAL_FUNCTION_PARAM, "TIME$ cannot be used for setting field length!");
            }
        }

        private sealed class STStringScalarDateValue : STValue
        {
            private static readonly DateTimeFormatter FORMATTER = DateTimeFormatter.ISO_LOCAL_DATE;
            private LocalDate date;
            public override string PrintFormat()
            {
                return GetString();
            }

            public override string WriteFormat()
            {
                return GetString();
            }

            public override void Assign(ISTValue entry)
            {
                SetString(entry.GetString());
            }

            public override int GetInt32()
            {
                throw new PuffinBasicInternalError("Can't cast String to int32");
            }

            public override long GetInt64()
            {
                throw new PuffinBasicInternalError("Can't cast String to int64");
            }

            public override float GetFloat32()
            {
                throw new PuffinBasicInternalError("Can't cast String to float32");
            }

            public override double GetFloat64()
            {
                throw new PuffinBasicInternalError("Can't cast String to float64");
            }

            public override int GetRoundedInt32()
            {
                throw new PuffinBasicInternalError("Can't cast String to int32");
            }

            public override long GetRoundedInt64()
            {
                throw new PuffinBasicInternalError("Can't cast String to int64");
            }

            public override string GetString()
            {
                return FormatLocalDate(date != null ? date : LocalDate.Now());
            }

            private string FormatLocalDate(LocalDate date)
            {
                return date.Format(FORMATTER);
            }

            public override void SetInt32(int value)
            {
                throw new PuffinBasicInternalError("Can't cast int32 to String");
            }

            public override void SetInt64(long value)
            {
                throw new PuffinBasicInternalError("Can't cast int64 to String");
            }

            public override void SetFloat32(float value)
            {
                throw new PuffinBasicInternalError("Can't cast float32 to String");
            }

            public override void SetFloat64(double value)
            {
                throw new PuffinBasicInternalError("Can't cast float64 to String");
            }

            public override void SetString(string value)
            {
                this.date = LocalDate.Parse(value, FORMATTER);
            }

            public new int GetFieldLength()
            {
                return 0;
            }

            public new void SetFieldLength(int fieldLength)
            {
                throw new PuffinBasicRuntimeError(ILLEGAL_FUNCTION_PARAM, "DATE$ cannot be used for setting field length!");
            }
        }

        public class ArrayReferenceValue :STValue
        {
            private readonly STLValue variable;
            private int index1d;
            public ArrayReferenceValue(STLValue variable)
            {
                this.variable = variable;
            }

            private AbstractSTArrayValue GetValue()
            {
                return (AbstractSTArrayValue)variable.GetValue();
            }

            public new void SetArrayReferenceIndex1D(int index1d)
            {
                this.index1d = index1d;
            }

            public override string PrintFormat()
            {
                var array = GetValue();
                array.SetArrayIndexID(index1d);
                return array.PrintFormat();
            }

            public override string WriteFormat()
            {
                var array = GetValue();
                array.SetArrayIndexID(index1d);
                return array.WriteFormat();
            }

            public override void Assign(ISTValue entry)
            {
                var array = GetValue();
                array.SetArrayIndexID(index1d);
                array.Assign(entry);
            }

            public override int GetInt32()
            {
                var array = GetValue();
                array.SetArrayIndexID(index1d);
                return array.GetInt32();
            }

            public override long GetInt64()
            {
                var array = GetValue();
                array.SetArrayIndexID(index1d);
                return array.GetInt64();
            }

            public override float GetFloat32()
            {
                var array = GetValue();
                array.SetArrayIndexID(index1d);
                return array.GetFloat32();
            }

            public override double GetFloat64()
            {
                var array = GetValue();
                array.SetArrayIndexID(index1d);
                return array.GetFloat64();
            }

            public override int GetRoundedInt32()
            {
                var array = GetValue();
                array.SetArrayIndexID(index1d);
                return array.GetRoundedInt32();
            }

            public override long GetRoundedInt64()
            {
                var array = GetValue();
                array.SetArrayIndexID(index1d);
                return array.GetRoundedInt64();
            }

            public override string GetString()
            {
                var array = GetValue();
                array.SetArrayIndexID(index1d);
                return array.GetString();
            }

            public override void SetInt32(int value)
            {
                var array = GetValue();
                array.SetArrayIndexID(index1d);
                array.SetInt32(value);
            }

            public override void SetInt64(long value)
            {
                var array = GetValue();
                array.SetArrayIndexID(index1d);
                array.SetInt64(value);
            }

            public override void SetFloat32(float value)
            {
                var array = GetValue();
                array.SetArrayIndexID(index1d);
                array.SetFloat32(value);
            }

            public override void SetFloat64(double value)
            {
                var array = GetValue();
                array.SetArrayIndexID(index1d);
                array.SetFloat64(value);
            }

            public override void SetString(string value)
            {
                var array = GetValue();
                array.SetArrayIndexID(index1d);
                array.SetString(value);
            }
        }

        public abstract class AbstractSTArrayValue : STValue
        {
            private List<int> dimensions;
            private int totalLength;
            private int index1d;
            private int ndim;
            public new void Replace(ISTValue entry)
            {
                var from = (AbstractSTArrayValue)entry;
                dimensions = from.dimensions;
                totalLength = from.totalLength;
                ndim = from.ndim;
            }

            public new int GetTotalLength()
            {
                return totalLength;
            }

            public new int GetNumArrayDimensions()
            {
                return ndim;
            }

            public new void SetArrayDimensions(List<int> dims)
            {
                this.dimensions = new List<int>(dims);
                this.ndim = dimensions.Count;
                int totalLen = 1;
                for (int i = 0; i < ndim; i++)
                {
                    totalLen *= dimensions.ElementAt(i);
                }

                totalLength = totalLen;
            }

            public new List<int> GetArrayDimensions()
            {
                return dimensions;
            }

            public new void ResetArrayIndex()
            {
                this.index1d = 0;
            }

            public new void SetArrayIndex(int dim, int index)
            {
                if (dim < 0 || dim >= dimensions.Count)
                {
                    throw new PuffinBasicRuntimeError(ARRAY_INDEX_OUT_OF_BOUNDS, "Dimension index " + dim + " is out of range, #dims=" + dimensions.Count);
                }

                if (index < 0 || index >= dimensions.ElementAt(dim))
                {
                    throw new PuffinBasicRuntimeError(ARRAY_INDEX_OUT_OF_BOUNDS, "Index " + index + " is out of range for dimension[" + dim + "]=" + dimensions.GetInt(dim));
                }

                int dIplus1 = dim + 1 < ndim ? dimensions.ElementAt(dim + 1) : 1;
                this.index1d = (this.index1d + index) * dIplus1;
            }

            public new int GetArrayIndex1D()
            {
                return index1d;
            }

            public void SetArrayIndexID(int index1d)
            {
                this.index1d = index1d;
            }
        }

        public sealed class STInt32ArrayValue : AbstractSTArrayValue
        {
            private int[] value;
            public new void Replace(ISTValue entry)
            {
                base.Replace(entry);
                var from = (STInt32ArrayValue)entry;
                value = from.value;
            }

            public new void Fill(Number fill)
            {
                Arrays.Fill(value, fill.IntValue());
            }

            public int[] GetValue()
            {
                return value;
            }

            public new int[] GetInt32Array1D()
            {
                return value;
            }

            public new void SetArrayDimensions(List<int> dims)
            {
                base.SetArrayDimensions(dims);
                this.value = new int[GetTotalLength()];
            }

            public override string PrintFormat()
            {
                return Formatter.PrintFormatInt32(value[GetArrayIndex1D()]);
            }

            public override string WriteFormat()
            {
                return Formatter.WriteFormatInt32(value[GetArrayIndex1D()]);
            }

            public override void Assign(ISTValue entry)
            {
                value[GetArrayIndex1D()] = entry.GetInt32();
            }

            public override int GetInt32()
            {
                return value[GetArrayIndex1D()];
            }

            public override long GetInt64()
            {
                return value[GetArrayIndex1D()];
            }

            public override float GetFloat32()
            {
                return value[GetArrayIndex1D()];
            }

            public override double GetFloat64()
            {
                return value[GetArrayIndex1D()];
            }

            public override int GetRoundedInt32()
            {
                return value[GetArrayIndex1D()];
            }

            public override long GetRoundedInt64()
            {
                return value[GetArrayIndex1D()];
            }

            public override string GetString()
            {
                throw new PuffinBasicInternalError("Can't cast int32 to String");
            }

            public override void SetInt32(int value)
            {
                this.value[GetArrayIndex1D()] = value;
            }

            public override void SetInt64(long value)
            {
                this.value[GetArrayIndex1D()] = (int)value;
            }

            public override void SetFloat32(float value)
            {
                this.value[GetArrayIndex1D()] = (int)value;
            }

            public override void SetFloat64(double value)
            {
                this.value[GetArrayIndex1D()] = (int)value;
            }

            public override void SetString(string value)
            {
                throw new PuffinBasicInternalError("Can't cast String to int32: '" + value + "'");
            }
        }

        public sealed class STInt64ArrayValue : AbstractSTArrayValue
        {
            private long[] value;
            public new void Fill(Number fill)
            {
                Arrays.Fill(value, fill.LongValue());
            }

            public long[] GetValue()
            {
                return value;
            }

            public new void SetArrayDimensions(List<int> dims)
            {
                base.SetArrayDimensions(dims);
                this.value = new long[GetTotalLength()];
            }

            public override string PrintFormat()
            {
                return Formatter.PrintFormatInt64(value[GetArrayIndex1D()]);
            }

            public override string WriteFormat()
            {
                return Formatter.WriteFormatInt64(value[GetArrayIndex1D()]);
            }

            public override void Assign(ISTValue entry)
            {
                value[GetArrayIndex1D()] = entry.GetInt64();
            }

            public override int GetInt32()
            {
                return (int)value[GetArrayIndex1D()];
            }

            public override long GetInt64()
            {
                return value[GetArrayIndex1D()];
            }

            public override float GetFloat32()
            {
                return value[GetArrayIndex1D()];
            }

            public override double GetFloat64()
            {
                return value[GetArrayIndex1D()];
            }

            public override int GetRoundedInt32()
            {
                return (int)value[GetArrayIndex1D()];
            }

            public override long GetRoundedInt64()
            {
                return value[GetArrayIndex1D()];
            }

            public override string GetString()
            {
                throw new PuffinBasicInternalError("Can't cast int32 to String");
            }

            public override void SetInt32(int value)
            {
                this.value[GetArrayIndex1D()] = value;
            }

            public override void SetInt64(long value)
            {
                this.value[GetArrayIndex1D()] = (int)value;
            }

            public override void SetFloat32(float value)
            {
                this.value[GetArrayIndex1D()] = (int)value;
            }

            public override void SetFloat64(double value)
            {
                this.value[GetArrayIndex1D()] = (int)value;
            }

            public override void SetString(string value)
            {
                throw new PuffinBasicInternalError("Can't cast String to int32: '" + value + "'");
            }
        }

        public sealed class STFloat32ArrayValue : AbstractSTArrayValue
        {
            private float[] value;
            public new void Fill(Number fill)
            {
                Arrays.Fill(value, fill.FloatValue());
            }

            public float[] GetValue()
            {
                return value;
            }

            public new void SetArrayDimensions(List<int> dims)
            {
                base.SetArrayDimensions(dims);
                this.value = new float[GetTotalLength()];
            }

            public override string PrintFormat()
            {
                return Formatter.PrintFormatFloat32(value[GetArrayIndex1D()]);
            }

            public override string WriteFormat()
            {
                return Formatter.WriteFormatFloat32(value[GetArrayIndex1D()]);
            }

            public override void Assign(ISTValue entry)
            {
                value[GetArrayIndex1D()] = entry.GetFloat32();
            }

            public override int GetInt32()
            {
                return (int)value[GetArrayIndex1D()];
            }

            public override long GetInt64()
            {
                return (long)value[GetArrayIndex1D()];
            }

            public override float GetFloat32()
            {
                return value[GetArrayIndex1D()];
            }

            public override double GetFloat64()
            {
                return value[GetArrayIndex1D()];
            }

            public override int GetRoundedInt32()
            {
                return (int)Math.Round(value[GetArrayIndex1D()]);
            }

            public override long GetRoundedInt64()
            {
                return (long)Math.Round(value[GetArrayIndex1D()]);
            }

            public override string GetString()
            {
                throw new PuffinBasicInternalError("Can't cast int32 to String");
            }

            public override void SetInt32(int value)
            {
                this.value[GetArrayIndex1D()] = value;
            }

            public override void SetInt64(long value)
            {
                this.value[GetArrayIndex1D()] = (int)value;
            }

            public override void SetFloat32(float value)
            {
                this.value[GetArrayIndex1D()] = (int)value;
            }

            public override void SetFloat64(double value)
            {
                this.value[GetArrayIndex1D()] = (int)value;
            }

            public override void SetString(string value)
            {
                throw new PuffinBasicInternalError("Can't cast String to int32: '" + value + "'");
            }
        }

        public sealed class STFloat64ArrayValue : AbstractSTArrayValue
        {
            private double[] value;
            public new void Fill(Number fill)
            {
                Arrays.Fill(value, fill.DoubleValue());
            }

            public double[] GetValue()
            {
                return value;
            }

            public new void SetArrayDimensions(List<int> dims)
            {
                base.SetArrayDimensions(dims);
                this.value = new double[GetTotalLength()];
            }

            public override string PrintFormat()
            {
                return Formatter.PrintFormatFloat64(value[GetArrayIndex1D()]);
            }

            public override string WriteFormat()
            {
                return Formatter.WriteFormatFloat64(value[GetArrayIndex1D()]);
            }

            public override void Assign(ISTValue entry)
            {
                value[GetArrayIndex1D()] = entry.GetFloat64();
            }

            public override int GetInt32()
            {
                return (int)value[GetArrayIndex1D()];
            }

            public override long GetInt64()
            {
                return (long)value[GetArrayIndex1D()];
            }

            public override float GetFloat32()
            {
                return (float)value[GetArrayIndex1D()];
            }

            public override double GetFloat64()
            {
                return value[GetArrayIndex1D()];
            }

            public override int GetRoundedInt32()
            {
                return (int)Math.Round(value[GetArrayIndex1D()]);
            }

            public override long GetRoundedInt64()
            {
                return (long)Math.Round(value[GetArrayIndex1D()]);
            }

            public override string GetString()
            {
                throw new PuffinBasicInternalError("Can't cast int32 to String");
            }

            public override void SetInt32(int value)
            {
                this.value[GetArrayIndex1D()] = value;
            }

            public override void SetInt64(long value)
            {
                this.value[GetArrayIndex1D()] = (int)value;
            }

            public override void SetFloat32(float value)
            {
                this.value[GetArrayIndex1D()] = (int)value;
            }

            public override void SetFloat64(double value)
            {
                this.value[GetArrayIndex1D()] = (int)value;
            }

            public override void SetString(string value)
            {
                throw new PuffinBasicInternalError("Can't cast String to int32: '" + value + "'");
            }
        }

        public sealed class STStringArrayValue : AbstractSTArrayValue
        {
            private String[] value;
            public new void FillString(string fill)
            {
                Arrays.Fill(value, fill);
            }

            public String[] GetValue()
            {
                return value;
            }

            public new void SetArrayDimensions(List<int> dims)
            {
                base.SetArrayDimensions(dims);
                this.value = new string[GetTotalLength()];
                Arrays.Fill(value, 0, value.length, "");
            }

            public override string PrintFormat()
            {
                return Formatter.PrintFormatString(value[GetArrayIndex1D()]);
            }

            public override string WriteFormat()
            {
                return Formatter.WriteFormatString(value[GetArrayIndex1D()]);
            }

            public override void Assign(ISTValue entry)
            {
                value[GetArrayIndex1D()] = entry.GetString();
            }

            public override int GetInt32()
            {
                throw new PuffinBasicInternalError("Can't cast String to int32");
            }

            public override long GetInt64()
            {
                throw new PuffinBasicInternalError("Can't cast String to int64");
            }

            public override float GetFloat32()
            {
                throw new PuffinBasicInternalError("Can't cast String to float32");
            }

            public override double GetFloat64()
            {
                throw new PuffinBasicInternalError("Can't cast String to float64");
            }

            public override int GetRoundedInt32()
            {
                throw new PuffinBasicInternalError("Can't cast String to int32");
            }

            public override long GetRoundedInt64()
            {
                throw new PuffinBasicInternalError("Can't cast String to int64");
            }

            public override string GetString()
            {
                return value[GetArrayIndex1D()];
            }

            public override void SetInt32(int value)
            {
                throw new PuffinBasicInternalError("Can't cast int32 to String");
            }

            public override void SetInt64(long value)
            {
                throw new PuffinBasicInternalError("Can't cast int64 to String");
            }

            public override void SetFloat32(float value)
            {
                throw new PuffinBasicInternalError("Can't cast float32 to String");
            }

            public override void SetFloat64(double value)
            {
                throw new PuffinBasicInternalError("Can't cast float64 to String");
            }

            public override void SetString(string value)
            {
                this.value[GetArrayIndex1D()] = value;
            }
        }

        public abstract class STCompositeValue : STValue
        {
            private readonly PuffinBasicTypeId type;
            private readonly PuffinBasicAtomTypeId atomType;
            public STCompositeValue(PuffinBasicTypeId type, PuffinBasicAtomTypeId atomType)
            {
                this.type = type;
                this.atomType = atomType;
            }

            public override string PrintFormat()
            {
                throw new PuffinBasicInternalError("Not implemented");
            }

            public override string WriteFormat()
            {
                throw new PuffinBasicInternalError("Not implemented");
            }

            public override void Assign(ISTValue entry)
            {
                throw new PuffinBasicInternalError("Not implemented");
            }

            public override int GetInt32()
            {
                throw new PuffinBasicInternalError("Not implemented");
            }

            public override long GetInt64()
            {
                throw new PuffinBasicInternalError("Not implemented");
            }

            public override float GetFloat32()
            {
                throw new PuffinBasicInternalError("Not implemented");
            }

            public override double GetFloat64()
            {
                throw new PuffinBasicInternalError("Not implemented");
            }

            public override int GetRoundedInt32()
            {
                throw new PuffinBasicInternalError("Not implemented");
            }

            public override long GetRoundedInt64()
            {
                throw new PuffinBasicInternalError("Not implemented");
            }

            public override string GetString()
            {
                throw new PuffinBasicInternalError("Not implemented");
            }

            public override void SetInt32(int value)
            {
                throw new PuffinBasicInternalError("Not implemented");
            }

            public override void SetInt64(long value)
            {
                throw new PuffinBasicInternalError("Not implemented");
            }

            public override void SetFloat32(float value)
            {
                throw new PuffinBasicInternalError("Not implemented");
            }

            public override void SetFloat64(double value)
            {
                throw new PuffinBasicInternalError("Not implemented");
            }

            public override void SetString(string value)
            {
                throw new PuffinBasicInternalError("Not implemented");
            }
        }

        sealed class STList : STCompositeValue
        {
            private readonly IList<object> list;
            private readonly MemberFunctions memberFunctions;
            public STList(PuffinBasicType type, MemberFunctions memberFunctions) : base(PuffinBasicTypeId.LIST, type.GetAtomTypeId())
            {
                this.memberFunctions = memberFunctions;
                this.list = new List<object>();
            }

            public void Call(string funcName, STValue[] @params, ISTValue result)
            {
                memberFunctions[funcName].callHandler.Call(list, @params, result);
            }

            public new bool HasLen()
            {
                return true;
            }

            public new int Len()
            {
                return list.Count;
            }
        }

        sealed class STSet : STCompositeValue
        {
            private readonly HashSet<object> set;
            private readonly MemberFunctions memberFunctions;
            public STSet(PuffinBasicType type, MemberFunctions memberFunctions) : base(PuffinBasicTypeId.SET, type.GetAtomTypeId())
            {
                this.memberFunctions = memberFunctions;
                this.set = new HashSet<object>();
            }

            public void Call(string funcName, STValue[] @params, ISTValue result)
            {
                memberFunctions[funcName].callHandler.Call(set, @params, result);
            }

            public new bool HasLen()
            {
                return true;
            }

            public new int Len()
            {
                return set.Count;
            }
        }

        sealed class STDict : STCompositeValue
        {
            private readonly Dictionary<object, object> dict;
            private readonly MemberFunctions memberFunctions;
            public STDict(PuffinBasicType valueType, MemberFunctions memberFunctions) : base(PuffinBasicTypeId.DICT, valueType.GetAtomTypeId())
            {
                this.memberFunctions = memberFunctions;
                this.dict = new Dictionary<object, object>();
            }

            public void Call(string funcName, STValue[] @params, ISTValue result)
            {
                memberFunctions[funcName].callHandler.Call(dict, @params, result);
            }

            public new bool HasLen()
            {
                return true;
            }

            public new int Len()
            {
                return dict.Count;
            }
        }

        public sealed class STStruct : STCompositeValue
        {
            private readonly StructType structType;
            private readonly Dictionary<int, int> memberRefIdToValueId;
            public STStruct(PuffinBasicSymbolTable symbolTable, StructType type) : base(PuffinBasicTypeId.STRUCT, PuffinBasicAtomTypeId.COMPOSITE)
            {
                this.structType = type;
                this.memberRefIdToValueId = new Dictionary<int, int>();
                foreach (var entry in structType.nameToRefIdMap)
                {
                    var memberRefId = entry.Value;
                    var valueType = structType.refIdToTypeMap[memberRefId];
                    var valueId = symbolTable.AddTmp(valueType, (e) => e.GetValue().SetInitialized());
                    this.memberRefIdToValueId.Add(memberRefId, valueId);
                }
            }

            public int GetMember(int memberRefId)
            {
                if (memberRefIdToValueId.TryGetValue(memberRefId, out int memberValueId))
                    return memberValueId;
                return NULL_ID;
            }

            public override void Assign(ISTValue entry)
            {
                if (!(entry is STStruct))
                {
                    throw new PuffinBasicRuntimeError(DATA_TYPE_MISMATCH, "Expected STStruct but found: " + entry.GetType());
                }

                STStruct other = (STStruct)entry;
                if (!structType.Equals(other.structType))
                {
                    throw new PuffinBasicRuntimeError(DATA_TYPE_MISMATCH, "Expected struct " + structType + ", but found " + other.structType);
                }

                this.memberRefIdToValueId.Clear();
                foreach (var kv in other.memberRefIdToValueId)  
                    this.memberRefIdToValueId[kv.Key] = kv.Value;
            }
        }
    }
}

