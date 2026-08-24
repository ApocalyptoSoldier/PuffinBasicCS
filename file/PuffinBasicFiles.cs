using It.Unimi.Dsi.Fastutil.Ints;
using Org.Puffinbasic.Error;
using Org.Puffinbasic.File.PuffinBasicFile;
using Org.Puffinbasic.Error.PuffinBasicRuntimeError.ErrorCode;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;

namespace Org.Puffinbasic.File
{
    public class PuffinBasicFiles
    {
        public readonly IPuffinBasicFile sys;
        private readonly Dictionary<int, IPuffinBasicFile> files;
        public PuffinBasicFiles(IPuffinBasicFile sys)
        {
            this.files = new Int2ObjectOpenHashMap();
            this.sys = sys;
        }

        public virtual IPuffinBasicFile Open(int fileNumber, string filename, FileOpenMode openMode, FileAccessMode accessMode, int recordLen)
        {
            AssertPositiveFileNumber(fileNumber);
            IPuffinBasicFile file;
            if (openMode == FileOpenMode.RANDOM)
            {
                file = new PuffinBasicRandomAccessFile(filename, accessMode, recordLen);
            }
            else if (openMode == FileOpenMode.INPUT)
            {
                file = new PuffinBasicSequentialAccessInputFile(filename);
            }
            else if (openMode == FileOpenMode.OUTPUT)
            {
                file = new PuffinBasicSequentialAccessOutputFile(filename, false);
            }
            else
            {
                file = new PuffinBasicSequentialAccessOutputFile(filename, true);
            }

            var existing = files[fileNumber];
            if (existing != null && existing.IsOpen())
            {
                throw new PuffinBasicRuntimeError(ILLEGAL_FILE_ACCESS, "FileNumber: " + fileNumber + " is already open, cannot open another file: " + filename + " with same file number.");
            }

            files.Put(fileNumber, file);
            return file;
        }

        private void AssertPositiveFileNumber(int fileNumber)
        {
            if (fileNumber < 0)
            {
                throw new PuffinBasicRuntimeError(PuffinBasicRuntimeError.ErrorCode.ILLEGAL_FUNCTION_PARAM, "File number: " + fileNumber + " cannot be negative");
            }
        }

        public virtual IPuffinBasicFile Get(int fileNumber)
        {
            AssertPositiveFileNumber(fileNumber);
            var file = files[fileNumber];
            if (file == null)
            {
                throw new PuffinBasicRuntimeError(ILLEGAL_FILE_ACCESS, "Failed to find file for fileNumber: " + fileNumber);
            }

            return file;
        }

        public virtual void CloseAll()
        {
            foreach (var file in files.Values())
            {
                if (file.IsOpen())
                {
                    file.Dispose();
                }
            }
        }
    }
}

