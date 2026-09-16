namespace PuffinBasicCS.File
{
    using PuffinBasicCS.Error;
    using static PuffinBasicCS.Error.PuffinBasicRuntimeError.ErrorCode;

    using System;
    using System.Collections.Generic;
    using System.IO;

    using PuffinBasicCS.Domain;
    using PuffinBasicCS.Common;

    public class PuffinBasicSequentialAccessOutputFile : IPuffinBasicFile
    {
        private readonly string filename;
        private readonly FileStream @out;
        private long bytesAccessed;
        private IPuffinBasicFile.FileState fileState;
        public PuffinBasicSequentialAccessOutputFile(string filename, bool append)
        {
            if (filename == null) throw new ArgumentNullException("filename");

            this.filename = filename;
            try
            { 
                @out = System.IO.File.OpenWrite(filename);
                
                this.bytesAccessed = append ? @out.Length : 0;
            }
            catch (Exception e) {
                throw new PuffinBasicRuntimeError(IO_ERROR, $"Failed to open file {filename} for writing, error: ${e.Message}");
            }

            this.fileState = IPuffinBasicFile.FileState.OPEN;
        }

        public virtual void SetFieldParams(PuffinBasicSymbolTable symbolTable, List<int> recordParts)
        {
            throw GetIllegalAccess();
        }

        public virtual int GetCurrentRecordNumber()
        {
            return (int)(bytesAccessed / PuffinBasicFile.DEFAULT_RECORD_LEN);
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
            @out.Write(ISOEncoding.GetBytes(s), (int)@out.Position, s.Length);
        }

        public virtual void WriteByte(byte b)
        {
            bytesAccessed++;
            try
            {
                @out.WriteByte(b);
            }
            catch (Exception e)
            {
                throw new PuffinBasicRuntimeError(IO_ERROR, $"Failed to write buffer to output, error: {e.Message}");
            }
        }

        public virtual bool Eof()
        {
            return false;
        }

        public virtual void Put(int? recordNumber, PuffinBasicSymbolTable symbolTable)
        {
            throw GetIllegalAccess();
        }

        public virtual void Get(int? recordNumber, PuffinBasicSymbolTable symbolTable)
        {
            throw GetIllegalAccess();
        }

        private static PuffinBasicRuntimeError GetIllegalAccess()
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
                this.@out.Flush();
                this.@out.Dispose();
            }
            catch (Exception e)
            {
                throw new PuffinBasicRuntimeError(IO_ERROR, $"Failed to close file '{filename}', error: {e.Message}");
            }

            this.fileState = IPuffinBasicFile.FileState.CLOSED;
        }

        private void AssertOpen()
        {
            if (!IsOpen())
            {
                throw new PuffinBasicRuntimeError(ILLEGAL_FILE_ACCESS, $"File {filename} is not open!");
            }
        }
    }
}

