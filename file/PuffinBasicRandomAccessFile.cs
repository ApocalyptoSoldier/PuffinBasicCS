//using Com.Google.Common.Base;
//using It.Unimi.Dsi.Fastutil.Ints;
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
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using static Org.Puffinbasic.File.IPuffinBasicFile;

namespace Org.Puffinbasic.File
{
    public class PuffinBasicRandomAccessFile : IPuffinBasicFile
    {
        private readonly string filename;
        private readonly FileAccessMode accessMode;
        private readonly System.IO.FileInfo file; // System.IO.RandomAccess?
        private readonly int recordLength;
        private readonly byte[] recordBuffer;
        private List<int> recordParts;
        private long currentFilePosBytes;
        private int lastGetRecordNumber;
        private int lastPutRecordNumber;
        private FileState fileState;
        public PuffinBasicRandomAccessFile(string filename, FileAccessMode accessMode, int recordLen)
        {
            if (String.IsNullOrWhiteSpace(filename)) throw new ArgumentNullException("filename");
            if (recordLen  < 0) throw new ArgumentOutOfRangeException(nameof(recordLen));
            if (accessMode == null) throw new ArgumentNullException(nameof(accessMode));

            this.filename = filename;
            this.accessMode = accessMode;
            this.recordLength = recordLen;
            this.recordBuffer = new byte[recordLength];
            this.lastPutRecordNumber = this.lastGetRecordNumber = -1;
            this.currentFilePosBytes = 0;
            try
            {
                this.file = new RandomAccessFile(filename, accessMode.mode);
            }
            catch (System.IO.FileNotFoundException e)
            {
                throw new PuffinBasicRuntimeError(IO_ERROR, "Failed to open file '" + filename + "' for writing, error: " + e.Message);
            }

            this.fileState = FileState.OPEN;
        }

        public virtual void SetFieldParams(PuffinBasicSymbolTable symbolTable, List<int> recordParts)
        {
            if (symbolTable == null) throw new ArgumentNullException(nameof(symbolTable));
            if (recordParts == null) throw new ArgumentNullException(nameof(recordParts));

            int totalComputedLength = 0;
            foreach (var recordPart in recordParts)
            {
                var entry = symbolTable[recordPart];
                var value = entry.GetValue();
                var dataType = entry.GetType().GetAtomTypeId();
                if (dataType != STRING)
                {
                    throw new PuffinBasicInternalError("Expected String recordPart but found: " + dataType);
                }

                totalComputedLength += value.GetFieldLength();
            }

            if (totalComputedLength != recordLength)
            {
                throw new PuffinBasicInternalError("Sum of capacity of recordParts (=" + totalComputedLength + ") don't match recordLength (=" + recordLength + ")");
            }

            this.recordParts = recordParts;
        }

        public virtual int GetCurrentRecordNumber()
        {
            AssertOpen();
            return (int)(currentFilePosBytes / recordLength);
        }

        public virtual long GetFileSizeInBytes()
        {
            AssertOpen();
            try
            {
                return file.Length();
            }
            catch (System.IO.IOException e)
            {
                throw new PuffinBasicRuntimeError(IO_ERROR, "Failed to get length of the file '" + filename + "', error: " + e.Message);
            }
        }

        public virtual bool Eof()
        {
            return currentFilePosBytes >= GetFileSizeInBytes();
        }

        public virtual void Put(int recordNumber, PuffinBasicSymbolTable symbolTable)
        {
            AssertOpen();
            if (accessMode == FileAccessMode.READ_ONLY)
            {
                throw new PuffinBasicRuntimeError(ILLEGAL_FILE_ACCESS, "File " + filename + " is open for read-only");
            }

            if (recordNumber == null)
            {
                recordNumber = lastPutRecordNumber + 1;
            }

            SeekToRecord(recordNumber);
            this.lastPutRecordNumber = recordNumber;

            // Create a new buffer and fill with spaces.
            var bb = ClearAndGetRecordBuffer();
            for (int i = 0; i < recordParts.Count; i++)
            {
                var entry = symbolTable[recordParts.ElementAt(i)].GetValue();
                var value = entry.GetString();
                var valueLength = value.Length();
                var fieldLength = entry.GetFieldLength();

                // Put first fieldLength bytes only
                if (fieldLength < valueLength)
                {
                    value = value.Substring(0, fieldLength);
                }

                bb.Put(value.GetBytes());

                // If fieldLength > valueLength, skip fieldLength - valueLength
                if (fieldLength > valueLength)
                {
                    bb.Position(bb.Position() + fieldLength - valueLength);
                }
            }


            // Write the record buffer to file
            try
            {
                file.Write(recordBuffer);
            }
            catch (System.IO.IOException e)
            {
                throw new PuffinBasicRuntimeError(IO_ERROR, "Failed to write to file '" + filename + "', error: " + e.Message);
            }

            UpdateCurrentBytePos();
        }

