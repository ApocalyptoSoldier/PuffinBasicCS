using It.Unimi.Dsi.Fastutil.Ints;
using Org.Puffinbasic.Domain;
using Org.Puffinbasic.Error;
using Org.Jetbrains.Annotations;
using Java.Io;
using Java.Nio.Charset;
using Org.Puffinbasic.Error.PuffinBasicRuntimeError.ErrorCode;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;

namespace Org.Puffinbasic.File
{
    public class SystemInputOutputFile : IPuffinBasicFile
    {
        private readonly BufferedReader in;
        private readonly PrintStream out;
        public SystemInputOutputFile(InputStream @in, PrintStream @out)
        {
            this.@in = new BufferedReader(new InputStreamReader(@in));
            this.@out = @out;
        }

        public virtual void SetFieldParams(PuffinBasicSymbolTable symbolTable, IntList recordParts)
        {
            throw new PuffinBasicRuntimeError(ILLEGAL_FILE_ACCESS, "Not supported for System IN/OUT!");
        }

        public virtual int GetCurrentRecordNumber()
        {
            return 0;
        }

        public virtual long GetFileSizeInBytes()
        {
            return 0;
        }

        public virtual byte[] ReadBytes(int n)
        {
            byte[] line = ReadLine().GetBytes(StandardCharsets.US_ASCII);
            if (n >= line.length)
            {
                return line;
            }
            else
            {
                byte[] copy = new byte[Math.Min(n, line.length)];
                System.Arraycopy(line, 0, copy, 0, n);
                return copy;
            }
        }

        public virtual string ReadLine()
        {
            try
            {
                return @in.ReadLine().StripTrailing();
            }
            catch (IOException e)
            {
                throw new PuffinBasicRuntimeError(IO_ERROR, "Failed to read line!");
            }
        }

        public virtual void Print(string s)
        {
            @out.Print(s);
        }

        public virtual void WriteByte(byte b)
        {
            try
            {
                @out.Write((char)b);
            }
            catch (Exception e)
            {
                throw new PuffinBasicRuntimeError(IO_ERROR, "Failed to write buffer to output, error: " + e.GetMessage());
            }
        }

        public virtual bool Eof()
        {
            return false;
        }

        public virtual void Put(int recordNumber, PuffinBasicSymbolTable symbolTable)
        {
            throw new PuffinBasicRuntimeError(ILLEGAL_FILE_ACCESS, "Not supported for System IN/OUT!");
        }

        public virtual void Get(int recordNumber, PuffinBasicSymbolTable symbolTable)
        {
            throw new PuffinBasicRuntimeError(ILLEGAL_FILE_ACCESS, "Not supported for System IN/OUT!");
        }

        public virtual bool IsOpen()
        {
            return true;
        }

        public virtual void Dispose()
        {
            throw new PuffinBasicRuntimeError(ILLEGAL_FILE_ACCESS, "Not supported for System IN/OUT!");
        }
    }
}

