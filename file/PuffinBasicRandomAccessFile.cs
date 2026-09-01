//using Com.Google.Common.Base;
//using It.Unimi.Dsi.Fastutil.Ints;
namespace Org.Puffinbasic.File
{
    using Org.Puffinbasic.Domain;
    using Org.Puffinbasic.Error;
    //using Org.Jetbrains.Annotations;
    //using Java.Io;
    //using Java.Nio;
    //using Java.Util;
    using static Org.Puffinbasic.Domain.STObjects.PuffinBasicAtomTypeId;
    using static Org.Puffinbasic.Error.PuffinBasicRuntimeError.ErrorCode;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using static Org.Puffinbasic.File.IPuffinBasicFile;
    using System.IO;
    using Org.Puffinbasic.Common;
    using Microsoft.Win32.SafeHandles;

    public class PuffinBasicRandomAccessFile : PuffinBasicFile
    {
        private readonly string filename;
        private readonly FileAccessMode accessMode;
        //private readonly System.IO.FileInfo file; // System.IO.RandomAccess?
        //private readonly FileStream file;
        private FileStream file;
        private readonly SafeFileHandle fileHandle;
        private readonly int recordLength;
        private readonly byte[] recordBuffer;
        private List<int> recordParts;
        private long currentFilePosBytes;
        private int lastGetRecordNumber;
        private int lastPutRecordNumber;
        private FileState fileState;
        public PuffinBasicRandomAccessFile(string filename, FileAccessMode accessMode, int recordLen)
        {
            ArgumentNullException.ThrowIfNullOrWhiteSpace(filename);
            if (recordLen  < 0) throw new ArgumentOutOfRangeException(nameof(recordLen));
            if (accessMode == null) throw new ArgumentNullException(nameof(accessMode));

            this.filename = filename.Replace('/', '\\');
            this.accessMode = accessMode;
            this.recordLength = recordLen;
            this.recordBuffer = new byte[recordLength];
            this.lastPutRecordNumber = this.lastGetRecordNumber = -1;
            this.currentFilePosBytes = 0;
            try
            {
                FileAccess fileAccess = accessMode == FileAccessMode.READ_ONLY ? FileAccess.Read : 
                    accessMode == FileAccessMode.READ_WRITE ? FileAccess.ReadWrite : FileAccess.Write;
                this.fileHandle = System.IO.File.OpenHandle(this.filename, FileMode.OpenOrCreate, FileAccess.ReadWrite);
                //this.file = new RandomAccessFile(filename, accessMode.mode);
                //this.file = System.IO.File.Open(filename, FileMode.OpenOrCreate); // TODO: Add conversion for FileAccessMode
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
                var value = entry.GetValue();
                var dataType = entry.GetType().GetAtomTypeId();
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
                return file.Length;
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
                var entry = symbolTable[recordParts.ElementAt(i)].GetValue();
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

                Array.Fill(byteBuffer, (byte)' ');
                Array.Copy(valueBytes, byteBuffer, valueBytes.Length);

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

        // Create a new buffer and fill with spaces.
        // Put first fieldLength bytes only
        // If fieldLength > valueLength, skip fieldLength - valueLength
        // Write the record buffer to file
        public override void Get(int? recordNumber, PuffinBasicSymbolTable symbolTable)
        {
            //throw new NotImplementedException();
            AssertOpen();
            if (accessMode == FileAccessMode.WRITE_ONLY)
            {
                throw new PuffinBasicRuntimeError(ILLEGAL_FILE_ACCESS, $"File {filename} is open for write-only");
            }

            this.lastGetRecordNumber = recordNumber.GetValueOrDefault(this.lastGetRecordNumber + 1);

            SeekToRecord(this.lastGetRecordNumber);

            var recordPartBuffers = new List<Memory<byte>>();

            for (int i = 0; i < recordParts.Count; i++) {
                var entry = symbolTable[recordParts[i]].GetValue();
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
                var entry = symbolTable[recordParts[i]].GetValue();
                entry.SetString(ISOEncoding.GetString(recordPartBuffers[i].ToArray()));
            }

            UpdateCurrentBytePos();
        }

        // Create a new buffer and fill with spaces.
        // Put first fieldLength bytes only
        // If fieldLength > valueLength, skip fieldLength - valueLength
        // Write the record buffer to file
        // Seek to record number and read the record into record buffer
        private BinaryWriter ClearAndGetRecordBuffer()
        {
            Array.Fill(recordBuffer, (byte)' ');
            
            var ms = new MemoryStream(recordBuffer);
            return new BinaryWriter(ms);
        }

        // Create a new buffer and fill with spaces.
        // Put first fieldLength bytes only
        // If fieldLength > valueLength, skip fieldLength - valueLength
        // Write the record buffer to file
        // Seek to record number and read the record into record buffer
        private void UpdateCurrentBytePos()
        {
            currentFilePosBytes += recordLength;
        }

        // Create a new buffer and fill with spaces.
        // Put first fieldLength bytes only
        // If fieldLength > valueLength, skip fieldLength - valueLength
        // Write the record buffer to file
        // Seek to record number and read the record into record buffer
        private long GetRecordBytePos(long recordNumber)
        {
            return recordNumber * recordLength;
        }

        // Create a new buffer and fill with spaces.
        // Put first fieldLength bytes only
        // If fieldLength > valueLength, skip fieldLength - valueLength
        // Write the record buffer to file
        // Seek to record number and read the record into record buffer
        private void SeekToRecord(int recordNumber)
        {
            // TODO: we're only using this method to keep track of currentFilePosBytes, with System.IO.RandomAccess we don't actually need to seek
            // so rework this
            currentFilePosBytes = GetRecordBytePos(recordNumber);
        }

        // Create a new buffer and fill with spaces.
        // Put first fieldLength bytes only
        // If fieldLength > valueLength, skip fieldLength - valueLength
        // Write the record buffer to file
        // Seek to record number and read the record into record buffer
        // Seek only when record number is not sequential
        private void AssertOpen()
        {
            if (!IsOpen())
            {
                throw new PuffinBasicRuntimeError(ILLEGAL_FILE_ACCESS, $"File {filename} is not open!");
            }
        }

        // Create a new buffer and fill with spaces.
        // Put first fieldLength bytes only
        // If fieldLength > valueLength, skip fieldLength - valueLength
        // Write the record buffer to file
        // Seek to record number and read the record into record buffer
        // Seek only when record number is not sequential
        public override bool IsOpen()
        {
            return fileState == FileState.OPEN;
        }

        // Create a new buffer and fill with spaces.
        // Put first fieldLength bytes only
        // If fieldLength > valueLength, skip fieldLength - valueLength
        // Write the record buffer to file
        // Seek to record number and read the record into record buffer
        // Seek only when record number is not sequential
        public override void Dispose()
        {
            AssertOpen();

            this.fileHandle.Dispose();

            this.fileState = FileState.CLOSED;
        }

        // Create a new buffer and fill with spaces.
        // Put first fieldLength bytes only
        // If fieldLength > valueLength, skip fieldLength - valueLength
        // Write the record buffer to file
        // Seek to record number and read the record into record buffer
        // Seek only when record number is not sequential
        public override byte[] ReadBytes(int n)
        {
            throw new PuffinBasicRuntimeError(ILLEGAL_FILE_ACCESS, "Can't read single bytes from RandomAccessFile!");
        }

        // Create a new buffer and fill with spaces.
        // Put first fieldLength bytes only
        // If fieldLength > valueLength, skip fieldLength - valueLength
        // Write the record buffer to file
        // Seek to record number and read the record into record buffer
        // Seek only when record number is not sequential
        public override void Print(string s)
        {
            throw new PuffinBasicRuntimeError(ILLEGAL_FILE_ACCESS, "Not implemented for RandomAccessFile!");
        }

        // Create a new buffer and fill with spaces.
        // Put first fieldLength bytes only
        // If fieldLength > valueLength, skip fieldLength - valueLength
        // Write the record buffer to file
        // Seek to record number and read the record into record buffer
        // Seek only when record number is not sequential
        public override string ReadLine()
        {
            throw new PuffinBasicRuntimeError(ILLEGAL_FILE_ACCESS, "Not implemented for RandomAccessFile!");
        }

        // Create a new buffer and fill with spaces.
        // Put first fieldLength bytes only
        // If fieldLength > valueLength, skip fieldLength - valueLength
        // Write the record buffer to file
        // Seek to record number and read the record into record buffer
        // Seek only when record number is not sequential
        public override void WriteByte(byte b)
        {
            throw new PuffinBasicRuntimeError(ILLEGAL_FILE_ACCESS, "Not implemented for RandomAccessFile!");
        }
    }
}

