namespace PuffinBasicCS.File
{
    using PuffinBasicCS.Error;
    using static PuffinBasicCS.Error.PuffinBasicRuntimeError.ErrorCode;

    using System;
    using System.Collections.Generic;
    using System.IO;

    using PuffinBasicCS.Domain;
    using PuffinBasicCS.Common;

    public class SystemInputOutputFile : PuffinBasicFile
    {
        private readonly TextReader @in;
        private readonly TextWriter @out;
        public SystemInputOutputFile(TextReader @in, TextWriter @out)
        {
            this.@in = @in;
            this.@out = @out;
        }

        public override void SetFieldParams(PuffinBasicSymbolTable symbolTable, List<int> recordParts)
        {
            throw new PuffinBasicRuntimeError(ILLEGAL_FILE_ACCESS, "Not supported for System IN/OUT!");
        }

        public override int GetCurrentRecordNumber()
        {
            return 0;
        }

        public override long GetFileSizeInBytes()
        {
            return 0;
        }

        public override byte[] ReadBytes(int n)
        {
            return ISOEncoding.GetBytes(ReadLine());
        }

        public override string ReadLine()
        {
            try
            {
                return @in.ReadLine().TrimEnd();
            }
            catch (IOException)
            {
                throw new PuffinBasicRuntimeError(IO_ERROR, "Failed to read line!");
            }
        }

        public override void Print(string s)
        {
            @out.Write(s);
        }

        public override void WriteByte(byte b)
        {
            try
            {
                @out.Write((char)b);
            }
            catch (Exception e)
            {
                throw new PuffinBasicRuntimeError(IO_ERROR, $"Failed to write buffer to output, error: {e.Message}");
            }
        }

        public override bool Eof()
        {
            return false;
        }

        public override void Put(int? recordNumber, PuffinBasicSymbolTable symbolTable)
        {
            throw new PuffinBasicRuntimeError(ILLEGAL_FILE_ACCESS, "Not supported for System IN/OUT!");
        }

        public override void Get(int? recordNumber, PuffinBasicSymbolTable symbolTable)
        {
            throw new PuffinBasicRuntimeError(ILLEGAL_FILE_ACCESS, "Not supported for System IN/OUT!");
        }

        public override bool IsOpen()
        {
            return true;
        }

        public override void Dispose()
        {
            throw new PuffinBasicRuntimeError(ILLEGAL_FILE_ACCESS, "Not supported for System IN/OUT!");
        }
    }
}

