//using It.Unimi.Dsi.Fastutil.Chars;
//using It.Unimi.Dsi.Fastutil.Objects;
namespace Org.Puffinbasic.Domain
{
    using Org.Puffinbasic.Domain.Scope;

    using static Org.Puffinbasic.Domain.STObjects;
    using static Org.Puffinbasic.Domain.Variable;
    using Org.Puffinbasic.Error;
    //using Java.Util;
    //using Java.Util.Concurrent.Atomic;
    //using Java.Util.Function;
    using static Org.Puffinbasic.Domain.STObjects.PuffinBasicAtomTypeId;
    using static Org.Puffinbasic.Error.PuffinBasicRuntimeError.ErrorCode;
    using System;
    using System.Collections.Generic;
    using System.Threading;

    public class PuffinBasicSymbolTable
    {
        public delegate void VariableConsumer(int id, ISTEntry entry, Variable variable);

        public static readonly int NULL_ID = -1;
        private readonly Dictionary<char, PuffinBasicAtomTypeId> defaultDataTypes;
        private readonly Dictionary<string, StructType> userDefinedTypes;
        private readonly Dictionary<string, int> labelNameToId;
        private IScope currentScope;
        private int id;
        private int lastId;
        private int lastLastId;
        private ISTEntry lastEntry;
        private ISTEntry lastLastEntry;
        public PuffinBasicSymbolTable()
        {
            this.defaultDataTypes = new Dictionary<char, PuffinBasicAtomTypeId>();
            this.userDefinedTypes = new Dictionary<string, StructType>();
            this.labelNameToId = new Dictionary<string, int>();
            this.currentScope = new GlobalScope();
            this.lastId = this.lastLastId = -1;
        }

        private int GenerateNextId()
        {
            return Interlocked.Increment(ref id);
        }

        public virtual IScope GetCurrentScope()
        {
            return currentScope;
        }

        private IScope? FindScope(Predicate<IScope> predicate)
        {
            var scope = GetCurrentScope();
            while (scope != null)
            {
                if (predicate(scope))
                {
                    return scope;
                }
                else
                {
                    scope = scope.GetSearchScope();
                }
            }

            return null;
        }

        public ISTEntry this[int id]
        {
            get => GetEntry(id);
        }

        private ISTEntry GetEntry(int id)
        {
            var scope = GetCurrentScope();
            var entry = scope.GetNullableEntry(id);
            if (entry != null)
            {
                return entry;
            }
            else
            {
                scope = scope.GetParent();
                while (scope != null)
                {
                    entry = scope.GetNullableEntry(id);
                    if (entry != null)
                    {
                        return entry;
                    }

                    scope = scope.GetParent();
                }
            }

            throw new PuffinBasicInternalError("Failed to find entry for id: " + id);
        }

        public virtual ISTEntry Get(int id)
        {

            // Cache for better performance
            if (id == lastId)
            {
                return lastEntry;
            }

            if (id == lastLastId)
            {
                return lastLastEntry;
            }

            lastLastId = lastId;
            lastLastEntry = lastEntry;
            lastId = id;
            lastEntry = GetEntry(id);
            return lastEntry;
        }

        public virtual int GetCompositeVariableIdForVariable(VariableName variableName)
        {
            var scope = FindScope((s) => s.ContainsVariable(variableName)) ?? GetCurrentScope();
            int id = scope.GetIdForVariable(variableName);
            if (id == -1)
            {
                throw new PuffinBasicInternalError("Failed to find variable: " + variableName);
            }

            return id;
        }

        // TODO: check if this could or should be ISTVariable instead
        public virtual ISTEntry GetVariable(int id)
        {
            var entry = Get(id);
            if (!entry.IsLValue())
            {
                throw new PuffinBasicRuntimeError(ILLEGAL_FUNCTION_PARAM, "Entry for id: " + id + " is not a variable");
            }

            return entry;
        }

        public virtual int AddVariableOrUDF(VariableName variableName, Func<VariableName, Variable> variableCreator, VariableConsumer consumer)
        {
            var scope = FindScope((s) => s.ContainsVariable(variableName)) ?? GetCurrentScope();
            int id = scope.GetIdForVariable(variableName);
            ISTVariable entry;
            if (id == -1)
            {
                id = GenerateNextId();
                scope.PutVariable(variableName, id);
                var variable = variableCreator.Invoke(variableName);
                entry = variableName.GetDataType().CreateVariableEntry(variable);
                scope.PutEntry(id, entry);
            }
            else
            {
                entry = (ISTVariable)Get(id);
            }

            consumer.Invoke(id, entry, entry.GetVariable());
            return id;
        }

