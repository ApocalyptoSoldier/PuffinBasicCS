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
        int DEFAULT_RECORD_LEN = 128;
        void SetFieldParams(PuffinBasicSymbolTable symbolTable, List<int> recordParts);
        int GetCurrentRecordNumber();
        long GetFileSizeInBytes();
        string ReadLine();
        byte[] ReadBytes(int n);
        void Print(string s);
        void WriteByte(byte b);
        bool Eof();
        void Put(int recordNumber, PuffinBasicSymbolTable symbolTable);
        void Get(int recordNumber, PuffinBasicSymbolTable symbolTable);
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
        public static IPuffinBasicFile.FileAccessMode ValueOf(this IPuffinBasicFile.FileAccessMode _, string name)
        {
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

        public static IPuffinBasicFile.FileOpenMode ValueOf(this IPuffinBasicFile.FileOpenMode _, string name)
        {
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

        public static IPuffinBasicFile.LockMode ValueOf(this IPuffinBasicFile.LockMode _, string name)
        {
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
}

