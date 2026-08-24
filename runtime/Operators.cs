using Org.Puffinbasic.Domain;
using Org.Puffinbasic.Error;
using Org.Puffinbasic.Parser.PuffinBasicIR;
using Org.Puffinbasic.Domain.STObjects.PuffinBasicAtomTypeId;
using Org.Puffinbasic.Error.PuffinBasicRuntimeError.ErrorCode;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;

namespace Org.Puffinbasic.Runtime
{
    sealed class Operators
    {
        public static void UnaryMinus(PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            var op1Entry = symbolTable[instruction.op1];
            var op1 = op1Entry.GetValue();
            var result = symbolTable[instruction.result].GetValue();
            switch (op1Entry.GetType().GetAtomTypeId())
            {
                case INT32:
                    result.SetInt32(-op1.GetInt32());
                    break;
                case INT64:
                    result.SetInt64(-op1.GetInt64());
                    break;
                case FLOAT:
                    result.SetFloat32(-op1.GetFloat32());
                    break;
                case DOUBLE:
                    result.SetFloat64(-op1.GetFloat64());
                    break;
                default:
                    throw new PuffinBasicInternalError("Unary minus is not supported for data type: " + op1Entry.GetType().GetAtomTypeId());
                    break;
            }
        }

        public static void Concat(PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            var v1 = symbolTable[instruction.op1].GetValue().GetString();
            var v2 = symbolTable[instruction.op2].GetValue().GetString();
            var result = symbolTable[instruction.result].GetValue();
            result.SetString(v1 + v2);
        }

        public static void LeftShift(PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            var v1Entry = symbolTable[instruction.op1];
            var v1 = v1Entry.GetValue();
            var v2Entry = symbolTable[instruction.op2];
            var v2 = v2Entry.GetValue();
            var result = symbolTable[instruction.result].GetValue();
            if (v1Entry.GetType().GetAtomTypeId() == INT32 && v2Entry.GetType().GetAtomTypeId() == INT32)
            {
                result.SetInt32(v1.GetRoundedInt32() << v2.GetRoundedInt32());
            }
            else
            {
                result.SetInt64(v1.GetRoundedInt64() << v2.GetRoundedInt64());
            }
        }

        public static void RightShift(PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            var v1Entry = symbolTable[instruction.op1];
            var v1 = v1Entry.GetValue();
            var v2Entry = symbolTable[instruction.op2];
            var v2 = v2Entry.GetValue();
            var result = symbolTable[instruction.result].GetValue();
            if (v1Entry.GetType().GetAtomTypeId() == INT32 && v2Entry.GetType().GetAtomTypeId() == INT32)
            {
                result.SetInt32(v1.GetRoundedInt32() >> v2.GetRoundedInt32());
            }
            else
            {
                result.SetInt64(v1.GetRoundedInt64() >> v2.GetRoundedInt64());
            }
        }

        public static void Mod(PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            var v1Entry = symbolTable[instruction.op1];
            var v1 = v1Entry.GetValue();
            var v2Entry = symbolTable[instruction.op2];
            var v2 = v2Entry.GetValue();
            var result = symbolTable[instruction.result].GetValue();
            if (v1Entry.GetType().GetAtomTypeId() == INT32 && v2Entry.GetType().GetAtomTypeId() == INT32)
            {
                result.SetInt32(v1.GetRoundedInt32() % v2.GetRoundedInt32());
            }
            else
            {
                result.SetInt64(v1.GetRoundedInt64() % v2.GetRoundedInt64());
            }
        }

        public static void Idiv(PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            var v1Entry = symbolTable[instruction.op1];
            var v1 = v1Entry.GetValue();
            var v2Entry = symbolTable[instruction.op2];
            var v2 = v2Entry.GetValue();
            var result = symbolTable[instruction.result].GetValue();
            if (v1Entry.GetType().GetAtomTypeId() == INT32 && v2Entry.GetType().GetAtomTypeId() == INT32)
            {
                if (v2.GetRoundedInt32() == 0)
                {
                    throw new PuffinBasicRuntimeError(DIVISION_BY_ZERO, "Division by zero");
                }

                result.SetInt32(v1.GetRoundedInt32() / v2.GetRoundedInt32());
            }
            else
            {
                if (v2.GetRoundedInt64() == 0)
                {
                    throw new PuffinBasicRuntimeError(DIVISION_BY_ZERO, "Division by zero");
                }

                result.SetInt64(v1.GetRoundedInt64() / v2.GetRoundedInt64());
            }
        }

