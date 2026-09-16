namespace PuffinBasicCS.File
{
    using PuffinBasicCS.Error;
    using static PuffinBasicCS.Domain.STObjects.PuffinBasicAtomTypeId;
    using static PuffinBasicCS.Error.PuffinBasicRuntimeError.ErrorCode;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using static PuffinBasicCS.File.IPuffinBasicFile;
    using System.IO;
    using Microsoft.Win32.SafeHandles;
    using PuffinBasicCS.Domain;
    using PuffinBasicCS.Common;

    public class PuffinBasicRandomAccessFile : PuffinBasicFile
    {
        private readonly string filename;
        private readonly FileAccessMode accessMode;
        private readonly SafeFileHandle fileHandle;
        private readonly int recordLength;
        private List<int> recordParts;
        private long currentFilePosBytes = 0;
        private int lastGetRecordNumber = 0;
        private int lastPutRecordNumber = -1;
        private FileState fileState;
        public PuffinBasicRandomAccessFile(string filename, FileAccessMode accessMode, int recordLen)
        {
            ArgumentNullException.ThrowIfNullOrWhiteSpace(filename);
            if (recordLen  < 0) throw new ArgumentOutOfRangeException(nameof(recordLen));
            if (accessMode == null) throw new ArgumentNullException(nameof(accessMode));

            this.filename = filename.Replace('/', '\\');
            this.accessMode = accessMode;
            this.recordLength = recordLen;

            try
            {
                FileAccess fileAccess = accessMode == FileAccessMode.READ_ONLY ? FileAccess.Read : 
                    accessMode == FileAccessMode.READ_WRITE ? FileAccess.ReadWrite : FileAccess.Write;
                this.fileHandle = System.IO.File.OpenHandle(this.filename, FileMode.OpenOrCreate, FileAccess.ReadWrite);
            }
            catch (System.IO.FileNotFoundException e)
            {
                throw new PuffinBasicRuntimeError(IO_ERROR, $"Failed to open file '{filename}' for writing, error: {e.Message}");
            }

            this.fileState = FileState.OPEN;
        }

        public override void SetFieldParams(PuffinBasicSymbolTable symbolTable, List<int> recordParts)
        {
            ArgumentNullException.ThrowIfNull(symbolTable);
            ArgumentNullException.ThrowIfNull(recordParts);

            int totalComputedLength = 0;
            foreach (var recordPart in recordParts)
            {
                var entry = symbolTable[recordPart];
                var value = entry.Value;
                var dataType = entry.Type.AtomTypeId;
                if (dataType != STRING)
                {
                    throw new PuffinBasicInternalError($"Expected String recordPart but found: {dataType}");
                }

                totalComputedLength += value.GetFieldLength();
            }

            if (totalComputedLength != recordLength)
            {
                throw new PuffinBasicInternalError($"Sum of capacity of recordParts (={totalComputedLength}) don't match recordLength (={recordLength})");
            }

            this.recordParts = recordParts;
        }

        public override int GetCurrentRecordNumber()
        {
            AssertOpen();
            return (int)(currentFilePosBytes / recordLength);
        }

        public override long GetFileSizeInBytes()
        {
            AssertOpen();
            try
            {
                return RandomAccess.GetLength(fileHandle);
            }
            catch (System.IO.IOException e)
            {
                throw new PuffinBasicRuntimeError(IO_ERROR, $"Failed to get length of the file '{filename}', error: {e.Message}");
            }
        }

        public override bool Eof()
        {
            return currentFilePosBytes >= GetFileSizeInBytes();
        }

        public override void Put(int? recordNumber, PuffinBasicSymbolTable symbolTable)
        {
            AssertOpen();
            if (accessMode == FileAccessMode.READ_ONLY)
            {
                throw new PuffinBasicRuntimeError(ILLEGAL_FILE_ACCESS, $"File {filename} is open for read-only");
            }

            this.lastPutRecordNumber = recordNumber.GetValueOrDefault(lastPutRecordNumber + 1);
            SeekToRecord(this.lastPutRecordNumber);

            var pos = GetRecordBytePos(this.lastPutRecordNumber);

            List <ReadOnlyMemory<byte>> recordBufferParts = new List<ReadOnlyMemory<byte>>();

            for (int i = 0; i < recordParts.Count; i++)
            {
                var entry = symbolTable[recordParts.ElementAt(i)].Value;
                var value = entry.GetString();
                var valueLength = value.Length;
                var fieldLength = entry.GetFieldLength();

                // Put first fieldLength bytes only
                if (fieldLength < valueLength)
                    value = value.Substring(0, fieldLength);

                // TODO: figure out if there's a better way to do this
                value = value.Replace('\0', ' ');

                var valueBytes = ISOEncoding.GetBytes(value);

                var byteBuffer = new byte[fieldLength];

                Array.Copy(valueBytes, byteBuffer, valueBytes.Length);
                // Fill the remaining slots with ' '
                if (fieldLength > valueLength)
                    Array.Fill(byteBuffer, (byte)' ', valueBytes.Length, fieldLength - valueLength);

                recordBufferParts.Add(new ReadOnlyMemory<byte>(byteBuffer));
            }

            // Write the record buffer to file
            try
            {
                RandomAccess.Write(fileHandle, recordBufferParts, pos);
            }
            catch (System.IO.IOException e)
            {
                throw new PuffinBasicRuntimeError(IO_ERROR, $"Failed to write to file '{filename}', error: {e.Message}");
            }

            UpdateCurrentBytePos();
        }

        public override void Get(int? recordNumber, PuffinBasicSymbolTable symbolTable)
        {
            AssertOpen();
            if (accessMode == FileAccessMode.WRITE_ONLY)
            {
                throw new PuffinBasicRuntimeError(ILLEGAL_FILE_ACCESS, $"File {filename} is open for write-only");
            }

            this.lastGetRecordNumber = recordNumber.GetValueOrDefault(this.lastGetRecordNumber + 1);

            SeekToRecord(this.lastGetRecordNumber);

            var recordPartBuffers = new List<Memory<byte>>();

            for (int i = 0; i < recordParts.Count; i++) {
                var entry = symbolTable[recordParts[i]].Value;
                recordPartBuffers.Add(new Memory<byte>(new byte[entry.GetFieldLength()]));
            }

            try
            {
                RandomAccess.Read(fileHandle, recordPartBuffers, currentFilePosBytes);
            }
            catch (System.IO.IOException e)
            {
                throw new PuffinBasicRuntimeError(IO_ERROR, $"Failed to read from file '{filename}, recordNumber: {recordNumber}', error: {e.Message}");
            }

            for (int i = 0; i != recordParts.Count; i++) {
                var entry = symbolTable[recordParts[i]].Value;
                entry.SetString(ISOEncoding.GetString(recordPartBuffers[i].ToArray()));
            }

            UpdateCurrentBytePos();
        }

        private void UpdateCurrentBytePos()
        {
            currentFilePosBytes += recordLength;
        }

        private long GetRecordBytePos(long recordNumber)
        {
            return recordNumber * recordLength;
        }

        private void SeekToRecord(int recordNumber)
        {
            // TODO: we're only using this method to keep track of currentFilePosBytes, with System.IO.RandomAccess we don't actually need to seek
            // so rework this
            currentFilePosBytes = GetRecordBytePos(recordNumber);
        }

        private void AssertOpen()
        {
            if (!IsOpen())
            {
                throw new PuffinBasicRuntimeError(ILLEGAL_FILE_ACCESS, $"File {filename} is not open!");
            }
        }

        public override bool IsOpen()
        {
            return fileState == FileState.OPEN;
        }

        public override void Dispose()
        {
            AssertOpen();

            this.fileHandle.Dispose();

            this.fileState = FileState.CLOSED;
        }

        public override byte[] ReadBytes(int n)
        {
            throw new PuffinBasicRuntimeError(ILLEGAL_FILE_ACCESS, "Can't read single bytes from RandomAccessFile!");
        }

        public override void Print(string s)
        {
            throw new PuffinBasicRuntimeError(ILLEGAL_FILE_ACCESS, "Not implemented for RandomAccessFile!");
        }

        public override string ReadLine()
        {
            throw new PuffinBasicRuntimeError(ILLEGAL_FILE_ACCESS, "Not implemented for RandomAccessFile!");
        }

        public override void WriteByte(byte b)
        {
            throw new PuffinBasicRuntimeError(ILLEGAL_FILE_ACCESS, "Not implemented for RandomAccessFile!");
        }
    }
}

