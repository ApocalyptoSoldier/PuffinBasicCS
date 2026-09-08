namespace PuffinBasicCS.Runtime
{
    using PuffinBasicCS.Error;
    using static PuffinBasicCS.Domain.STObjects;
    using static PuffinBasicCS.Parser.PuffinBasicIR;
    //using Java.Util.Function;
    using static PuffinBasicCS.Domain.STObjects.PuffinBasicAtomTypeId;
    using static PuffinBasicCS.Domain.STObjects.PuffinBasicTypeId;

    using RuntimeErrorCode = Error.PuffinBasicRuntimeError.ErrorCode;
    using SemanticErrorCode = Error.PuffinBasicSemanticError.ErrorCode;

    using System;

    using PuffinBasicCS.Domain;

    public class Types
    {
        public static void Copy(PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            var fromEntry = symbolTable[instruction.op1];
            var toEntry = symbolTable[instruction.op2];
            toEntry.Value.Assign(fromEntry.Value);
        }

        public static void ParamCopy(PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            var fromEntry = symbolTable[instruction.op1];
            var toEntry = symbolTable[instruction.op2];
            if (toEntry.Type.TypeId == SCALAR)
            {
                toEntry.Value.Assign(fromEntry.Value);
            }
            else if (toEntry.IsLValue())
            {
                ((STLValue)toEntry).SetValue(fromEntry.Value);
            }
            else
            {
                throw new PuffinBasicRuntimeError(RuntimeErrorCode.BAD_FIELD, $"Expected LValue, but found: {toEntry.Type}");
            }
        }

        public static void Varref(PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            var src = symbolTable[instruction.op1];
            var dst = symbolTable[instruction.op2];
            if (dst.IsLValue())
            {
                ((STLValue)dst).SetValue(src.Value);
            }
            else
            {
                throw new PuffinBasicRuntimeError(RuntimeErrorCode.BAD_FIELD, $"Expected LValue, but found: {dst.Type}");
            }
        }

        public static string Unquote(string txt)
        {
            if (String.IsNullOrEmpty(txt))
            {
                return txt;
            }
            else
            {
                if (txt.Length > 1 && txt[0] == '"' && txt[txt.Length - 1] == '"')
                {
                    return txt.Substring(1, txt.Length - 2);
                }
                else
                {
                    return "";
                }
            }
        }

        public static void AssertString(PuffinBasicAtomTypeId dt, string line)
        {
            if (dt != STRING)
            {
                throw new PuffinBasicSemanticError(SemanticErrorCode.DATA_TYPE_MISMATCH, line, $"Expected String type but found: {dt}");
            }
        }

        public static void AssertNumeric(PuffinBasicAtomTypeId dt, string line)
        {
            if (dt == STRING)
            {
                throw new PuffinBasicSemanticError(SemanticErrorCode.DATA_TYPE_MISMATCH, line, "Expected numeric type but found String!");
            }
        }

        public static void AssertIntType(PuffinBasicAtomTypeId dt, string line)
        {
            if (dt != INT32 && dt != INT64)
            {
                throw new PuffinBasicSemanticError(SemanticErrorCode.DATA_TYPE_MISMATCH, line, $"Expected int type but found: {dt}");
            }
        }

        public static void AssertNumeric(PuffinBasicAtomTypeId dt1, PuffinBasicAtomTypeId dt2, string line)
        {
            if (dt1 == STRING || dt2 == STRING)
            {
                throw new PuffinBasicSemanticError(SemanticErrorCode.DATA_TYPE_MISMATCH, line, "Expected numeric type but found String!");
            }
        }

        public static void AssertBothStringOrNumeric(PuffinBasicAtomTypeId dt1, PuffinBasicAtomTypeId dt2, string line)
        {
            if ((dt1 != STRING || dt2 != STRING) && (dt1 == STRING || dt2 == STRING))
            {
                throw new PuffinBasicSemanticError(SemanticErrorCode.DATA_TYPE_MISMATCH, line, $"Expected either both numeric or both string type but found: {dt1} and {dt2}");
            }
        }

        public static PuffinBasicAtomTypeId Upcast(PuffinBasicAtomTypeId dt1, PuffinBasicAtomTypeId dt2, string line)
        {
            AssertNumeric(dt1, dt2, line);
            if (dt1 == PuffinBasicAtomTypeId.DOUBLE || dt2 == PuffinBasicAtomTypeId.DOUBLE)
            {
                return PuffinBasicAtomTypeId.DOUBLE;
            }
            else if (dt1 == PuffinBasicAtomTypeId.INT64 || dt2 == PuffinBasicAtomTypeId.INT64)
            {
                return PuffinBasicAtomTypeId.INT64;
            }
            else if (dt1 == PuffinBasicAtomTypeId.FLOAT || dt2 == PuffinBasicAtomTypeId.FLOAT)
            {
                return PuffinBasicAtomTypeId.FLOAT;
            }
            else
            {
                return PuffinBasicAtomTypeId.INT32;
            }
        }
    }
}

