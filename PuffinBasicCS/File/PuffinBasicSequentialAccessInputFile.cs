//using Com.Google.Common.Base;
//using It.Unimi.Dsi.Fastutil.Ints;
namespace Org.Puffinbasic.File
{
    using Org.Puffinbasic.Domain;
    using Org.Puffinbasic.Error;
    //using Org.Jetbrains.Annotations;
    //using Java.Io;
    //using Java.Nio.Charset;
    using static Org.Puffinbasic.Error.PuffinBasicRuntimeError.ErrorCode;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using static Org.Puffinbasic.File.IPuffinBasicFile;
    using System.IO;
    using Org.Puffinbasic.Common;

    public class PuffinBasicSequentialAccessInputFile : PuffinBasicFile
    {
        private readonly string filename;
        private readonly FileStream @in;
        private long bytesAccessed;
        private FileState fileState;
        private string lastLine;
        public PuffinBasicSequentialAccessInputFile(string filename)
        {
            if (filename == null) throw new ArgumentNullException(nameof(filename));

            this.filename = filename;
            this.bytesAccessed = 0;
            try
            {
                this.@in = System.IO.File.OpenRead(filename);
            }
            catch (System.IO.FileNotFoundException e)
            {
                throw new PuffinBasicRuntimeError(IO_ERROR, $"Failed to open file '{filename}' for reading, error: {e.Message}");
            }

            this.fileState = FileState.OPEN;
        }

        public override void SetFieldParams(PuffinBasicSymbolTable symbolTable, List<int> recordParts)
        {
            ThrowIllegalAccess();
        }

        public override int GetCurrentRecordNumber()
        {
            AssertOpen();
            return (int)(bytesAccessed / PuffinBasicFile.DEFAULT_RECORD_LEN);
        }

        public override long GetFileSizeInBytes()
        {
            AssertOpen();
            return new FileInfo(filename).Length;
        }

        public override string ReadLine()
        {
            AssertOpen();
            try
            {
                if (lastLine == null)
                {
                    StringBuilder sb = new StringBuilder();

                    char c;
                    do
                    {
                        c = (char)@in.ReadByte();
                        bytesAccessed++;

                        sb.Append(c);

                    } while (c != '\n');

                    lastLine = sb.ToString();
                }

                var result = lastLine.TrimEnd();
                lastLine = null;
                return result;
            }
            catch (System.IO.IOException e)
            {
                throw new PuffinBasicRuntimeError(IO_ERROR, $"Failed to read line!, error: {e.Message}");
            }
        }

        public override byte[] ReadBytes(int n)
        {
            //throw new NotImplementedException();
            //byte[] line = ReadLine().GetBytes(StandardCharsets.US_ASCII);
            byte[] line = ISOEncoding.GetBytes(ReadLine());
            if (n >= line.Length)
            {
                return line;
            }
            else
            {
                return line.Take(Math.Min(n, line.Length)).ToArray();
            }
        }

        public override void Print(string s)
        {
            ThrowIllegalAccess();
        }

        public override void WriteByte(byte b)
        {
            ThrowIllegalAccess();
        }

        public override bool Eof()
        {
            AssertOpen();
            try
            {
                return @in.ReadByte() == -1;
            }
            catch (System.IO.IOException e)
            {
                throw new PuffinBasicRuntimeError(IO_ERROR, $"Failed to read line!, error: {e.Message}");
            }
        }

        public override void Put(int? recordNumber, PuffinBasicSymbolTable symbolTable)
        {
            ThrowIllegalAccess();
        }

        public override void Get(int? recordNumber, PuffinBasicSymbolTable symbolTable)
        {
            ThrowIllegalAccess();
        }

        private void ThrowIllegalAccess()
        {
            throw new PuffinBasicRuntimeError(ILLEGAL_FILE_ACCESS, "Not implemented for SequentialAccessInputFile!");
        }

        public override bool IsOpen()
        {
            return fileState == FileState.OPEN;
        }

        public override void Dispose()
        {
            AssertOpen();
            try
            {
                this.@in.Dispose();
            }
            catch (Exception e)
            {
                throw new PuffinBasicRuntimeError(IO_ERROR, $"Failed to close file '{filename}', error: {e.Message}");
            }

            this.fileState = FileState.CLOSED;
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