        // Create a new buffer and fill with spaces.
        // Put first fieldLength bytes only
        // If fieldLength > valueLength, skip fieldLength - valueLength
        // Write the record buffer to file
        public virtual void Get(int recordNumber, PuffinBasicSymbolTable symbolTable)
        {
            AssertOpen();
            if (accessMode == FileAccessMode.WRITE_ONLY)
            {
                throw new PuffinBasicRuntimeError(ILLEGAL_FILE_ACCESS, "File " + filename + " is open for write-only");
            }

            if (recordNumber == null)
            {
                recordNumber = lastGetRecordNumber + 1;
            }

            SeekToRecord(recordNumber);
            this.lastGetRecordNumber = recordNumber;

            // Seek to record number and read the record into record buffer
            try
            {
                file.ReadFully(recordBuffer);
            }
            catch (System.IO.IOException e)
            {
                throw new PuffinBasicRuntimeError(IO_ERROR, "Failed to read from file '" + filename + ", recordNumber: " + recordNumber + "', error: " + e.Message);
            }

            var bb = ByteBuffer.Wrap(recordBuffer);
            for (int i = 0; i < recordParts.Count; i++)
            {
                var entry = symbolTable[recordParts.ElementAt(i)].GetValue();
                var fieldLength = entry.GetFieldLength();
                var strBytes = new byte[fieldLength];
                bb[strBytes];
                entry.SetString(new string (strBytes));
            }

            UpdateCurrentBytePos();
        }

        // Create a new buffer and fill with spaces.
        // Put first fieldLength bytes only
        // If fieldLength > valueLength, skip fieldLength - valueLength
        // Write the record buffer to file
        // Seek to record number and read the record into record buffer
        private ByteBuffer ClearAndGetRecordBuffer()
        {
            Arrays.Fill(recordBuffer, 0, recordBuffer.Length, (byte)' ');
            return ByteBuffer.Wrap(recordBuffer);
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

            // Seek only when record number is not sequential
            try
            {
                long destPosBytes = GetRecordBytePos(recordNumber);
                if (destPosBytes != currentFilePosBytes)
                {
                    file.Seek(destPosBytes);
                    currentFilePosBytes = destPosBytes;
                }
            }
            catch (System.IO.IOException e)
            {
                throw new PuffinBasicRuntimeError(IO_ERROR, "Failed to read from file '" + filename + "', error: " + e.Message);
            }
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
                throw new PuffinBasicRuntimeError(ILLEGAL_FILE_ACCESS, "File " + filename + " is not open!");
            }
        }

        // Create a new buffer and fill with spaces.
        // Put first fieldLength bytes only
        // If fieldLength > valueLength, skip fieldLength - valueLength
        // Write the record buffer to file
        // Seek to record number and read the record into record buffer
        // Seek only when record number is not sequential
        public virtual bool IsOpen()
        {
            return fileState == FileState.OPEN;
        }

        // Create a new buffer and fill with spaces.
        // Put first fieldLength bytes only
        // If fieldLength > valueLength, skip fieldLength - valueLength
        // Write the record buffer to file
        // Seek to record number and read the record into record buffer
        // Seek only when record number is not sequential
        public virtual void Dispose()
        {
            AssertOpen();
            try
            {
                this.file.Dispose();
            }
            catch (Exception e)
            {
                throw new PuffinBasicRuntimeError(IO_ERROR, "Failed to close file '" + filename + "', error: " + e.Message);
            }

            this.fileState = FileState.CLOSED;
        }

        // Create a new buffer and fill with spaces.
        // Put first fieldLength bytes only
        // If fieldLength > valueLength, skip fieldLength - valueLength
        // Write the record buffer to file
        // Seek to record number and read the record into record buffer
        // Seek only when record number is not sequential
        public virtual byte[] ReadBytes(int n)
        {
            throw new PuffinBasicRuntimeError(ILLEGAL_FILE_ACCESS, "Can't read single bytes from RandomAccessFile!");
        }

        // Create a new buffer and fill with spaces.
        // Put first fieldLength bytes only
        // If fieldLength > valueLength, skip fieldLength - valueLength
        // Write the record buffer to file
        // Seek to record number and read the record into record buffer
        // Seek only when record number is not sequential
        public virtual void Print(string s)
        {
            throw new PuffinBasicRuntimeError(ILLEGAL_FILE_ACCESS, "Not implemented for RandomAccessFile!");
        }

        // Create a new buffer and fill with spaces.
        // Put first fieldLength bytes only
        // If fieldLength > valueLength, skip fieldLength - valueLength
        // Write the record buffer to file
        // Seek to record number and read the record into record buffer
        // Seek only when record number is not sequential
        public virtual string ReadLine()
        {
            throw new PuffinBasicRuntimeError(ILLEGAL_FILE_ACCESS, "Not implemented for RandomAccessFile!");
        }

        // Create a new buffer and fill with spaces.
        // Put first fieldLength bytes only
        // If fieldLength > valueLength, skip fieldLength - valueLength
        // Write the record buffer to file
        // Seek to record number and read the record into record buffer
        // Seek only when record number is not sequential
        public virtual void WriteByte(byte b)
        {
            throw new PuffinBasicRuntimeError(ILLEGAL_FILE_ACCESS, "Not implemented for RandomAccessFile!");
        }
    }
}

