using Org.Puffinbasic.Domain;
using static Org.Puffinbasic.Domain.STObjects;
using Org.Puffinbasic.Error;
using Org.Puffinbasic.Parser;
using static Org.Puffinbasic.Parser.PuffinBasicIR;
//using Java.Util.Function;
using static Org.Puffinbasic.Domain.STObjects.PuffinBasicAtomTypeId;
using static Org.Puffinbasic.Domain.STObjects.PuffinBasicTypeId;
using RuntimeErrorCode = Org.Puffinbasic.Error.PuffinBasicRuntimeError.ErrorCode;
using SemanticErrorCode = Org.Puffinbasic.Error.PuffinBasicSemanticError.ErrorCode;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;

namespace Org.Puffinbasic.Runtime
{
    public class Types
    {
        public static void Copy(PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            var fromEntry = symbolTable[instruction.op1];
            var toEntry = symbolTable[instruction.op2];
            toEntry.GetValue().Assign(fromEntry.GetValue());
        }

        public static void ParamCopy(PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            var fromEntry = symbolTable[instruction.op1];
            var toEntry = symbolTable[instruction.op2];
            if (toEntry.GetType().GetTypeId() == SCALAR)
            {
                toEntry.GetValue().Assign(fromEntry.GetValue());
            }
            else if (toEntry.IsLValue())
            {
                ((STLValue)toEntry).SetValue(fromEntry.GetValue());
            }
            else
            {
                throw new PuffinBasicRuntimeError(RuntimeErrorCode.BAD_FIELD, "Expected LValue, but found: " + toEntry.GetType());
            }
        }

        public static void Varref(PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            var src = symbolTable[instruction.op1];
            var dst = symbolTable[instruction.op2];
            if (dst.IsLValue())
            {
                ((STLValue)dst).SetValue(src.GetValue());
            }
            else
            {
                throw new PuffinBasicRuntimeError(RuntimeErrorCode.BAD_FIELD, "Expected LValue, but found: " + dst.GetType());
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
                    return txt.Substring(1, txt.Length - 1);
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
                throw new PuffinBasicSemanticError(SemanticErrorCode.DATA_TYPE_MISMATCH, line, "Expected String type but found: " + dt);
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
                throw new PuffinBasicSemanticError(SemanticErrorCode.DATA_TYPE_MISMATCH, line, "Expected int type but found: " + dt);
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
                throw new PuffinBasicSemanticError(SemanticErrorCode.DATA_TYPE_MISMATCH, line, "Expected either both numeric or both string type but found: " + dt1 + " and " + dt2);
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

