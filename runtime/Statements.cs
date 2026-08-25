//using It.Unimi.Dsi.Fastutil.Ints;
//using Org.Apache.Commons.Csv;
using Org.Puffinbasic.Domain;
using static Org.Puffinbasic.Domain.STObjects;
using Org.Puffinbasic.Error;
using Org.Puffinbasic.File;
using static Org.Puffinbasic.File.IPuffinBasicFile;
using static Org.Puffinbasic.Parser.PuffinBasicIR;
using static Org.Puffinbasic.Runtime.Formatter;
//using Java.Io;
//using Java.Time;
//using Java.Util;
//using Java.Util.Concurrent;
//using Java.Util.Concurrent.Locks;
using static Org.Puffinbasic.Domain.PuffinBasicSymbolTable;
using static Org.Puffinbasic.Domain.STObjects.PuffinBasicAtomTypeId;
using static Org.Puffinbasic.Error.PuffinBasicRuntimeError.ErrorCode;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;

namespace Org.Puffinbasic.Runtime
{
    public class Statements
    {
        public static void Sleep(PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            int millis = symbolTable[instruction.op1].GetValue().GetInt32();
            if (millis < 0)
            {
                throw new PuffinBasicRuntimeError(DATA_OUT_OF_RANGE, "Sleep time millis cannot be less than 0.");
            }

            LockSupport.ParkNanos(TimeUnit.MILLISECONDS.ToNanos(millis));
        }

        public sealed class ReadData
        {
            private readonly IList<ISTEntry> data;
            private int cursor;
            public ReadData(IList<ISTEntry> data)
            {
                this.data = data;
            }

            public ISTEntry Next()
            {
                if (cursor < data.Count)
                {
                    return data[cursor++];
                }
                else
                {
                    throw new PuffinBasicRuntimeError(OUT_OF_DATA, "Out of data!");
                }
            }

            public void Restore()
            {
                cursor = 0;
            }
        }

        public static void Print(PrintBuffer printBuffer, PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            printBuffer.AppendAtCursor(symbolTable[instruction.op1].GetValue().PrintFormat());
        }

        public static void Write(PrintBuffer printBuffer, PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            printBuffer.AppendAtCursor(symbolTable[instruction.op1].GetValue().WriteFormat());
        }

        public static void Printusing(FormatterCache cache, PrintBuffer printBuffer, PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            var format = symbolTable[instruction.op1].GetValue().GetString();
            var formatter = cache[format];
            var entry = symbolTable[instruction.op2];
            var value = entry.GetValue();
            string result;
            switch (entry.GetType().GetAtomTypeId())
            {
                case INT32:
                case INT64:
                    if (!formatter.SupportsNumeric())
                    {
                        throw new PuffinBasicRuntimeError(DATA_TYPE_MISMATCH, "String formatter doesn't work with numeric type: " + format);
                    }

                    result = formatter.Format(value.GetInt64()) + " ";
                    break;
                case FLOAT:
                case DOUBLE:
                    if (!formatter.SupportsNumeric())
                    {
                        throw new PuffinBasicRuntimeError(DATA_TYPE_MISMATCH, "String formatter doesn't work with numeric type: " + format);
                    }

                    result = formatter.Format(value.GetFloat64()) + " ";
                    break;
                case STRING:
                    if (!formatter.SupportsString())
                    {
                        throw new PuffinBasicRuntimeError(DATA_TYPE_MISMATCH, "Numeric formatter doesn't work with string type: " + format);
                    }

                    result = formatter.Format(value.GetString());
                    break;
                default:
                    throw new PuffinBasicInternalError("Unsupported data type: " + entry.GetType().GetAtomTypeId());
                    break;
            }

            printBuffer.AppendAtCursor(result);
        }

        public static void Flush(PuffinBasicFiles files, PrintBuffer printBuffer, PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            if (instruction.op1 == NULL_ID)
            {
                printBuffer.Flush(files.sys);
            }
            else
            {
                var fileNumber = symbolTable[instruction.op1].GetValue().GetInt32();
                printBuffer.Flush(files[fileNumber]);
            }
        }