        public static void AddInt32(PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            var v1 = symbolTable[instruction.op1].GetValue();
            var v2 = symbolTable[instruction.op2].GetValue();
            var result = symbolTable[instruction.result].GetValue();
            result.SetInt32(v1.GetInt32() + v2.GetInt32());
        }

        public static void AddInt64(PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            var v1 = symbolTable[instruction.op1].GetValue();
            var v2 = symbolTable[instruction.op2].GetValue();
            var result = symbolTable[instruction.result].GetValue();
            result.SetInt64(v1.GetInt64() + v2.GetInt64());
        }

        public static void AddFloat32(PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            var v1 = symbolTable[instruction.op1].GetValue();
            var v2 = symbolTable[instruction.op2].GetValue();
            var result = symbolTable[instruction.result].GetValue();
            result.SetFloat32(v1.GetFloat32() + v2.GetFloat32());
        }

        public static void AddFloat64(PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            var v1 = symbolTable[instruction.op1].GetValue();
            var v2 = symbolTable[instruction.op2].GetValue();
            var result = symbolTable[instruction.result].GetValue();
            result.SetFloat64(v1.GetFloat64() + v2.GetFloat64());
        }

        public static void SubInt32(PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            var v1 = symbolTable[instruction.op1].GetValue();
            var v2 = symbolTable[instruction.op2].GetValue();
            var result = symbolTable[instruction.result].GetValue();
            result.SetInt32(v1.GetInt32() - v2.GetInt32());
        }

        public static void SubInt64(PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            var v1 = symbolTable[instruction.op1].GetValue();
            var v2 = symbolTable[instruction.op2].GetValue();
            var result = symbolTable[instruction.result].GetValue();
            result.SetInt64(v1.GetInt64() - v2.GetInt64());
        }

        public static void SubFloat32(PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            var v1 = symbolTable[instruction.op1].GetValue();
            var v2 = symbolTable[instruction.op2].GetValue();
            var result = symbolTable[instruction.result].GetValue();
            result.SetFloat32(v1.GetFloat32() - v2.GetFloat32());
        }

        public static void SubFloat64(PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            var v1 = symbolTable[instruction.op1].GetValue();
            var v2 = symbolTable[instruction.op2].GetValue();
            var result = symbolTable[instruction.result].GetValue();
            result.SetFloat64(v1.GetFloat64() - v2.GetFloat64());
        }

        public static void MulInt32(PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            var v1 = symbolTable[instruction.op1].GetValue();
            var v2 = symbolTable[instruction.op2].GetValue();
            var result = symbolTable[instruction.result].GetValue();
            result.SetInt32(v1.GetInt32() * v2.GetInt32());
        }

        public static void MulInt64(PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            var v1 = symbolTable[instruction.op1].GetValue();
            var v2 = symbolTable[instruction.op2].GetValue();
            var result = symbolTable[instruction.result].GetValue();
            result.SetInt64(v1.GetInt64() * v2.GetInt64());
        }

        public static void MulFloat32(PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            var v1 = symbolTable[instruction.op1].GetValue();
            var v2 = symbolTable[instruction.op2].GetValue();
            var result = symbolTable[instruction.result].GetValue();
            result.SetFloat32(v1.GetFloat32() * v2.GetFloat32());
        }

        public static void MulFloat64(PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            var v1 = symbolTable[instruction.op1].GetValue();
            var v2 = symbolTable[instruction.op2].GetValue();
            var result = symbolTable[instruction.result].GetValue();
            result.SetFloat64(v1.GetFloat64() * v2.GetFloat64());
        }

        public static void Fdiv(PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            var v1 = symbolTable[instruction.op1].GetValue();
            var v2 = symbolTable[instruction.op2].GetValue();
            var result = symbolTable[instruction.result].GetValue();
            if (v2.GetFloat64() == 0)
            {
                throw new PuffinBasicRuntimeError(DIVISION_BY_ZERO, "Division by zero");
            }

            result.SetFloat64(v1.GetFloat64() / v2.GetFloat64());
        }

        public static void ExpInt32(PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            var v1 = symbolTable[instruction.op1].GetValue();
            var v2 = symbolTable[instruction.op2].GetValue();
            var result = symbolTable[instruction.result].GetValue();
            result.SetInt32((int)Math.Pow(v1.GetInt32(), v2.GetInt32()));
        }

        public static void ExpInt64(PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            var v1 = symbolTable[instruction.op1].GetValue();
            var v2 = symbolTable[instruction.op2].GetValue();
            var result = symbolTable[instruction.result].GetValue();
            result.SetInt64((long)Math.Pow(v1.GetInt64(), v2.GetInt64()));
        }

