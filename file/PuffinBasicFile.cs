using It.Unimi.Dsi.Fastutil.Ints;
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
        void SetFieldParams(PuffinBasicSymbolTable symbolTable, IntList recordParts);
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
}