        public static void Swap(PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            var op1Entry = symbolTable[instruction.op1];
            var op1 = op1Entry.GetValue();
            var op2Entry = symbolTable[instruction.op2];
            var op2 = op2Entry.GetValue();
            var dt1 = op1Entry.GetType().GetAtomTypeId();
            var dt2 = op2Entry.GetType().GetAtomTypeId();
            if (dt1 == STRING && dt2 == STRING)
            {
                var tmp = op1.GetString();
                op1.SetString(op2.GetString());
                op2.SetString(tmp);
            }
            else
            {
                if (dt1 == DOUBLE || dt2 == DOUBLE)
                {
                    var tmp = op1.GetFloat64();
                    op1.SetFloat64(op2.GetFloat64());
                    op2.SetFloat64(tmp);
                }
                else if (dt1 == INT64 || dt2 == INT64)
                {
                    var tmp = op1.GetInt64();
                    op1.SetInt64(op2.GetInt64());
                    op2.SetInt64(tmp);
                }
                else if (dt1 == FLOAT || dt2 == FLOAT)
                {
                    var tmp = op1.GetFloat32();
                    op1.SetFloat32(op2.GetFloat32());
                    op2.SetFloat32(tmp);
                }
                else
                {
                    var tmp = op1.GetInt32();
                    op1.SetInt32(op2.GetInt32());
                    op2.SetInt32(tmp);
                }
            }
        }

        public static void Lset(PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            var destEntry = symbolTable[instruction.op1].GetValue();
            var value = symbolTable[instruction.op2].GetValue().GetString();
            var valLen = value.Length();
            var destLen = destEntry.GetFieldLength();
            if (destLen == 0)
            {
                destLen = destEntry.GetString().Length();
                destEntry.SetFieldLength(destLen);
            }

            string result;
            if (valLen > destLen)
            {
                result = value.Substring(0, destLen);
            }
            else if (valLen == destLen)
            {
                result = value;
            }
            else
            {
                byte[] bytes = new byte[destLen];
                Array.Copy(value.GetBytes(), 0, bytes, 0, valLen);
                java.util.Arrays.Fill(bytes, valLen, destLen, (byte)' ');
                result = new string (bytes);
            }

            destEntry.SetString(result);
        }

        public static void Rset(PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            var destEntry = symbolTable[instruction.op1].GetValue();
            var value = symbolTable[instruction.op2].GetValue().GetString();
            var valLen = value.Length();
            var destLen = destEntry.GetFieldLength();
            if (destLen == 0)
            {
                destLen = destEntry.GetString().Length;
                destEntry.SetFieldLength(destLen);
            }

            string result;
            if (valLen > destLen)
            {
                result = value.Substring(0, destLen);
            }
            else if (valLen == destLen)
            {
                result = value;
            }
            else
            {
                byte[] bytes = new byte[destLen];
                int offset = destLen - valLen;
                Arrays.Fill(bytes, 0, offset, (byte)' ');
                Array.Copy(value.GetBytes(), 0, bytes, offset, valLen);
                result = new string (bytes);
            }

            destEntry.SetString(result);
        }

        public static void Open(PuffinBasicFiles files, PuffinBasicSymbolTable symbolTable, Instruction instr_fn_fn_0, Instruction instr_om_am_1, Instruction instr_lm_rl_2)
        {
            var fileName = symbolTable[instr_fn_fn_0.op1].GetValue().GetString();
            var fileNumber = symbolTable[instr_fn_fn_0.op2].GetValue().GetInt32();
            var fileOpenMode = FileOpenMode.ValueOf(symbolTable[instr_om_am_1.op1].GetValue().GetString());
            var fileAccessMode = FileAccessMode.ValueOf(symbolTable[instr_om_am_1.op2].GetValue().GetString());
            var fileLockMode = LockMode.ValueOf(symbolTable[instr_lm_rl_2.op1].GetValue().GetString());
            var recordLen = symbolTable[instr_lm_rl_2.op2].GetValue().GetInt32();
            files.Open(fileNumber, fileName, fileOpenMode, fileAccessMode, recordLen);
        }

        public static void CloseAll(PuffinBasicFiles files)
        {
            files.CloseAll();
        }

        public static void Dispose(PuffinBasicFiles files, PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            var fileNumber = symbolTable[instruction.op1].GetValue().GetInt32();
            files[fileNumber].Dispose();
        }

