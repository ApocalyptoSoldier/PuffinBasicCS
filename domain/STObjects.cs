//using Com.Google.Common.Collect;

//using It.Unimi.Dsi.Fastutil.Ints;
//using It.Unimi.Dsi.Fastutil.Objects;

//using Java.Time;
//using Java.Time.Format;
//using Java.Util;

using Org.Puffinbasic.Error;
using Org.Puffinbasic.Runtime;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Configuration;
using System.Linq;
using System.Runtime.Remoting.Messaging;
using System.Security;
using System.Security.Cryptography;
using System.Text;

using static Org.Puffinbasic.Domain.PuffinBasicSymbolTable;
using static Org.Puffinbasic.Domain.STObjects;
using static Org.Puffinbasic.Domain.STObjects.PuffinBasicAtomTypeId;
using static Org.Puffinbasic.Domain.Variable;
using static Org.Puffinbasic.Domain.Variable;
using static Org.Puffinbasic.Error.PuffinBasicRuntimeError.ErrorCode;

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
            INT32,
            INT64,
            FLOAT,
            DOUBLE,
            STRING,
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

            public virtual bool CanBeLValue() => false;

            public virtual PuffinBasicType GetFuncCallReturnType(string funcName) => throw new PuffinBasicRuntimeError(BAD_FIELD, "Unsupported function: " + funcName + " in type: " + this);

            public virtual void CheckFuncCallArguments(string funcName, IList<PuffinBasicType> paramTypes)
            {
            }

            //public bool IsCompatibleWith(PuffinBasicType other) => this.Equals(other);
            public virtual bool IsCompatibleWith(PuffinBasicType other) => this.GetTypeId() == other.GetTypeId() && this.GetAtomTypeId() == other.GetAtomTypeId();

            public StructType AsStruct()
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

            public override PuffinBasicTypeId GetTypeId() => PuffinBasicTypeId.SCALAR;

            public override PuffinBasicAtomTypeId GetAtomTypeId() => atomType;

            public override STValue NewInstance(PuffinBasicSymbolTable symbolTable) => atomType.CreateValue();

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
                int hash = 17;
                hash = hash * 23 + GetTypeId().GetHashCode();
                hash = hash * 23 + GetAtomTypeId().GetHashCode();
                return hash;
            }

            public override bool IsCompatibleWith(PuffinBasicType other) => GetAtomTypeId().IsCompatibleWith(other.GetAtomTypeId());
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

            public void SetArrayDimensions(List<int> dims)
            {
                this.dims.Clear();
                this.dims.AddRange(dims);
            }

            public bool IsNDArray(int n) => dims != null && dims.Count == n;

            public override bool CanBeLValue() => canBeLValue;

            public override PuffinBasicTypeId GetTypeId() => PuffinBasicTypeId.ARRAY;

            public override PuffinBasicAtomTypeId GetAtomTypeId() => atomType;

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
                int hash = 17;
                hash = hash * 23 + GetTypeId().GetHashCode();
                hash = hash * 23 + GetAtomTypeId().GetHashCode();
                return hash;
            }

            public override bool IsCompatibleWith(PuffinBasicType other) => atomType.IsCompatibleWith(other.GetAtomTypeId());
        }

        public class UDFType : PuffinBasicType
        {
            private readonly PuffinBasicAtomTypeId atomType;
            public UDFType(PuffinBasicAtomTypeId atomType)
            {
                this.atomType = atomType;
            }

            public override PuffinBasicTypeId GetTypeId() => PuffinBasicTypeId.UDF;

            public override PuffinBasicAtomTypeId GetAtomTypeId() => atomType;

            public override STValue NewInstance(PuffinBasicSymbolTable symbolTable) => throw new PuffinBasicInternalError("Not implemented!");

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
                int hash = 17;
                hash = hash * 23 + GetTypeId().GetHashCode();
                hash = hash * 23 + GetAtomTypeId().GetHashCode();
                return hash;
            }

            public override bool IsCompatibleWith(PuffinBasicType other) => GetTypeId() == other.GetTypeId() && GetAtomTypeId().IsCompatibleWith(other.GetAtomTypeId());
        }

        public sealed class StructType : PuffinBasicType
        {
            private readonly string typeName;
            internal readonly Dictionary<int, PuffinBasicType> refIdToTypeMap = new Dictionary<int, PuffinBasicType>();
            //internal readonly Dictionary<VariableName, int> nameToRefIdMap;
            internal readonly Dictionary<string, int> nameToRefIdMap = new Dictionary<string, int>();
            private int counter;
            public StructType(string typeName)
            {
                this.typeName = typeName;
                //this.refIdToTypeMap = new Dictionary<int, PuffinBasicType>();
                //this.nameToRefIdMap = new Dictionary<VariableName, int>();
            }

            public string GetTypeName() => typeName;

            public PuffinBasicType GetMemberType(VariableName memberName) => refIdToTypeMap[GetMemberRefId(memberName)];

            public bool ContainsMember(VariableName memberName) => nameToRefIdMap.ContainsKey(memberName.ToString());

            public int GetMemberRefId(VariableName memberName)
            {
                if (!nameToRefIdMap.TryGetValue(memberName.ToString(), out int memberRefId))
                    throw new PuffinBasicRuntimeError(BAD_FIELD, "Missing field " + typeName + "." + memberName);

                return memberRefId;
            }

            public void DeclareField(VariableName memberName, PuffinBasicType type)
            {
                int refId = counter++;
                refIdToTypeMap.Add(refId, type);
                nameToRefIdMap.Add(memberName.ToString(), refId);
            }

            public override PuffinBasicTypeId GetTypeId() => PuffinBasicTypeId.STRUCT;

            public override PuffinBasicAtomTypeId GetAtomTypeId() => PuffinBasicAtomTypeId.COMPOSITE;

            public override STValue NewInstance(PuffinBasicSymbolTable symbolTable) => new STStruct(symbolTable, this);

            public override bool Equals(object obj)
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

            public override int GetHashCode()
            {
                int hash = 17;
                hash = hash * 23 + GetTypeId().GetHashCode();
                hash = hash * 23 + GetAtomTypeId().GetHashCode();
                return hash;
            }
        }

        internal delegate void MemberCallHandler(object o, STValue[] @params, STValue result);

        internal sealed class MemberFunction
        {
            internal readonly string functionName;
            internal readonly PuffinBasicType[] paramTypes;
            internal readonly PuffinBasicType returnType;
            internal readonly MemberCallHandler callHandler;
            public MemberFunction(string functionName, PuffinBasicType[] paramTypes, PuffinBasicType returnType, MemberCallHandler callHandler)
            {
                this.functionName = functionName;
                this.paramTypes = paramTypes;
                this.returnType = returnType;
                this.callHandler = callHandler;
            }
        }

        internal sealed class MemberFunctions
        {
            private readonly Dictionary<string, MemberFunction> memberFunctions;
            public MemberFunctions(IList<MemberFunction> memberFunctions)
            {
                this.memberFunctions = new Dictionary<string, MemberFunction>();
                foreach (MemberFunction mf in memberFunctions)
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
                    throw new PuffinBasicRuntimeError(BAD_FUNCTION_CALL, "Function " + funcName + " expects " + expectedParamTypes.Length + " params, but called with " + paramTypes.Count + " params");
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
                // ImmutableList
                List<MemberFunction> memberFunctionList = new List<MemberFunction>();

                memberFunctionList.Add(new MemberFunction("append", new PuffinBasicType[] { type }, ScalarType.INT32, (obj, @params, result) =>
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
                }));

                memberFunctionList.Add(new MemberFunction("insert", new PuffinBasicType[] { ScalarType.INT32, type }, ScalarType.INT32, (obj, @params, result) =>
                {
                    var list = (IList<object>)obj;
                    int index = @params[0].GetInt32();
                    if (type.GetTypeId() == PuffinBasicTypeId.SCALAR)
                    {
                        var value = type.GetAtomTypeId().GetValueFrom(@params[1]);
                        //list.Add(index, value);
                        list[index] = value;
                    }
                    else
                    {
                        //list.Add(index, @params[1]);
                        list[index] = @params[1];
                    }

                    result.SetInt32(0);
                }));

                memberFunctionList.Add(new MemberFunction("get", new PuffinBasicType[] { ScalarType.INT32 }, type, (obj, @params, result) =>
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
                }));

                memberFunctionList.Add(new MemberFunction("values", new PuffinBasicType[] { }, valuesType, (obj, @params, result) =>
                {
                    var list = (IList<object>)obj;
                    if (type.GetTypeId() == PuffinBasicTypeId.SCALAR)
                    {
                        type.GetAtomTypeId().CopyArray(list.ToList(), result);
                    }
                    else
                    {
                        throw new PuffinBasicRuntimeError(BAD_FUNCTION_CALL, "values() not supported for non-scalar type!");
                    }
                }));
                memberFunctionList.Add(new MemberFunction("clear", new PuffinBasicType[] { }, ScalarType.INT32, (obj, @params, result) =>
                {
                    var list = (IList<object>)obj;
                    list.Clear();
                    result.SetInt32(0);
                }));

                this.memberFunctions = new MemberFunctions(memberFunctionList);
            }

            public override PuffinBasicTypeId GetTypeId() => PuffinBasicTypeId.LIST;

            public override PuffinBasicAtomTypeId GetAtomTypeId() => COMPOSITE;

            public override STValue NewInstance(PuffinBasicSymbolTable symbolTable) => new STList(type, memberFunctions);

            public PuffinBasicType GetFuncCallReturnType(string funcName) => memberFunctions[funcName].returnType;

            public void CheckFuncCallArguments(string funcName, IList<PuffinBasicType> paramTypes) => memberFunctions.CheckFuncCallArguments(funcName, paramTypes);

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

            public override int GetHashCode()
            {
                int hash = 17;
                hash = hash * 23 + GetTypeId().GetHashCode();
                hash = hash * 23 + GetAtomTypeId().GetHashCode();
                return hash;
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
                List<MemberFunction> memberFunctionList = new List<MemberFunction>();

                memberFunctionList.Add(new MemberFunction("add", new PuffinBasicType[] { type }, ScalarType.INT32, (obj, @params, result) =>
                {
                    var set = (HashSet<object>)obj;
                    var value = type.GetAtomTypeId().GetValueFrom(@params[0]);
                    set.Add(value);
                    result.SetInt32(0);
                }));
                memberFunctionList.Add(new MemberFunction("remove", new PuffinBasicType[] { type }, ScalarType.INT32, (obj, @params, result) =>
                {
                    var set = (HashSet<object>)obj;
                    var value = type.GetAtomTypeId().GetValueFrom(@params[0]);
                    var removeRes = set.Remove(value);
                    result.SetInt32(removeRes ? -1 : 0);
                }));
                memberFunctionList.Add(new MemberFunction("contains", new PuffinBasicType[] { type }, ScalarType.INT32, (obj, @params, result) =>
                {
                    var set = (HashSet<object>)obj;
                    var value = type.GetAtomTypeId().GetValueFrom(@params[0]);
                    result.SetInt32(set.Contains(value) ? -1 : 0);
                }));
                memberFunctionList.Add(new MemberFunction("values", new PuffinBasicType[] { }, valuesType, (obj, @params, result) =>
                {
                    var set = (HashSet<object>)obj;
                    type.GetAtomTypeId().CopyArray(set.ToList(), result);
                }));
                memberFunctionList.Add(new MemberFunction("clear", new PuffinBasicType[] { }, ScalarType.INT32, (obj, @params, result) =>
                {
                    var set = (HashSet<object>)obj;
                    set.Clear();
                    result.SetInt32(0);
                }));

                this.memberFunctions = new MemberFunctions(memberFunctionList);
            }

            public override PuffinBasicTypeId GetTypeId() => PuffinBasicTypeId.SET;

            public override PuffinBasicAtomTypeId GetAtomTypeId() => COMPOSITE;

            public override STValue NewInstance(PuffinBasicSymbolTable symbolTable) => new STSet(type, memberFunctions);

            public PuffinBasicType GetFuncCallReturnType(string funcName) => memberFunctions[funcName].returnType;

            public void CheckFuncCallArguments(string funcName, IList<PuffinBasicType> paramTypes) => memberFunctions.CheckFuncCallArguments(funcName, paramTypes);

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

            public override int GetHashCode()
            {
                int hash = 17;
                hash = hash * 23 + GetTypeId().GetHashCode();
                hash = hash * 23 + GetAtomTypeId().GetHashCode();
                return hash;
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
                List<MemberFunction> memberFunctionList = new List<MemberFunction>();
                memberFunctionList.Add(new MemberFunction("put", new PuffinBasicType[] { keyType, valueType }, ScalarType.INT32, (obj, @params, result) =>
                {
                    var dict = (Dictionary<object, object>)obj;
                    var key = keyType.GetAtomTypeId().GetValueFrom(@params[0]);
                    var value = valueType.GetAtomTypeId().GetValueFrom(@params[1]);
                    dict.Add(key, value);
                    result.SetInt32(0);
                }));
                memberFunctionList.Add(new MemberFunction("removeKey", new PuffinBasicType[] { keyType }, ScalarType.INT32, (obj, @params, result) =>
                {
                    var dict = (Dictionary<object, object>)obj;
                    var key = keyType.GetAtomTypeId().GetValueFrom(@params[0]);
                    var removeRes = dict.Remove(key);
                    result.SetInt32(removeRes != null ? -1 : 0);
                }));
                memberFunctionList.Add(new MemberFunction("getOrDefault", new PuffinBasicType[] { keyType, valueType }, valueType, (obj, @params, result) =>
                {
                    var dict = (Dictionary<object, object>)obj;
                    var key = keyType.GetAtomTypeId().GetValueFrom(@params[0]);
                    var value = valueType.GetAtomTypeId().GetValueFrom(@params[1]);
                    if (!dict.TryGetValue(key, out var getRes))
                        getRes = value;
                    valueType.GetAtomTypeId().SetValueIn(getRes, result);
                }));
                memberFunctionList.Add(new MemberFunction("containsKey", new PuffinBasicType[] { keyType }, ScalarType.INT32, (obj, @params, result) =>
                {
                    var dict = (Dictionary<object, object>)obj;
                    var key = keyType.GetAtomTypeId().GetValueFrom(@params[0]);
                    result.SetInt32(dict.ContainsKey(key) ? -1 : 0);
                }));
                memberFunctionList.Add(new MemberFunction("keys", new PuffinBasicType[] { }, valuesType, (obj, @params, result) =>
                {
                    var dict = (Dictionary<object, object>)obj;
                    keyType.GetAtomTypeId().CopyArray(dict.Keys.ToList(), result);
                }));
                memberFunctionList.Add(new MemberFunction("clear", new PuffinBasicType[] { }, ScalarType.INT32, (obj, @params, result) =>
                {
                    var dict = (Dictionary<object, object>)obj;
                    dict.Clear();
                    result.SetInt32(0);
                }));

                this.memberFunctions = new MemberFunctions(memberFunctionList);
            }

            public override PuffinBasicTypeId GetTypeId() => PuffinBasicTypeId.DICT;

            public override PuffinBasicAtomTypeId GetAtomTypeId() => COMPOSITE;

            public override STValue NewInstance(PuffinBasicSymbolTable symbolTable) => new STDict(valueType, memberFunctions);

            public PuffinBasicType GetFuncCallReturnType(string funcName) => memberFunctions[funcName].returnType;

            public void CheckFuncCallArguments(string funcName, IList<PuffinBasicType> paramTypes) => memberFunctions.CheckFuncCallArguments(funcName, paramTypes);

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
                int hash = 17;
                hash = hash * 23 + GetTypeId().GetHashCode();
                hash = hash * 23 + keyType.GetHashCode();
                hash = hash * 23 + valueType.GetHashCode();
                return hash;
            }
        }

        public interface ISTEntry
        {
            bool IsLValue();
            STValue GetValue();
            void SetValue(STValue value);
            PuffinBasicType GetType();
            void CreateAndSetInstance(PuffinBasicSymbolTable symbolTable);
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

            public virtual PuffinBasicType GetType() => type; // TODO: check if I actually need this instead of using the property
            public virtual void SetValue(STValue value) => this.value = value;

            public virtual STValue GetValue()
            {
                if (value == null)
                {
                    throw new PuffinBasicInternalError("Value is not set for type: " + GetType());
                }

                return value;
            }

            public void CreateAndSetInstance(PuffinBasicSymbolTable symbolTable) => SetValue(GetType().NewInstance(symbolTable));

            public virtual bool IsLValue() => false;
        }

        public class STLValue : AbstractSTEntry
        {
            public STLValue(STValue value, PuffinBasicType type) : base(value, type)
            {
            }

            public override bool IsLValue() => true;
        }

        public interface ISTVariable : ISTEntry
        {
           public Variable GetVariable();
        }

        public class STVariable : STLValue, ISTVariable
        {
            private readonly Variable variable;
            public STVariable(STValue value, Variable variable) : base(value, variable.GetType())
            {
                this.variable = variable;
            }

            public Variable GetVariable() => variable;
        }

        public sealed class STUDF : STVariable
        {
            private readonly List<int> paramIds;
            public STUDF(STValue value, Variable variable) : base(value, variable)
            {
                this.paramIds = new List<int>();
            }

            public void DeclareParam(int paramId) => paramIds.Add(paramId);

            public int GetNumDeclaredParams() => paramIds.Count;

            public int GetDeclaredParam(int i) => paramIds.ElementAt(i);
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

            public override STValue GetValue() => GetRef().GetValue();
        }

        public sealed class STTmp : AbstractSTEntry
        {
            public STTmp(STValue value, PuffinBasicType type) : base(value, type)
            {
            }
        }

        public sealed class STLabel : AbstractSTEntry
        {
            public STLabel() : base(new STInt32ScalarValue(), null)
            {
            }

            public override PuffinBasicType GetType() => throw new PuffinBasicInternalError("Labels don't have a type!");
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

            public void Fill(int fill);
            public void Fill(long fill);
            public void Fill(float fill);
            public void Fill(double fill);

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
            public virtual void Replace(ISTValue entry) => Assign(entry);

            public virtual int GetFieldLength() => 0;

            public virtual void SetFieldLength(int fieldLength)
            {
            }

            public virtual void SetArrayDimensions(List<int> dims)
            {
            }

            public virtual List<int> GetArrayDimensions() => new List<int>();

            public virtual int GetTotalLength() => 0;

            public virtual int GetNumArrayDimensions() => 0;

            public virtual void SetArrayIndex(int dim, int index)
            {
            }

            public virtual void ResetArrayIndex()
            {
            }

            public virtual int GetArrayIndex1D() => 0;

            public virtual void SetArrayReferenceIndex1D(int index1d) => throw new PuffinBasicInternalError("Unsupported");

            public virtual int[] GetInt32Array1D() => throw new PuffinBasicInternalError("Unsupported");

            public virtual void Fill(int fill) => throw new PuffinBasicInternalError("Unsupported");
            public virtual void Fill(long fill) => throw new PuffinBasicInternalError("Unsupported");
            public virtual void Fill(float fill) => throw new PuffinBasicInternalError("Unsupported");
            public virtual void Fill(double fill) => throw new PuffinBasicInternalError("Unsupported");

            public virtual void FillString(string fill) => throw new PuffinBasicInternalError("Unsupported");

            public virtual bool IsInitialized() => true;

            public virtual void CheckInitialized()
            {
                if (!IsInitialized())
                {
                    throw new PuffinBasicRuntimeError(NOT_INITIALIZED, "Value cannot be read without initializing");
                }
            }

            public virtual void SetInitialized()
            {
            }

            public virtual void Call(string funcName, STValue[] @params, ISTValue result) => throw new PuffinBasicRuntimeError(BAD_FIELD, "Function call is not supported: " + funcName);

            public virtual bool HasLen() => false;

            public virtual int Len() => throw new PuffinBasicInternalError("Not implemented");

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

        internal sealed class STInt32ScalarValue : STValue
        {
            private bool isSet;
            private int value;
            public override bool IsInitialized() => isSet;

            public override void SetInitialized() => isSet = true;

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

            public override string GetString() => throw new PuffinBasicInternalError("Can't cast int32 to String");

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

            public override void SetString(string value) => throw new PuffinBasicInternalError("Can't cast String to int32: '" + value + "'");
        }

        internal sealed class STInt64ScalarValue : STValue
        {
            private bool isSet;
            private long value;
            public override bool IsInitialized() => isSet;

            public override void SetInitialized() => isSet = true;

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

            public override string GetString() => throw new PuffinBasicInternalError("Can't cast int64 to String");

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

            public override void SetString(string value) => throw new PuffinBasicInternalError("Can't cast String to int64: '" + value + "'");
        }

        internal sealed class STFloat32ScalarValue : STValue
        {
            private bool isSet;
            private float value;
            public override bool IsInitialized() => isSet;

            public override void SetInitialized() => isSet = true;

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

            public override string GetString() => throw new PuffinBasicInternalError("Can't cast float32 to String");

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

            public override void SetString(string value) => throw new PuffinBasicInternalError("Can't cast String to float32: '" + value + "'");
        }

        internal sealed class STFloat64ScalarValue : STValue
        {
            private bool isSet;
            private double value;
            public override bool IsInitialized() => isSet;

            public override void SetInitialized() => isSet = true;

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
                return (long)Math.Round(value);
            }

            public override string GetString() => throw new PuffinBasicInternalError("Can't cast float64 to String");

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

            public override void SetString(string value) => throw new PuffinBasicInternalError("Can't cast String to float64: '" + value + "'");
        }

        internal sealed class STStringScalarValue : STValue
        {
            private bool isSet;
            private int fieldLength;
            private string value = "";
            public override bool IsInitialized() => isSet;

            public override void SetInitialized() => isSet = true;

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

            public override int GetInt32() => throw new PuffinBasicInternalError("Can't cast String to int32");

            public override long GetInt64() => throw new PuffinBasicInternalError("Can't cast String to int64");

            public override float GetFloat32() => throw new PuffinBasicInternalError("Can't cast String to float32");

            public override double GetFloat64() => throw new PuffinBasicInternalError("Can't cast String to float64");

            public override int GetRoundedInt32() => throw new PuffinBasicInternalError("Can't cast String to int32");

            public override long GetRoundedInt64() => throw new PuffinBasicInternalError("Can't cast String to int64");

            public override string GetString()
            {
                CheckInitialized();
                return value;
            }

            public override void SetInt32(int value) => throw new PuffinBasicInternalError("Can't cast int32 to String");

            public override void SetInt64(long value) => throw new PuffinBasicInternalError("Can't cast int64 to String");

            public override void SetFloat32(float value) => throw new PuffinBasicInternalError("Can't cast float32 to String");

            public override void SetFloat64(double value) => throw new PuffinBasicInternalError("Can't cast float64 to String");

            public override void SetString(string value)
            {
                this.isSet = true;
                this.value = value;
            }

            public override int GetFieldLength() => fieldLength;

            public override void SetFieldLength(int fieldLength) => this.fieldLength = fieldLength;

            public override bool HasLen() => true;

            public override int Len() => GetString().Length;
        }

        internal sealed class STStringScalarTimeValue : STValue
        {
            private static readonly string FORMAT = @"hh:mm:ss";
            private TimeSpan time;
            public override string PrintFormat() => GetString();

            public override string WriteFormat() => GetString();

            public override void Assign(ISTValue entry) => SetString(entry.GetString());

            public override int GetInt32() => throw new PuffinBasicInternalError("Can't cast String to int32");

            public override long GetInt64() => throw new PuffinBasicInternalError("Can't cast String to int64");

            public override float GetFloat32() => throw new PuffinBasicInternalError("Can't cast String to float32");

            public override double GetFloat64() => throw new PuffinBasicInternalError("Can't cast String to float64");

            public override int GetRoundedInt32() => throw new PuffinBasicInternalError("Can't cast String to int32");

            public override long GetRoundedInt64() => throw new PuffinBasicInternalError("Can't cast String to int64");

            public override string GetString() => FormatLocalTime(time != null ? time : DateTime.Now.TimeOfDay);

            private string FormatLocalTime(TimeSpan time) => time.ToString(FORMAT);

            public override void SetInt32(int value) => throw new PuffinBasicInternalError("Can't cast int32 to String");

            public override void SetInt64(long value) => throw new PuffinBasicInternalError("Can't cast int64 to String");

            public override void SetFloat32(float value) => throw new PuffinBasicInternalError("Can't cast float32 to String");

            public override void SetFloat64(double value) => throw new PuffinBasicInternalError("Can't cast float64 to String");

            public override void SetString(string value) => this.time = TimeSpan.ParseExact(value, FORMAT, null);

            public override int GetFieldLength() => 0;

            public override void SetFieldLength(int fieldLength) => throw new PuffinBasicRuntimeError(ILLEGAL_FUNCTION_PARAM, "TIME$ cannot be used for setting field length!");
        }

        internal sealed class STStringScalarDateValue : STValue
        {
            private static readonly string FORMAT = "yyyy-mm-dd";
            private DateTime date;
            public override string PrintFormat() => GetString();

            public override string WriteFormat() => GetString();

            public override void Assign(ISTValue entry) => SetString(entry.GetString());

            public override int GetInt32() => throw new PuffinBasicInternalError("Can't cast String to int32");

            public override long GetInt64() => throw new PuffinBasicInternalError("Can't cast String to int64");

            public override float GetFloat32() => throw new PuffinBasicInternalError("Can't cast String to float32");

            public override double GetFloat64() => throw new PuffinBasicInternalError("Can't cast String to float64");

            public override int GetRoundedInt32() => throw new PuffinBasicInternalError("Can't cast String to int32");

            public override long GetRoundedInt64() => throw new PuffinBasicInternalError("Can't cast String to int64");

            public override string GetString() => FormatLocalDate(date != null ? date : DateTime.Now.Date);

            private string FormatLocalDate(DateTime date) => date.ToString(FORMAT);

            public override void SetInt32(int value) => throw new PuffinBasicInternalError("Can't cast int32 to String");

            public override void SetInt64(long value) => throw new PuffinBasicInternalError("Can't cast int64 to String");

            public override void SetFloat32(float value) => throw new PuffinBasicInternalError("Can't cast float32 to String");

            public override void SetFloat64(double value) => throw new PuffinBasicInternalError("Can't cast float64 to String");

            public override void SetString(string value) => this.date = DateTime.ParseExact(value, FORMAT, null);

            public override int GetFieldLength() => 0;

            public override void SetFieldLength(int fieldLength) => throw new PuffinBasicRuntimeError(ILLEGAL_FUNCTION_PARAM, "DATE$ cannot be used for setting field length!");
        }

        public class ArrayReferenceValue : STValue
        {
            private readonly STLValue variable;
            private int index1d;
            public ArrayReferenceValue(STLValue variable)
            {
                this.variable = variable;
            }

            private AbstractSTArrayValue GetValue() => (AbstractSTArrayValue)variable.GetValue();

            public override void SetArrayReferenceIndex1D(int index1d) => this.index1d = index1d;

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
            public override void Replace(ISTValue entry)
            {
                var from = (AbstractSTArrayValue)entry;
                dimensions = from.dimensions;
                totalLength = from.totalLength;
                ndim = from.ndim;
            }

            public override int GetTotalLength() => totalLength;

            public override int GetNumArrayDimensions() => ndim;

            public override void SetArrayDimensions(List<int> dims)
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

            public override List<int> GetArrayDimensions() => dimensions;

            public override void ResetArrayIndex() => this.index1d = 0;

            public override void SetArrayIndex(int dim, int index)
            {
                if (dim < 0 || dim >= dimensions.Count)
                {
                    throw new PuffinBasicRuntimeError(ARRAY_INDEX_OUT_OF_BOUNDS, "Dimension index " + dim + " is out of range, #dims=" + dimensions.Count);
                }

                if (index < 0 || index >= dimensions.ElementAt(dim))
                {
                    throw new PuffinBasicRuntimeError(ARRAY_INDEX_OUT_OF_BOUNDS, "Index " + index + " is out of range for dimension[" + dim + "]=" + dimensions.ElementAt(dim));
                }

                int dIplus1 = dim + 1 < ndim ? dimensions.ElementAt(dim + 1) : 1;
                this.index1d = (this.index1d + index) * dIplus1;
            }

            public override int GetArrayIndex1D() => index1d;

            public void SetArrayIndexID(int index1d) => this.index1d = index1d;
        }

        public sealed class STInt32ArrayValue : AbstractSTArrayValue
        {
            private int[] value;
            public override void Replace(ISTValue entry)
            {
                base.Replace(entry);
                var from = (STInt32ArrayValue)entry;
                value = from.value;
            }

            public override void Fill(int fill) => Arrays.Fill(value, (int)fill);
            public override void Fill(long fill) => Arrays.Fill(value, (int)fill);
            public override void Fill(float fill) => Arrays.Fill(value, (int)fill);
            public override void Fill(double fill) => Arrays.Fill(value, (int)fill);

            public int[] GetValue() => value;

            public override int[] GetInt32Array1D() => value;

            public override void SetArrayDimensions(List<int> dims)
            {
                base.SetArrayDimensions(dims);
                this.value = new int[GetTotalLength()];
            }

            public override string PrintFormat() => Formatter.PrintFormatInt32(value[GetArrayIndex1D()]);

            public override string WriteFormat() => Formatter.WriteFormatInt32(value[GetArrayIndex1D()]);

            public override void Assign(ISTValue entry) => value[GetArrayIndex1D()] = entry.GetInt32();

            public override int GetInt32() => value[GetArrayIndex1D()];

            public override long GetInt64() => value[GetArrayIndex1D()];

            public override float GetFloat32() => value[GetArrayIndex1D()];

            public override double GetFloat64() => value[GetArrayIndex1D()];

            public override int GetRoundedInt32() => value[GetArrayIndex1D()];

            public override long GetRoundedInt64() => value[GetArrayIndex1D()];

            public override string GetString() => throw new PuffinBasicInternalError("Can't cast int32 to String");

            public override void SetInt32(int value) => this.value[GetArrayIndex1D()] = value;

            public override void SetInt64(long value) => this.value[GetArrayIndex1D()] = (int)value;

            public override void SetFloat32(float value) => this.value[GetArrayIndex1D()] = (int)value;

            public override void SetFloat64(double value) => this.value[GetArrayIndex1D()] = (int)value;

            public override void SetString(string value) => throw new PuffinBasicInternalError("Can't cast String to int32: '" + value + "'");
        }

        public sealed class STInt64ArrayValue : AbstractSTArrayValue
        {
            private long[] value;
            public override void Fill(int fill) => Arrays.Fill(value, (long)fill);
            public override void Fill(long fill) => Arrays.Fill(value, (long)fill);
            public override void Fill(float fill) => Arrays.Fill(value, (long)fill);
            public override void Fill(double fill) => Arrays.Fill(value, (long)fill);

            public long[] GetValue() => value;

            public override void SetArrayDimensions(List<int> dims)
            {
                base.SetArrayDimensions(dims);
                this.value = new long[GetTotalLength()];
            }

            public override string PrintFormat() => Formatter.PrintFormatInt64(value[GetArrayIndex1D()]);

            public override string WriteFormat() => Formatter.WriteFormatInt64(value[GetArrayIndex1D()]);

            public override void Assign(ISTValue entry) => value[GetArrayIndex1D()] = entry.GetInt64();

            public override int GetInt32() => (int)value[GetArrayIndex1D()];

            public override long GetInt64() => value[GetArrayIndex1D()];

            public override float GetFloat32() => value[GetArrayIndex1D()];

            public override double GetFloat64() => value[GetArrayIndex1D()];

            public override int GetRoundedInt32() => (int)value[GetArrayIndex1D()];

            public override long GetRoundedInt64() => value[GetArrayIndex1D()];

            public override string GetString() => throw new PuffinBasicInternalError("Can't cast int32 to String");

            public override void SetInt32(int value) => this.value[GetArrayIndex1D()] = value;

            public override void SetInt64(long value) => this.value[GetArrayIndex1D()] = (int)value;

            public override void SetFloat32(float value) => this.value[GetArrayIndex1D()] = (int)value;

            public override void SetFloat64(double value) => this.value[GetArrayIndex1D()] = (int)value;

            public override void SetString(string value) => throw new PuffinBasicInternalError("Can't cast String to int32: '" + value + "'");
        }

        public sealed class STFloat32ArrayValue : AbstractSTArrayValue
        {
            private float[] value;
            public override void Fill(int fill) => Arrays.Fill(value, (float)fill);
            public override void Fill(long fill) => Arrays.Fill(value, (float)fill);
            public override void Fill(float fill) => Arrays.Fill(value, (float)fill);
            public override void Fill(double fill) => Arrays.Fill(value, (float)fill);

            public float[] GetValue() => value;

            public override void SetArrayDimensions(List<int> dims)
            {
                base.SetArrayDimensions(dims);
                this.value = new float[GetTotalLength()];
            }

            public override string PrintFormat() => Formatter.PrintFormatFloat32(value[GetArrayIndex1D()]);

            public override string WriteFormat() => Formatter.WriteFormatFloat32(value[GetArrayIndex1D()]);

            public override void Assign(ISTValue entry) => value[GetArrayIndex1D()] = entry.GetFloat32();

            public override int GetInt32() => (int)value[GetArrayIndex1D()];

            public override long GetInt64() => (long)value[GetArrayIndex1D()];

            public override float GetFloat32() => value[GetArrayIndex1D()];

            public override double GetFloat64() => value[GetArrayIndex1D()];

            public override int GetRoundedInt32() => (int)Math.Round(value[GetArrayIndex1D()]);

            public override long GetRoundedInt64() => (long)Math.Round(value[GetArrayIndex1D()]);

            public override string GetString() => throw new PuffinBasicInternalError("Can't cast int32 to String");

            public override void SetInt32(int value) => this.value[GetArrayIndex1D()] = value;

            public override void SetInt64(long value) => this.value[GetArrayIndex1D()] = (int)value;

            public override void SetFloat32(float value) => this.value[GetArrayIndex1D()] = (int)value;

            public override void SetFloat64(double value) => this.value[GetArrayIndex1D()] = (int)value;

            public override void SetString(string value) => throw new PuffinBasicInternalError("Can't cast String to int32: '" + value + "'");
        }

        public sealed class STFloat64ArrayValue : AbstractSTArrayValue
        {
            private double[] value;
            public override void Fill(int fill) => Arrays.Fill(value, (double)fill);
            public override void Fill(long fill) => Arrays.Fill(value, (double)fill);
            public override void Fill(float fill) => Arrays.Fill(value, (double)fill);
            public override void Fill(double fill) => Arrays.Fill(value, (double)fill);

            public double[] GetValue() => value;

            public override void SetArrayDimensions(List<int> dims)
            {
                base.SetArrayDimensions(dims);
                this.value = new double[GetTotalLength()];
            }

            public override string PrintFormat() => Formatter.PrintFormatFloat64(value[GetArrayIndex1D()]);

            public override string WriteFormat() => Formatter.WriteFormatFloat64(value[GetArrayIndex1D()]);

            public override void Assign(ISTValue entry) => value[GetArrayIndex1D()] = entry.GetFloat64();

            public override int GetInt32() => (int)value[GetArrayIndex1D()];

            public override long GetInt64() => (long)value[GetArrayIndex1D()];

            public override float GetFloat32() => (float)value[GetArrayIndex1D()];

            public override double GetFloat64() => value[GetArrayIndex1D()];

            public override int GetRoundedInt32() => (int)Math.Round(value[GetArrayIndex1D()]);

            public override long GetRoundedInt64() => (long)Math.Round(value[GetArrayIndex1D()]);

            public override string GetString() => throw new PuffinBasicInternalError("Can't cast int32 to String");

            public override void SetInt32(int value) => this.value[GetArrayIndex1D()] = value;

            public override void SetInt64(long value) => this.value[GetArrayIndex1D()] = (int)value;

            public override void SetFloat32(float value) => this.value[GetArrayIndex1D()] = (int)value;

            public override void SetFloat64(double value) => this.value[GetArrayIndex1D()] = (int)value;

            public override void SetString(string value) => throw new PuffinBasicInternalError("Can't cast String to int32: '" + value + "'");
        }

        public sealed class STStringArrayValue : AbstractSTArrayValue
        {
            private String[] value;
            public override void FillString(string fill) => Arrays.Fill(value, fill);

            public String[] GetValue() => value;

            public override void SetArrayDimensions(List<int> dims)
            {
                base.SetArrayDimensions(dims);
                this.value = new string[GetTotalLength()];
                Arrays.Fill(value, "", 0, value.Length);
            }

            public override string PrintFormat() => Formatter.PrintFormatString(value[GetArrayIndex1D()]);

            public override string WriteFormat() => Formatter.WriteFormatString(value[GetArrayIndex1D()]);

            public override void Assign(ISTValue entry) => value[GetArrayIndex1D()] = entry.GetString();

            public override int GetInt32() => throw new PuffinBasicInternalError("Can't cast String to int32");

            public override long GetInt64() => throw new PuffinBasicInternalError("Can't cast String to int64");

            public override float GetFloat32() => throw new PuffinBasicInternalError("Can't cast String to float32");

            public override double GetFloat64() => throw new PuffinBasicInternalError("Can't cast String to float64");

            public override int GetRoundedInt32() => throw new PuffinBasicInternalError("Can't cast String to int32");

            public override long GetRoundedInt64() => throw new PuffinBasicInternalError("Can't cast String to int64");

            public override string GetString() => value[GetArrayIndex1D()];

            public override void SetInt32(int value) => throw new PuffinBasicInternalError("Can't cast int32 to String");

            public override void SetInt64(long value) => throw new PuffinBasicInternalError("Can't cast int64 to String");

            public override void SetFloat32(float value) => throw new PuffinBasicInternalError("Can't cast float32 to String");

            public override void SetFloat64(double value) => throw new PuffinBasicInternalError("Can't cast float64 to String");

            public override void SetString(string value) => this.value[GetArrayIndex1D()] = value;
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

            public override string PrintFormat() => throw new PuffinBasicInternalError("Not implemented");

            public override string WriteFormat() => throw new PuffinBasicInternalError("Not implemented");

            public override void Assign(ISTValue entry) => throw new PuffinBasicInternalError("Not implemented");

            public override int GetInt32() => throw new PuffinBasicInternalError("Not implemented");

            public override long GetInt64() => throw new PuffinBasicInternalError("Not implemented");

            public override float GetFloat32() => throw new PuffinBasicInternalError("Not implemented");

            public override double GetFloat64() => throw new PuffinBasicInternalError("Not implemented");

            public override int GetRoundedInt32() => throw new PuffinBasicInternalError("Not implemented");

            public override long GetRoundedInt64() => throw new PuffinBasicInternalError("Not implemented");

            public override string GetString() => throw new PuffinBasicInternalError("Not implemented");

            public override void SetInt32(int value) => throw new PuffinBasicInternalError("Not implemented");

            public override void SetInt64(long value) => throw new PuffinBasicInternalError("Not implemented");

            public override void SetFloat32(float value) => throw new PuffinBasicInternalError("Not implemented");

            public override void SetFloat64(double value) => throw new PuffinBasicInternalError("Not implemented");

            public override void SetString(string value) => throw new PuffinBasicInternalError("Not implemented");
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

            public void Call(string funcName, STValue[] @params, STValue result) => memberFunctions[funcName].callHandler.Invoke(list, @params, result);

            public override bool HasLen() => true;

            public override int Len() => list.Count;
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

            public void Call(string funcName, STValue[] @params, STValue result) => memberFunctions[funcName].callHandler.Invoke(set, @params, result);

            public override bool HasLen() => true;

            public override int Len() => set.Count;
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

            public void Call(string funcName, STValue[] @params, STValue result) => memberFunctions[funcName].callHandler.Invoke(dict, @params, result);

            public override bool HasLen() => true;

            public override int Len() => dict.Count;
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

    public static class PuffinBasicAtomTypeIdExtensions
    {
        public static ISTVariable CreateVariableEntry(this PuffinBasicAtomTypeId atomTypeId, Variable variable)
        {
            switch (atomTypeId)
            {
                case PuffinBasicAtomTypeId.INT32:
                    if (variable.IsArray())
                        return new STVariable(new STInt32ArrayValue(), variable);
                    else if (variable.IsUDF())
                        return new STUDF(new STInt32ScalarValue(), variable);
                    else if (variable.IsScalar())
                        return new STVariable(new STInt32ScalarValue(), variable);
                    else
                        throw new PuffinBasicInternalError("Variable type not supported: " + variable);

                case PuffinBasicAtomTypeId.INT64:
                    if (variable.IsArray())
                        return new STVariable(new STInt64ArrayValue(), variable);
                    else if (variable.IsUDF())
                        return new STUDF(new STInt64ScalarValue(), variable);
                    else if (variable.IsScalar())
                        return new STVariable(new STInt64ScalarValue(), variable);
                    else
                        throw new PuffinBasicInternalError("Variable type not supported: " + variable);

                case PuffinBasicAtomTypeId.FLOAT:
                    if (variable.IsArray())
                        return new STVariable(new STFloat32ArrayValue(), variable);
                    else if (variable.IsUDF())
                        return new STUDF(new STFloat32ScalarValue(), variable);
                    else if (variable.IsScalar())
                        return new STVariable(new STFloat32ScalarValue(), variable);
                    else
                        throw new PuffinBasicInternalError("Variable type not supported: " + variable);

                case PuffinBasicAtomTypeId.DOUBLE:
                    if (variable.IsArray())
                        return new STVariable(new STFloat64ArrayValue(), variable);
                    else if (variable.IsUDF())
                        return new STUDF(new STFloat64ScalarValue(), variable);
                    else if (variable.IsScalar())
                        return new STVariable(new STFloat64ScalarValue(), variable);
                    else
                        throw new PuffinBasicInternalError("Variable type not supported: " + variable);

                case PuffinBasicAtomTypeId.STRING:
                    if (variable.IsArray())
                        return new STVariable(new STStringArrayValue(), variable);
                    else if (variable.IsUDF())
                        return new STUDF(new STStringScalarValue(), variable);
                    else if (variable.IsScalar())
                    {
                        string varname = variable.GetVariableName().GetVarname();
                        if (varname.Equals("date", StringComparison.OrdinalIgnoreCase))
                            return new STVariable(new STStringScalarDateValue(), variable);
                        else if (varname.Equals("time", StringComparison.OrdinalIgnoreCase))
                            return new STVariable(new STStringScalarTimeValue(), variable);
                        else
                            return new STVariable(new STStringScalarValue(), variable);
                    }
                    else
                        throw new PuffinBasicInternalError("Variable type not supported: " + variable);

                case PuffinBasicAtomTypeId.COMPOSITE:
                    return new STVariable(null, variable);

                default:
                    return null;
            }
        }

        public static STTmp CreateTmpEntry(this PuffinBasicAtomTypeId atomTypeId)
        {
            switch (atomTypeId)
            {
                case PuffinBasicAtomTypeId.INT32:
                    return new STTmp(new STInt32ScalarValue(), ScalarType.INT32);
                case PuffinBasicAtomTypeId.INT64:
                    return new STTmp(new STInt64ScalarValue(), ScalarType.INT64);
                case PuffinBasicAtomTypeId.FLOAT:
                    return new STTmp(new STFloat32ScalarValue(), ScalarType.FLOAT32);
                case PuffinBasicAtomTypeId.DOUBLE:
                    return new STTmp(new STFloat64ScalarValue(), ScalarType.FLOAT64);
                case PuffinBasicAtomTypeId.STRING:
                    return new STTmp(new STStringScalarValue(), ScalarType.STRING);
                case PuffinBasicAtomTypeId.COMPOSITE:
                default:
                    throw new PuffinBasicInternalError("Not implemented");
            }
        }

        public static STTmp CreateArrayEntry(this PuffinBasicAtomTypeId atomTypeId)
        {
            switch (atomTypeId)
            {
                case PuffinBasicAtomTypeId.INT32:
                    return new STTmp(new STInt32ArrayValue(), ScalarType.INT32);
                case PuffinBasicAtomTypeId.INT64:
                    return new STTmp(new STInt64ArrayValue(), ScalarType.INT64);
                case PuffinBasicAtomTypeId.FLOAT:
                    return new STTmp(new STFloat32ArrayValue(), ScalarType.FLOAT32);
                case PuffinBasicAtomTypeId.DOUBLE:
                    return new STTmp(new STFloat64ArrayValue(), ScalarType.FLOAT64);
                case PuffinBasicAtomTypeId.STRING:
                    return new STTmp(new STStringArrayValue(), ScalarType.STRING);
                case PuffinBasicAtomTypeId.COMPOSITE:
                default:
                    throw new PuffinBasicInternalError("Not implemented");
            }
        }

        public static STValue CreateValue(this PuffinBasicAtomTypeId atomTypeId)
        {
            switch (atomTypeId)
            {
                case PuffinBasicAtomTypeId.INT32:
                    return new STInt32ScalarValue();
                case PuffinBasicAtomTypeId.INT64:
                    return new STInt64ScalarValue();
                case PuffinBasicAtomTypeId.FLOAT:
                    return new STFloat32ScalarValue();
                case PuffinBasicAtomTypeId.DOUBLE:
                    return new STFloat64ScalarValue();
                case PuffinBasicAtomTypeId.STRING:
                    return new STStringScalarValue();
                case PuffinBasicAtomTypeId.COMPOSITE:
                default:
                    throw new PuffinBasicInternalError("Not implemented");
            }
        }

        public static object GetValueFrom(this PuffinBasicAtomTypeId atomTypeId, STValue src)
        {
            switch (atomTypeId)
            {
                case PuffinBasicAtomTypeId.INT32:
                    return src.GetInt32();
                case PuffinBasicAtomTypeId.INT64:
                    return src.GetInt64();
                case PuffinBasicAtomTypeId.FLOAT:
                    return src.GetFloat32();
                case PuffinBasicAtomTypeId.DOUBLE:
                    return src.GetFloat64();
                case PuffinBasicAtomTypeId.STRING:
                    return src.GetString();
                case PuffinBasicAtomTypeId.COMPOSITE:
                default:
                    throw new PuffinBasicInternalError("Not implemented");
            }
        }


        public static void SetValueIn(this PuffinBasicAtomTypeId atomTypeId, object value, STValue dest)
        {
            switch (atomTypeId)
            {
                case PuffinBasicAtomTypeId.INT32:
                    dest.SetInt32((int)value);
                    break;
                case PuffinBasicAtomTypeId.INT64:
                    dest.SetInt64((long)value);
                    break;
                case PuffinBasicAtomTypeId.FLOAT:
                    dest.SetFloat32((float)value);
                    break;
                case PuffinBasicAtomTypeId.DOUBLE:
                    dest.SetFloat64((double)value);
                    break;
                case PuffinBasicAtomTypeId.STRING:
                    dest.SetString((string)value);
                    break;
                case PuffinBasicAtomTypeId.COMPOSITE:
                default:
                    throw new PuffinBasicInternalError("Not implemented");
            }
        }

        public static void CopyArray(this PuffinBasicAtomTypeId atomTypeId, IList src, STValue dst)
        {

            if (atomTypeId == PuffinBasicAtomTypeId.INT32)
            {
                dst.SetArrayDimensions(new List<int> { src.Count });
                int[] array = (dst as STInt32ArrayValue).GetValue();
                int i = 0;
                foreach (int o in src)
                {
                    array[i++] = o;
                }
            }
            else if (atomTypeId == PuffinBasicAtomTypeId.INT64)
            {
                dst.SetArrayDimensions(new List<int> { src.Count });
                long[] array = (dst as STInt64ArrayValue).GetValue();
                int i = 0;
                foreach (long o in src)
                {
                    array[i++] = o;
                }
            }
            else if (atomTypeId == PuffinBasicAtomTypeId.FLOAT)
            {
                dst.SetArrayDimensions(new List<int> { src.Count });
                float[] array = (dst as STFloat32ArrayValue).GetValue();
                int i = 0;
                foreach (float o in src)
                {
                    array[i++] = o;
                }
            }
            else if (atomTypeId == PuffinBasicAtomTypeId.DOUBLE)
            {
                dst.SetArrayDimensions(new List<int> { src.Count });
                double[] array = (dst as STFloat64ArrayValue).GetValue();
                int i = 0;
                foreach (double o in src)
                {
                    array[i++] = o;
                }
            }
            else if (atomTypeId == PuffinBasicAtomTypeId.STRING)
            {
                dst.SetArrayDimensions(new List<int> { src.Count });
                string[] array = (dst as STStringArrayValue).GetValue();
                int i = 0;
                foreach (string o in src)
                {
                    array[i++] = o;
                }
            }
            else
                throw new PuffinBasicInternalError("Not implemented");
        }

        public static bool IsCompatibleWith(this PuffinBasicAtomTypeId atomTypeId, PuffinBasicAtomTypeId other) => atomTypeId switch
        {
            PuffinBasicAtomTypeId.INT32 or PuffinBasicAtomTypeId.INT64 or PuffinBasicAtomTypeId.FLOAT or PuffinBasicAtomTypeId.DOUBLE => other == PuffinBasicAtomTypeId.INT32
                                    || other == PuffinBasicAtomTypeId.INT64
                                    || other == PuffinBasicAtomTypeId.FLOAT
                                    || other == PuffinBasicAtomTypeId.DOUBLE,
            PuffinBasicAtomTypeId.STRING or PuffinBasicAtomTypeId.COMPOSITE => other == atomTypeId,
            _ => throw new PuffinBasicInternalError("Not implemented"),
        };

        public static string? GetRepr(this PuffinBasicAtomTypeId atomTypeId)
        {
            switch (atomTypeId) {
                case PuffinBasicAtomTypeId.INT32:
                    return "%";
                case PuffinBasicAtomTypeId.INT64:
                    return "@";
                case PuffinBasicAtomTypeId.FLOAT:
                    return "!";
                case PuffinBasicAtomTypeId.DOUBLE:
                    return "#";
                case PuffinBasicAtomTypeId.STRING:
                    return "$";
                case PuffinBasicAtomTypeId.COMPOSITE:
                    return null;
                default:
                    throw new PuffinBasicInternalError("Not implemented");
            }
        }

        public static PuffinBasicAtomTypeId Lookup(string symbol)
        {
            switch (symbol)
            {
                case "%":
                    return PuffinBasicAtomTypeId.INT32;
                case "@":
                    return PuffinBasicAtomTypeId.INT64;
                case "!":
                    return PuffinBasicAtomTypeId.FLOAT;
                case "#":
                    return PuffinBasicAtomTypeId.DOUBLE;
                case "$":
                    return PuffinBasicAtomTypeId.STRING;
                case null:
                    return PuffinBasicAtomTypeId.COMPOSITE;
                default:
                    throw new PuffinBasicInternalError("Not implemented");
            }
        }
    }
}