        public static void ExpFloat32(PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            var v1 = symbolTable[instruction.op1].GetValue();
            var v2 = symbolTable[instruction.op2].GetValue();
            var result = symbolTable[instruction.result].GetValue();
            result.SetFloat32((float)Math.Pow(v1.GetFloat32(), v2.GetFloat32()));
        }

        public static void ExpFloat64(PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            var v1 = symbolTable[instruction.op1].GetValue();
            var v2 = symbolTable[instruction.op2].GetValue();
            var result = symbolTable[instruction.result].GetValue();
            result.SetFloat64(Math.Pow(v1.GetFloat64(), v2.GetFloat64()));
        }

        public static void And(PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            var v1 = symbolTable[instruction.op1].GetValue().GetInt64();
            var v2 = symbolTable[instruction.op2].GetValue().GetInt64();
            var result = symbolTable[instruction.result].GetValue();
            if ((v1 == -1 || v1 == 0) && (v2 == -1 || v2 == 0))
            {
                var b1 = v1 == -1;
                var b2 = v2 == -1;
                result.SetInt64(b1 && b2 ? -1 : 0);
            }
            else
            {
                result.SetInt64(v1 & v2);
            }
        }

        public static void Or(PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            var v1 = symbolTable[instruction.op1].GetValue().GetInt64();
            var v2 = symbolTable[instruction.op2].GetValue().GetInt64();
            var result = symbolTable[instruction.result].GetValue();
            if ((v1 == -1 || v1 == 0) && (v2 == -1 || v2 == 0))
            {
                var b1 = v1 == -1;
                var b2 = v2 == -1;
                result.SetInt64(b1 || b2 ? -1 : 0);
            }
            else
            {
                result.SetInt64(v1 | v2);
            }
        }

        public static void Xor(PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            var v1 = symbolTable[instruction.op1].GetValue().GetInt64();
            var v2 = symbolTable[instruction.op2].GetValue().GetInt64();
            var result = symbolTable[instruction.result].GetValue();
            if ((v1 == -1 || v1 == 0) && (v2 == -1 || v2 == 0))
            {
                var b1 = v1 == -1;
                var b2 = v2 == -1;
                result.SetInt64(b1 ^ b2 ? -1 : 0);
            }
            else
            {
                result.SetInt64(v1 ^ v2);
            }
        }

        public static void Eqv(PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            var v1 = symbolTable[instruction.op1].GetValue().GetInt64();
            var v2 = symbolTable[instruction.op2].GetValue().GetInt64();
            var result = symbolTable[instruction.result].GetValue();
            if ((v1 == -1 || v1 == 0) && (v2 == -1 || v2 == 0))
            {
                var b1 = v1 == -1;
                var b2 = v2 == -1;
                result.SetInt64(b1 == b2 ? -1 : 0);
            }
            else
            {
                result.SetInt64(~(v1 ^ v2));
            }
        }

        public static void Imp(PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            var v1 = symbolTable[instruction.op1].GetValue().GetInt64();
            var v2 = symbolTable[instruction.op2].GetValue().GetInt64();
            var result = symbolTable[instruction.result].GetValue();
            if ((v1 == -1 || v1 == 0) && (v2 == -1 || v2 == 0))
            {
                var b1 = v1 == -1;
                var b2 = v2 == -1;
                result.SetInt64((!b1) || b2 ? -1 : 0);
            }
            else
            {
                result.SetInt64((~v1) | v2);
            }
        }

        public static void LtInt32(PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            var e1 = symbolTable[instruction.op1].GetValue();
            var e2 = symbolTable[instruction.op2].GetValue();
            var result = symbolTable[instruction.result].GetValue();
            result.SetInt64(e1.GetInt32() < e2.GetInt32() ? -1 : 0);
        }

        public static void LtInt64(PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            var e1 = symbolTable[instruction.op1].GetValue();
            var e2 = symbolTable[instruction.op2].GetValue();
            var result = symbolTable[instruction.result].GetValue();
            result.SetInt64(e1.GetInt64() < e2.GetInt64() ? -1 : 0);
        }

        public static void LtFloat32(PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            var e1 = symbolTable[instruction.op1].GetValue();
            var e2 = symbolTable[instruction.op2].GetValue();
            var result = symbolTable[instruction.result].GetValue();
            result.SetInt64(Float.Compare(e1.GetFloat32(), e2.GetFloat32()) < 0 ? -1 : 0);
        }

        public static void LtFloat64(PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            var e1 = symbolTable[instruction.op1].GetValue();
            var e2 = symbolTable[instruction.op2].GetValue();
            var result = symbolTable[instruction.result].GetValue();
            result.SetInt64(Double.Compare(e1.GetFloat64(), e2.GetFloat64()) < 0 ? -1 : 0);
        }