        public static void Field(PuffinBasicFiles files, PuffinBasicSymbolTable symbolTable, IList<Instruction> fields, Instruction instruction)
        {
            var varList = new List<int>(fields.Count);
            foreach (var instrI in fields)
            {
                var recordPartLen = symbolTable[instrI.op2].GetValue().GetInt32();
                symbolTable[instrI.op1].GetValue().SetFieldLength(recordPartLen);
                varList.Add(instrI.op1);
            }

            var fileNumber = symbolTable[instruction.op1].GetValue().GetInt32();
            files[fileNumber].SetFieldParams(symbolTable, varList);
        }

        public static void Putf(PuffinBasicFiles files, PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            var fileNumber = symbolTable[instruction.op1].GetValue().GetInt32();
            int recordNumber = instruction.op2 == NULL_ID ? null : symbolTable[instruction.op2].GetValue().GetInt32();
            files[fileNumber].Put(recordNumber, symbolTable);
        }

        public static void Getf(PuffinBasicFiles files, PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            var fileNumber = symbolTable[instruction.op1].GetValue().GetInt32();
            int recordNumber = instruction.op2 == NULL_ID ? null : symbolTable[instruction.op2].GetValue().GetInt32();
            files[fileNumber].Get(recordNumber, symbolTable);
        }

        public static void Randomize(Random random, PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            var seed = symbolTable[instruction.op1].GetValue().GetInt64();
            random.SetSeed(seed);
        }

        public static void RandomizeTimer(Random random)
        {
            var seed = DateTime.Now().GetEpochSecond();
            random.SetSeed(seed);
        }

        public static void Input(PuffinBasicFiles files, PuffinBasicSymbolTable symbolTable, IList<Instruction> instructions, Instruction instruction)
        {
            bool printPrompt = false;
            if (instruction.op1 != NULL_ID)
            {
                var prompt = symbolTable[instruction.op1].GetValue().GetString();
                files.sys.Print(prompt);
                printPrompt = true;
            }

            IPuffinBasicFile file;
            if (instruction.op2 != NULL_ID)
            {
                var fileNumber = symbolTable[instruction.op2].GetValue().GetInt32();
                file = files[fileNumber];
            }
            else
            {
                file = files.sys;
            }

            CSVRecord record = null;
            bool retry = false;
            do
            {
                if (retry)
                {
                    if (printPrompt)
                    {
                        Console.Error.WriteLine("?Redo from start");
                    }
                    else
                    {
                        throw new PuffinBasicRuntimeError(IO_ERROR, "Record mismatch: expected=" + instructions.Count + ", found in file=" + record.Count + ", record: " + record);
                    }
                }

                CSVParser parser;
                try
                {
                    parser = CSVParser.Parse(file.ReadLine(), CSVFormat.DEFAULT);
                }
                catch (System.IO.IOException e)
                {
                    throw new PuffinBasicRuntimeError(IO_ERROR, "Failed to read inputs, error: " + e.Message);
                }

                record = parser.Iterator().Next();
                retry = true;
            }
            while (record.Count != instructions.Count);
            int i = 0;
            foreach (var instr0 in instructions)
            {
                var entry = symbolTable[instr0.op1];
                var value = entry.GetValue();
                switch (entry.GetType().GetAtomTypeId())
                {
                    case INT32:
                        value.SetInt32(int.Parse(record[i].Trim()));
                        break;
                    case INT64:
                        value.SetInt64(long.Parse(record[i].Trim()));
                        break;
                    case FLOAT:
                        value.SetFloat32(float.Parse(record[i].Trim()));
                        break;
                    case DOUBLE:
                        value.SetFloat64(Double.Parse(record[i].Trim()));
                        break;
                    case STRING:
                        value.SetString(record[i].Trim());
                        break;
                }

                ++i;
            }
        }

