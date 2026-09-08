//using It.Unimi.Dsi.Fastutil.Ints;
//using It.Unimi.Dsi.Fastutil.Objects;
namespace PuffinBasicCS.Domain
{
    using System;
    using System.Collections.Generic;

    using static PuffinBasicCS.Domain.PuffinBasicSymbolTable;
    using static PuffinBasicCS.Domain.STObjects;
    using static PuffinBasicCS.Domain.Variable;

    public interface IScope
    {
        int GetCallerInstrId();
        IScope CreateRuntimeScope(int callerInstrId);
        IScope CreateChild(int funcId, bool localScope);
        IScope GetChild(int funcId);
        IScope? GetSearchScope();
        IScope? GetParent();
        int GetIdForVariable(VariableName variableName);
        void PutVariable(VariableName variableName, int id);
        bool ContainsVariable(VariableName variableName);
        void PutEntry(int id, ISTEntry entry);
        ISTEntry GetEntry(int id);
        ISTEntry? GetNullableEntry(int id);
        ISTEntry? GetNullableVariable(VariableName variableName);

        int TryGetIdForVariable(VariableName variableName);
    }

    public abstract class Scope : IScope
    {
        internal readonly IScope? parent;
        internal readonly Dictionary<int, IScope> funcIdToScope;
        internal readonly Dictionary<VariableName, int> variableNameToEntry;
        internal readonly int callerInstrId;
        internal ISTEntry[] entryMap;

        public Scope(int callerInstrId, Dictionary<int, IScope> funcIdToScope, Dictionary<VariableName, int> variableNameToEntry, ISTEntry[] entryMap)
        {
            this.callerInstrId = callerInstrId;
            this.funcIdToScope = funcIdToScope;
            this.entryMap = entryMap;
            this.variableNameToEntry = variableNameToEntry;
        }

        public Scope(IScope? parent, int callerInstrId, Dictionary<int, IScope> funcIdToScope, Dictionary<VariableName, int> variableNameToEntry, ISTEntry[] entryMap)
        {
            this.parent = parent;
            this.funcIdToScope = funcIdToScope ?? throw new ArgumentNullException(nameof(funcIdToScope));
            this.variableNameToEntry = variableNameToEntry ?? throw new ArgumentNullException(nameof(variableNameToEntry));
            this.callerInstrId = callerInstrId;
            this.entryMap = entryMap ?? throw new ArgumentNullException(nameof(entryMap));
        }

        public int GetIdForVariable(VariableName variableName)
        {
            if (ContainsVariable(variableName))
                return variableNameToEntry[variableName];
            return -1;
        }

        public bool ContainsVariable(VariableName variableName) => variableNameToEntry.ContainsKey(variableName);

        public void PutVariable(VariableName variableName, int id) => variableNameToEntry.Add(variableName, id);

        public int GetCallerInstrId() => callerInstrId;

        public abstract IScope CreateRuntimeScope(int callerInstrId);
        public abstract IScope CreateChild(int funcId, bool localScope);
        public IScope GetChild(int funcId) => funcIdToScope[funcId];
        public IScope? GetParent() => parent;
        public abstract IScope? GetSearchScope();

        private void Resize(int index)
        {
            int newLen = entryMap.Length << 1;
            if (newLen < index)
            {
                do
                {
                    newLen = newLen << 1;
                }
                while (newLen < index);
            }

            var newEntryMap = new ISTEntry[newLen];
            Array.Copy(entryMap, newEntryMap, entryMap.Length);
            entryMap = newEntryMap;
        }

        public void PutEntry(int id, ISTEntry entry)
        {
            int sz = entryMap.Length;
            if (id >= sz)
            {
                Resize(id);
            }

            entryMap[id] = entry;
        }

        public ISTEntry GetEntry(int id)
        {
            return entryMap[id];
        }

        public ISTEntry? GetNullableEntry(int id)
        {
            if (id >= 0 && id < entryMap.Length)
            {
                return entryMap[id];
            }

            return null;
        }

        public ISTEntry? GetNullableVariable(VariableName variableName)
        {
            if (ContainsVariable(variableName))
                return entryMap[GetIdForVariable(variableName)];

            return null;
        }