        public static void LtStr(PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            var e1 = symbolTable[instruction.op1].GetValue();
            var e2 = symbolTable[instruction.op2].GetValue();
            var result = symbolTable[instruction.result].GetValue();
            result.SetInt64(e1.GetString().CompareTo(e2.GetString()) < 0 ? -1 : 0);
        }

        public static void LeInt32(PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            var e1 = symbolTable[instruction.op1].GetValue();
            var e2 = symbolTable[instruction.op2].GetValue();
            var result = symbolTable[instruction.result].GetValue();
            result.SetInt64(e1.GetInt32() <= e2.GetInt32() ? -1 : 0);
        }

        public static void LeInt64(PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            var e1 = symbolTable[instruction.op1].GetValue();
            var e2 = symbolTable[instruction.op2].GetValue();
            var result = symbolTable[instruction.result].GetValue();
            result.SetInt64(e1.GetInt64() <= e2.GetInt64() ? -1 : 0);
        }

        public static void LeFloat32(PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            var e1 = symbolTable[instruction.op1].GetValue();
            var e2 = symbolTable[instruction.op2].GetValue();
            var result = symbolTable[instruction.result].GetValue();
            result.SetInt64(Float.Compare(e1.GetFloat32(), e2.GetFloat32()) <= 0 ? -1 : 0);
        }

        public static void LeFloat64(PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            var e1 = symbolTable[instruction.op1].GetValue();
            var e2 = symbolTable[instruction.op2].GetValue();
            var result = symbolTable[instruction.result].GetValue();
            result.SetInt64(Double.Compare(e1.GetFloat64(), e2.GetFloat64()) <= 0 ? -1 : 0);
        }

        public static void LeStr(PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            var e1 = symbolTable[instruction.op1].GetValue();
            var e2 = symbolTable[instruction.op2].GetValue();
            var result = symbolTable[instruction.result].GetValue();
            result.SetInt64(e1.GetString().CompareTo(e2.GetString()) <= 0 ? -1 : 0);
        }

        public static void GtInt32(PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            var e1 = symbolTable[instruction.op1].GetValue();
            var e2 = symbolTable[instruction.op2].GetValue();
            var result = symbolTable[instruction.result].GetValue();
            result.SetInt64(e1.GetInt32() > e2.GetInt32() ? -1 : 0);
        }

        public static void GtInt64(PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            var e1 = symbolTable[instruction.op1].GetValue();
            var e2 = symbolTable[instruction.op2].GetValue();
            var result = symbolTable[instruction.result].GetValue();
            long longResult;
            longResult = e1.GetInt64() > e2.GetInt64() ? -1 : 0;
            result.SetInt64(longResult);
        }

        public static void GtFloat32(PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            var e1 = symbolTable[instruction.op1].GetValue();
            var e2 = symbolTable[instruction.op2].GetValue();
            var result = symbolTable[instruction.result].GetValue();
            result.SetInt64(Float.Compare(e1.GetFloat32(), e2.GetFloat32()) > 0 ? -1 : 0);
        }

        public static void GtFloat64(PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            var e1 = symbolTable[instruction.op1].GetValue();
            var e2 = symbolTable[instruction.op2].GetValue();
            var result = symbolTable[instruction.result].GetValue();
            result.SetInt64(Double.Compare(e1.GetFloat64(), e2.GetFloat64()) > 0 ? -1 : 0);
        }

        public static void GtStr(PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            var e1 = symbolTable[instruction.op1].GetValue();
            var e2 = symbolTable[instruction.op2].GetValue();
            var result = symbolTable[instruction.result].GetValue();
            result.SetInt64(e1.GetString().CompareTo(e2.GetString()) > 0 ? -1 : 0);
        }

        public static void GeInt32(PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            var e1 = symbolTable[instruction.op1].GetValue();
            var e2 = symbolTable[instruction.op2].GetValue();
            var result = symbolTable[instruction.result].GetValue();
            result.SetInt64(e1.GetInt32() >= e2.GetInt32() ? -1 : 0);
        }

        public static void GeInt64(PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            var e1 = symbolTable[instruction.op1].GetValue();
            var e2 = symbolTable[instruction.op2].GetValue();
            var result = symbolTable[instruction.result].GetValue();
            result.SetInt64(e1.GetInt64() >= e2.GetInt64() ? -1 : 0);
        }