        public virtual int AddCompositeVariable(VariableName variableName, STVariable variable)
        {
            var scope = FindScope((s) => s.ContainsVariable(variableName)) ?? GetCurrentScope();
            int id = GenerateNextId();
            scope.PutVariable(variableName, id);
            scope.PutEntry(id, variable);
            return id;
        }

        public virtual int AddLabel(string label)
        {
            if (!labelNameToId.TryGetValue(label, out int id))
            { 
                id = AddLabel();
                labelNameToId[label] = id;
            }

            return id;
        }

        public virtual int AddLabel()
        {
            var scope = GetCurrentScope();
            var id = GenerateNextId();
            var entry = new STLabel();
            scope.PutEntry(id, entry);
            return id;
        }

        public virtual int AddGotoTarget()
        {
            var scope = GetCurrentScope();
            int id = GenerateNextId();
            var entry = PuffinBasicAtomTypeId.INT32.CreateTmpEntry();
            scope.PutEntry(id, entry);
            return id;
        }

        public virtual int AddArrayReference(STLValue lvalue)
        {
            var @ref = new ArrayReferenceValue(lvalue);
            int id = GenerateNextId();
            var entry = new STLValue(@ref, lvalue.GetType());
            GetCurrentScope().PutEntry(id, entry);
            return id;
        }

        public virtual int AddTmp(PuffinBasicType type, Action<ISTEntry> consumer)
        {
            var scope = GetCurrentScope();
            int id = GenerateNextId();
            ISTEntry entry = type.CanBeLValue() ? new STLValue(null, type) : new STTmp(null, type);
            entry.CreateAndSetInstance(this);
            scope.PutEntry(id, entry);
            consumer.Invoke(entry);
            return id;
        }

        public virtual int AddTmp(PuffinBasicAtomTypeId dataType, Action<ISTEntry> consumer)
        {
            var scope = GetCurrentScope();
            int id = GenerateNextId();
            var entry = dataType.CreateTmpEntry();
            scope.PutEntry(id, entry);
            consumer.Invoke(entry);
            return id;
        }

        public virtual int AddRef(PuffinBasicType type)
        {
            var scope = GetCurrentScope();
            int id = GenerateNextId();
            var entry = new STRef(type);
            scope.PutEntry(id, entry);
            return id;
        }

        public virtual int AddTmpCompatibleWith(int srcId)
        {
            var scope = GetCurrentScope();
            var dataType = scope.GetEntry(srcId).GetType().GetAtomTypeId();
            int id = GenerateNextId();
            scope.PutEntry(id, dataType.CreateTmpEntry());
            return id;
        }

        public virtual PuffinBasicAtomTypeId GetDataTypeFor(string varname, string suffix)
        {
            var scope = GetCurrentScope();
            if (scope.ContainsVariable(new VariableName(varname, null, COMPOSITE)))
            {
                return COMPOSITE;
            }

            if (varname.Length == 0)
            {
                throw new PuffinBasicInternalError("Empty variable name: " + varname);
            }

            if (suffix == null)
            {
                var firstChar = varname[0];

                return defaultDataTypes.TryGetValue(firstChar, out PuffinBasicAtomTypeId value) ? value : DOUBLE;
            }
            else
            {
                return PuffinBasicAtomTypeIdExtensions.Lookup(suffix);
            }
        }

        public virtual void SetDefaultDataType(char c, PuffinBasicAtomTypeId dataType) => defaultDataTypes.Add(c, dataType);

        public virtual void AddStructType(string name, StructType type) => userDefinedTypes.Add(name, type);

        public virtual void CheckUnused(string name)
        {
            if (userDefinedTypes.ContainsKey(name))
            {
                throw new PuffinBasicRuntimeError(BAD_FIELD, "Name: " + name + " is already used!");
            }
        }

        public virtual StructType GetStructType(string name)
        {
            var type = userDefinedTypes[name];
            if (type == null)
            {
                throw new PuffinBasicRuntimeError(MISSING_STRUCT, "Missing struct: " + name);
            }

            return type;
        }

        public virtual void PushDeclarationScope(int funcId, bool localScope)
        {
            currentScope = GetCurrentScope().CreateChild(funcId, localScope);
        }

        public virtual void PushRuntimeScope(int funcId, int callerInstrId)
        {
            var funcDeclScope = GetCurrentScope().GetChild(funcId);
            if (funcDeclScope == null)
            {
                throw new PuffinBasicInternalError("Failed to find scope for id: " + funcId);
            }

            currentScope = funcDeclScope.CreateRuntimeScope(callerInstrId);
        }

        public virtual void PopScope()
        {
            var parent = GetCurrentScope().GetParent();
            currentScope = parent ?? throw new PuffinBasicInternalError("Scope underflow!");
        }
    }
}

