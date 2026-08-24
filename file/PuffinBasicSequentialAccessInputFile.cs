//using Com.Google.Common.Base;
//using It.Unimi.Dsi.Fastutil.Ints;
using Org.Puffinbasic.Domain;
using Org.Puffinbasic.Error;
//using Org.Jetbrains.Annotations;
//using Java.Io;
//using Java.Nio.Charset;
using Org.Puffinbasic.Error.PuffinBasicRuntimeError.ErrorCode;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;

namespace Org.Puffinbasic.File
{
    public class PuffinBasicSequentialAccessInputFile : IPuffinBasicFile
    {
        private readonly string filename;
        private readonly BufferedReader in;
        private long bytesAccessed;
        private FileState fileState;
        private string lastLine;
        public PuffinBasicSequentialAccessInputFile(string filename)
        {
            Preconditions.CheckNotNull(filename);
            this.filename = filename;
            this.bytesAccessed = 0;
            try
            {
                this.@in = new BufferedReader(new FileReader(filename));
            }
            catch (FileNotFoundException e)
            {
                throw new PuffinBasicRuntimeError(IO_ERROR, "Failed to open file '" + filename + "' for reading, error: " + e.GetMessage());
            }

            this.fileState = FileState.OPEN;
        }

        public virtual void SetFieldParams(PuffinBasicSymbolTable symbolTable, List<int> recordParts)
        {
            ThrowIllegalAccess();
        }

        public virtual int GetCurrentRecordNumber()
        {
            AssertOpen();
            return (int)(bytesAccessed / PuffinBasicFile.DEFAULT_RECORD_LEN);
        }

        public virtual long GetFileSizeInBytes()
        {
            AssertOpen();
            return new File(filename).Length();
        }

        public virtual string ReadLine()
        {
            AssertOpen();
            try
            {
                if (lastLine == null)
                {
                    lastLine = @in.ReadLine();
                }

                bytesAccessed += lastLine.Length();
                var result = lastLine.StripTrailing();
                lastLine = null;
                return result;
            }
            catch (IOException e)
            {
                throw new PuffinBasicRuntimeError(IO_ERROR, "Failed to read line!, error: " + e.GetMessage());
            }
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

        public virtual void Print(string s)
        {
            ThrowIllegalAccess();
        }

        public virtual void WriteByte(byte b)
        {
            ThrowIllegalAccess();
        }

        public virtual bool Eof()
        {
            AssertOpen();
            try
            {
                this.lastLine = @in.ReadLine();
            }
            catch (IOException e)
            {
                throw new PuffinBasicRuntimeError(IO_ERROR, "Failed to read line!, error: " + e.GetMessage());
            }

            return lastLine == null;
        }

        public virtual void Put(int recordNumber, PuffinBasicSymbolTable symbolTable)
        {
            ThrowIllegalAccess();
        }

        public virtual void Get(int recordNumber, PuffinBasicSymbolTable symbolTable)
        {
            ThrowIllegalAccess();
        }

        private void ThrowIllegalAccess()
        {
            throw new PuffinBasicRuntimeError(ILLEGAL_FILE_ACCESS, "Not implemented for SequentialAccessInputFile!");
        }

        public virtual bool IsOpen()
        {
            return fileState == FileState.OPEN;
        }

        public virtual void Dispose()
        {
            AssertOpen();
            try
            {
                this.@in.Dispose();
            }
            catch (Exception e)
            {
                throw new PuffinBasicRuntimeError(IO_ERROR, "Failed to close file '" + filename + "', error: " + e.GetMessage());
            }

            this.fileState = FileState.CLOSED;
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