        public static void Lineinput(PuffinBasicFiles files, PuffinBasicSymbolTable symbolTable, Instruction instr0, Instruction instruction)
        {
            if (instruction.op1 != NULL_ID)
            {
                var prompt = symbolTable[instruction.op1].GetValue().GetString();
                if (!prompt.IsEmpty())
                {
                    files.sys.Print(prompt);
                }
            }

            IPuffinBasicFile file;
            if (instruction.op2 != NULL_ID)
            {
                var fileNumber = symbolTable[instruction.op2].GetValue().GetInt32();
                file = files[fileNumber];
            }
            else
            {
                file = files.sys;
            }

            symbolTable[instr0.op1].GetValue().SetString(file.ReadLine());
        }

        public static void Middlr(PuffinBasicSymbolTable symbolTable, Instruction instr0, Instruction instr)
        {
            var dest = symbolTable[instr0.op1].GetValue();
            var n = symbolTable[instr0.op2].GetValue().GetInt32();
            var m = symbolTable[instr.op1].GetValue().GetInt32();
            var replacement = symbolTable[instr.op2].GetValue().GetString();
            string varValue = dest.GetString();
            var varlen = varValue.Length;
            string result;
            if (n <= 0)
            {
                throw new PuffinBasicRuntimeError(INDEX_OUT_OF_BOUNDS, "INSTR: expected n > 0, actual=" + n);
            }
            else if (n > varlen)
            {
                result = varValue;
            }
            else
            {
                if (m == -1 || m > replacement.Length())
                {
                    m = replacement.Length();
                }

                result = varValue.Substring(0, n - 1) + replacement.Substring(0, Math.Min(m, varlen - n + 1)) + varValue.Substring(Math.Min(n + m - 1, varlen - 1));
            }

            dest.SetString(result);
        }

        public static void Read(ReadData readData, PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            var variable = symbolTable.GetVariable(instruction.op1);
            var data = readData.Next();
            Types.AssertBothStringOrNumeric(variable.GetType().GetAtomTypeId(), data.GetType().GetAtomTypeId(), () => "Read Data mismatch for variable: " + variable + " and data: " + data.GetValue().PrintFormat());
            variable.GetValue().Assign(data.GetValue());
        }

        public static void CreateInstance(PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            var entry = (STVariable)symbolTable[instruction.op1];
            entry.CreateAndSetInstance(symbolTable);
        }

        public static void StructLValue(PuffinBasicSymbolTable symbolTable, IList<Instruction> @params, Instruction instruction)
        {
            var root = (STObjects.STStruct)symbolTable[instruction.op1].GetValue();
            for (int i = 0; i < @params.Count - 1; i++)
            {
                var localChildId = symbolTable[@params[i].op1].GetValue().GetInt32();
                var localValueId = root.GetMember(localChildId);
                root = (STObjects.STStruct)symbolTable[localValueId].GetValue();
            }

            var childId = symbolTable[@params[@params.Count - 1].op1].GetValue().GetInt32();
            var valueId = root.GetMember(childId);
            ((STRef)symbolTable[instruction.result]).SetRef(symbolTable[valueId]);
        }

        public static void MemberFuncCall(PuffinBasicSymbolTable symbolTable, IList<Instruction> @params, Instruction instruction)
        {
            STValue[] funcParams = (STValue[])(new ISTValue[@params.Count]);
            var @object = symbolTable[instruction.op1].GetValue();
            var funcName = symbolTable[instruction.op2].GetValue().GetString();
            ISTValue result = symbolTable[instruction.result].GetValue();
            for (int i = 0; i < @params.Count; i++)
            {
                funcParams[i] = symbolTable[@params[i].op1].GetValue();
            }

            @object.Call(funcName, funcParams, result);
        }

        public static void StructMemberRef(PuffinBasicSymbolTable symbolTable, IList<Instruction> @params, Instruction instruction)
        {
            var root = (STObjects.STStruct)symbolTable[instruction.op1].GetValue();
            for (int i = 0; i < @params.Count - 1; i++)
            {
                var localChildId = symbolTable[@params[i].op1].GetValue().GetInt32();
                var localValueId = root.GetMember(localChildId);
                root = (STObjects.STStruct)symbolTable[localValueId].GetValue();
            }

            var childId = symbolTable[@params[@params.Count - 1].op1].GetValue().GetInt32();
            var valueId = root.GetMember(childId);
            symbolTable[instruction.result].GetValue().Assign(symbolTable[valueId].GetValue());
        }
    }
}

