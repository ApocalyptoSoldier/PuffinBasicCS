//using It.Unimi.Dsi.Fastutil.Ints;
using Org.Puffinbasic.Domain;
//using Org.Jetbrains.Annotations;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;

namespace Org.Puffinbasic.File
{
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
            // READ_ONLY("r")
            READ_ONLY,
            // WRITE_ONLY("w")
            WRITE_ONLY,
            // READ_WRITE("rw")
            READ_WRITE 

            // --------------------
            // TODO enum body members
            // public final String mode;
            // FileAccessMode(String mode) {
            //     this.mode = mode;
            // }
            // --------------------
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

    public static class FileEnumExtensions
    {
        public static IPuffinBasicFile.FileAccessMode FileAccessModeValueOf(string name)
        {
            // TODO: decide if I should fix this to match the original values or rework everything to match ToString()
            foreach (var val in (IPuffinBasicFile.FileAccessMode[])Enum.GetValues(typeof(IPuffinBasicFile.FileAccessMode)))
                if (name == val.ToString())
                    return val;

            switch (name) {
                case "r":
                    return IPuffinBasicFile.FileAccessMode.READ_ONLY;
                case "w":
                    return IPuffinBasicFile.FileAccessMode.WRITE_ONLY;
                case "rw":
                    return IPuffinBasicFile.FileAccessMode.READ_WRITE;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public static IPuffinBasicFile.FileOpenMode FileOpenModeValueOf(string name)
        {
            // TODO: decide if I should fix this to match the original values or rework everything to match ToString()
            foreach (var val in (IPuffinBasicFile.FileOpenMode[])Enum.GetValues(typeof(IPuffinBasicFile.FileOpenMode)))
                if (name == val.ToString())
                    return val;

            switch (name) {
                case "INPUT":
                    return IPuffinBasicFile.FileOpenMode.INPUT;
                case "OUTPUT":
                    return IPuffinBasicFile.FileOpenMode.OUTPUT;
                case "APPEND":
                    return IPuffinBasicFile.FileOpenMode.APPEND;
                case "RANDOM":
                    return IPuffinBasicFile.FileOpenMode.RANDOM;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public static IPuffinBasicFile.LockMode LockModeValueOf(string name)
        {
            // TODO: decide if I should fix this to match the original values or rework everything to match ToString()
            foreach (var val in (IPuffinBasicFile.LockMode[])Enum.GetValues(typeof(IPuffinBasicFile.LockMode)))
                if (name == val.ToString())
                    return val;

            switch (name)
            {
                case "SHARED":
                    return IPuffinBasicFile.LockMode.SHARED;
                case "READ":
                    return IPuffinBasicFile.LockMode.READ;
                case "WRITE":
                    return IPuffinBasicFile.LockMode.WRITE;
                case "READ_WRITE":
                    return IPuffinBasicFile.LockMode.READ_WRITE;
                case "DEFAULT":
                    return IPuffinBasicFile.LockMode.DEFAULT;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public static IPuffinBasicFile.FileState ValueOf(this IPuffinBasicFile.FileState _, string name) {
            if (name == "OPEN")
                return IPuffinBasicFile.FileState.OPEN;
            if (name == "CLOSED")
                return IPuffinBasicFile.FileState.CLOSED;

            throw new ArgumentOutOfRangeException();
        }
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

