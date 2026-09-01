//using It.Unimi.Dsi.Fastutil.Ints;
namespace Org.Puffinbasic.File
{
    using Org.Puffinbasic.Error;

    using static Org.Puffinbasic.File.IPuffinBasicFile;
    using static Org.Puffinbasic.Error.PuffinBasicRuntimeError.ErrorCode;

    using System.Collections.Generic;

    public class PuffinBasicFiles
    {
        public readonly IPuffinBasicFile sys;
        private readonly Dictionary<int, IPuffinBasicFile> files;
        public PuffinBasicFiles(IPuffinBasicFile sys)
        {
            this.files = new Dictionary<int, IPuffinBasicFile>();
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

            if (files.TryGetValue(fileNumber, out var existing) && existing.IsOpen()) {
                throw new PuffinBasicRuntimeError(ILLEGAL_FILE_ACCESS, $"FileNumber: {fileNumber} is already open, cannot open another file: {filename} with same file number.");
            }

            files[fileNumber] = file;
            return file;
        }

        private void AssertPositiveFileNumber(int fileNumber)
        {
            if (fileNumber < 0)
            {
                throw new PuffinBasicRuntimeError(PuffinBasicRuntimeError.ErrorCode.ILLEGAL_FUNCTION_PARAM, $"File number: {fileNumber} cannot be negative");
            }
        }

        public virtual IPuffinBasicFile Get(int fileNumber)
        {
            AssertPositiveFileNumber(fileNumber);
            if (files.TryGetValue(fileNumber, out var file))
                return file;
            
            throw new PuffinBasicRuntimeError(ILLEGAL_FILE_ACCESS, $"Failed to find file for fileNumber: {fileNumber}");
        }

        public IPuffinBasicFile this[int fileNumber] => Get(fileNumber);
        public virtual void CloseAll()
        {
            foreach (var file in files.Values)
            {
                if (file.IsOpen())
                {
                    file.Dispose();
                }
            }
        }
    }
}

