//using It.Unimi.Dsi.Fastutil.Ints;
//using It.Unimi.Dsi.Fastutil.Objects;
using Org.Puffinbasic.Domain.Scope;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;

using static Org.Puffinbasic.Domain.PuffinBasicSymbolTable;
using static Org.Puffinbasic.Domain.STObjects;
using static Org.Puffinbasic.Domain.Variable;

namespace Org.Puffinbasic.Domain.Scope
{
    public interface IScope
    {
        int GetCallerInstrId();
        IScope CreateRuntimeScope(int callerInstrId);
        IScope CreateChild(int funcId, bool localScope);
        IScope GetChild(int funcId);
        IScope GetSearchScope();
        IScope GetParent();
        int GetIdForVariable(VariableName variableName);
        void PutVariable(VariableName variableName, int id);
        bool ContainsVariable(VariableName variableName);
        void PutEntry(int id, ISTEntry entry);
        ISTEntry GetEntry(int id);
        ISTEntry? GetNullableEntry(int id);
    }

    public abstract class Scope : IScope
    {
        internal readonly IScope parent;
        internal readonly Dictionary<int, IScope> funcIdToScope;
        //internal readonly Dictionary<VariableName, int> variableNameToEntry;
        internal readonly Dictionary<string, int> variableNameToEntry;
        internal readonly int callerInstrId;
        internal ISTEntry[] entryMap;


        public Scope(int callerInstrId, Dictionary<int, IScope> funcIdToScope, Dictionary<string, int> variableNameToEntry, ISTEntry[] entryMap)
        {
            this.callerInstrId = callerInstrId;
            this.funcIdToScope = funcIdToScope;
            this.entryMap = entryMap;
            this.variableNameToEntry = variableNameToEntry;
        }

        public Scope(IScope parent, int callerInstrId, Dictionary<int, IScope> funcIdToScope, Dictionary<string, int> variableNameToEntry, ISTEntry[] entryMap)
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
                return variableNameToEntry[variableName.ToString()];
            return -1;
        }
        public bool ContainsVariable(VariableName variableName) => variableNameToEntry.ContainsKey(variableName.ToString());

        public void PutVariable(VariableName variableName, int id) => variableNameToEntry.Add(variableName.ToString(), id);

        public int GetCallerInstrId() => callerInstrId;