        public static void GeFloat32(PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            var e1 = symbolTable[instruction.op1].GetValue();
            var e2 = symbolTable[instruction.op2].GetValue();
            var result = symbolTable[instruction.result].GetValue();
            result.SetInt64(Float.Compare(e1.GetFloat32(), e2.GetFloat32()) >= 0 ? -1 : 0);
        }

        public static void GeFloat64(PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            var e1 = symbolTable[instruction.op1].GetValue();
            var e2 = symbolTable[instruction.op2].GetValue();
            var result = symbolTable[instruction.result].GetValue();
            result.SetInt64(Double.Compare(e1.GetFloat64(), e2.GetFloat64()) >= 0 ? -1 : 0);
        }

        public static void GeStr(PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            var e1 = symbolTable[instruction.op1].GetValue();
            var e2 = symbolTable[instruction.op2].GetValue();
            var result = symbolTable[instruction.result].GetValue();
            result.SetInt64(e1.GetString().CompareTo(e2.GetString()) >= 0 ? -1 : 0);
        }

        public static void EqInt32(PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            var e1 = symbolTable[instruction.op1].GetValue();
            var e2 = symbolTable[instruction.op2].GetValue();
            var result = symbolTable[instruction.result].GetValue();
            result.SetInt64(e1.GetInt32() == e2.GetInt32() ? -1 : 0);
        }

        public static void EqInt64(PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            var e1 = symbolTable[instruction.op1].GetValue();
            var e2 = symbolTable[instruction.op2].GetValue();
            var result = symbolTable[instruction.result].GetValue();
            result.SetInt64(e1.GetInt64() == e2.GetInt64() ? -1 : 0);
        }

        public static void EqFloat32(PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            var e1 = symbolTable[instruction.op1].GetValue();
            var e2 = symbolTable[instruction.op2].GetValue();
            var result = symbolTable[instruction.result].GetValue();
            result.SetInt64(Float.Compare(e1.GetFloat32(), e2.GetFloat32()) == 0 ? -1 : 0);
        }

        public static void EqFloat64(PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            var e1 = symbolTable[instruction.op1].GetValue();
            var e2 = symbolTable[instruction.op2].GetValue();
            var result = symbolTable[instruction.result].GetValue();
            result.SetInt64(Double.Compare(e1.GetFloat64(), e2.GetFloat64()) == 0 ? -1 : 0);
        }

        public static void EqStr(PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            var e1 = symbolTable[instruction.op1].GetValue();
            var e2 = symbolTable[instruction.op2].GetValue();
            var result = symbolTable[instruction.result].GetValue();
            result.SetInt64(e1.GetString().Equals(e2.GetString()) ? -1 : 0);
        }

        public static void NeInt32(PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            var e1 = symbolTable[instruction.op1].GetValue();
            var e2 = symbolTable[instruction.op2].GetValue();
            var result = symbolTable[instruction.result].GetValue();
            result.SetInt64(e1.GetInt32() != e2.GetInt32() ? -1 : 0);
        }

        public static void NeInt64(PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            var e1 = symbolTable[instruction.op1].GetValue();
            var e2 = symbolTable[instruction.op2].GetValue();
            var result = symbolTable[instruction.result].GetValue();
            result.SetInt64(e1.GetInt64() != e2.GetInt64() ? -1 : 0);
        }

        public static void NeFloat32(PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            var e1 = symbolTable[instruction.op1].GetValue();
            var e2 = symbolTable[instruction.op2].GetValue();
            var result = symbolTable[instruction.result].GetValue();
            result.SetInt64(Float.Compare(e1.GetFloat32(), e2.GetFloat32()) != 0 ? -1 : 0);
        }

        public static void NeFloat64(PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            var e1 = symbolTable[instruction.op1].GetValue();
            var e2 = symbolTable[instruction.op2].GetValue();
            var result = symbolTable[instruction.result].GetValue();
            result.SetInt64(Double.Compare(e1.GetFloat64(), e2.GetFloat64()) != 0 ? -1 : 0);
        }

        public static void NeStr(PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            var e1 = symbolTable[instruction.op1].GetValue();
            var e2 = symbolTable[instruction.op2].GetValue();
            var result = symbolTable[instruction.result].GetValue();
            result.SetInt64(!e1.GetString().Equals(e2.GetString()) ? -1 : 0);
        }

        public static void UnaryNot(PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            var v = symbolTable[instruction.op1].GetValue().GetInt64();
            var result = symbolTable[instruction.result].GetValue();
            if (v == -1)
            {
                result.SetInt64(0);
            }
            else if (v == 0)
            {
                result.SetInt64(-1);
            }
            else
            {
                result.SetInt64(~v);
            }
        }
    }
}

