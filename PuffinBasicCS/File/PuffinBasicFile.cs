namespace PuffinBasicCS.File
{
    using PuffinBasicCS.Common;
    using PuffinBasicCS.Domain;
    using System;
    using System.Collections.Frozen;
    using System.Collections.Generic;
    using System.ComponentModel;

    public interface IPuffinBasicFile
    {
        void SetFieldParams(PuffinBasicSymbolTable symbolTable, List<int> recordParts);
        int GetCurrentRecordNumber();
        long GetFileSizeInBytes();
        string ReadLine();
        byte[] ReadBytes(int n);
        void Print(string s);
        void WriteByte(byte b);
        bool Eof();
        void Put(int? recordNumber, PuffinBasicSymbolTable symbolTable);
        void Get(int? recordNumber, PuffinBasicSymbolTable symbolTable);
        bool IsOpen();
        void Dispose();
        enum FileOpenMode
        {
            INPUT,
            OUTPUT,
            APPEND,
            RANDOM
        }

        enum FileAccessMode
        {
            [Description("r")]
            READ_ONLY,
            [Description("w")]
            WRITE_ONLY,
            [Description("rw")]
            READ_WRITE
        }

        enum LockMode
        {
            SHARED,
            READ,
            WRITE,
            READ_WRITE,
            DEFAULT
        }

        enum FileState
        {
            OPEN,
            CLOSED
        }
    }

    // We probably don't need all these dictionaries when there are so few values involved, but at least this way everything is in a central location
    public static class FileEnums
    {
        private static readonly FrozenDictionary<string, IPuffinBasicFile.FileAccessMode> accessModeNameToValue;
        private static readonly FrozenDictionary<IPuffinBasicFile.FileAccessMode, string> accessModeValueToName;

        private static readonly FrozenDictionary<string, IPuffinBasicFile.FileOpenMode> openModeNameToValue
            = EnumHelpers.GetEnumLabels<IPuffinBasicFile.FileOpenMode>()
            .ToFrozenDictionary(x => x.Value, static x => x.Key);

        private static readonly FrozenDictionary<string, IPuffinBasicFile.LockMode> lockModeNameToValue
            = EnumHelpers.GetEnumLabels<IPuffinBasicFile.LockMode>()
            .ToFrozenDictionary(x => x.Value, static x => x.Key);

        static FileEnums()
        {
            accessModeValueToName = EnumHelpers.GetEnumLabels<IPuffinBasicFile.FileAccessMode>().ToFrozenDictionary();
            accessModeNameToValue = accessModeValueToName.ToFrozenDictionary(x => x.Value, static x => x.Key);
        }

        public static IPuffinBasicFile.FileAccessMode ParseAccessMode(string name) => accessModeNameToValue[name];
        public static IPuffinBasicFile.FileOpenMode ParseOpenMode(string name) => openModeNameToValue[name];
        public static IPuffinBasicFile.LockMode ParseLockMode(string name) => lockModeNameToValue[name];

        public static string Repr(this IPuffinBasicFile.FileAccessMode accessMode) => accessModeValueToName[accessMode];
        public static string Repr(this IPuffinBasicFile.FileOpenMode openMode) => openMode.ToString();
        public static string Repr(this IPuffinBasicFile.LockMode lockMode) => lockMode.ToString();
    }

    public abstract class PuffinBasicFile : IPuffinBasicFile
    {
        internal static readonly int DEFAULT_RECORD_LEN = 128;
        public abstract void Dispose();
        public abstract bool Eof();
        public abstract void Get(int? recordNumber, PuffinBasicSymbolTable symbolTable);
        public abstract int GetCurrentRecordNumber();
        public abstract long GetFileSizeInBytes();
        public abstract bool IsOpen();
        public abstract void Print(string s);
        public abstract void Put(int? recordNumber, PuffinBasicSymbolTable symbolTable);
        public abstract byte[] ReadBytes(int n);
        public abstract string ReadLine();
        public abstract void SetFieldParams(PuffinBasicSymbolTable symbolTable, List<int> recordParts);
        public abstract void WriteByte(byte b);
    }
}