        public abstract IScope CreateRuntimeScope(int callerInstrId);
        public abstract IScope CreateChild(int funcId, bool localScope);
        public IScope GetChild(int funcId) => funcIdToScope[funcId];
        public IScope GetParent() => parent;
        public abstract IScope GetSearchScope();

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
            Array.Copy(entryMap, newEntryMap, newLen);
            //System.Arraycopy(entryMap, 0, newEntryMap, 0, entryMap.Length);
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
    }

    public class GlobalScope : Scope
    {
        private static readonly int INITIAL_ENTRY_TABLE_SIZE = 1024;

        public GlobalScope() : base(NULL_ID, new Dictionary<int, IScope>(), new Dictionary<string, int>(), new ISTEntry[INITIAL_ENTRY_TABLE_SIZE])
        {
        }

        public GlobalScope(int callerInstrId) : base(callerInstrId, new Dictionary<int, IScope>(), new Dictionary<string, int>(), new ISTEntry[INITIAL_ENTRY_TABLE_SIZE])
        {
        }

        public GlobalScope(int callerInstrId, Dictionary<int, IScope> funcIdToScope, Dictionary<string, int> variableNameToEntry, ISTEntry[] entryMap) : base(callerInstrId, funcIdToScope, variableNameToEntry, entryMap)
        {
        }

        public override IScope CreateRuntimeScope(int callerInstrId)
        {
            return new GlobalScope(callerInstrId, funcIdToScope, variableNameToEntry, entryMap);
        }

        public override IScope CreateChild(int funcId, bool localScope)
        {
            var child = funcIdToScope[funcId];
            if (child == null)
            {
                child = localScope ? new LocalScope(this) : new ChildScope(this);
                funcIdToScope.Add(funcId, child);
            }

            return child;
        }

        public override IScope GetSearchScope()
        {
            return null;
        }
    }

    /*sealed*/
    public class ChildScope : Scope
    {
        public ChildScope(IScope parent) : base(parent, NULL_ID, new Dictionary<int, IScope>(), new Dictionary<string, int>(), new ISTEntry[100])
        {
        }

        public ChildScope(IScope parent, int callerInstrId) : base(parent, callerInstrId, new Dictionary<int, IScope>(), new Dictionary<string, int>(), new ISTEntry[100])
        {

        }

        public ChildScope(IScope parent, int callerInstrId, Dictionary<int, IScope> funcIdToScope, Dictionary<string, int> variableNameToEntry, ISTEntry[] entryMap) 
            : base(parent, callerInstrId, funcIdToScope, variableNameToEntry, entryMap)
        {
        }

        public override IScope CreateRuntimeScope(int callerInstrId)
        {
            return new ChildScope(parent, callerInstrId, new Dictionary<int, IScope>(funcIdToScope), new Dictionary<string, int>(variableNameToEntry), entryMap.Clone() as ISTEntry[]);
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

        public override IScope GetSearchScope()
        {
            return parent;
        }
    }

    /*sealed*/
    public class LocalScope : Scope
    {
        public LocalScope(IScope parent) : base(parent, NULL_ID, new Dictionary<int, IScope>(), new Dictionary<string, int>(), new ISTEntry[100])
        {

        }
        public LocalScope(IScope parent, int callerInstrId, Dictionary<int, IScope> funcIdToScope, Dictionary<string, int> variableNameToEntry, ISTEntry[] entryMap) 
            : base(parent, callerInstrId, funcIdToScope, variableNameToEntry, entryMap)
        {
        }

        public override IScope CreateRuntimeScope(int callerInstrId)
        {
            return new LocalScope(parent, callerInstrId, new Dictionary<int, IScope>(funcIdToScope), new Dictionary<string, int>(variableNameToEntry), entryMap.Clone() as ISTEntry[]);
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

        public override IScope GetSearchScope()
        {
            return null;
        }
    }
}

/*

public class GlobalScope : IScope
{
    private static readonly int INITIAL_ENTRY_TABLE_SIZE = 1024;
    private readonly int callerInstrId;
    private readonly Dictionary<int, IScope> funcIdToScope;
    // This is an optimization to make entry access fast at runtime.
    //private final ObjectList<STEntry> entryMap;
    private STEntry[] entryMap;
    // This is an optimization to make entry access fast at runtime.
    //private final ObjectList<STEntry> entryMap;
    private readonly Dictionary<VariableName, int> variableNameToEntry;
    // This is an optimization to make entry access fast at runtime.
    //private final ObjectList<STEntry> entryMap;
    GlobalScope() : this(NULL_ID, new Int2ObjectOpenHashMap(), new STEntry[INITIAL_ENTRY_TABLE_SIZE], new Object2IntOpenHashMap())
    {
    }

    // This is an optimization to make entry access fast at runtime.
    //private final ObjectList<STEntry> entryMap;
    private GlobalScope(int callerInstrId, Dictionary<int, IScope> funcIdToScope, STEntry[] entryMap, Dictionary<VariableName, int> variableNameToEntry)
    {
        this.callerInstrId = callerInstrId;
        this.funcIdToScope = funcIdToScope;
        this.entryMap = entryMap;
        this.variableNameToEntry = variableNameToEntry;
    }

    // This is an optimization to make entry access fast at runtime.
    //private final ObjectList<STEntry> entryMap;
    public IScope CreateRuntimeScope(int callerInstrId)
    {
        return new GlobalScope(callerInstrId, funcIdToScope, entryMap, variableNameToEntry);
    }

    // This is an optimization to make entry access fast at runtime.
    //private final ObjectList<STEntry> entryMap;
    public int GetCallerInstrId()
    {
        return callerInstrId;
    }

    // This is an optimization to make entry access fast at runtime.
    //private final ObjectList<STEntry> entryMap;
    public IScope CreateChild(int funcId, bool localScope)
    {
        var child = funcIdToScope[funcId];
        if (child == null)
        {
            child = localScope ? new LocalScope(this) : new ChildScope(this);
            funcIdToScope.Add(funcId, child);
        }

        return child;
    }

    // This is an optimization to make entry access fast at runtime.
    //private final ObjectList<STEntry> entryMap;
    public IScope GetChild(int funcId)
    {
        return funcIdToScope[funcId];
    }

    // This is an optimization to make entry access fast at runtime.
    //private final ObjectList<STEntry> entryMap;
    public IScope GetParent()
    {
        return null;
    }

    // This is an optimization to make entry access fast at runtime.
    //private final ObjectList<STEntry> entryMap;
    public IScope GetSearchScope()
    {
        return null;
    }

    // This is an optimization to make entry access fast at runtime.
    //private final ObjectList<STEntry> entryMap;
    public int GetIdForVariable(VariableName variableName)
    {
        return variableNameToEntry.GetOrDefault(variableName, NULL_ID);
    }

    // This is an optimization to make entry access fast at runtime.
    //private final ObjectList<STEntry> entryMap;
    public void PutVariable(VariableName variableName, int id)
    {
        variableNameToEntry.Add(variableName, id);
    }

    // This is an optimization to make entry access fast at runtime.
    //private final ObjectList<STEntry> entryMap;
    public bool ContainsVariable(VariableName variableName)
    {
        return variableNameToEntry.ContainsKey(variableName);
    }

    // This is an optimization to make entry access fast at runtime.
    //private final ObjectList<STEntry> entryMap;
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

        var newEntryMap = new STEntry[newLen];
        System.Arraycopy(entryMap, 0, newEntryMap, 0, entryMap.Length);
        entryMap = newEntryMap;
    }

    // This is an optimization to make entry access fast at runtime.
    //private final ObjectList<STEntry> entryMap;
    public void PutEntry(int id, STEntry entry)
    {
        int sz = entryMap.Length;
        if (id >= sz)
        {
            Resize(id);
        }

        entryMap[id] = entry;
    }

    // This is an optimization to make entry access fast at runtime.
    //private final ObjectList<STEntry> entryMap;
    public STEntry GetEntry(int id)
    {
        return entryMap[id];
    }

    // This is an optimization to make entry access fast at runtime.
    //private final ObjectList<STEntry> entryMap;
    public STEntry GetNullableEntry(int id)
    {
        if (id >= 0 && id < entryMap.Length)
        {
            return entryMap[id];
        }

        return null;
    }
}

public class ChildScope : IScope
{
    private readonly IScope parent;
    private readonly int callerInstrId;
    private readonly Dictionary<int, IScope> funcIdToScope;
    private readonly Dictionary<int, STEntry> entryMap;
    private readonly Dictionary<VariableName, int> variableNameToEntry;
    ChildScope(IScope parent) : this(parent, NULL_ID, new Int2ObjectOpenHashMap(), new Int2ObjectOpenHashMap(), new Object2IntOpenHashMap())
    {
    }

    private ChildScope(IScope parent, int callerInstrId, Dictionary<int, IScope> funcIdToScope, Dictionary<int, STEntry> entryMap, Dictionary<VariableName, int> variableNameToEntry)
    {
        this.parent = parent;
        this.callerInstrId = callerInstrId;
        this.funcIdToScope = funcIdToScope;
        this.entryMap = entryMap;
        this.variableNameToEntry = variableNameToEntry;
    }

    public IScope CreateRuntimeScope(int callerInstrId)
    {
        return new ChildScope(parent, callerInstrId, new Int2ObjectOpenHashMap(funcIdToScope), new Int2ObjectOpenHashMap(entryMap), new Object2IntOpenHashMap(variableNameToEntry));
    }

    public int GetCallerInstrId()
    {
        return callerInstrId;
    }

    public IScope CreateChild(int funcId, bool localScope)
    {
        var child = funcIdToScope[funcId];
        if (child == null)
        {
            child = new ChildScope(this);
            funcIdToScope.Add(funcId, child);
        }

        return child;
    }

    public IScope GetChild(int funcId)
    {
        return funcIdToScope[funcId];
    }

    public IScope GetParent()
    {
        return parent;
    }

    public IScope GetSearchScope()
    {
        return parent;
    }

    public int GetIdForVariable(VariableName variableName)
    {
        variableNameToEntry.TryGetValue(variableName, out var id);
        return id;
        //return variableNameToEntry.GetOrDefault(variableName, -1);
    }

    public void PutVariable(VariableName variableName, int id)
    {
        variableNameToEntry.Put(variableName, id);
    }

    public bool ContainsVariable(VariableName variableName)
    {
        return variableNameToEntry.ContainsKey(variableName);
    }

    public void PutEntry(int id, STEntry entry)
    {
        entryMap.Add(id, entry);
    }

    public STEntry GetEntry(int id)
    {
        return entryMap[id];
    }

    public STEntry GetNullableEntry(int id)
    {
        return entryMap[id];
    }
}

public class LocalScope : IScope
{
    private readonly IScope parent;
    private readonly int callerInstrId;
    private readonly Dictionary<int, IScope> funcIdToScope;
    private readonly Dictionary<int, STEntry> entryMap;
    private readonly Dictionary<VariableName, int> variableNameToEntry;
    LocalScope(IScope parent) : this(parent, NULL_ID, new Int2ObjectOpenHashMap(), new Int2ObjectOpenHashMap(), new Object2IntOpenHashMap())
    {
    }

    private LocalScope(IScope parent, int callerInstrId, Dictionary<int, IScope> funcIdToScope, Dictionary<int, STEntry> entryMap, Dictionary<VariableName, int> variableNameToEntry)
    {
        this.parent = parent;
        this.callerInstrId = callerInstrId;
        this.funcIdToScope = funcIdToScope;
        this.entryMap = entryMap;
        this.variableNameToEntry = variableNameToEntry;
    }

    public IScope CreateRuntimeScope(int callerInstrId)
    {
        return new LocalScope(parent, callerInstrId, new Int2ObjectOpenHashMap(funcIdToScope), new Int2ObjectOpenHashMap(entryMap), new Object2IntOpenHashMap(variableNameToEntry));
    }

    public int GetCallerInstrId()
    {
        return callerInstrId;
    }

    public IScope CreateChild(int funcId, bool localScope)
    {
        var child = funcIdToScope[funcId];
        if (child == null)
        {
            child = new ChildScope(this);
            funcIdToScope.Put(funcId, child);
        }

        return child;
    }

    public IScope GetChild(int funcId)
    {
        return funcIdToScope[funcId];
    }

    public IScope GetParent()
    {
        return parent;
    }

    public IScope GetSearchScope()
    {
        return null;
    }

    public int GetIdForVariable(VariableName variableName)
    {
        variableNameToEntry.TryGetValue(variableName, out var id);
        return id;
        //return variableNameToEntry.GetOrDefault(variableName, -1);
    }

    public void PutVariable(VariableName variableName, int id)
    {
        variableNameToEntry.Put(variableName, id);
    }

    public bool ContainsVariable(VariableName variableName)
    {
        return variableNameToEntry.ContainsKey(variableName);
    }

    public void PutEntry(int id, STEntry entry)
    {
        entryMap.Put(id, entry);
    }

    public STEntry GetEntry(int id)
    {
        return entryMap[id];
    }

    public STEntry GetNullableEntry(int id)
    {
        return entryMap[id];
    }
}
*/