        public int TryGetIdForVariable(VariableName variableName)
        {
            if (ContainsVariable(variableName))
                return GetIdForVariable(variableName);

            return this.GetSearchScope()?.TryGetIdForVariable(variableName) ?? -1;
        }
    }

    public class GlobalScope : Scope
    {
        private static readonly int INITIAL_ENTRY_TABLE_SIZE = 1024;

        public GlobalScope() : base(NULL_ID, new Dictionary<int, IScope>(), new Dictionary<VariableName, int>(), new ISTEntry[INITIAL_ENTRY_TABLE_SIZE])
        {
        }

        public GlobalScope(int callerInstrId) : base(callerInstrId, new Dictionary<int, IScope>(), new Dictionary<VariableName, int>(), new ISTEntry[INITIAL_ENTRY_TABLE_SIZE])
        {
        }

        public GlobalScope(int callerInstrId, Dictionary<int, IScope> funcIdToScope, Dictionary<VariableName, int> variableNameToEntry, ISTEntry[] entryMap) : base(callerInstrId, funcIdToScope, variableNameToEntry, entryMap)
        {
        }

        public override IScope CreateRuntimeScope(int callerInstrId)
        {
            return new GlobalScope(callerInstrId, funcIdToScope, variableNameToEntry, entryMap);
        }

        public override IScope CreateChild(int funcId, bool localScope)
        {
            #pragma warning disable CS8600 // Converting null literal or possible null value to non-nullable type.
            if (!funcIdToScope.TryGetValue(funcId, out IScope child))
            { 
                child = localScope ? new LocalScope(this) : new ChildScope(this);
                funcIdToScope.Add(funcId, child);
            }
            #pragma warning restore CS8600 // Converting null literal or possible null value to non-nullable type.

            return child;
        }

        public override IScope? GetSearchScope()
        {
            return null;
        }
    }

    /*sealed*/
    public class ChildScope : Scope
    {
        public ChildScope(IScope? parent) : base(parent, NULL_ID, new Dictionary<int, IScope>(), new Dictionary<VariableName, int>(), new ISTEntry[100])
        {
        }

        public ChildScope(IScope? parent, int callerInstrId) : base(parent, callerInstrId, new Dictionary<int, IScope>(), new Dictionary<VariableName, int>(), new ISTEntry[100])
        {

        }

        public ChildScope(IScope? parent, int callerInstrId, Dictionary<int, IScope> funcIdToScope, Dictionary<VariableName, int> variableNameToEntry, ISTEntry[] entryMap) 
            : base(parent, callerInstrId, funcIdToScope, variableNameToEntry, entryMap)
        {
        }

        public override IScope CreateRuntimeScope(int callerInstrId)
        {
            return new ChildScope(parent, callerInstrId, new Dictionary<int, IScope>(funcIdToScope), new Dictionary<VariableName, int>(variableNameToEntry), (ISTEntry[])entryMap.Clone());
        }

        public override IScope CreateChild(int funcId, bool localScope)
        {
            var child = funcIdToScope[funcId];
            if (child == null)
            {
                child = new ChildScope(this);
                funcIdToScope.Add(funcId, child);
            }

            return child;
        }

        public override IScope? GetSearchScope()
        {
            return parent;
        }
    }

    /*sealed*/
    public class LocalScope : Scope
    {
        public LocalScope(IScope? parent) : base(parent, NULL_ID, new Dictionary<int, IScope>(), new Dictionary<VariableName, int>(), new ISTEntry[100])
        {

        }
        public LocalScope(IScope? parent, int callerInstrId, Dictionary<int, IScope> funcIdToScope, Dictionary<VariableName, int> variableNameToEntry, ISTEntry[] entryMap) 
            : base(parent, callerInstrId, funcIdToScope, variableNameToEntry, entryMap)
        {
        }

        public override IScope CreateRuntimeScope(int callerInstrId)
        {
            return new LocalScope(parent, callerInstrId, new Dictionary<int, IScope>(funcIdToScope), new Dictionary<VariableName, int>(variableNameToEntry), (ISTEntry[])entryMap.Clone());
        }

        public override IScope CreateChild(int funcId, bool localScope)
        {
            var child = funcIdToScope[funcId];
            if (child == null)
            {
                child = new ChildScope(this);
                funcIdToScope.Add(funcId, child);
            }

            return child;
        }

        public override IScope? GetSearchScope()
        {
            return null;
        }
    }
}