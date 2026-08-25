//using Com.Google.Common.Base;
//using It.Unimi.Dsi.Fastutil.Ints;
using Org.Puffinbasic.Domain;
using Org.Puffinbasic.Error;
//using Org.Jetbrains.Annotations;
//using Java.Io;
using static Org.Puffinbasic.Error.PuffinBasicRuntimeError.ErrorCode;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;

namespace Org.Puffinbasic.File
{
    public class PuffinBasicSequentialAccessOutputFile : IPuffinBasicFile
    {
        private readonly string filename;
        private readonly TextWriter @out;
        private long bytesAccessed;
        private IPuffinBasicFile.FileState fileState;
        private string lastLine;
        public PuffinBasicSequentialAccessOutputFile(string filename, bool append)
        {
            if (filename == null) throw new ArgumentNullException("filename");

            this.filename = filename;
            this.bytesAccessed = 0;
            try
            {
                this.@out = new PrintStream(new BufferedOutputStream(new FileOutputStream(filename, append)));
            }
            catch (FileNotFoundException e)
            {
                throw new PuffinBasicRuntimeError(IO_ERROR, "Failed to open file '" + filename + "' for writing, error: " + e.Message);
            }

            this.fileState = IPuffinBasicFile.FileState.OPEN;
        }

        public virtual void SetFieldParams(PuffinBasicSymbolTable symbolTable, List<int> recordParts)
        {
            throw GetIllegalAccess();
        }

        public virtual int GetCurrentRecordNumber()
        {
            return (int)(bytesAccessed / IPuffinBasicFile.DEFAULT_RECORD_LEN);
        }

        public virtual long GetFileSizeInBytes()
        {
            return 0;
        }

        public virtual string ReadLine()
        {
            throw GetIllegalAccess();
        }

        public virtual byte[] ReadBytes(int n)
        {
            throw GetIllegalAccess();
        }

        public virtual void Print(string s)
        {
            bytesAccessed += s.Length;
            @out.Write(s);
        }

        public virtual void WriteByte(byte b)
        {
            bytesAccessed++;
            try
            {
                @out.Write((char)b);
            }
            catch (Exception e)
            {
                throw new PuffinBasicRuntimeError(IO_ERROR, "Failed to write buffer to output, error: " + e.Message);
            }
        }

        public virtual bool Eof()
        {
            return false;
        }

        public virtual void Put(int recordNumber, PuffinBasicSymbolTable symbolTable)
        {
            throw GetIllegalAccess();
        }

        public virtual void Get(int recordNumber, PuffinBasicSymbolTable symbolTable)
        {
            throw GetIllegalAccess();
        }

        private PuffinBasicRuntimeError GetIllegalAccess()
        {
            return new PuffinBasicRuntimeError(ILLEGAL_FILE_ACCESS, "Not implemented for SequentialAccessOutputFile!");
        }

        public virtual bool IsOpen()
        {
            return fileState == IPuffinBasicFile.FileState.OPEN;
        }

        public virtual void Dispose()
        {
            AssertOpen();
            try
            {
                this.@out.Dispose();
            }
            catch (Exception e)
            {
                throw new PuffinBasicRuntimeError(IO_ERROR, "Failed to close file '" + filename + "', error: " + e.Message);
            }

            this.fileState = IPuffinBasicFile.FileState.CLOSED;
        }

        private void AssertOpen()
        {
            if (!IsOpen())
            {
                throw new PuffinBasicRuntimeError(ILLEGAL_FILE_ACCESS, "File " + filename + " is not open!");
            }
        }
    }
}

