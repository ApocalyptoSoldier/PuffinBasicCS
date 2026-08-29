//using It.Unimi.Dsi.Fastutil.Ints;
//using It.Unimi.Dsi.Fastutil.Objects;
//using Org.Antlr.V4.Runtime;
//using Org.Antlr.V4.Runtime.Misc;
//using Org.Antlr.V4.Runtime.Tree;
//using Org.Jetbrains.Annotations;
//using Org.Puffinbasic.Antlr4;
//using Org.Puffinbasic.Antlr4.PuffinBasicParser;
using Org.Puffinbasic.Domain;
using static Org.Puffinbasic.Domain.STObjects;
using static Org.Puffinbasic.Domain.Variable;
using Org.Puffinbasic.Error;
using static Org.Puffinbasic.File.IPuffinBasicFile;
using static Org.Puffinbasic.Parser.PuffinBasicIR;
using Org.Puffinbasic.Runtime;
//using Java.Util;
//using Java.Util.Concurrent.Atomic;
//using Java.Util.Function;
//using Java.Util.Stream;
using static Org.Puffinbasic.Domain.PuffinBasicSymbolTable;
using static Org.Puffinbasic.Domain.STObjects.PuffinBasicAtomTypeId;
using static Org.Puffinbasic.Domain.STObjects.PuffinBasicTypeId;
using static Org.Puffinbasic.Error.PuffinBasicSemanticError.ErrorCode;
using static Org.Puffinbasic.Parser.LinenumberListener;
using static Org.Puffinbasic.Runtime.Types;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using Antlr4.Runtime;
using Antlr4.Runtime.Tree;
using Antlr4.Runtime.Misc;
using Org.Puffinbasic.Antlr;
using static Org.Puffinbasic.Antlr.PuffinBasicParser;
using System.Threading;
using Org.Puffinbasic.Domain.Scope;

namespace Org.Puffinbasic.Parser
{
    public class PuffinBasicIRListener : PuffinBasicBaseListener
    {
        private enum NumericOrString
        {
            NUMERIC,
            STRING
        }

        private readonly PuffinBasicSourceFile sourceFile;
        private readonly ICharStream @in;
        private readonly PuffinBasicIR ir;
        private readonly bool graphics;
        private readonly ParseTreeProperty<Instruction> nodeToInstruction;
        private readonly Dictionary<Variable, UDFState> udfStateMap = new Dictionary<Variable, UDFState>();
        //private readonly Dictionary<int, UDFState> udfStateMap = new Dictionary<int, UDFState>();
        private readonly LinkedList<WhileLoopState> whileLoopStateList = new LinkedList<WhileLoopState>();
        private readonly LinkedList<ForLoopState> forLoopStateList = new LinkedList<ForLoopState>();
        private readonly LinkedList<IfState> ifStateList;
        private UDFState currentUdfState;
        private readonly ParseTreeProperty<IfState> nodeToIfState;
        private int currentLineNumber;
        public PuffinBasicIRListener(PuffinBasicSourceFile sourceFile, ICharStream @in, PuffinBasicIR ir, bool graphics)
        {
            this.sourceFile = sourceFile;
            this.@in = @in;
            this.ir = ir;
            this.graphics = graphics;
            this.nodeToInstruction = new ParseTreeProperty<Instruction>();
            this.nodeToIfState = new ParseTreeProperty<IfState>();
        }

        public virtual void SemanticCheckAfterParsing()
        {
            if (whileLoopStateList.Any())
            {
                throw new PuffinBasicSemanticError(WHILE_WITHOUT_WEND, "<UNKNOWN LINE>", "WHILE without WEND");
            }

            if (forLoopStateList.Any())
            {
                throw new PuffinBasicSemanticError(FOR_WITHOUT_NEXT, "<UNKNOWN LINE>", "FOR without NEXT");
            }
        }

        private string GetCtxString(ParserRuleContext ctx)
        {
            return @in.GetText(new Interval(ctx.Start.StartIndex, ctx.Stop.StopIndex));
        }

        private Instruction LookupInstruction(ParserRuleContext ctx)
        {
            var exprInstruction = nodeToInstruction.Get(ctx);
            if (exprInstruction == null)
            {
                throw new PuffinBasicInternalError("Failed to find instruction for node: " + ctx.GetText());
            }

            return exprInstruction;
        }

        public override void EnterLine(PuffinBasicParser.LineContext ctx)
        {
            this.currentLineNumber = ctx.linenum() != null ? ParseLinenum(ctx.linenum().DECIMAL().GetText()) : Interlocked.Increment(ref this.currentLineNumber);
        }

        //
        // Variable, Number, etc.
        //
        public override void ExitNumber(PuffinBasicParser.NumberContext ctx)
        {
            int id;
            if (ctx.integer() != null)
            {
                bool isLong = ctx.integer().AT() != null;
                bool isDouble = ctx.integer().HASH() != null;
                bool isFloat = ctx.integer().EXCLAMATION() != null;
                string strValue;
                int @base;
                if (ctx.integer().HEXADECIMAL() != null)
                {
                    strValue = ctx.integer().HEXADECIMAL().GetText().Substring(2);
                    @base = 16;
                }
                else if (ctx.integer().OCTAL() != null)
                {
                    var octalStr = ctx.integer().OCTAL().GetText();
                    strValue = (octalStr.StartsWith("&O") ? octalStr.Substring(2) : octalStr.Substring(1));
                    @base = 8;
                }
                else
                {
                    strValue = ctx.integer().DECIMAL().GetText();
                    @base = 10;
                }

                if (isLong || isDouble)
                {
                    long parsed = Numbers.ParseInt64(strValue, @base, GetCtxString(ctx));
                    id = ir.GetSymbolTable().AddTmp(isLong ? PuffinBasicAtomTypeId.INT64 : PuffinBasicAtomTypeId.DOUBLE, (entry) => entry.GetValue().SetInt64(parsed));
                }
                else
                {
                    id = ir.GetSymbolTable().AddTmp(isFloat ? PuffinBasicAtomTypeId.FLOAT : PuffinBasicAtomTypeId.INT32, (entry) => entry.GetValue().SetInt32(Numbers.ParseInt32(strValue, @base, GetCtxString(ctx))));
                }
            }
            else if (ctx.FLOAT() != null)
            {
                var floatStr = ctx.FLOAT().GetText();
                if (floatStr.EndsWith("!"))
                {
                    floatStr = floatStr.Substring(0, floatStr.Length - 1);
                }

                var floatValue = Numbers.ParseFloat32(floatStr, GetCtxString(ctx));
                id = ir.GetSymbolTable().AddTmp(PuffinBasicAtomTypeId.FLOAT, (entry) => entry.GetValue().SetFloat32(floatValue));
            }
            else
            {
                var doubleStr = ctx.DOUBLE().GetText();
                if (doubleStr.EndsWith("#"))
                {
                    doubleStr = doubleStr.Substring(0, doubleStr.Length - 1);
                }

                var doubleValue = Numbers.ParseFloat64(doubleStr, GetCtxString(ctx));
                id = ir.GetSymbolTable().AddTmp(PuffinBasicAtomTypeId.DOUBLE, (entry) => entry.GetValue().SetFloat64(doubleValue));
            }

            var instr = ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.VALUE, id, NULL_ID, id);
            nodeToInstruction.Put(ctx, instr);
        }

        //
        // Variable, Number, etc.
        //
        public override void ExitVariable(VariableContext ctx)
        {
            Instruction instruction = ctx.leafvariable() != null ? ExitLeafVariable(ctx.leafvariable()) : ExitStructVariable(ctx.structvariable());
            nodeToInstruction.Put(ctx, instruction);
        }

        //
        // Variable, Number, etc.
        //
        private Instruction ExitLeafVariable(PuffinBasicParser.LeafvariableContext ctx)
        {
            PuffinBasicSymbolTable symbolTable = ir.GetSymbolTable();
            symbolTable.CheckUnused(ctx.varname().VARNAME().GetText()); // Check that the variable name doesn't match an existing user defined type

            IScope currentScope = symbolTable.GetCurrentScope();

            var variableName = GetVariableNameFromCtx(ctx.varname(), ctx.varsuffix());

            int refId = 0;
            ir.GetSymbolTable().AddVariableOrUDF(variableName,
                (variableName1) => Variable.Of(variableName1, VariableKindHint.DERIVE_FROM_NAME, GetCtxString(ctx)), 
                (varId, varEntry, variable) =>
            {
                refId = varId;
                if (variable.IsScalar())
                {

                    // Scalar
                    if (ctx.expr().Count() != 0)
                    {
                        var expr = ctx.expr().ToList();
                        throw new PuffinBasicSemanticError(PuffinBasicSemanticError.ErrorCode.SCALAR_VARIABLE_CANNOT_BE_INDEXED, GetCtxString(ctx), "Scalar variable cannot be indexed: " + variable);
                    }
                }
                else if (variable.IsArray())
                {
                    if (ctx.expr().Count() != 0)
                    {

                        // Array
                        ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.RESET_ARRAY_IDX, varId, NULL_ID, NULL_ID);
                        foreach (var exprCtx in ctx.expr())
                        {
                            var exprInstr = LookupInstruction(exprCtx);
                            ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.SET_ARRAY_IDX, varId, exprInstr.result, NULL_ID);
                        }

                        refId = ir.GetSymbolTable().AddArrayReference((STLValue)varEntry);
                        ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.ARRAYREF, varId, refId, refId);
                    }
                }
                else if (variable.IsUDF())
                {

                    // UDF
                    var udfEntry = (STUDF)varEntry;
                    var udfState = udfStateMap[variable];

                    // Create & Push Runtime scope
                    var pushScopeInstr = ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.PUSH_RT_SCOPE, varId, NULL_ID, NULL_ID);

                    // Copy caller params to Runtime scope
                    if (ctx.expr().Count() != udfEntry.GetNumDeclaredParams())
                    {
                        throw new PuffinBasicSemanticError(INSUFFICIENT_UDF_ARGS, GetCtxString(ctx), variable + " expects " + udfEntry.GetNumDeclaredParams() + ", #args passed: " + ctx.expr().Count());
                    }

                    int i = 0;
                    foreach (var exprCtx in ctx.expr())
                    {
                        var exprInstr = LookupInstruction(exprCtx);
                        var declParamId = udfEntry.GetDeclaredParam(i++);
                        ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.PARAM_COPY, exprInstr.result, declParamId, declParamId);
                    }


                    // GOTO labelFuncStart
                    ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.GOTO_LABEL, udfState.labelFuncStart.op1, NULL_ID, NULL_ID);

                    // LABEL caller return address
                    var labelCallerReturn = ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.LABEL, ir.GetSymbolTable().AddLabel(), NULL_ID, NULL_ID);

                    // Patch address of the caller
                    pushScopeInstr.PatchOp2(labelCallerReturn.op1);

                    // Pop Runtime scope
                    ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.POP_RT_SCOPE, varId, NULL_ID, NULL_ID);
                }
            });

            return ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.VARIABLE, refId, NULL_ID, refId);
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        private Instruction ExitStructVariable(PuffinBasicParser.StructvariableContext ctx)
        {
            var root = ctx.varname(0).VARNAME().GetText();
            var rootId = ir.GetSymbolTable().GetCompositeVariableIdForVariable(new VariableName(root, null, COMPOSITE));
            var structType = ir.GetSymbolTable()[rootId].GetType().AsStruct();
            var parentTypeName = structType.GetTypeName();
            for (int i = 1; i < ctx.varname().Count(); i++)
            {
                var localStruct = ir.GetSymbolTable().GetStructType(parentTypeName);
                var childVarname = ctx.varname(i).VARNAME().GetText();
                var childName = new VariableName(childVarname, null, COMPOSITE);
                var childRefId = localStruct.GetMemberRefId(childName);
                var childTypeName = localStruct.GetMemberType(childName).AsStruct().GetTypeName();
                ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.PARAM1, ir.GetSymbolTable().AddTmp(INT32, (e) => e.GetValue().SetInt32(childRefId)), NULL_ID, NULL_ID);
                parentTypeName = childTypeName;
            }

            var @struct = ir.GetSymbolTable().GetStructType(parentTypeName);
            var leafCtx = ctx.leafvariable();
            var leafVarname = leafCtx.varname().VARNAME().GetText();
            var leafDataType = @struct.ContainsMember(new VariableName(leafVarname, null, COMPOSITE)) ? @struct.GetMemberType(new VariableName(leafVarname, null, COMPOSITE)).GetAtomTypeId() : ir.GetSymbolTable().GetDataTypeFor(leafVarname, leafCtx.varsuffix() != null ? leafCtx.varsuffix().GetText() : null);
            var leafName = new VariableName(leafVarname, leafDataType.GetRepr(), leafDataType);
            var leafRefId = @struct.GetMemberRefId(leafName);
            var leafType = @struct.GetMemberType(leafName);
            ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.PARAM1, ir.GetSymbolTable().AddTmp(INT32, (e) => e.GetValue().SetInt32(leafRefId)), NULL_ID, NULL_ID);
            var result = ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.STRUCT_LVALUE, rootId, NULL_ID, ir.GetSymbolTable().AddRef(leafType));
            if (ctx.expr().Any())
            {
                ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.RESET_ARRAY_IDX, result.result, NULL_ID, NULL_ID);
                foreach (var exprCtx in ctx.expr())
                {
                    var exprInstr = LookupInstruction(exprCtx);
                    ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.SET_ARRAY_IDX, result.result, exprInstr.result, NULL_ID);
                }

                var refId = ir.GetSymbolTable().AddArrayReference((STObjects.STLValue)ir.GetSymbolTable()[result.result]);
                result = ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.ARRAYREF, result.result, refId, refId);
            }

            return result;
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        //
        // Expr
        //
        private void CopyAndRegisterExprResult(ParserRuleContext ctx, Instruction instruction, bool shouldCopy)
        {
            if (shouldCopy)
            {
                var copy = ir.GetSymbolTable().AddTmpCompatibleWith(instruction.result);
                instruction = ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.COPY, instruction.result, copy, copy);
            }

            nodeToInstruction.Put(ctx, instruction);
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        //
        // Expr
        //
        public override void ExitExprVariable(PuffinBasicParser.ExprVariableContext ctx)
        {
            var instruction = nodeToInstruction.Get(ctx.variable());
            var varEntry = ir.GetSymbolTable()[instruction.result];
            bool copy = (varEntry is STVariable) && ((STVariable)varEntry).GetVariable().IsUDF();
            if (ctx.MINUS() != null)
            {
                if (ir.GetSymbolTable()[instruction.result].GetType().GetAtomTypeId() == PuffinBasicAtomTypeId.STRING)
                {
                    throw new PuffinBasicSemanticError(DATA_TYPE_MISMATCH, GetCtxString(ctx), "Unary minus cannot be used with a String!");
                }

                instruction = ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.UNARY_MINUS, instruction.result, NULL_ID, ir.GetSymbolTable().AddTmpCompatibleWith(instruction.result));
                copy = true;
            }

            CopyAndRegisterExprResult(ctx, instruction, copy);
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        //
        // Expr
        //
        public override void ExitExprParen(PuffinBasicParser.ExprParenContext ctx)
        {
            nodeToInstruction.Put(ctx, LookupInstruction(ctx.expr()));
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        //
        // Expr
        //
        public override void ExitExprNumber(PuffinBasicParser.ExprNumberContext ctx)
        {
            var instruction = nodeToInstruction.Get(ctx.number());
            if (ctx.MINUS() != null)
            {
                instruction = ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.UNARY_MINUS, instruction.result, NULL_ID, ir.GetSymbolTable().AddTmpCompatibleWith(instruction.result));
            }

            CopyAndRegisterExprResult(ctx, instruction, false);
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        //
        // Expr
        //
        public override void ExitExprFunc(PuffinBasicParser.ExprFuncContext ctx)
        {
            var instruction = nodeToInstruction.Get(ctx.func());
            if (ctx.MINUS() != null)
            {
                instruction = ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.UNARY_MINUS, instruction.result, NULL_ID, ir.GetSymbolTable().AddTmpCompatibleWith(instruction.result));
            }

            CopyAndRegisterExprResult(ctx, instruction, false);
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        //
        // Expr
        //
        public override void ExitExprString(PuffinBasicParser.ExprStringContext ctx)
        {
            var v = ctx.@string().STRING();
            var w = v.GetText();
  
            var text = Unquote(ctx.@string().STRING().GetText());
            var id = ir.GetSymbolTable().AddTmp(PuffinBasicAtomTypeId.STRING, (entry) => entry.GetValue().SetString(text));
            CopyAndRegisterExprResult(ctx, ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.VALUE, id, NULL_ID, id), false);
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        //
        // Expr
        //
        public override void ExitExprExp(PuffinBasicParser.ExprExpContext ctx)
        {
            var expr1 = ctx.expr(0);
            var expr2 = ctx.expr(1);
            int instr1res = LookupInstruction(expr1).result;
            int instr2res = LookupInstruction(expr2).result;
            var dt1 = ir.GetSymbolTable()[instr1res].GetType().GetAtomTypeId();
            var dt2 = ir.GetSymbolTable()[instr2res].GetType().GetAtomTypeId();
            Types.AssertNumeric(dt1, dt2, GetCtxString(ctx));
            var upcast = Types.Upcast(dt1, dt2, GetCtxString(ctx));
            var result = ir.GetSymbolTable().AddTmp(upcast, (e) =>
            {
            });
            OpCode opCode;
            switch (upcast)
            {
                case PuffinBasicAtomTypeId.INT32:
                    opCode = OpCode.EXPI32;
                    break;
                case PuffinBasicAtomTypeId.INT64:
                    opCode = OpCode.EXPI64;
                    break;
                case PuffinBasicAtomTypeId.FLOAT:
                    opCode = OpCode.EXPF32;
                    break;
                case PuffinBasicAtomTypeId.DOUBLE:
                    opCode = OpCode.EXPF64;
                    break;
                default:
                    throw new PuffinBasicInternalError("Bad type: " + upcast);
            }

            nodeToInstruction.Put(ctx, ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, opCode, instr1res, instr2res, result));
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        //
        // Expr
        //
        public override void ExitExprMulDiv(PuffinBasicParser.ExprMulDivContext ctx)
        {
            var expr1 = ctx.expr(0);
            var expr2 = ctx.expr(1);
            int instr1res = LookupInstruction(expr1).result;
            int instr2res = LookupInstruction(expr2).result;
            var dt1 = ir.GetSymbolTable()[instr1res].GetType().GetAtomTypeId();
            var dt2 = ir.GetSymbolTable()[instr2res].GetType().GetAtomTypeId();
            Types.AssertNumeric(dt1, dt2, GetCtxString(ctx));
            var upcast = Types.Upcast(dt1, dt2, GetCtxString(ctx));
            int result;
            OpCode opCode;
            if (ctx.MUL() != null)
            {
                result = ir.GetSymbolTable().AddTmp(upcast, (e) =>
                {
                });
                switch (upcast)
                {
                    case PuffinBasicAtomTypeId.INT32:
                        opCode = OpCode.MULI32;
                        break;
                    case PuffinBasicAtomTypeId.INT64:
                        opCode = OpCode.MULI64;
                        break;
                    case PuffinBasicAtomTypeId.FLOAT:
                        opCode = OpCode.MULF32;
                        break;
                    case PuffinBasicAtomTypeId.DOUBLE:
                        opCode = OpCode.MULF64;
                        break;
                    default:
                        throw new PuffinBasicInternalError("Bad type: " + upcast);
                }
            }
            else if (ctx.INT_DIV() != null)
            {
                result = ir.GetSymbolTable().AddTmp(upcast, (e) =>
                {
                });
                opCode = OpCode.IDIV;
            }
            else
            {
                result = ir.GetSymbolTable().AddTmp(PuffinBasicAtomTypeId.DOUBLE, (e) =>
                {
                });
                opCode = OpCode.FDIV;
            }

            nodeToInstruction.Put(ctx, ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, opCode, instr1res, instr2res, result));
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        //
        // Expr
        //
        public override void ExitExprMod(PuffinBasicParser.ExprModContext ctx)
        {
            AddArithmeticOpExpr(ctx, OpCode.MOD, ctx.expr(0), ctx.expr(1));
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        //
        // Expr
        //
        public override void ExitExprPlusMinus(PuffinBasicParser.ExprPlusMinusContext ctx)
        {
            var expr1 = ctx.expr(0);
            var expr2 = ctx.expr(1);
            int instr1res = LookupInstruction(expr1).result;
            int instr2res = LookupInstruction(expr2).result;
            var dt1 = ir.GetSymbolTable()[instr1res].GetType().GetAtomTypeId();
            var dt2 = ir.GetSymbolTable()[instr2res].GetType().GetAtomTypeId();
            bool plus = ctx.PLUS() != null;
            if (dt1 == PuffinBasicAtomTypeId.STRING && dt2 == PuffinBasicAtomTypeId.STRING)
            {
                if (plus)
                {
                    nodeToInstruction.Put(ctx, ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.CONCAT, instr1res, instr2res, 
                        ir.GetSymbolTable().AddTmp(PuffinBasicAtomTypeId.STRING, (e) =>
                    {
                    })));
                }
                else
                {
                    throw new PuffinBasicSemanticError(DATA_TYPE_MISMATCH, GetCtxString(ctx), "Minus ('-') doesn't work with String data type!");
                }
            }
            else
            {
                Types.AssertNumeric(dt1, dt2, GetCtxString(ctx));
                var upcast = Types.Upcast(dt1, dt2, GetCtxString(ctx));
                var result = ir.GetSymbolTable().AddTmp(upcast, (e) =>
                {
                });
                OpCode opCode;
                switch (upcast)
                {
                    case PuffinBasicAtomTypeId.INT32:
                        opCode = plus ? OpCode.ADDI32 : OpCode.SUBI32;
                        break;
                    case PuffinBasicAtomTypeId.INT64:
                        opCode = plus ? OpCode.ADDI64 : OpCode.SUBI64;
                        break;
                    case PuffinBasicAtomTypeId.FLOAT:
                        opCode = plus ? OpCode.ADDF32 : OpCode.SUBF32;
                        break;
                    case PuffinBasicAtomTypeId.DOUBLE:
                        opCode = plus ? OpCode.ADDF64 : OpCode.SUBF64;
                        break;
                    default:
                        throw new PuffinBasicInternalError("Bad type: " + upcast);
                        break;
                }

                nodeToInstruction.Put(ctx, ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, opCode, instr1res, instr2res, result));
            }
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        //
        // Expr
        //
        private void AddArithmeticOpExpr(ParserRuleContext parent, OpCode opCode, PuffinBasicParser.ExprContext exprLeft, PuffinBasicParser.ExprContext exprRight)
        {
            var exprL = LookupInstruction(exprLeft);
            var exprR = LookupInstruction(exprRight);
            var dt1 = ir.GetSymbolTable()[exprL.result].GetType().GetAtomTypeId();
            var dt2 = ir.GetSymbolTable()[exprR.result].GetType().GetAtomTypeId();
            Types.AssertNumeric(dt1, dt2, GetCtxString(parent));
            var result = ir.GetSymbolTable().AddTmp(Types.Upcast(dt1, ir.GetSymbolTable()[exprR.result].GetType().GetAtomTypeId(), GetCtxString(parent)), (e) =>
            {
            });
            nodeToInstruction.Put(parent, ir.AddInstruction(sourceFile, currentLineNumber, parent.Start.StartIndex, parent.Stop.StopIndex, opCode, exprL.result, exprR.result, result));
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        //
        // Expr
        //
        public override void ExitExprRelational(PuffinBasicParser.ExprRelationalContext ctx)
        {
            var exprL = LookupInstruction(ctx.expr(0));
            var exprR = LookupInstruction(ctx.expr(1));
            var dt1 = ir.GetSymbolTable()[exprL.result].GetType().GetAtomTypeId();
            var dt2 = ir.GetSymbolTable()[exprR.result].GetType().GetAtomTypeId();
            CheckDataTypeMatch(dt1, dt2, GetCtxString(ctx));
            OpCode? opCode;
            if (dt1 == PuffinBasicAtomTypeId.STRING && dt2 == PuffinBasicAtomTypeId.STRING)
            {
                opCode = ctx.RELEQ() != null ? OpCode.EQSTR : ctx.RELNEQ() != null ? OpCode.NESTR : ctx.RELLT() != null ? OpCode.LTSTR : ctx.RELGT() != null ? OpCode.GTSTR : ctx.RELLE() != null ? OpCode.LESTR : ctx.RELGE() != null ? OpCode.GESTR : null;
            }
            else
            {
                if (dt1 == PuffinBasicAtomTypeId.DOUBLE || dt2 == PuffinBasicAtomTypeId.DOUBLE)
                {
                    opCode = ctx.RELEQ() != null ? OpCode.EQF64 : ctx.RELNEQ() != null ? OpCode.NEF64 : ctx.RELLT() != null ? OpCode.LTF64 : ctx.RELGT() != null ? OpCode.GTF64 : ctx.RELLE() != null ? OpCode.LEF64 : ctx.RELGE() != null ? OpCode.GEF64 : null;
                }
                else if (dt1 == PuffinBasicAtomTypeId.INT64 || dt2 == PuffinBasicAtomTypeId.INT64)
                {
                    opCode = ctx.RELEQ() != null ? OpCode.EQI64 : ctx.RELNEQ() != null ? OpCode.NEI64 : ctx.RELLT() != null ? OpCode.LTI64 : ctx.RELGT() != null ? OpCode.GTI64 : ctx.RELLE() != null ? OpCode.LEI64 : ctx.RELGE() != null ? OpCode.GEI64 : null;
                }
                else if (dt1 == PuffinBasicAtomTypeId.FLOAT || dt2 == PuffinBasicAtomTypeId.FLOAT)
                {
                    opCode = ctx.RELEQ() != null ? OpCode.EQF32 : ctx.RELNEQ() != null ? OpCode.NEF32 : ctx.RELLT() != null ? OpCode.LTF32 : ctx.RELGT() != null ? OpCode.GTF32 : ctx.RELLE() != null ? OpCode.LEF32 : ctx.RELGE() != null ? OpCode.GEF32 : null;
                }
                else
                {
                    opCode = ctx.RELEQ() != null ? OpCode.EQI32 : ctx.RELNEQ() != null ? OpCode.NEI32 : ctx.RELLT() != null ? OpCode.LTI32 : ctx.RELGT() != null ? OpCode.GTI32 : ctx.RELLE() != null ? OpCode.LEI32 : ctx.RELGE() != null ? OpCode.GEI32 : null;
                }
            }

            if (opCode == null)
            {
                throw new PuffinBasicSemanticError(DATA_TYPE_MISMATCH, GetCtxString(ctx), "Unsupported operator!");
            }

            var result = ir.GetSymbolTable().AddTmp(INT64, (e) =>
            {
            });
            nodeToInstruction.Put(ctx, ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, opCode.Value, exprL.result, exprR.result, result));
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        //
        // Expr
        //
        public override void ExitExprLogNot(PuffinBasicParser.ExprLogNotContext ctx)
        {
            var expr = LookupInstruction(ctx.expr());
            Types.AssertNumeric(ir.GetSymbolTable()[expr.result].GetType().GetAtomTypeId(), GetCtxString(ctx));
            var result = ir.GetSymbolTable().AddTmp(INT64, (e) =>
            {
            });
            nodeToInstruction.Put(ctx, ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.NOT, expr.result, NULL_ID, result));
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        //
        // Expr
        //
        public override void ExitExprLogical(PuffinBasicParser.ExprLogicalContext ctx)
        {
            OpCode? opCode = ctx.LOGAND() != null ? OpCode.AND : ctx.LOGOR() != null ? OpCode.OR : ctx.LOGXOR() != null ? OpCode.XOR : ctx.LOGEQV() != null ? OpCode.EQV : ctx.LOGIMP() != null ? OpCode.IMP : null;
            if (opCode == null)
            {
                throw new PuffinBasicSemanticError(DATA_TYPE_MISMATCH, GetCtxString(ctx), "Unsupported operator!");
            }

            AddLogicalOpExpr(ctx, opCode.Value, ctx.expr(0), ctx.expr(1));
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        //
        // Expr
        //
        public override void ExitExprBitwise(PuffinBasicParser.ExprBitwiseContext ctx)
        {
            OpCode? opCode = ctx.BWLSFT() != null ? OpCode.LEFTSHIFT : ctx.BWRSFT() != null ? OpCode.RIGHTSHIFT : null;
            if (opCode == null)
            {
                throw new PuffinBasicSemanticError(DATA_TYPE_MISMATCH, GetCtxString(ctx), "Unsupported operator!");
            }

            AddBitwiseOpExpr(ctx, opCode.Value, ctx.expr(0), ctx.expr(1));
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        //
        // Expr
        //
        private void AddLogicalOpExpr(ParserRuleContext parent, OpCode opCode, PuffinBasicParser.ExprContext exprLeft, PuffinBasicParser.ExprContext exprRight)
        {
            var exprL = LookupInstruction(exprLeft);
            var exprR = LookupInstruction(exprRight);
            Types.AssertNumeric(ir.GetSymbolTable()[exprL.result].GetType().GetAtomTypeId(), ir.GetSymbolTable()[exprR.result].GetType().GetAtomTypeId(), GetCtxString(parent));
            var result = ir.GetSymbolTable().AddTmp(INT64, (e) =>
            {
            });
            nodeToInstruction.Put(parent, ir.AddInstruction(sourceFile, currentLineNumber, parent.Start.StartIndex, parent.Stop.StopIndex, opCode, exprL.result, exprR.result, result));
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        //
        // Expr
        //
        private void AddBitwiseOpExpr(ParserRuleContext parent, OpCode opCode, PuffinBasicParser.ExprContext exprLeft, PuffinBasicParser.ExprContext exprRight)
        {
            var exprL = LookupInstruction(exprLeft);
            var exprR = LookupInstruction(exprRight);
            Types.AssertNumeric(ir.GetSymbolTable()[exprL.result].GetType().GetAtomTypeId(), ir.GetSymbolTable()[exprR.result].GetType().GetAtomTypeId(), GetCtxString(parent));
            var result = ir.GetSymbolTable().AddTmp(INT64, (e) =>
            {
            });
            nodeToInstruction.Put(parent, ir.AddInstruction(sourceFile, currentLineNumber, parent.Start.StartIndex, parent.Stop.StopIndex, opCode, exprL.result, exprR.result, result));
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        //
        // Expr
        //
        //
        // Functions
        //
        public override void ExitFuncAbs(PuffinBasicParser.FuncAbsContext ctx)
        {
            nodeToInstruction.Put(ctx, AddFuncWithExprInstruction(OpCode.ABS, ctx, ctx.expr(), NumericOrString.NUMERIC));
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        //
        // Expr
        //
        //
        // Functions
        //
        public override void ExitFuncAsc(PuffinBasicParser.FuncAscContext ctx)
        {
            nodeToInstruction.Put(ctx, AddFuncWithExprInstruction(OpCode.ASC, ctx, ctx.expr(), NumericOrString.STRING, ir.GetSymbolTable().AddTmp(INT32, (c) =>
            {
            })));
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        //
        // Expr
        //
        //
        // Functions
        //
        public override void ExitFuncSin(PuffinBasicParser.FuncSinContext ctx)
        {
            nodeToInstruction.Put(ctx, AddFuncWithExprInstruction(OpCode.SIN, ctx, ctx.expr(), NumericOrString.NUMERIC, ir.GetSymbolTable().AddTmp(PuffinBasicAtomTypeId.DOUBLE, (c) =>
            {
            })));
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        //
        // Expr
        //
        //
        // Functions
        //
        public override void ExitFuncCos(PuffinBasicParser.FuncCosContext ctx)
        {
            nodeToInstruction.Put(ctx, AddFuncWithExprInstruction(OpCode.COS, ctx, ctx.expr(), NumericOrString.NUMERIC, ir.GetSymbolTable().AddTmp(PuffinBasicAtomTypeId.DOUBLE, (c) =>
            {
            })));
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        //
        // Expr
        //
        //
        // Functions
        //
        public override void ExitFuncTan(PuffinBasicParser.FuncTanContext ctx)
        {
            nodeToInstruction.Put(ctx, AddFuncWithExprInstruction(OpCode.TAN, ctx, ctx.expr(), NumericOrString.NUMERIC, ir.GetSymbolTable().AddTmp(PuffinBasicAtomTypeId.DOUBLE, (c) =>
            {
            })));
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        //
        // Expr
        //
        //
        // Functions
        //
        public override void ExitFuncASin(PuffinBasicParser.FuncASinContext ctx)
        {
            nodeToInstruction.Put(ctx, AddFuncWithExprInstruction(OpCode.ASIN, ctx, ctx.expr(), NumericOrString.NUMERIC, ir.GetSymbolTable().AddTmp(PuffinBasicAtomTypeId.DOUBLE, (c) =>
            {
            })));
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        //
        // Expr
        //
        //
        // Functions
        //
        public override void ExitFuncACos(PuffinBasicParser.FuncACosContext ctx)
        {
            nodeToInstruction.Put(ctx, AddFuncWithExprInstruction(OpCode.ACOS, ctx, ctx.expr(), NumericOrString.NUMERIC, ir.GetSymbolTable().AddTmp(PuffinBasicAtomTypeId.DOUBLE, (c) =>
            {
            })));
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        //
        // Expr
        //
        //
        // Functions
        //
        public override void ExitFuncAtn(PuffinBasicParser.FuncAtnContext ctx)
        {
            nodeToInstruction.Put(ctx, AddFuncWithExprInstruction(OpCode.ATN, ctx, ctx.expr(), NumericOrString.NUMERIC, ir.GetSymbolTable().AddTmp(PuffinBasicAtomTypeId.DOUBLE, (c) =>
            {
            })));
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        //
        // Expr
        //
        //
        // Functions
        //
        public override void ExitFuncSinh(PuffinBasicParser.FuncSinhContext ctx)
        {
            nodeToInstruction.Put(ctx, AddFuncWithExprInstruction(OpCode.SINH, ctx, ctx.expr(), NumericOrString.NUMERIC, ir.GetSymbolTable().AddTmp(PuffinBasicAtomTypeId.DOUBLE, (c) =>
            {
            })));
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        //
        // Expr
        //
        //
        // Functions
        //
        public override void ExitFuncCosh(PuffinBasicParser.FuncCoshContext ctx)
        {
            nodeToInstruction.Put(ctx, AddFuncWithExprInstruction(OpCode.COSH, ctx, ctx.expr(), NumericOrString.NUMERIC, ir.GetSymbolTable().AddTmp(PuffinBasicAtomTypeId.DOUBLE, (c) =>
            {
            })));
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        //
        // Expr
        //
        //
        // Functions
        //
        public override void ExitFuncTanh(PuffinBasicParser.FuncTanhContext ctx)
        {
            nodeToInstruction.Put(ctx, AddFuncWithExprInstruction(OpCode.TANH, ctx, ctx.expr(), NumericOrString.NUMERIC, ir.GetSymbolTable().AddTmp(PuffinBasicAtomTypeId.DOUBLE, (c) =>
            {
            })));
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        //
        // Expr
        //
        //
        // Functions
        //
        public override void ExitFuncExp(PuffinBasicParser.FuncExpContext ctx)
        {
            nodeToInstruction.Put(ctx, AddFuncWithExprInstruction(OpCode.EEXP, ctx, ctx.expr(), NumericOrString.NUMERIC, ir.GetSymbolTable().AddTmp(PuffinBasicAtomTypeId.DOUBLE, (c) =>
            {
            })));
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        //
        // Expr
        //
        //
        // Functions
        //
        public override void ExitFuncLog10(PuffinBasicParser.FuncLog10Context ctx)
        {
            nodeToInstruction.Put(ctx, AddFuncWithExprInstruction(OpCode.LOG10, ctx, ctx.expr(), NumericOrString.NUMERIC, ir.GetSymbolTable().AddTmp(PuffinBasicAtomTypeId.DOUBLE, (c) =>
            {
            })));
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        //
        // Expr
        //
        //
        // Functions
        //
        public override void ExitFuncLog2(PuffinBasicParser.FuncLog2Context ctx)
        {
            nodeToInstruction.Put(ctx, AddFuncWithExprInstruction(OpCode.LOG2, ctx, ctx.expr(), NumericOrString.NUMERIC, ir.GetSymbolTable().AddTmp(PuffinBasicAtomTypeId.DOUBLE, (c) =>
            {
            })));
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        //
        // Expr
        //
        //
        // Functions
        //
        public override void ExitFuncToRad(PuffinBasicParser.FuncToRadContext ctx)
        {
            nodeToInstruction.Put(ctx, AddFuncWithExprInstruction(OpCode.TORAD, ctx, ctx.expr(), NumericOrString.NUMERIC, ir.GetSymbolTable().AddTmp(PuffinBasicAtomTypeId.DOUBLE, (c) =>
            {
            })));
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        //
        // Expr
        //
        //
        // Functions
        //
        public override void ExitFuncToDeg(PuffinBasicParser.FuncToDegContext ctx)
        {
            nodeToInstruction.Put(ctx, AddFuncWithExprInstruction(OpCode.TODEG, ctx, ctx.expr(), NumericOrString.NUMERIC, ir.GetSymbolTable().AddTmp(PuffinBasicAtomTypeId.DOUBLE, (c) =>
            {
            })));
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        //
        // Expr
        //
        //
        // Functions
        //
        public override void ExitFuncFloor(PuffinBasicParser.FuncFloorContext ctx)
        {
            nodeToInstruction.Put(ctx, AddFuncWithExprInstruction(OpCode.FLOOR, ctx, ctx.expr(), NumericOrString.NUMERIC, ir.GetSymbolTable().AddTmp(PuffinBasicAtomTypeId.DOUBLE, (c) =>
            {
            })));
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        //
        // Expr
        //
        //
        // Functions
        //
        public override void ExitFuncCeil(PuffinBasicParser.FuncCeilContext ctx)
        {
            nodeToInstruction.Put(ctx, AddFuncWithExprInstruction(OpCode.CEIL, ctx, ctx.expr(), NumericOrString.NUMERIC, ir.GetSymbolTable().AddTmp(PuffinBasicAtomTypeId.DOUBLE, (c) =>
            {
            })));
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        //
        // Expr
        //
        //
        // Functions
        //
        public override void ExitFuncRound(PuffinBasicParser.FuncRoundContext ctx)
        {
            nodeToInstruction.Put(ctx, AddFuncWithExprInstruction(OpCode.ROUND, ctx, ctx.expr(), NumericOrString.NUMERIC, ir.GetSymbolTable().AddTmp(PuffinBasicAtomTypeId.DOUBLE, (c) =>
            {
            })));
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        //
        // Expr
        //
        //
        // Functions
        //
        public override void ExitFuncSqr(PuffinBasicParser.FuncSqrContext ctx)
        {
            nodeToInstruction.Put(ctx, AddFuncWithExprInstruction(OpCode.SQR, ctx, ctx.expr(), NumericOrString.NUMERIC, ir.GetSymbolTable().AddTmp(PuffinBasicAtomTypeId.DOUBLE, (c) =>
            {
            })));
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        //
        // Expr
        //
        //
        // Functions
        //
        public override void ExitFuncCint(PuffinBasicParser.FuncCintContext ctx)
        {
            nodeToInstruction.Put(ctx, AddFuncWithExprInstruction(OpCode.CINT, ctx, ctx.expr(), NumericOrString.NUMERIC, ir.GetSymbolTable().AddTmp(INT32, (c) =>
            {
            })));
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        //
        // Expr
        //
        //
        // Functions
        //
        public override void ExitFuncClng(PuffinBasicParser.FuncClngContext ctx)
        {
            nodeToInstruction.Put(ctx, AddFuncWithExprInstruction(OpCode.CLNG, ctx, ctx.expr(), NumericOrString.NUMERIC, ir.GetSymbolTable().AddTmp(INT64, (c) =>
            {
            })));
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        //
        // Expr
        //
        //
        // Functions
        //
        public override void ExitFuncCsng(PuffinBasicParser.FuncCsngContext ctx)
        {
            nodeToInstruction.Put(ctx, AddFuncWithExprInstruction(OpCode.CSNG, ctx, ctx.expr(), NumericOrString.NUMERIC, ir.GetSymbolTable().AddTmp(PuffinBasicAtomTypeId.FLOAT, (c) =>
            {
            })));
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        //
        // Expr
        //
        //
        // Functions
        //
        public override void ExitFuncCdbl(PuffinBasicParser.FuncCdblContext ctx)
        {
            nodeToInstruction.Put(ctx, AddFuncWithExprInstruction(OpCode.CDBL, ctx, ctx.expr(), NumericOrString.NUMERIC, ir.GetSymbolTable().AddTmp(PuffinBasicAtomTypeId.DOUBLE, (c) =>
            {
            })));
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        //
        // Expr
        //
        //
        // Functions
        //
        public override void ExitFuncCvi(PuffinBasicParser.FuncCviContext ctx)
        {
            nodeToInstruction.Put(ctx, AddFuncWithExprInstruction(OpCode.CVI, ctx, ctx.expr(), NumericOrString.STRING, ir.GetSymbolTable().AddTmp(INT32, (c) =>
            {
            })));
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        //
        // Expr
        //
        //
        // Functions
        //
        public override void ExitFuncCvl(PuffinBasicParser.FuncCvlContext ctx)
        {
            nodeToInstruction.Put(ctx, AddFuncWithExprInstruction(OpCode.CVL, ctx, ctx.expr(), NumericOrString.STRING, ir.GetSymbolTable().AddTmp(INT64, (c) =>
            {
            })));
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        //
        // Expr
        //
        //
        // Functions
        //
        public override void ExitFuncCvs(PuffinBasicParser.FuncCvsContext ctx)
        {
            nodeToInstruction.Put(ctx, AddFuncWithExprInstruction(OpCode.CVS, ctx, ctx.expr(), NumericOrString.STRING, ir.GetSymbolTable().AddTmp(PuffinBasicAtomTypeId.FLOAT, (c) =>
            {
            })));
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        //
        // Expr
        //
        //
        // Functions
        //
        public override void ExitFuncCvd(PuffinBasicParser.FuncCvdContext ctx)
        {
            nodeToInstruction.Put(ctx, AddFuncWithExprInstruction(OpCode.CVD, ctx, ctx.expr(), NumericOrString.STRING, ir.GetSymbolTable().AddTmp(PuffinBasicAtomTypeId.DOUBLE, (c) =>
            {
            })));
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        //
        // Expr
        //
        //
        // Functions
        //
        public override void ExitFuncMkiDlr(PuffinBasicParser.FuncMkiDlrContext ctx)
        {
            nodeToInstruction.Put(ctx, AddFuncWithExprInstruction(OpCode.MKIDLR, ctx, ctx.expr(), NumericOrString.NUMERIC, ir.GetSymbolTable().AddTmp(PuffinBasicAtomTypeId.STRING, (c) =>
            {
            })));
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        //
        // Expr
        //
        //
        // Functions
        //
        public override void ExitFuncMklDlr(PuffinBasicParser.FuncMklDlrContext ctx)
        {
            nodeToInstruction.Put(ctx, AddFuncWithExprInstruction(OpCode.MKLDLR, ctx, ctx.expr(), NumericOrString.NUMERIC, ir.GetSymbolTable().AddTmp(PuffinBasicAtomTypeId.STRING, (c) =>
            {
            })));
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        //
        // Expr
        //
        //
        // Functions
        //
        public override void ExitFuncMksDlr(PuffinBasicParser.FuncMksDlrContext ctx)
        {
            nodeToInstruction.Put(ctx, AddFuncWithExprInstruction(OpCode.MKSDLR, ctx, ctx.expr(), NumericOrString.NUMERIC, ir.GetSymbolTable().AddTmp(PuffinBasicAtomTypeId.STRING, (c) =>
            {
            })));
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        //
        // Expr
        //
        //
        // Functions
        //
        public override void ExitFuncMkdDlr(PuffinBasicParser.FuncMkdDlrContext ctx)
        {
            nodeToInstruction.Put(ctx, AddFuncWithExprInstruction(OpCode.MKDDLR, ctx, ctx.expr(), NumericOrString.NUMERIC, ir.GetSymbolTable().AddTmp(PuffinBasicAtomTypeId.STRING, (c) =>
            {
            })));
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        //
        // Expr
        //
        //
        // Functions
        //
        public override void ExitFuncSpaceDlr(PuffinBasicParser.FuncSpaceDlrContext ctx)
        {
            nodeToInstruction.Put(ctx, AddFuncWithExprInstruction(OpCode.SPACEDLR, ctx, ctx.expr(), NumericOrString.NUMERIC, ir.GetSymbolTable().AddTmp(PuffinBasicAtomTypeId.STRING, (c) =>
            {
            })));
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        //
        // Expr
        //
        //
        // Functions
        //
        public override void ExitFuncStrDlr(PuffinBasicParser.FuncStrDlrContext ctx)
        {
            nodeToInstruction.Put(ctx, AddFuncWithExprInstruction(OpCode.STRDLR, ctx, ctx.expr(), NumericOrString.NUMERIC, ir.GetSymbolTable().AddTmp(PuffinBasicAtomTypeId.STRING, (c) =>
            {
            })));
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        //
        // Expr
        //
        //
        // Functions
        //
        public override void ExitFuncVal(PuffinBasicParser.FuncValContext ctx)
        {
            nodeToInstruction.Put(ctx, AddFuncWithExprInstruction(OpCode.VAL, ctx, ctx.expr(), NumericOrString.STRING, ir.GetSymbolTable().AddTmp(PuffinBasicAtomTypeId.DOUBLE, (c) =>
            {
            })));
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        //
        // Expr
        //
        //
        // Functions
        //
        public override void ExitFuncInt(PuffinBasicParser.FuncIntContext ctx)
        {
            nodeToInstruction.Put(ctx, AddFuncWithExprInstruction(OpCode.INT, ctx, ctx.expr(), NumericOrString.NUMERIC));
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        //
        // Expr
        //
        //
        // Functions
        //
        public override void ExitFuncFix(PuffinBasicParser.FuncFixContext ctx)
        {
            nodeToInstruction.Put(ctx, AddFuncWithExprInstruction(OpCode.FIX, ctx, ctx.expr(), NumericOrString.NUMERIC));
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        //
        // Expr
        //
        //
        // Functions
        //
        public override void ExitFuncLog(PuffinBasicParser.FuncLogContext ctx)
        {
            nodeToInstruction.Put(ctx, AddFuncWithExprInstruction(OpCode.LOG, ctx, ctx.expr(), NumericOrString.NUMERIC, ir.GetSymbolTable().AddTmp(PuffinBasicAtomTypeId.DOUBLE, (c) =>
            {
            })));
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        //
        // Expr
        //
        //
        // Functions
        //
        public override void ExitFuncLen(PuffinBasicParser.FuncLenContext ctx)
        {
            var exprInstruction = LookupInstruction(ctx.expr(0));
            var axisId = ctx.axis != null ? LookupInstruction(ctx.axis).result : NULL_ID;
            nodeToInstruction.Put(ctx, ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.LEN, exprInstruction.result, axisId, ir.GetSymbolTable().AddTmp(INT32, (c) =>
            {
            })));
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        //
        // Expr
        //
        //
        // Functions
        //
        public override void ExitFuncChrDlr(PuffinBasicParser.FuncChrDlrContext ctx)
        {
            nodeToInstruction.Put(ctx, AddFuncWithExprInstruction(OpCode.CHRDLR, ctx, ctx.expr(), NumericOrString.NUMERIC, ir.GetSymbolTable().AddTmp(PuffinBasicAtomTypeId.STRING, (c) =>
            {
            })));
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        //
        // Expr
        //
        //
        // Functions
        //
        public override void ExitFuncHexDlr(PuffinBasicParser.FuncHexDlrContext ctx)
        {
            nodeToInstruction.Put(ctx, AddFuncWithExprInstruction(OpCode.HEXDLR, ctx, ctx.expr(), NumericOrString.NUMERIC, ir.GetSymbolTable().AddTmp(PuffinBasicAtomTypeId.STRING, (c) =>
            {
            })));
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        //
        // Expr
        //
        //
        // Functions
        //
        public override void ExitFuncOctDlr(PuffinBasicParser.FuncOctDlrContext ctx)
        {
            nodeToInstruction.Put(ctx, AddFuncWithExprInstruction(OpCode.OCTDLR, ctx, ctx.expr(), NumericOrString.NUMERIC, ir.GetSymbolTable().AddTmp(PuffinBasicAtomTypeId.STRING, (c) =>
            {
            })));
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        //
        // Expr
        //
        //
        // Functions
        //
        public override void ExitFuncLeftDlr(PuffinBasicParser.FuncLeftDlrContext ctx)
        {
            var xdlr = LookupInstruction(ctx.expr(0));
            var n = LookupInstruction(ctx.expr(1));
            Types.AssertString(ir.GetSymbolTable()[xdlr.result].GetType().GetAtomTypeId(), GetCtxString(ctx));
            Types.AssertNumeric(ir.GetSymbolTable()[n.result].GetType().GetAtomTypeId(), GetCtxString(ctx));
            nodeToInstruction.Put(ctx, ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.LEFTDLR, xdlr.result, n.result, ir.GetSymbolTable().AddTmp(PuffinBasicAtomTypeId.STRING, (c) =>
            {
            })));
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        //
        // Expr
        //
        //
        // Functions
        //
        public override void ExitFuncRightDlr(PuffinBasicParser.FuncRightDlrContext ctx)
        {
            var xdlr = LookupInstruction(ctx.expr(0));
            var n = LookupInstruction(ctx.expr(1));
            Types.AssertString(ir.GetSymbolTable()[xdlr.result].GetType().GetAtomTypeId(), GetCtxString(ctx));
            Types.AssertNumeric(ir.GetSymbolTable()[n.result].GetType().GetAtomTypeId(), GetCtxString(ctx));
            nodeToInstruction.Put(ctx, ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.RIGHTDLR, xdlr.result, n.result, ir.GetSymbolTable().AddTmp(PuffinBasicAtomTypeId.STRING, (c) =>
            {
            })));
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        //
        // Expr
        //
        //
        // Functions
        //
        public override void ExitFuncInstr(PuffinBasicParser.FuncInstrContext ctx)
        {
            int xdlr, ydlr, n;
            if (ctx.expr().Count()== 3)
            {

                // n, x$, y$
                n = LookupInstruction(ctx.expr(0)).result;
                xdlr = LookupInstruction(ctx.expr(1)).result;
                ydlr = LookupInstruction(ctx.expr(2)).result;
                Types.AssertNumeric(ir.GetSymbolTable()[n].GetType().GetAtomTypeId(), GetCtxString(ctx));
            }
            else
            {

                // x$, y$
                n = ir.GetSymbolTable().AddTmp(INT32, (e) => e.GetValue().SetInt32(1));
                xdlr = LookupInstruction(ctx.expr(0)).result;
                ydlr = LookupInstruction(ctx.expr(1)).result;
            }

            Types.AssertString(ir.GetSymbolTable()[xdlr].GetType().GetAtomTypeId(), GetCtxString(ctx));
            Types.AssertString(ir.GetSymbolTable()[ydlr].GetType().GetAtomTypeId(), GetCtxString(ctx));
            ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.PARAM2, xdlr, ydlr, NULL_ID);
            nodeToInstruction.Put(ctx, ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.INSTR, n, NULL_ID, ir.GetSymbolTable().AddTmp(INT32, (c) =>
            {
            })));
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        //
        // Expr
        //
        //
        // Functions
        //
        // n, x$, y$
        // x$, y$
        public override void ExitFuncMidDlr(PuffinBasicParser.FuncMidDlrContext ctx)
        {
            int xdlr, n, m;
            if (ctx.expr().Count()== 3)
            {

                // x$, n, m
                xdlr = LookupInstruction(ctx.expr(0)).result;
                n = LookupInstruction(ctx.expr(1)).result;
                m = LookupInstruction(ctx.expr(2)).result;
                Types.AssertNumeric(ir.GetSymbolTable()[m].GetType().GetAtomTypeId(), GetCtxString(ctx));
            }
            else
            {

                // x$, n
                xdlr = LookupInstruction(ctx.expr(0)).result;
                n = LookupInstruction(ctx.expr(1)).result;
                m = ir.GetSymbolTable().AddTmp(INT32, (e) => e.GetValue().SetInt32(int.MaxValue));
            }

            Types.AssertString(ir.GetSymbolTable()[xdlr].GetType().GetAtomTypeId(), GetCtxString(ctx));
            Types.AssertNumeric(ir.GetSymbolTable()[n].GetType().GetAtomTypeId(), GetCtxString(ctx));
            ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.PARAM2, xdlr, n, NULL_ID);
            nodeToInstruction.Put(ctx, ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.MIDDLR, m, NULL_ID, ir.GetSymbolTable().AddTmp(PuffinBasicAtomTypeId.STRING, (c) =>
            {
            })));
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        //
        // Expr
        //
        //
        // Functions
        //
        // n, x$, y$
        // x$, y$
        // x$, n, m
        // x$, n
        public override void ExitFuncRnd(PuffinBasicParser.FuncRndContext ctx)
        {
            nodeToInstruction.Put(ctx, ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.RND, NULL_ID, NULL_ID, ir.GetSymbolTable().AddTmp(PuffinBasicAtomTypeId.DOUBLE, (c) =>
            {
            })));
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        //
        // Expr
        //
        //
        // Functions
        //
        // n, x$, y$
        // x$, y$
        // x$, n, m
        // x$, n
        public override void ExitFuncSgn(PuffinBasicParser.FuncSgnContext ctx)
        {
            nodeToInstruction.Put(ctx, AddFuncWithExprInstruction(OpCode.SGN, ctx, ctx.expr(), NumericOrString.NUMERIC, ir.GetSymbolTable().AddTmp(INT32, (c) =>
            {
            })));
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        //
        // Expr
        //
        //
        // Functions
        //
        // n, x$, y$
        // x$, y$
        // x$, n, m
        // x$, n
        public override void ExitFuncTimer(PuffinBasicParser.FuncTimerContext ctx)
        {
            nodeToInstruction.Put(ctx, ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.TIMER, NULL_ID, NULL_ID, ir.GetSymbolTable().AddTmp(PuffinBasicAtomTypeId.DOUBLE, (c) =>
            {
            })));
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        //
        // Expr
        //
        //
        // Functions
        //
        // n, x$, y$
        // x$, y$
        // x$, n, m
        // x$, n
        public override void ExitFuncTimerMillis(PuffinBasicParser.FuncTimerMillisContext ctx)
        {
            nodeToInstruction.Put(ctx, ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.TIMERMILLIS, NULL_ID, NULL_ID, ir.GetSymbolTable().AddTmp(INT64, (c) =>
            {
            })));
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        //
        // Expr
        //
        //
        // Functions
        //
        // n, x$, y$
        // x$, y$
        // x$, n, m
        // x$, n
        public override void ExitFuncStringDlr(PuffinBasicParser.FuncStringDlrContext ctx)
        {
            int n = LookupInstruction(ctx.expr(0)).result;
            int jOrxdlr = LookupInstruction(ctx.expr(1)).result;
            Types.AssertNumeric(ir.GetSymbolTable()[n].GetType().GetAtomTypeId(), GetCtxString(ctx));
            nodeToInstruction.Put(ctx, ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.STRINGDLR, n, jOrxdlr, ir.GetSymbolTable().AddTmp(PuffinBasicAtomTypeId.STRING, (c) =>
            {
            })));
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        //
        // Expr
        //
        //
        // Functions
        //
        // n, x$, y$
        // x$, y$
        // x$, n, m
        // x$, n
        public override void ExitFuncLoc(PuffinBasicParser.FuncLocContext ctx)
        {
            var fileNumber = LookupInstruction(ctx.expr());
            Types.AssertNumeric(ir.GetSymbolTable()[fileNumber.result].GetType().GetAtomTypeId(), GetCtxString(ctx));
            nodeToInstruction.Put(ctx, ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.LOC, fileNumber.result, NULL_ID, ir.GetSymbolTable().AddTmp(INT32, (c) =>
            {
            })));
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        //
        // Expr
        //
        //
        // Functions
        //
        // n, x$, y$
        // x$, y$
        // x$, n, m
        // x$, n
        public override void ExitFuncLof(PuffinBasicParser.FuncLofContext ctx)
        {
            var fileNumber = LookupInstruction(ctx.expr());
            Types.AssertNumeric(ir.GetSymbolTable()[fileNumber.result].GetType().GetAtomTypeId(), GetCtxString(ctx));
            nodeToInstruction.Put(ctx, ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.LOF, fileNumber.result, NULL_ID, ir.GetSymbolTable().AddTmp(INT64, (c) =>
            {
            })));
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        //
        // Expr
        //
        //
        // Functions
        //
        // n, x$, y$
        // x$, y$
        // x$, n, m
        // x$, n
        public override void ExitFuncEof(PuffinBasicParser.FuncEofContext ctx)
        {
            var fileNumber = LookupInstruction(ctx.expr());
            Types.AssertNumeric(ir.GetSymbolTable()[fileNumber.result].GetType().GetAtomTypeId(), GetCtxString(ctx));
            nodeToInstruction.Put(ctx, ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.EOF, fileNumber.result, NULL_ID, ir.GetSymbolTable().AddTmp(INT32, (c) =>
            {
            })));
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        //
        // Expr
        //
        //
        // Functions
        //
        // n, x$, y$
        // x$, y$
        // x$, n, m
        // x$, n
        public override void ExitFuncEnvironDlr(PuffinBasicParser.FuncEnvironDlrContext ctx)
        {
            var expr = LookupInstruction(ctx.expr());
            Types.AssertString(ir.GetSymbolTable()[expr.result].GetType().GetAtomTypeId(), GetCtxString(ctx));
            nodeToInstruction.Put(ctx, ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.ENVIRONDLR, expr.result, NULL_ID, ir.GetSymbolTable().AddTmp(PuffinBasicAtomTypeId.STRING, (c) =>
            {
            })));
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        //
        // Expr
        //
        //
        // Functions
        //
        // n, x$, y$
        // x$, y$
        // x$, n, m
        // x$, n
        public override void ExitFuncInputDlr(PuffinBasicParser.FuncInputDlrContext ctx)
        {
            var x = LookupInstruction(ctx.expr(0));
            Types.AssertNumeric(ir.GetSymbolTable()[x.result].GetType().GetAtomTypeId(), GetCtxString(ctx));
            int fileNumberId;
            if (ctx.expr().Count()== 2)
            {
                var fileNumber = LookupInstruction(ctx.expr(1));
                Types.AssertNumeric(ir.GetSymbolTable()[fileNumber.result].GetType().GetAtomTypeId(), GetCtxString(ctx));
                fileNumberId = fileNumber.result;
            }
            else
            {
                fileNumberId = ir.GetSymbolTable().AddTmp(INT32, (e) => e.GetValue().SetInt32(-1));
            }

            nodeToInstruction.Put(ctx, ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.INPUTDLR, x.result, fileNumberId, ir.GetSymbolTable().AddTmp(PuffinBasicAtomTypeId.STRING, (c) =>
            {
            })));
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        //
        // Expr
        //
        //
        // Functions
        //
        // n, x$, y$
        // x$, y$
        // x$, n, m
        // x$, n
        public override void ExitFuncInkeyDlr(PuffinBasicParser.FuncInkeyDlrContext ctx)
        {
            AssertGraphics();
            nodeToInstruction.Put(ctx, ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.INKEYDLR, NULL_ID, NULL_ID, ir.GetSymbolTable().AddTmp(PuffinBasicAtomTypeId.STRING, (c) =>
            {
            })));
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        //
        // Expr
        //
        //
        // Functions
        //
        // n, x$, y$
        // x$, y$
        // x$, n, m
        // x$, n
        public override void ExitFuncE(PuffinBasicParser.FuncEContext ctx)
        {
            nodeToInstruction.Put(ctx, ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.E, NULL_ID, NULL_ID, ir.GetSymbolTable().AddTmp(PuffinBasicAtomTypeId.DOUBLE, (c) =>
            {
            })));
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        //
        // Expr
        //
        //
        // Functions
        //
        // n, x$, y$
        // x$, y$
        // x$, n, m
        // x$, n
        public override void ExitFuncPI(PuffinBasicParser.FuncPIContext ctx)
        {
            nodeToInstruction.Put(ctx, ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.PI, NULL_ID, NULL_ID, ir.GetSymbolTable().AddTmp(PuffinBasicAtomTypeId.DOUBLE, (c) =>
            {
            })));
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        //
        // Expr
        //
        //
        // Functions
        //
        // n, x$, y$
        // x$, y$
        // x$, n, m
        // x$, n
        public override void ExitFuncMin(PuffinBasicParser.FuncMinContext ctx)
        {
            var expr1 = LookupInstruction(ctx.expr(0));
            var expr2 = LookupInstruction(ctx.expr(1));
            var dt1 = ir.GetSymbolTable()[expr1.result].GetType().GetAtomTypeId();
            var dt2 = ir.GetSymbolTable()[expr2.result].GetType().GetAtomTypeId();
            Types.AssertNumeric(dt1, GetCtxString(ctx));
            Types.AssertNumeric(dt2, GetCtxString(ctx));
            var resdt = Types.Upcast(dt1, dt2, GetCtxString(ctx));
            nodeToInstruction.Put(ctx, ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.MIN, expr1.result, expr2.result, ir.GetSymbolTable().AddTmp(resdt, (e) =>
            {
            })));
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        //
        // Expr
        //
        //
        // Functions
        //
        // n, x$, y$
        // x$, y$
        // x$, n, m
        // x$, n
        public override void ExitFuncMax(PuffinBasicParser.FuncMaxContext ctx)
        {
            var expr1 = LookupInstruction(ctx.expr(0));
            var expr2 = LookupInstruction(ctx.expr(1));
            var dt1 = ir.GetSymbolTable()[expr1.result].GetType().GetAtomTypeId();
            var dt2 = ir.GetSymbolTable()[expr2.result].GetType().GetAtomTypeId();
            Types.AssertNumeric(dt1, GetCtxString(ctx));
            Types.AssertNumeric(dt2, GetCtxString(ctx));
            var resdt = Types.Upcast(dt1, dt2, GetCtxString(ctx));
            nodeToInstruction.Put(ctx, ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.MAX, expr1.result, expr2.result, ir.GetSymbolTable().AddTmp(resdt, (e) =>
            {
            })));
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        //
        // Expr
        //
        //
        // Functions
        //
        // n, x$, y$
        // x$, y$
        // x$, n, m
        // x$, n
        private Instruction GetArray1dVariableInstruction(ParserRuleContext ctx, VariableContext varCtx, bool numeric)
        {
            var varInstr = LookupInstruction(varCtx);
            AssertVariable(ir.GetSymbolTable()[varInstr.result], GetCtxString(ctx));
            var varEntry = (STVariable)ir.GetSymbolTable()[varInstr.result];
            Assert1DArray(varEntry, GetCtxString(ctx));
            if (numeric)
            {
                AssertNumeric(varEntry.GetType().GetAtomTypeId(), GetCtxString(ctx));
            }

            return varInstr;
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        //
        // Expr
        //
        //
        // Functions
        //
        // n, x$, y$
        // x$, y$
        // x$, n, m
        // x$, n
        private Instruction GetArray2dVariableInstruction(ParserRuleContext ctx, VariableContext varCtx)
        {
            var varInstr = LookupInstruction(varCtx);
            AssertVariable(ir.GetSymbolTable()[varInstr.result], GetCtxString(ctx));
            var varEntry = (STVariable)ir.GetSymbolTable()[varInstr.result];
            Assert2DArray(varEntry, GetCtxString(ctx));
            return varInstr;
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        //
        // Expr
        //
        //
        // Functions
        //
        // n, x$, y$
        // x$, y$
        // x$, n, m
        // x$, n
        private Instruction GetArrayNdVariableInstruction(ParserRuleContext ctx, VariableContext varCtx)
        {
            var varInstr = LookupInstruction(varCtx);
            AssertVariable(ir.GetSymbolTable()[varInstr.result], GetCtxString(ctx));
            var varEntry = (STVariable)ir.GetSymbolTable()[varInstr.result];
            AssertNDArray(varEntry, GetCtxString(ctx));
            return varInstr;
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        //
        // Expr
        //
        //
        // Functions
        //
        // n, x$, y$
        // x$, y$
        // x$, n, m
        // x$, n
        public override void ExitFuncArray1DMin(PuffinBasicParser.FuncArray1DMinContext ctx)
        {
            var var1Instr = GetArray1dVariableInstruction(ctx, ctx.variable(), true);
            nodeToInstruction.Put(ctx, ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.ARRAY1DMIN, var1Instr.result, NULL_ID, ir.GetSymbolTable().AddTmpCompatibleWith(var1Instr.result)));
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        //
        // Expr
        //
        //
        // Functions
        //
        // n, x$, y$
        // x$, y$
        // x$, n, m
        // x$, n
        public override void ExitFuncArray1DMax(PuffinBasicParser.FuncArray1DMaxContext ctx)
        {
            var var1Instr = GetArray1dVariableInstruction(ctx, ctx.variable(), true);
            nodeToInstruction.Put(ctx, ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.ARRAY1DMAX, var1Instr.result, NULL_ID, ir.GetSymbolTable().AddTmpCompatibleWith(var1Instr.result)));
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        //
        // Expr
        //
        //
        // Functions
        //
        // n, x$, y$
        // x$, y$
        // x$, n, m
        // x$, n
        public override void ExitFuncArray1DMean(PuffinBasicParser.FuncArray1DMeanContext ctx)
        {
            var var1Instr = GetArray1dVariableInstruction(ctx, ctx.variable(), true);
            nodeToInstruction.Put(ctx, ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.ARRAY1DMEAN, var1Instr.result, NULL_ID, ir.GetSymbolTable().AddTmp(PuffinBasicAtomTypeId.DOUBLE, (e) =>
            {
            })));
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        //
        // Expr
        //
        //
        // Functions
        //
        // n, x$, y$
        // x$, y$
        // x$, n, m
        // x$, n
        public override void ExitFuncArray1DSum(PuffinBasicParser.FuncArray1DSumContext ctx)
        {
            var var1Instr = GetArray1dVariableInstruction(ctx, ctx.variable(), true);
            nodeToInstruction.Put(ctx, ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.ARRAY1DSUM, var1Instr.result, NULL_ID, ir.GetSymbolTable().AddTmp(PuffinBasicAtomTypeId.DOUBLE, (e) =>
            {
            })));
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        //
        // Expr
        //
        //
        // Functions
        //
        // n, x$, y$
        // x$, y$
        // x$, n, m
        // x$, n
        public override void ExitFuncArray1DStd(PuffinBasicParser.FuncArray1DStdContext ctx)
        {
            var var1Instr = GetArray1dVariableInstruction(ctx, ctx.variable(), true);
            nodeToInstruction.Put(ctx, ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.ARRAY1DSTD, var1Instr.result, NULL_ID, ir.GetSymbolTable().AddTmp(PuffinBasicAtomTypeId.DOUBLE, (e) =>
            {
            })));
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        //
        // Expr
        //
        //
        // Functions
        //
        // n, x$, y$
        // x$, y$
        // x$, n, m
        // x$, n
        public override void ExitFuncArray1DMedian(PuffinBasicParser.FuncArray1DMedianContext ctx)
        {
            var var1Instr = GetArray1dVariableInstruction(ctx, ctx.variable(), true);
            nodeToInstruction.Put(ctx, ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.ARRAY1DMEDIAN, var1Instr.result, NULL_ID, ir.GetSymbolTable().AddTmp(PuffinBasicAtomTypeId.DOUBLE, (e) =>
            {
            })));
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        //
        // Expr
        //
        //
        // Functions
        //
        // n, x$, y$
        // x$, y$
        // x$, n, m
        // x$, n
        public override void ExitFuncArray1DBinSearch(PuffinBasicParser.FuncArray1DBinSearchContext ctx)
        {
            var var1Instr = GetArray1dVariableInstruction(ctx, ctx.variable(), false);
            var expr = LookupInstruction(ctx.expr());
            Types.AssertNumeric(ir.GetSymbolTable()[expr.result].GetType().GetAtomTypeId(), GetCtxString(ctx));
            nodeToInstruction.Put(ctx, ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.ARRAY1DBINSEARCH, var1Instr.result, expr.result, ir.GetSymbolTable().AddTmp(INT32, (e) =>
            {
            })));
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        //
        // Expr
        //
        //
        // Functions
        //
        // n, x$, y$
        // x$, y$
        // x$, n, m
        // x$, n
        public override void ExitFuncArray1DPct(PuffinBasicParser.FuncArray1DPctContext ctx)
        {
            var var1Instr = GetArray1dVariableInstruction(ctx, ctx.variable(), true);
            var expr = LookupInstruction(ctx.expr());
            Types.AssertNumeric(ir.GetSymbolTable()[expr.result].GetType().GetAtomTypeId(), GetCtxString(ctx));
            nodeToInstruction.Put(ctx, ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.ARRAY1DPCT, var1Instr.result, expr.result, ir.GetSymbolTable().AddTmp(PuffinBasicAtomTypeId.DOUBLE, (e) =>
            {
            })));
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        //
        // Expr
        //
        //
        // Functions
        //
        // n, x$, y$
        // x$, y$
        // x$, n, m
        // x$, n
        public override void ExitFuncArray2DFindRow(PuffinBasicParser.FuncArray2DFindRowContext ctx)
        {
            var varInstr = GetArray2dVariableInstruction(ctx, ctx.variable());
            var x1 = LookupInstruction(ctx.x1);
            var y1 = LookupInstruction(ctx.y1);
            var x2 = LookupInstruction(ctx.x2);
            var y2 = LookupInstruction(ctx.y2);
            var search = LookupInstruction(ctx.search);
            Types.AssertIntType(ir.GetSymbolTable()[x1.result].GetType().GetAtomTypeId(), GetCtxString(ctx));
            Types.AssertIntType(ir.GetSymbolTable()[y1.result].GetType().GetAtomTypeId(), GetCtxString(ctx));
            Types.AssertIntType(ir.GetSymbolTable()[x2.result].GetType().GetAtomTypeId(), GetCtxString(ctx));
            Types.AssertIntType(ir.GetSymbolTable()[y2.result].GetType().GetAtomTypeId(), GetCtxString(ctx));
            Types.AssertIntType(ir.GetSymbolTable()[search.result].GetType().GetAtomTypeId(), GetCtxString(ctx));
            ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.PARAM2, x1.result, y1.result, NULL_ID);
            ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.PARAM2, x2.result, y2.result, NULL_ID);
            nodeToInstruction.Put(ctx, ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.ARRAY2DFINDROW, varInstr.result, search.result, ir.GetSymbolTable().AddTmp(PuffinBasicAtomTypeId.DOUBLE, (e) =>
            {
            })));
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        //
        // Expr
        //
        //
        // Functions
        //
        // n, x$, y$
        // x$, y$
        // x$, n, m
        // x$, n
        public override void ExitFuncArray2DFindColumn(PuffinBasicParser.FuncArray2DFindColumnContext ctx)
        {
            var varInstr = GetArray2dVariableInstruction(ctx, ctx.variable());
            var x1 = LookupInstruction(ctx.x1);
            var y1 = LookupInstruction(ctx.y1);
            var x2 = LookupInstruction(ctx.x2);
            var y2 = LookupInstruction(ctx.y2);
            var search = LookupInstruction(ctx.search);
            Types.AssertIntType(ir.GetSymbolTable()[x1.result].GetType().GetAtomTypeId(), GetCtxString(ctx));
            Types.AssertIntType(ir.GetSymbolTable()[y1.result].GetType().GetAtomTypeId(), GetCtxString(ctx));
            Types.AssertIntType(ir.GetSymbolTable()[x2.result].GetType().GetAtomTypeId(), GetCtxString(ctx));
            Types.AssertIntType(ir.GetSymbolTable()[y2.result].GetType().GetAtomTypeId(), GetCtxString(ctx));
            Types.AssertIntType(ir.GetSymbolTable()[search.result].GetType().GetAtomTypeId(), GetCtxString(ctx));
            ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.PARAM2, x1.result, y1.result, NULL_ID);
            ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.PARAM2, x2.result, y2.result, NULL_ID);
            nodeToInstruction.Put(ctx, ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.ARRAY2DFINDCOLUMN, varInstr.result, search.result, ir.GetSymbolTable().AddTmp(PuffinBasicAtomTypeId.DOUBLE, (e) =>
            {
            })));
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        //
        // Expr
        //
        //
        // Functions
        //
        // n, x$, y$
        // x$, y$
        // x$, n, m
        // x$, n
        public override void ExitFuncHsb2Rgb(PuffinBasicParser.FuncHsb2RgbContext ctx)
        {
            var h = LookupInstruction(ctx.expr(0));
            var s = LookupInstruction(ctx.expr(1));
            var b = LookupInstruction(ctx.expr(2));
            Types.AssertNumeric(ir.GetSymbolTable()[h.result].GetType().GetAtomTypeId(), GetCtxString(ctx));
            Types.AssertNumeric(ir.GetSymbolTable()[s.result].GetType().GetAtomTypeId(), GetCtxString(ctx));
            Types.AssertNumeric(ir.GetSymbolTable()[b.result].GetType().GetAtomTypeId(), GetCtxString(ctx));
            ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.PARAM2, h.result, s.result, NULL_ID);
            nodeToInstruction.Put(ctx, ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.HSB2RGB, b.result, NULL_ID, ir.GetSymbolTable().AddTmp(INT32, (e) =>
            {
            })));
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        //
        // Expr
        //
        //
        // Functions
        //
        // n, x$, y$
        // x$, y$
        // x$, n, m
        // x$, n
        public override void ExitFuncMouseMovedX(PuffinBasicParser.FuncMouseMovedXContext ctx)
        {
            AssertGraphics();
            nodeToInstruction.Put(ctx, ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.MOUSEMOVEDX, NULL_ID, NULL_ID, ir.GetSymbolTable().AddTmp(INT32, (e) =>
            {
            })));
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        //
        // Expr
        //
        //
        // Functions
        //
        // n, x$, y$
        // x$, y$
        // x$, n, m
        // x$, n
        public override void ExitFuncMouseMovedY(PuffinBasicParser.FuncMouseMovedYContext ctx)
        {
            AssertGraphics();
            nodeToInstruction.Put(ctx, ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.MOUSEMOVEDY, NULL_ID, NULL_ID, ir.GetSymbolTable().AddTmp(INT32, (e) =>
            {
            })));
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        //
        // Expr
        //
        //
        // Functions
        //
        // n, x$, y$
        // x$, y$
        // x$, n, m
        // x$, n
        public override void ExitFuncMouseDraggedX(PuffinBasicParser.FuncMouseDraggedXContext ctx)
        {
            AssertGraphics();
            nodeToInstruction.Put(ctx, ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.MOUSEDRAGGEDX, NULL_ID, NULL_ID, ir.GetSymbolTable().AddTmp(INT32, (e) =>
            {
            })));
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        //
        // Expr
        //
        //
        // Functions
        //
        // n, x$, y$
        // x$, y$
        // x$, n, m
        // x$, n
        public override void ExitFuncMouseDraggedY(PuffinBasicParser.FuncMouseDraggedYContext ctx)
        {
            AssertGraphics();
            nodeToInstruction.Put(ctx, ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.MOUSEDRAGGEDY, NULL_ID, NULL_ID, ir.GetSymbolTable().AddTmp(INT32, (e) =>
            {
            })));
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        //
        // Expr
        //
        //
        // Functions
        //
        // n, x$, y$
        // x$, y$
        // x$, n, m
        // x$, n
        public override void ExitFuncMouseButtonClicked(PuffinBasicParser.FuncMouseButtonClickedContext ctx)
        {
            AssertGraphics();
            nodeToInstruction.Put(ctx, ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.MOUSEBUTTONCLICKED, NULL_ID, NULL_ID, ir.GetSymbolTable().AddTmp(INT32, (e) =>
            {
            })));
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        //
        // Expr
        //
        //
        // Functions
        //
        // n, x$, y$
        // x$, y$
        // x$, n, m
        // x$, n
        public override void ExitFuncMouseButtonPressed(PuffinBasicParser.FuncMouseButtonPressedContext ctx)
        {
            AssertGraphics();
            nodeToInstruction.Put(ctx, ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.MOUSEBUTTONPRESSED, NULL_ID, NULL_ID, ir.GetSymbolTable().AddTmp(INT32, (e) =>
            {
            })));
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        //
        // Expr
        //
        //
        // Functions
        //
        // n, x$, y$
        // x$, y$
        // x$, n, m
        // x$, n
        public override void ExitFuncMouseButtonReleased(PuffinBasicParser.FuncMouseButtonReleasedContext ctx)
        {
            AssertGraphics();
            nodeToInstruction.Put(ctx, ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.MOUSEBUTTONRELEASED, NULL_ID, NULL_ID, ir.GetSymbolTable().AddTmp(INT32, (e) =>
            {
            })));
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        //
        // Expr
        //
        //
        // Functions
        //
        // n, x$, y$
        // x$, y$
        // x$, n, m
        // x$, n
        public override void ExitFuncIsKeyPressed(PuffinBasicParser.FuncIsKeyPressedContext ctx)
        {
            AssertGraphics();
            var expr = LookupInstruction(ctx.expr());
            Types.AssertString(ir.GetSymbolTable()[expr.result].GetType().GetAtomTypeId(), GetCtxString(ctx));
            nodeToInstruction.Put(ctx, ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.ISKEYPRESSED, expr.result, NULL_ID, ir.GetSymbolTable().AddTmp(INT32, (e) =>
            {
            })));
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        //
        // Expr
        //
        //
        // Functions
        //
        // n, x$, y$
        // x$, y$
        // x$, n, m
        // x$, n
        public override void ExitFuncMemberMethodCall(PuffinBasicParser.FuncMemberMethodCallContext ctx)
        {
            var varInstruction = LookupInstruction(ctx.variable());
            var objectType = ir.GetSymbolTable()[varInstruction.result].GetType();
            var funcName = ctx.funcname().GetText();
            var returnType = objectType.GetFuncCallReturnType(funcName);
            IList<PuffinBasicType> paramTypes = new List<PuffinBasicType>(ctx.expr().Count());
            foreach (var exprCtx in ctx.expr())
            {
                var exprInstruction = LookupInstruction(exprCtx);
                ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.PARAM1, exprInstruction.result, NULL_ID, NULL_ID);
                paramTypes.Add(ir.GetSymbolTable()[exprInstruction.result].GetType());
            }

            objectType.CheckFuncCallArguments(funcName, paramTypes);
            nodeToInstruction.Put(ctx, ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.MEMBER_FUNC_CALL, varInstruction.result, ir.GetSymbolTable().AddTmp(PuffinBasicAtomTypeId.STRING, (e) => e.GetValue().SetString(funcName)), ir.GetSymbolTable().AddTmp(returnType, (e) =>
            {
            })));
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        //
        // Expr
        //
        //
        // Functions
        //
        // n, x$, y$
        // x$, y$
        // x$, n, m
        // x$, n
        public override void ExitFuncSplitDlr(PuffinBasicParser.FuncSplitDlrContext ctx)
        {
            var str = LookupInstruction(ctx.expr(0));
            var regex = LookupInstruction(ctx.expr(1));
            Types.AssertString(ir.GetSymbolTable()[str.result].GetType().GetAtomTypeId(), GetCtxString(ctx));
            Types.AssertString(ir.GetSymbolTable()[regex.result].GetType().GetAtomTypeId(), GetCtxString(ctx));
            nodeToInstruction.Put(ctx, ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.SPLITDLR, str.result, regex.result, ir.GetSymbolTable().AddTmp(new ArrayType(PuffinBasicAtomTypeId.STRING), (c) =>
            {
            })));
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        //
        // Expr
        //
        //
        // Functions
        //
        // n, x$, y$
        // x$, y$
        // x$, n, m
        // x$, n
        public override void ExitFuncAllocArray(PuffinBasicParser.FuncAllocArrayContext ctx)
        {
            var elementType = PuffinBasicAtomTypeIdExtensions.Lookup(ctx.varsuffix().GetText());
            foreach (var exprCtx in ctx.expr())
            {
                var exprInstr = LookupInstruction(exprCtx);
                ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.PARAM1, exprInstr.result, NULL_ID, NULL_ID);
            }

            nodeToInstruction.Put(ctx, ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.ALLOCARRAY, NULL_ID, NULL_ID, ir.GetSymbolTable().AddTmp(new ArrayType(elementType), (c) =>
            {
            })));
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        //
        // Expr
        //
        //
        // Functions
        //
        // n, x$, y$
        // x$, y$
        // x$, n, m
        // x$, n
        private Instruction AddFuncWithExprInstruction(OpCode opCode, ParserRuleContext parent, PuffinBasicParser.ExprContext expr, NumericOrString numericOrString)
        {
            var exprInstruction = LookupInstruction(expr);
            AssertNumericOrString(exprInstruction.result, parent, numericOrString);
            return ir.AddInstruction(sourceFile, currentLineNumber, parent.Start.StartIndex, parent.Stop.StopIndex, opCode, exprInstruction.result, NULL_ID, ir.GetSymbolTable().AddTmpCompatibleWith(exprInstruction.result));
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        //
        // Expr
        //
        //
        // Functions
        //
        // n, x$, y$
        // x$, y$
        // x$, n, m
        // x$, n
        private Instruction AddFuncWithExprInstruction(OpCode opCode, ParserRuleContext parent, PuffinBasicParser.ExprContext expr, NumericOrString numericOrString, int result)
        {
            var exprInstruction = LookupInstruction(expr);
            AssertNumericOrString(exprInstruction.result, parent, numericOrString);
            return ir.AddInstruction(sourceFile, currentLineNumber, parent.Start.StartIndex, parent.Stop.StopIndex, opCode, exprInstruction.result, NULL_ID, result);
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        //
        // Expr
        //
        //
        // Functions
        //
        // n, x$, y$
        // x$, y$
        // x$, n, m
        // x$, n
        private void AssertNumericOrString(int id, ParserRuleContext parent, NumericOrString numericOrString)
        {
            var dt = ir.GetSymbolTable()[id].GetType().GetAtomTypeId();
            if (numericOrString == NumericOrString.NUMERIC)
            {
                Types.AssertNumeric(dt, GetCtxString(parent));
            }
            else
            {
                Types.AssertString(dt, GetCtxString(parent));
            }
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        //
        // Expr
        //
        //
        // Functions
        //
        // n, x$, y$
        // x$, y$
        // x$, n, m
        // x$, n
        //
        // Stmt
        //
        public override void ExitListstmt(PuffinBasicParser.ListstmtContext ctx)
        {
            PuffinBasicType itemType;
            if (ctx.typename != null)
            {

                // struct
                var typeName = ctx.typename.VARNAME().GetText();
                itemType = ir.GetSymbolTable().GetStructType(typeName);
            }
            else if (ctx.dimtypesuffix != null)
            {

                // array
                var atomType = PuffinBasicAtomTypeIdExtensions.Lookup(ctx.dimtypesuffix.GetText());
                itemType = new ArrayType(atomType);
            }
            else
            {

                // scalar data type
                var atomType = PuffinBasicAtomTypeIdExtensions.Lookup(ctx.typesuffix.GetText());
                itemType = new ScalarType(atomType);
            }

            var instanceName = ctx.listname.VARNAME().GetText();
            var variableName = new VariableName(instanceName, null, COMPOSITE);
            var listType = new ListType(itemType);
            var id = ir.GetSymbolTable().AddCompositeVariable(variableName, new STVariable(null, new Variable(variableName, listType)));
            ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.CREATE_INSTANCE, id, NULL_ID, id);
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        //
        // Expr
        //
        //
        // Functions
        //
        // n, x$, y$
        // x$, y$
        // x$, n, m
        // x$, n
        //
        // Stmt
        //
        // struct
        // array
        // scalar data type
        public override void ExitSetstmt(PuffinBasicParser.SetstmtContext ctx)
        {
            var atomType = PuffinBasicAtomTypeIdExtensions.Lookup(ctx.typesuffix.GetText());
            PuffinBasicType itemType = new ScalarType(atomType);
            var instanceName = ctx.setname.VARNAME().GetText();
            var variableName = new VariableName(instanceName, null, COMPOSITE);
            var setType = new SetType(itemType);
            var id = ir.GetSymbolTable().AddCompositeVariable(variableName, new STVariable(null, new Variable(variableName, setType)));
            ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.CREATE_INSTANCE, id, NULL_ID, id);
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        //
        // Expr
        //
        //
        // Functions
        //
        // n, x$, y$
        // x$, y$
        // x$, n, m
        // x$, n
        //
        // Stmt
        //
        // struct
        // array
        // scalar data type
        public override void ExitDictstmt(PuffinBasicParser.DictstmtContext ctx)
        {
            var keyAtomType = PuffinBasicAtomTypeIdExtensions.Lookup(ctx.dictk1.GetText());
            PuffinBasicType keyType = new ScalarType(keyAtomType);
            PuffinBasicType valueType;
            if (ctx.dictv1 != null)
            {

                // struct
                var typeName = ctx.dictv1.VARNAME().GetText();
                valueType = ir.GetSymbolTable().GetStructType(typeName);
            }
            else
            {

                // scalar data type
                var atomType = PuffinBasicAtomTypeIdExtensions.Lookup(ctx.dictv2.GetText());
                valueType = new ScalarType(atomType);
            }

            var instanceName = ctx.dictname.VARNAME().GetText();
            var variableName = new VariableName(instanceName, null, COMPOSITE);
            var dictType = new DictType(keyType, valueType);
            var id = ir.GetSymbolTable().AddCompositeVariable(variableName, new STVariable(null, new Variable(variableName, dictType)));
            ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.CREATE_INSTANCE, id, NULL_ID, id);
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        //
        // Expr
        //
        //
        // Functions
        //
        // n, x$, y$
        // x$, y$
        // x$, n, m
        // x$, n
        //
        // Stmt
        //
        // struct
        // array
        // scalar data type
        // struct
        // scalar data type
        public override void ExitStructinstancestmt(PuffinBasicParser.StructinstancestmtContext ctx)
        {
            var typeName = ctx.varname(0).VARNAME().GetText();
            var instanceName = ctx.varname(1).VARNAME().GetText();
            var variableName = new VariableName(instanceName, null, COMPOSITE);
            var type = ir.GetSymbolTable().GetStructType(typeName);
            var id = ir.GetSymbolTable().AddCompositeVariable(variableName, new STVariable(null, new Variable(variableName, type)));
            ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.CREATE_INSTANCE, id, NULL_ID, id);
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        //
        // Expr
        //
        //
        // Functions
        //
        // n, x$, y$
        // x$, y$
        // x$, n, m
        // x$, n
        //
        // Stmt
        //
        // struct
        // array
        // scalar data type
        // struct
        // scalar data type
        public override void ExitStructstmt(PuffinBasicParser.StructstmtContext ctx)
        {
            var typeName = ctx.varname().VARNAME().GetText();
            var @struct = new StructType(typeName);
            foreach (var compCtx in ctx.compositetype())
            {
                if (compCtx.var1 != null)
                {

                    // scalar
                    var scalarVarName = compCtx.var1.VARNAME().GetText();
                    var scalarAtomTypeId = ir.GetSymbolTable().GetDataTypeFor(scalarVarName, compCtx.var2 != null ? compCtx.var2.GetText() : null);

                    //PuffinBasicAtomTypeId.lookup(compCtx.var2.getText());
                    var name = new VariableName(scalarVarName, scalarAtomTypeId.GetRepr(), scalarAtomTypeId);
                    @struct.DeclareField(name, new ScalarType(name.GetDataType()));
                }
                else if (compCtx.DIM() != null)
                {

                    // array
                    var arrayName = compCtx.elem.VARNAME().GetText();
                    var arrayAtomType = ir.GetSymbolTable().GetDataTypeFor(arrayName, compCtx.elemsuffix != null ? compCtx.elemsuffix.GetText() : null);
                    List<int> dims = new List<int>(compCtx.DECIMAL().Count());
                    foreach (var dimStrNode in compCtx.DECIMAL())
                    {
                        dims.Add(Numbers.ParseInt32(dimStrNode.GetText(), GetCtxString(ctx)));
                    }

                    @struct.DeclareField(new VariableName(arrayName, arrayAtomType.GetRepr(), arrayAtomType), new ArrayType(arrayAtomType, dims, true));
                }
                else if (compCtx.LIST() != null)
                {

                    // list
                    var name = new VariableName(compCtx.elem.VARNAME().GetText(), null, COMPOSITE);
                    PuffinBasicType itemType;
                    if (compCtx.list1 != null)
                    {

                        // struct
                        itemType = ir.GetSymbolTable().GetStructType(compCtx.list1.VARNAME().GetText());
                    }
                    else
                    {

                        // scalar data type
                        itemType = new ScalarType(PuffinBasicAtomTypeIdExtensions.Lookup(compCtx.list2.GetText()));
                    }

                    @struct.DeclareField(name, new ListType(itemType));
                }
                else if (compCtx.SET() != null)
                {

                    // set
                    var name = new VariableName(compCtx.elem.VARNAME().GetText(), null, COMPOSITE);
                    @struct.DeclareField(name, new SetType(new ScalarType(PuffinBasicAtomTypeIdExtensions.Lookup(compCtx.set2.GetText()))));
                }
                else if (compCtx.DICT() != null)
                {

                    // dict
                    var name = new VariableName(compCtx.elem.VARNAME().GetText(), null, COMPOSITE);
                    var keyType = new ScalarType(PuffinBasicAtomTypeIdExtensions.Lookup(compCtx.dictk1.GetText()));
                    PuffinBasicType valueType;
                    if (compCtx.dictv1 != null)
                    {

                        // struct
                        valueType = ir.GetSymbolTable().GetStructType(compCtx.dictv1.VARNAME().GetText());
                    }
                    else
                    {

                        // scalar data type
                        valueType = new ScalarType(PuffinBasicAtomTypeIdExtensions.Lookup(compCtx.dictv2.GetText()));
                    }

                    @struct.DeclareField(name, new DictType(keyType, valueType));
                }
                else if (compCtx.struct1 != null)
                {

                    // struct
                    var memberType = compCtx.struct1.VARNAME().GetText();
                    var name = new VariableName(compCtx.elem.VARNAME().GetText(), null, COMPOSITE);
                    @struct.DeclareField(name, ir.GetSymbolTable().GetStructType(memberType));
                }
                else
                {

                    // throw
                    throw new PuffinBasicSemanticError(DATA_TYPE_MISMATCH, GetCtxString(ctx), "Bad struct field: " + compCtx.GetText());
                }
            }

            ir.GetSymbolTable().AddStructType(typeName, @struct);
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        //
        // Expr
        //
        //
        // Functions
        //
        // n, x$, y$
        // x$, y$
        // x$, n, m
        // x$, n
        //
        // Stmt
        //
        // struct
        // array
        // scalar data type
        // struct
        // scalar data type
        // scalar
        //PuffinBasicAtomTypeId.lookup(compCtx.var2.getText());
        // array
        // list
        // struct
        // scalar data type
        // set
        // dict
        // struct
        // scalar data type
        // struct
        // throw
        public override void ExitComment(PuffinBasicParser.CommentContext ctx)
        {
            ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.COMMENT, NULL_ID, NULL_ID, NULL_ID);
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        //
        // Expr
        //
        //
        // Functions
        //
        // n, x$, y$
        // x$, y$
        // x$, n, m
        // x$, n
        //
        // Stmt
        //
        // struct
        // array
        // scalar data type
        // struct
        // scalar data type
        // scalar
        //PuffinBasicAtomTypeId.lookup(compCtx.var2.getText());
        // array
        // list
        // struct
        // scalar data type
        // set
        // dict
        // struct
        // scalar data type
        // struct
        // throw
        public override void ExitLetstmt(PuffinBasicParser.LetstmtContext ctx)
        {
            var varInstruction = LookupInstruction(ctx.variable());
            var exprInstruction = LookupInstruction(ctx.expr());
            var varType = ir.GetSymbolTable()[varInstruction.result].GetType();
            if (varType.GetTypeId() == UDF)
            {
                throw new PuffinBasicSemanticError(BAD_ASSIGNMENT, GetCtxString(ctx), "Can't assign to UDF: " + varType);
            }

            if (!varType.IsCompatibleWith(ir.GetSymbolTable()[exprInstruction.result].GetType()))
            {
                throw new PuffinBasicSemanticError(DATA_TYPE_MISMATCH, GetCtxString(ctx), "Data type " + varType + " mismatches with " + ir.GetSymbolTable()[exprInstruction.result].GetType());
            }

            var assignInstruction = ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.ASSIGN, exprInstruction.result, varInstruction.result, varInstruction.result);
            nodeToInstruction.Put(ctx, assignInstruction);
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        //
        // Expr
        //
        //
        // Functions
        //
        // n, x$, y$
        // x$, y$
        // x$, n, m
        // x$, n
        //
        // Stmt
        //
        // struct
        // array
        // scalar data type
        // struct
        // scalar data type
        // scalar
        //PuffinBasicAtomTypeId.lookup(compCtx.var2.getText());
        // array
        // list
        // struct
        // scalar data type
        // set
        // dict
        // struct
        // scalar data type
        // struct
        // throw
        public override void ExitAutoletstmt(PuffinBasicParser.AutoletstmtContext ctx)
        {
            var varname = ctx.varname().GetText();
            var exprInstruction = LookupInstruction(ctx.expr());
            var resultType = ir.GetSymbolTable()[exprInstruction.result].GetType();
            int varId = ir.GetSymbolTable().AddVariableOrUDF(new VariableName(varname, null, resultType.GetAtomTypeId()), (variableName1) => new Variable(variableName1, resultType), (id, entry, v1) =>
            {
            });
            ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.VARREF, exprInstruction.result, varId, NULL_ID);
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        //
        // Expr
        //
        //
        // Functions
        //
        // n, x$, y$
        // x$, y$
        // x$, n, m
        // x$, n
        //
        // Stmt
        //
        // struct
        // array
        // scalar data type
        // struct
        // scalar data type
        // scalar
        //PuffinBasicAtomTypeId.lookup(compCtx.var2.getText());
        // array
        // list
        // struct
        // scalar data type
        // set
        // dict
        // struct
        // scalar data type
        // struct
        // throw
        public override void ExitPrintstmt(PuffinBasicParser.PrintstmtContext ctx)
        {
            HandlePrintstmt(ctx, ctx.printlist().children, null);
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        //
        // Expr
        //
        //
        // Functions
        //
        // n, x$, y$
        // x$, y$
        // x$, n, m
        // x$, n
        //
        // Stmt
        //
        // struct
        // array
        // scalar data type
        // struct
        // scalar data type
        // scalar
        //PuffinBasicAtomTypeId.lookup(compCtx.var2.getText());
        // array
        // list
        // struct
        // scalar data type
        // set
        // dict
        // struct
        // scalar data type
        // struct
        // throw
        public override void ExitPrinthashstmt(PuffinBasicParser.PrinthashstmtContext ctx)
        {
            var fileNumber = LookupInstruction(ctx.filenum);
            HandlePrintstmt(ctx, ctx.printlist().children, fileNumber);
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        //
        // Expr
        //
        //
        // Functions
        //
        // n, x$, y$
        // x$, y$
        // x$, n, m
        // x$, n
        //
        // Stmt
        //
        // struct
        // array
        // scalar data type
        // struct
        // scalar data type
        // scalar
        //PuffinBasicAtomTypeId.lookup(compCtx.var2.getText());
        // array
        // list
        // struct
        // scalar data type
        // set
        // dict
        // struct
        // scalar data type
        // struct
        // throw
        private void HandlePrintstmt(ParserRuleContext ctx, IList<IParseTree> children, Instruction fileNumber)
        {
            bool endsWithNewline = true;
            foreach (IParseTree child in children)
            {
                if (child is PuffinBasicParser.ExprContext)
                {
                    var exprInstruction = LookupInstruction((PuffinBasicParser.ExprContext)child);
                    ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.PRINT, exprInstruction.result, NULL_ID, NULL_ID);
                    endsWithNewline = true;
                }
                else
                {
                    endsWithNewline = false;
                }
            }

            if (endsWithNewline || fileNumber != null)
            {
                var newlineId = ir.GetSymbolTable().AddTmp(PuffinBasicAtomTypeId.STRING, (entry) => entry.GetValue().SetString(Environment.NewLine));
                ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.PRINT, newlineId, NULL_ID, NULL_ID);
            }

            int fileNumberId;
            if (fileNumber != null)
            {
                Types.AssertNumeric(ir.GetSymbolTable()[fileNumber.result].GetType().GetAtomTypeId(), GetCtxString(ctx));
                fileNumberId = fileNumber.result;
            }
            else
            {
                fileNumberId = NULL_ID;
            }

            ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.FLUSH, fileNumberId, NULL_ID, NULL_ID);
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        //
        // Expr
        //
        //
        // Functions
        //
        // n, x$, y$
        // x$, y$
        // x$, n, m
        // x$, n
        //
        // Stmt
        //
        // struct
        // array
        // scalar data type
        // struct
        // scalar data type
        // scalar
        //PuffinBasicAtomTypeId.lookup(compCtx.var2.getText());
        // array
        // list
        // struct
        // scalar data type
        // set
        // dict
        // struct
        // scalar data type
        // struct
        // throw
        public override void ExitPrintusingstmt(PuffinBasicParser.PrintusingstmtContext ctx)
        {
            HandlePrintusing(ctx, ctx.format, ctx.printlist().children, null);
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        //
        // Expr
        //
        //
        // Functions
        //
        // n, x$, y$
        // x$, y$
        // x$, n, m
        // x$, n
        //
        // Stmt
        //
        // struct
        // array
        // scalar data type
        // struct
        // scalar data type
        // scalar
        //PuffinBasicAtomTypeId.lookup(compCtx.var2.getText());
        // array
        // list
        // struct
        // scalar data type
        // set
        // dict
        // struct
        // scalar data type
        // struct
        // throw
        public override void ExitPrinthashusingstmt(PuffinBasicParser.PrinthashusingstmtContext ctx)
        {
            var fileNumber = LookupInstruction(ctx.filenum);
            HandlePrintusing(ctx, ctx.format, ctx.printlist().children, fileNumber);
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        //
        // Expr
        //
        //
        // Functions
        //
        // n, x$, y$
        // x$, y$
        // x$, n, m
        // x$, n
        //
        // Stmt
        //
        // struct
        // array
        // scalar data type
        // struct
        // scalar data type
        // scalar
        //PuffinBasicAtomTypeId.lookup(compCtx.var2.getText());
        // array
        // list
        // struct
        // scalar data type
        // set
        // dict
        // struct
        // scalar data type
        // struct
        // throw
        private void HandlePrintusing(ParserRuleContext ctx, PuffinBasicParser.ExprContext formatCtx, IList<IParseTree> children, Instruction fileNumber)
        {
            var format = LookupInstruction(formatCtx);
            bool endsWithNewline = true;
            foreach (IParseTree child in children)
            {
                if (child is PuffinBasicParser.ExprContext)
                {
                    var exprInstruction = LookupInstruction((PuffinBasicParser.ExprContext)child);
                    ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.PRINTUSING, format.result, exprInstruction.result, NULL_ID);
                    endsWithNewline = true;
                }
                else
                {
                    endsWithNewline = false;
                }
            }

            if (endsWithNewline || fileNumber != null)
            {
                var newlineId = ir.GetSymbolTable().AddTmp(PuffinBasicAtomTypeId.STRING, (entry) => entry.GetValue().SetString(Environment.NewLine));
                ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.PRINT, newlineId, NULL_ID, NULL_ID);
            }

            int fileNumberId;
            if (fileNumber != null)
            {
                Types.AssertNumeric(ir.GetSymbolTable()[fileNumber.result].GetType().GetAtomTypeId(), GetCtxString(ctx));
                fileNumberId = fileNumber.result;
            }
            else
            {
                fileNumberId = NULL_ID;
            }

            ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.FLUSH, fileNumberId, NULL_ID, NULL_ID);
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        //
        // Expr
        //
        //
        // Functions
        //
        // n, x$, y$
        // x$, y$
        // x$, n, m
        // x$, n
        //
        // Stmt
        //
        // struct
        // array
        // scalar data type
        // struct
        // scalar data type
        // scalar
        //PuffinBasicAtomTypeId.lookup(compCtx.var2.getText());
        // array
        // list
        // struct
        // scalar data type
        // set
        // dict
        // struct
        // scalar data type
        // struct
        // throw
        public override void ExitDimstmt(PuffinBasicParser.DimstmtContext ctx)
        {
            List<int> dims = new List<int>(ctx.expr().Count());
            for (int i = 0; i < ctx.expr().Count(); i++)
            {
                dims.Add(0);
            }

            var variableName = GetVariableNameFromCtx(ctx.varname(), ctx.varsuffix());
            var varId = ir.GetSymbolTable().AddVariableOrUDF(variableName, (variableName1) => new Variable(variableName1, new ArrayType(variableName1.GetDataType(), dims, true)), (id, entry, v1) => entry.GetValue().SetArrayDimensions(dims));
            foreach (var expr in ctx.expr())
            {
                var dimi = LookupInstruction(expr);
                Types.AssertNumeric(ir.GetSymbolTable()[dimi.result].GetType().GetAtomTypeId(), GetCtxString(ctx));
                ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.PARAM1, dimi.result, NULL_ID, NULL_ID);
            }

            ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.DIM, varId, NULL_ID, NULL_ID);
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        //
        // Expr
        //
        //
        // Functions
        //
        // n, x$, y$
        // x$, y$
        // x$, n, m
        // x$, n
        //
        // Stmt
        //
        // struct
        // array
        // scalar data type
        // struct
        // scalar data type
        // scalar
        //PuffinBasicAtomTypeId.lookup(compCtx.var2.getText());
        // array
        // list
        // struct
        // scalar data type
        // set
        // dict
        // struct
        // scalar data type
        // struct
        // throw
        public override void ExitReallocstmt(PuffinBasicParser.ReallocstmtContext ctx)
        {
            List<int> dims = new List<int>(ctx.expr().Count());
            for (int i = 0; i < ctx.expr().Count(); i++)
            {
                dims.Add(0);
            }

            var variableName = GetVariableNameFromCtx(ctx.varname(), ctx.varsuffix());
            var varId = ir.GetSymbolTable().AddVariableOrUDF(variableName, (variableName1) => new Variable(variableName1, new ArrayType(variableName1.GetDataType(), dims, true)), (id, entry, v1) => entry.GetValue().SetArrayDimensions(dims));
            foreach (var expr in ctx.expr())
            {
                var dimi = LookupInstruction(expr);
                Types.AssertNumeric(ir.GetSymbolTable()[dimi.result].GetType().GetAtomTypeId(), GetCtxString(ctx));
                ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.PARAM1, dimi.result, NULL_ID, NULL_ID);
            }

            ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.REALLOCARRAY, varId, NULL_ID, NULL_ID);
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        //
        // Expr
        //
        //
        // Functions
        //
        // n, x$, y$
        // x$, y$
        // x$, n, m
        // x$, n
        //
        // Stmt
        //
        // struct
        // array
        // scalar data type
        // struct
        // scalar data type
        // scalar
        //PuffinBasicAtomTypeId.lookup(compCtx.var2.getText());
        // array
        // list
        // struct
        // scalar data type
        // set
        // dict
        // struct
        // scalar data type
        // struct
        // throw
        public override void EnterDeffnstmt(PuffinBasicParser.DeffnstmtContext ctx)
        {
            var variableName = GetVariableNameFromCtx(ctx.varname(), ctx.varsuffix());

            ir.GetSymbolTable().AddVariableOrUDF(variableName, 
                (variableName1) => Variable.Of(variableName1, VariableKindHint.DERIVE_FROM_NAME, GetCtxString(ctx)), 
                (varId, varEntry, variable) =>
            {
                var udfState = new UDFState(variableName, (STUDF)varEntry);
                udfStateMap[variable] =  udfState;

                // GOTO postFuncDecl
                udfState.gotoPostFuncDecl = ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.GOTO_LABEL, 
                    ir.GetSymbolTable().AddGotoTarget(), NULL_ID, NULL_ID);

                // LABEL FuncStart
                udfState.labelFuncStart = ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.LABEL, 
                    ir.GetSymbolTable().AddLabel(), NULL_ID, NULL_ID);

                // Push child scope
                ir.GetSymbolTable().PushDeclarationScope(varId, false);
            });
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        //
        // Expr
        //
        //
        // Functions
        //
        // n, x$, y$
        // x$, y$
        // x$, n, m
        // x$, n
        //
        // Stmt
        //
        // struct
        // array
        // scalar data type
        // struct
        // scalar data type
        // scalar
        //PuffinBasicAtomTypeId.lookup(compCtx.var2.getText());
        // array
        // list
        // struct
        // scalar data type
        // set
        // dict
        // struct
        // scalar data type
        // struct
        // throw
        // GOTO postFuncDecl
        // LABEL FuncStart
        // Push child scope
        public override void ExitDeffnstmt(PuffinBasicParser.DeffnstmtContext ctx)
        {
            var variableName = GetVariableNameFromCtx(ctx.varname(), ctx.varsuffix());
            var symbolTable = ir.GetSymbolTable();

            ir.GetSymbolTable().AddVariableOrUDF(variableName, 
                (variableName1) => Variable.Of(variableName1, VariableKindHint.DERIVE_FROM_NAME, GetCtxString(ctx)), 
                (varId, varEntry, variable) =>
            {
                var udfEntry = (STUDF)varEntry;
                var udfState = udfStateMap[variable];
                foreach (VariableContext fnParamCtx in ctx.variable())
                {
                    var fnParamInstr = LookupInstruction(fnParamCtx);
                    udfEntry.DeclareParam(fnParamInstr.result);
                    //udfState.udfEntry.DeclareParam(fnParamInstr.result);
                }

                var exprInstr = LookupInstruction(ctx.expr());
                CheckDataTypeMatch(varId, exprInstr.result, GetCtxString(ctx));

                // Copy expr to result
                ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.COPY, exprInstr.result, varId, varId);

                // Pop declaration scope
                ir.GetSymbolTable().PopScope();

                // GOTO Caller
                ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.GOTO_CALLER, NULL_ID, NULL_ID, NULL_ID);

                // LABEL postFuncDecl
                var labelPostFuncDecl = ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.LABEL, 
                    ir.GetSymbolTable().AddLabel(), NULL_ID, NULL_ID);

                // Patch GOTO postFuncDecl
                udfState.gotoPostFuncDecl.PatchOp1(labelPostFuncDecl.op1);
            });
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        //
        // Expr
        //
        //
        // Functions
        //
        // n, x$, y$
        // x$, y$
        // x$, n, m
        // x$, n
        //
        // Stmt
        //
        // struct
        // array
        // scalar data type
        // struct
        // scalar data type
        // scalar
        //PuffinBasicAtomTypeId.lookup(compCtx.var2.getText());
        // array
        // list
        // struct
        // scalar data type
        // set
        // dict
        // struct
        // scalar data type
        // struct
        // throw
        // GOTO postFuncDecl
        // LABEL FuncStart
        // Push child scope
        // Copy expr to result
        // Pop declaration scope
        // GOTO Caller
        // LABEL postFuncDecl
        // Patch GOTO postFuncDecl
        private VariableName GetVariableNameFromCtx(PuffinBasicParser.VarnameContext varnameCtx, PuffinBasicParser.VarsuffixContext varsuffixCtx)
        {
            var varname = varnameCtx.GetText();
            var varsuffix = varsuffixCtx != null ? varsuffixCtx.GetText() : null;
            var dataType = ir.GetSymbolTable().GetDataTypeFor(varname, varsuffix);
            return new VariableName(varname, dataType.GetRepr(), dataType);
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        //
        // Expr
        //
        //
        // Functions
        //
        // n, x$, y$
        // x$, y$
        // x$, n, m
        // x$, n
        //
        // Stmt
        //
        // struct
        // array
        // scalar data type
        // struct
        // scalar data type
        // scalar
        //PuffinBasicAtomTypeId.lookup(compCtx.var2.getText());
        // array
        // list
        // struct
        // scalar data type
        // set
        // dict
        // struct
        // scalar data type
        // struct
        // throw
        // GOTO postFuncDecl
        // LABEL FuncStart
        // Push child scope
        // Copy expr to result
        // Pop declaration scope
        // GOTO Caller
        // LABEL postFuncDecl
        // Patch GOTO postFuncDecl
        public override void EnterFunctionbeginstmt(PuffinBasicParser.FunctionbeginstmtContext ctx)
        {
            var variableName = GetVariableNameFromCtx(ctx.varname(), ctx.varsuffix());
            var udfId = ir.GetSymbolTable().AddVariableOrUDF(variableName, (variableName1) => Variable.Of(variableName1, VariableKindHint.UDF, GetCtxString(ctx)), (varId, varEntry, variable) =>
            {
                if (currentUdfState != null)
                {
                    throw new PuffinBasicSemanticError(BAD_FUNCTION_DEF, GetCtxString(ctx), "Function " + variableName + " defined in another function: " + currentUdfState.variableName);
                }

                currentUdfState = new UDFState(variableName, (STUDF)varEntry);
                udfStateMap[variable] = currentUdfState;

                // GOTO postFuncDecl
                currentUdfState.gotoPostFuncDecl = ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.GOTO_LABEL, ir.GetSymbolTable().AddGotoTarget(), NULL_ID, NULL_ID);

                // LABEL FuncStart
                currentUdfState.labelFuncStart = ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.LABEL, ir.GetSymbolTable().AddLabel(), NULL_ID, NULL_ID);

                // Push child scope
                ir.GetSymbolTable().PushDeclarationScope(varId, true);
            });
            currentUdfState.udfId = udfId;
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        //
        // Expr
        //
        //
        // Functions
        //
        // n, x$, y$
        // x$, y$
        // x$, n, m
        // x$, n
        //
        // Stmt
        //
        // struct
        // array
        // scalar data type
        // struct
        // scalar data type
        // scalar
        //PuffinBasicAtomTypeId.lookup(compCtx.var2.getText());
        // array
        // list
        // struct
        // scalar data type
        // set
        // dict
        // struct
        // scalar data type
        // struct
        // throw
        // GOTO postFuncDecl
        // LABEL FuncStart
        // Push child scope
        // Copy expr to result
        // Pop declaration scope
        // GOTO Caller
        // LABEL postFuncDecl
        // Patch GOTO postFuncDecl
        // GOTO postFuncDecl
        // LABEL FuncStart
        // Push child scope
        public override void ExitFunctionbeginstmt(PuffinBasicParser.FunctionbeginstmtContext ctx)
        {
            if (currentUdfState == null)
            {
                throw new PuffinBasicInternalError("CurrentUDFState not set!");
            }

            foreach (var compCtx in ctx.compositetype())
            {
                VariableName paramName;
                PuffinBasicType paramType;
                if (compCtx.var1 != null)
                {

                    // scalar
                    var scalarVarName = compCtx.var1.VARNAME().GetText();
                    var scalarAtomTypeId = ir.GetSymbolTable().GetDataTypeFor(scalarVarName, compCtx.var2 != null ? compCtx.var2.GetText() : null);

                    //PuffinBasicAtomTypeId.lookup(compCtx.var2.getText());
                    paramName = new VariableName(scalarVarName, scalarAtomTypeId.GetRepr(), scalarAtomTypeId);
                    paramType = new ScalarType(paramName.GetDataType());
                }
                else if (compCtx.LIST() != null)
                {

                    // list
                    paramName = new VariableName(compCtx.elem.VARNAME().GetText(), null, COMPOSITE);
                    PuffinBasicType itemType;
                    if (compCtx.list1 != null)
                    {

                        // struct
                        itemType = ir.GetSymbolTable().GetStructType(compCtx.list1.VARNAME().GetText());
                    }
                    else if (compCtx.list3 != null)
                    {

                        // array
                        var atomType = PuffinBasicAtomTypeIdExtensions.Lookup(compCtx.list3.GetText());
                        itemType = new ArrayType(atomType);
                    }
                    else
                    {

                        // scalar data type
                        itemType = new ScalarType(PuffinBasicAtomTypeIdExtensions.Lookup(compCtx.list2.GetText()));
                    }

                    paramType = new ListType(itemType);
                }
                else if (compCtx.SET() != null)
                {

                    // set
                    paramName = new VariableName(compCtx.elem.VARNAME().GetText(), null, COMPOSITE);
                    paramType = new SetType(new ScalarType(PuffinBasicAtomTypeIdExtensions.Lookup(compCtx.set2.GetText())));
                }
                else if (compCtx.DICT() != null)
                {

                    // dict
                    paramName = new VariableName(compCtx.elem.VARNAME().GetText(), null, COMPOSITE);
                    var keyType = new ScalarType(PuffinBasicAtomTypeIdExtensions.Lookup(compCtx.dictk1.GetText()));
                    PuffinBasicType valueType;
                    if (compCtx.dictv1 != null)
                    {

                        // struct
                        valueType = ir.GetSymbolTable().GetStructType(compCtx.dictv1.VARNAME().GetText());
                    }
                    else
                    {

                        // scalar data type
                        valueType = new ScalarType(PuffinBasicAtomTypeIdExtensions.Lookup(compCtx.dictv2.GetText()));
                    }

                    paramType = new DictType(keyType, valueType);
                }
                else if (compCtx.struct1 != null)
                {

                    // struct
                    var memberType = compCtx.struct1.VARNAME().GetText();
                    paramName = new VariableName(compCtx.elem.VARNAME().GetText(), null, COMPOSITE);
                    paramType = ir.GetSymbolTable().GetStructType(memberType);
                }
                else if (compCtx.DIM() != null)
                {

                    // array
                    var arrayName = compCtx.elem.VARNAME().GetText();
                    var arrayAtomType = ir.GetSymbolTable().GetDataTypeFor(arrayName, compCtx.elemsuffix != null ? compCtx.elemsuffix.GetText() : null);
                    List<int> dims = new List<int>(compCtx.DECIMAL().Count());
                    foreach (var dimStrNode in compCtx.DECIMAL())
                    {
                        dims.Add(Numbers.ParseInt32(dimStrNode.GetText(), GetCtxString(ctx)));
                    }

                    paramName = new VariableName(arrayName, arrayAtomType.GetRepr(), arrayAtomType);
                    paramType = new ArrayType(arrayAtomType, dims, true);
                }
                else
                {

                    // throw
                    throw new PuffinBasicSemanticError(DATA_TYPE_MISMATCH, GetCtxString(ctx), "Bad struct field: " + compCtx.GetText());
                }

                var paramId = ir.GetSymbolTable().AddVariableOrUDF(paramName, (variableName1) => new Variable(variableName1, paramType), (varId, varEntry, variable) =>
                {
                });
                currentUdfState.udfEntry.DeclareParam(paramId);
            }
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        //
        // Expr
        //
        //
        // Functions
        //
        // n, x$, y$
        // x$, y$
        // x$, n, m
        // x$, n
        //
        // Stmt
        //
        // struct
        // array
        // scalar data type
        // struct
        // scalar data type
        // scalar
        //PuffinBasicAtomTypeId.lookup(compCtx.var2.getText());
        // array
        // list
        // struct
        // scalar data type
        // set
        // dict
        // struct
        // scalar data type
        // struct
        // throw
        // GOTO postFuncDecl
        // LABEL FuncStart
        // Push child scope
        // Copy expr to result
        // Pop declaration scope
        // GOTO Caller
        // LABEL postFuncDecl
        // Patch GOTO postFuncDecl
        // GOTO postFuncDecl
        // LABEL FuncStart
        // Push child scope
        // scalar
        //PuffinBasicAtomTypeId.lookup(compCtx.var2.getText());
        // list
        // struct
        // array
        // scalar data type
        // set
        // dict
        // struct
        // scalar data type
        // struct
        // array
        // throw
        public override void ExitFunctionreturnstmt(PuffinBasicParser.FunctionreturnstmtContext ctx)
        {
            if (currentUdfState == null)
            {
                throw new PuffinBasicSemanticError(BAD_FUNCTION_DEF, GetCtxString(ctx), "Function return called without function begin!");
            }

            var udfId = currentUdfState.udfId;
            var returnInstr = LookupInstruction(ctx.expr());
            CheckDataTypeMatch(udfId, returnInstr.result, GetCtxString(ctx));

            // Copy expr to result
            ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.COPY, returnInstr.result, udfId, udfId);

            // GOTO LABEL gotoCaller
            currentUdfState.gotoLabelGotoCaller.Add(ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.GOTO_LABEL, ir.GetSymbolTable().AddGotoTarget(), NULL_ID, NULL_ID));
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        //
        // Expr
        //
        //
        // Functions
        //
        // n, x$, y$
        // x$, y$
        // x$, n, m
        // x$, n
        //
        // Stmt
        //
        // struct
        // array
        // scalar data type
        // struct
        // scalar data type
        // scalar
        //PuffinBasicAtomTypeId.lookup(compCtx.var2.getText());
        // array
        // list
        // struct
        // scalar data type
        // set
        // dict
        // struct
        // scalar data type
        // struct
        // throw
        // GOTO postFuncDecl
        // LABEL FuncStart
        // Push child scope
        // Copy expr to result
        // Pop declaration scope
        // GOTO Caller
        // LABEL postFuncDecl
        // Patch GOTO postFuncDecl
        // GOTO postFuncDecl
        // LABEL FuncStart
        // Push child scope
        // scalar
        //PuffinBasicAtomTypeId.lookup(compCtx.var2.getText());
        // list
        // struct
        // array
        // scalar data type
        // set
        // dict
        // struct
        // scalar data type
        // struct
        // array
        // throw
        // Copy expr to result
        // GOTO LABEL gotoCaller
        public override void ExitFunctionendstmt(PuffinBasicParser.FunctionendstmtContext ctx)
        {
            if (currentUdfState == null)
            {
                throw new PuffinBasicSemanticError(BAD_FUNCTION_DEF, GetCtxString(ctx), "Function return called without function begin!");
            }


            // Pop declaration scope
            ir.GetSymbolTable().PopScope();

            // LABEL gotoCaller
            var labelGotocaller = ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.LABEL, ir.GetSymbolTable().AddLabel(), NULL_ID, NULL_ID);

            // GOTO Caller
            ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.GOTO_CALLER, NULL_ID, NULL_ID, NULL_ID);

            // LABEL postFuncDecl
            var labelPostFuncDecl = ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.LABEL, ir.GetSymbolTable().AddLabel(), NULL_ID, NULL_ID);

            // Patch GOTO LABEL gotoCaller
            foreach (var g in currentUdfState.gotoLabelGotoCaller)
                g.PatchOp1(labelGotocaller.op1);

            // Patch GOTO postFuncDecl
            currentUdfState.gotoPostFuncDecl.PatchOp1(labelPostFuncDecl.op1);

            // Unset current UDF state
            currentUdfState = null;
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        //
        // Expr
        //
        //
        // Functions
        //
        // n, x$, y$
        // x$, y$
        // x$, n, m
        // x$, n
        //
        // Stmt
        //
        // struct
        // array
        // scalar data type
        // struct
        // scalar data type
        // scalar
        //PuffinBasicAtomTypeId.lookup(compCtx.var2.getText());
        // array
        // list
        // struct
        // scalar data type
        // set
        // dict
        // struct
        // scalar data type
        // struct
        // throw
        // GOTO postFuncDecl
        // LABEL FuncStart
        // Push child scope
        // Copy expr to result
        // Pop declaration scope
        // GOTO Caller
        // LABEL postFuncDecl
        // Patch GOTO postFuncDecl
        // GOTO postFuncDecl
        // LABEL FuncStart
        // Push child scope
        // scalar
        //PuffinBasicAtomTypeId.lookup(compCtx.var2.getText());
        // list
        // struct
        // array
        // scalar data type
        // set
        // dict
        // struct
        // scalar data type
        // struct
        // array
        // throw
        // Copy expr to result
        // GOTO LABEL gotoCaller
        // Pop declaration scope
        // LABEL gotoCaller
        // GOTO Caller
        // LABEL postFuncDecl
        // Patch GOTO LABEL gotoCaller
        // Patch GOTO postFuncDecl
        // Unset current UDF state
        public override void ExitImportstmt(PuffinBasicParser.ImportstmtContext ctx)
        {
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        //
        // Expr
        //
        //
        // Functions
        //
        // n, x$, y$
        // x$, y$
        // x$, n, m
        // x$, n
        //
        // Stmt
        //
        // struct
        // array
        // scalar data type
        // struct
        // scalar data type
        // scalar
        //PuffinBasicAtomTypeId.lookup(compCtx.var2.getText());
        // array
        // list
        // struct
        // scalar data type
        // set
        // dict
        // struct
        // scalar data type
        // struct
        // throw
        // GOTO postFuncDecl
        // LABEL FuncStart
        // Push child scope
        // Copy expr to result
        // Pop declaration scope
        // GOTO Caller
        // LABEL postFuncDecl
        // Patch GOTO postFuncDecl
        // GOTO postFuncDecl
        // LABEL FuncStart
        // Push child scope
        // scalar
        //PuffinBasicAtomTypeId.lookup(compCtx.var2.getText());
        // list
        // struct
        // array
        // scalar data type
        // set
        // dict
        // struct
        // scalar data type
        // struct
        // array
        // throw
        // Copy expr to result
        // GOTO LABEL gotoCaller
        // Pop declaration scope
        // LABEL gotoCaller
        // GOTO Caller
        // LABEL postFuncDecl
        // Patch GOTO LABEL gotoCaller
        // Patch GOTO postFuncDecl
        // Unset current UDF state
        public override void ExitEndstmt(PuffinBasicParser.EndstmtContext ctx)
        {
            ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.END, NULL_ID, NULL_ID, NULL_ID);
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        //
        // Expr
        //
        //
        // Functions
        //
        // n, x$, y$
        // x$, y$
        // x$, n, m
        // x$, n
        //
        // Stmt
        //
        // struct
        // array
        // scalar data type
        // struct
        // scalar data type
        // scalar
        //PuffinBasicAtomTypeId.lookup(compCtx.var2.getText());
        // array
        // list
        // struct
        // scalar data type
        // set
        // dict
        // struct
        // scalar data type
        // struct
        // throw
        // GOTO postFuncDecl
        // LABEL FuncStart
        // Push child scope
        // Copy expr to result
        // Pop declaration scope
        // GOTO Caller
        // LABEL postFuncDecl
        // Patch GOTO postFuncDecl
        // GOTO postFuncDecl
        // LABEL FuncStart
        // Push child scope
        // scalar
        //PuffinBasicAtomTypeId.lookup(compCtx.var2.getText());
        // list
        // struct
        // array
        // scalar data type
        // set
        // dict
        // struct
        // scalar data type
        // struct
        // array
        // throw
        // Copy expr to result
        // GOTO LABEL gotoCaller
        // Pop declaration scope
        // LABEL gotoCaller
        // GOTO Caller
        // LABEL postFuncDecl
        // Patch GOTO LABEL gotoCaller
        // Patch GOTO postFuncDecl
        // Unset current UDF state
        public override void EnterWhilestmt(PuffinBasicParser.WhilestmtContext ctx)
        {
            var whileLoopState = new WhileLoopState();

            // LABEL beforeWhile
            whileLoopState.labelBeforeWhile = ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.LABEL, ir.GetSymbolTable().AddLabel(), NULL_ID, NULL_ID);
            whileLoopStateList.AddLast(whileLoopState);
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        //
        // Expr
        //
        //
        // Functions
        //
        // n, x$, y$
        // x$, y$
        // x$, n, m
        // x$, n
        //
        // Stmt
        //
        // struct
        // array
        // scalar data type
        // struct
        // scalar data type
        // scalar
        //PuffinBasicAtomTypeId.lookup(compCtx.var2.getText());
        // array
        // list
        // struct
        // scalar data type
        // set
        // dict
        // struct
        // scalar data type
        // struct
        // throw
        // GOTO postFuncDecl
        // LABEL FuncStart
        // Push child scope
        // Copy expr to result
        // Pop declaration scope
        // GOTO Caller
        // LABEL postFuncDecl
        // Patch GOTO postFuncDecl
        // GOTO postFuncDecl
        // LABEL FuncStart
        // Push child scope
        // scalar
        //PuffinBasicAtomTypeId.lookup(compCtx.var2.getText());
        // list
        // struct
        // array
        // scalar data type
        // set
        // dict
        // struct
        // scalar data type
        // struct
        // array
        // throw
        // Copy expr to result
        // GOTO LABEL gotoCaller
        // Pop declaration scope
        // LABEL gotoCaller
        // GOTO Caller
        // LABEL postFuncDecl
        // Patch GOTO LABEL gotoCaller
        // Patch GOTO postFuncDecl
        // Unset current UDF state
        // LABEL beforeWhile
        public override void ExitWhilestmt(PuffinBasicParser.WhilestmtContext ctx)
        {
            var whileLoopState = whileLoopStateList.Last();

            // expr()
            var expr = LookupInstruction(ctx.expr());

            // NOT expr()
            var notExpr = ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.NOT, expr.result, NULL_ID, ir.GetSymbolTable().AddTmp(INT64, (e) =>
            {
            }));

            // If expr is false, GOTO afterWend
            whileLoopState.gotoAfterWend = ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.GOTO_LABEL_IF, notExpr.result, ir.GetSymbolTable().AddLabel(), NULL_ID);
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        //
        // Expr
        //
        //
        // Functions
        //
        // n, x$, y$
        // x$, y$
        // x$, n, m
        // x$, n
        //
        // Stmt
        //
        // struct
        // array
        // scalar data type
        // struct
        // scalar data type
        // scalar
        //PuffinBasicAtomTypeId.lookup(compCtx.var2.getText());
        // array
        // list
        // struct
        // scalar data type
        // set
        // dict
        // struct
        // scalar data type
        // struct
        // throw
        // GOTO postFuncDecl
        // LABEL FuncStart
        // Push child scope
        // Copy expr to result
        // Pop declaration scope
        // GOTO Caller
        // LABEL postFuncDecl
        // Patch GOTO postFuncDecl
        // GOTO postFuncDecl
        // LABEL FuncStart
        // Push child scope
        // scalar
        //PuffinBasicAtomTypeId.lookup(compCtx.var2.getText());
        // list
        // struct
        // array
        // scalar data type
        // set
        // dict
        // struct
        // scalar data type
        // struct
        // array
        // throw
        // Copy expr to result
        // GOTO LABEL gotoCaller
        // Pop declaration scope
        // LABEL gotoCaller
        // GOTO Caller
        // LABEL postFuncDecl
        // Patch GOTO LABEL gotoCaller
        // Patch GOTO postFuncDecl
        // Unset current UDF state
        // LABEL beforeWhile
        // expr()
        // NOT expr()
        // If expr is false, GOTO afterWend
        public override void ExitWendstmt(PuffinBasicParser.WendstmtContext ctx)
        {
            //if (whileLoopStateList.IsEmpty())
            if (!whileLoopStateList.Any())
            {
                throw new PuffinBasicSemanticError(PuffinBasicSemanticError.ErrorCode.WEND_WITHOUT_WHILE, GetCtxString(ctx), "Wend without while");
            }

            var whileLoopState = whileLoopStateList.Last();
            whileLoopStateList.RemoveLast();

            // GOTO LABEL beforeWhile
            ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.GOTO_LABEL, whileLoopState.labelBeforeWhile.op1, NULL_ID, NULL_ID);

            // LABEL afterWend
            var labelAfterWend = ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.LABEL, ir.GetSymbolTable().AddLabel(), NULL_ID, NULL_ID);

            // Patch GOTO afterWend
            whileLoopState.gotoAfterWend.PatchOp2(labelAfterWend.op1);
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        //
        // Expr
        //
        //
        // Functions
        //
        // n, x$, y$
        // x$, y$
        // x$, n, m
        // x$, n
        //
        // Stmt
        //
        // struct
        // array
        // scalar data type
        // struct
        // scalar data type
        // scalar
        //PuffinBasicAtomTypeId.lookup(compCtx.var2.getText());
        // array
        // list
        // struct
        // scalar data type
        // set
        // dict
        // struct
        // scalar data type
        // struct
        // throw
        // GOTO postFuncDecl
        // LABEL FuncStart
        // Push child scope
        // Copy expr to result
        // Pop declaration scope
        // GOTO Caller
        // LABEL postFuncDecl
        // Patch GOTO postFuncDecl
        // GOTO postFuncDecl
        // LABEL FuncStart
        // Push child scope
        // scalar
        //PuffinBasicAtomTypeId.lookup(compCtx.var2.getText());
        // list
        // struct
        // array
        // scalar data type
        // set
        // dict
        // struct
        // scalar data type
        // struct
        // array
        // throw
        // Copy expr to result
        // GOTO LABEL gotoCaller
        // Pop declaration scope
        // LABEL gotoCaller
        // GOTO Caller
        // LABEL postFuncDecl
        // Patch GOTO LABEL gotoCaller
        // Patch GOTO postFuncDecl
        // Unset current UDF state
        // LABEL beforeWhile
        // expr()
        // NOT expr()
        // If expr is false, GOTO afterWend
        // GOTO LABEL beforeWhile
        // LABEL afterWend
        // Patch GOTO afterWend
        private PuffinBasicIR.OpCode GetLTOpCode(PuffinBasicAtomTypeId dt1, PuffinBasicAtomTypeId dt2)
        {
            OpCode opCode;
            if (dt1 == PuffinBasicAtomTypeId.STRING && dt2 == PuffinBasicAtomTypeId.STRING)
            {
                opCode = OpCode.LTSTR;
            }
            else
            {
                if (dt1 == PuffinBasicAtomTypeId.DOUBLE || dt2 == PuffinBasicAtomTypeId.DOUBLE)
                {
                    opCode = OpCode.LTF64;
                }
                else if (dt1 == INT64 || dt2 == INT64)
                {
                    opCode = OpCode.LTI64;
                }
                else if (dt1 == PuffinBasicAtomTypeId.FLOAT || dt2 == PuffinBasicAtomTypeId.FLOAT)
                {
                    opCode = OpCode.LTF32;
                }
                else
                {
                    opCode = OpCode.LTI32;
                }
            }

            return opCode;
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        //
        // Expr
        //
        //
        // Functions
        //
        // n, x$, y$
        // x$, y$
        // x$, n, m
        // x$, n
        //
        // Stmt
        //
        // struct
        // array
        // scalar data type
        // struct
        // scalar data type
        // scalar
        //PuffinBasicAtomTypeId.lookup(compCtx.var2.getText());
        // array
        // list
        // struct
        // scalar data type
        // set
        // dict
        // struct
        // scalar data type
        // struct
        // throw
        // GOTO postFuncDecl
        // LABEL FuncStart
        // Push child scope
        // Copy expr to result
        // Pop declaration scope
        // GOTO Caller
        // LABEL postFuncDecl
        // Patch GOTO postFuncDecl
        // GOTO postFuncDecl
        // LABEL FuncStart
        // Push child scope
        // scalar
        //PuffinBasicAtomTypeId.lookup(compCtx.var2.getText());
        // list
        // struct
        // array
        // scalar data type
        // set
        // dict
        // struct
        // scalar data type
        // struct
        // array
        // throw
        // Copy expr to result
        // GOTO LABEL gotoCaller
        // Pop declaration scope
        // LABEL gotoCaller
        // GOTO Caller
        // LABEL postFuncDecl
        // Patch GOTO LABEL gotoCaller
        // Patch GOTO postFuncDecl
        // Unset current UDF state
        // LABEL beforeWhile
        // expr()
        // NOT expr()
        // If expr is false, GOTO afterWend
        // GOTO LABEL beforeWhile
        // LABEL afterWend
        // Patch GOTO afterWend
        private PuffinBasicIR.OpCode GetGTOpCode(PuffinBasicAtomTypeId dt1, PuffinBasicAtomTypeId dt2)
        {
            OpCode opCode;
            if (dt1 == PuffinBasicAtomTypeId.STRING && dt2 == PuffinBasicAtomTypeId.STRING)
            {
                opCode = OpCode.GTSTR;
            }
            else
            {
                if (dt1 == PuffinBasicAtomTypeId.DOUBLE || dt2 == PuffinBasicAtomTypeId.DOUBLE)
                {
                    opCode = OpCode.GTF64;
                }
                else if (dt1 == INT64 || dt2 == INT64)
                {
                    opCode = OpCode.GTI64;
                }
                else if (dt1 == PuffinBasicAtomTypeId.FLOAT || dt2 == PuffinBasicAtomTypeId.FLOAT)
                {
                    opCode = OpCode.GTF32;
                }
                else
                {
                    opCode = OpCode.GTI32;
                }
            }

            return opCode;
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        //
        // Expr
        //
        //
        // Functions
        //
        // n, x$, y$
        // x$, y$
        // x$, n, m
        // x$, n
        //
        // Stmt
        //
        // struct
        // array
        // scalar data type
        // struct
        // scalar data type
        // scalar
        //PuffinBasicAtomTypeId.lookup(compCtx.var2.getText());
        // array
        // list
        // struct
        // scalar data type
        // set
        // dict
        // struct
        // scalar data type
        // struct
        // throw
        // GOTO postFuncDecl
        // LABEL FuncStart
        // Push child scope
        // Copy expr to result
        // Pop declaration scope
        // GOTO Caller
        // LABEL postFuncDecl
        // Patch GOTO postFuncDecl
        // GOTO postFuncDecl
        // LABEL FuncStart
        // Push child scope
        // scalar
        //PuffinBasicAtomTypeId.lookup(compCtx.var2.getText());
        // list
        // struct
        // array
        // scalar data type
        // set
        // dict
        // struct
        // scalar data type
        // struct
        // array
        // throw
        // Copy expr to result
        // GOTO LABEL gotoCaller
        // Pop declaration scope
        // LABEL gotoCaller
        // GOTO Caller
        // LABEL postFuncDecl
        // Patch GOTO LABEL gotoCaller
        // Patch GOTO postFuncDecl
        // Unset current UDF state
        // LABEL beforeWhile
        // expr()
        // NOT expr()
        // If expr is false, GOTO afterWend
        // GOTO LABEL beforeWhile
        // LABEL afterWend
        // Patch GOTO afterWend
        private PuffinBasicIR.OpCode GetGEOpCode(PuffinBasicAtomTypeId dt1, PuffinBasicAtomTypeId dt2)
        {
            OpCode opCode;
            if (dt1 == PuffinBasicAtomTypeId.STRING && dt2 == PuffinBasicAtomTypeId.STRING)
            {
                opCode = OpCode.GESTR;
            }
            else
            {
                if (dt1 == PuffinBasicAtomTypeId.DOUBLE || dt2 == PuffinBasicAtomTypeId.DOUBLE)
                {
                    opCode = OpCode.GEF64;
                }
                else if (dt1 == INT64 || dt2 == INT64)
                {
                    opCode = OpCode.GEI64;
                }
                else if (dt1 == PuffinBasicAtomTypeId.FLOAT || dt2 == PuffinBasicAtomTypeId.FLOAT)
                {
                    opCode = OpCode.GEF32;
                }
                else
                {
                    opCode = OpCode.GEI32;
                }
            }

            return opCode;
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        //
        // Expr
        //
        //
        // Functions
        //
        // n, x$, y$
        // x$, y$
        // x$, n, m
        // x$, n
        //
        // Stmt
        //
        // struct
        // array
        // scalar data type
        // struct
        // scalar data type
        // scalar
        //PuffinBasicAtomTypeId.lookup(compCtx.var2.getText());
        // array
        // list
        // struct
        // scalar data type
        // set
        // dict
        // struct
        // scalar data type
        // struct
        // throw
        // GOTO postFuncDecl
        // LABEL FuncStart
        // Push child scope
        // Copy expr to result
        // Pop declaration scope
        // GOTO Caller
        // LABEL postFuncDecl
        // Patch GOTO postFuncDecl
        // GOTO postFuncDecl
        // LABEL FuncStart
        // Push child scope
        // scalar
        //PuffinBasicAtomTypeId.lookup(compCtx.var2.getText());
        // list
        // struct
        // array
        // scalar data type
        // set
        // dict
        // struct
        // scalar data type
        // struct
        // array
        // throw
        // Copy expr to result
        // GOTO LABEL gotoCaller
        // Pop declaration scope
        // LABEL gotoCaller
        // GOTO Caller
        // LABEL postFuncDecl
        // Patch GOTO LABEL gotoCaller
        // Patch GOTO postFuncDecl
        // Unset current UDF state
        // LABEL beforeWhile
        // expr()
        // NOT expr()
        // If expr is false, GOTO afterWend
        // GOTO LABEL beforeWhile
        // LABEL afterWend
        // Patch GOTO afterWend
        public override void ExitForstmt(PuffinBasicParser.ForstmtContext ctx)
        {
            var varInstr = LookupInstruction(ctx.variable());
            var init = LookupInstruction(ctx.expr(0));
            var end = LookupInstruction(ctx.expr(1));
            Types.AssertNumeric(ir.GetSymbolTable()[init.result].GetType().GetAtomTypeId(), GetCtxString(ctx));
            Types.AssertNumeric(ir.GetSymbolTable()[end.result].GetType().GetAtomTypeId(), GetCtxString(ctx));
            var forLoopState = new ForLoopState();
            var stVariable = (STVariable)ir.GetSymbolTable()[varInstr.result];
            forLoopState.variable = stVariable.GetVariable();

            // stepCopy = step or 1 (default)
            Instruction stepCopy;
            if (ctx.expr(2) != null)
            {
                var step = LookupInstruction(ctx.expr(2));
                Types.AssertNumeric(ir.GetSymbolTable()[step.result].GetType().GetAtomTypeId(), GetCtxString(ctx));
                var tmpStep = ir.GetSymbolTable().AddTmpCompatibleWith(step.result);
                stepCopy = ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.COPY, step.result, tmpStep, tmpStep);
            }
            else
            {
                var tmpStep = ir.GetSymbolTable().AddTmp(INT32, (e) => e.GetValue().SetInt32(1));
                stepCopy = ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.VALUE, tmpStep, NULL_ID, tmpStep);
            }


            // var=init
            ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.ASSIGN, init.result, varInstr.result, varInstr.result);

            // endCopy=end
            var tmpEnd = ir.GetSymbolTable().AddTmpCompatibleWith(end.result);
            var endCopy = ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.ASSIGN, end.result, tmpEnd, tmpEnd);

            // GOTO LABEL CHECK
            var gotoLabelCheck = ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.GOTO_LABEL, ir.GetSymbolTable().AddGotoTarget(), NULL_ID, NULL_ID);

            // APPLY STEP
            // JUMP here from NEXT
            forLoopState.labelApplyStep = ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.LABEL, ir.GetSymbolTable().AddLabel(), NULL_ID, NULL_ID);

            // Add step
            var tmpAdd = ir.GetSymbolTable().AddTmpCompatibleWith(varInstr.result);
            OpCode addOpCode;
            switch (stVariable.GetType().GetAtomTypeId())
            {
                case INT32:
                    addOpCode = OpCode.ADDI32;
                    break;
                case INT64:
                    addOpCode = OpCode.ADDI64;
                    break;
                case PuffinBasicAtomTypeId.FLOAT:
                    addOpCode = OpCode.ADDF32;
                    break;
                case PuffinBasicAtomTypeId.DOUBLE:
                    addOpCode = OpCode.ADDF64;
                    break;
                default:
                    throw new PuffinBasicInternalError("Bad type: " + stVariable.GetType().GetAtomTypeId());
                    break;
            }

            ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, addOpCode, varInstr.result, stepCopy.result, tmpAdd);
            ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.ASSIGN, tmpAdd, varInstr.result, varInstr.result);

            // CHECK
            // If (step >= 0 and var > end) or (step < 0 and var < end) GOTO after "next"
            // step >= 0
            var labelCheck = ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.LABEL, ir.GetSymbolTable().AddLabel(), NULL_ID, NULL_ID);
            var zero = ir.GetSymbolTable().AddTmp(INT32, (e) => e.GetValue().SetInt32(0));
            var t1 = ir.GetSymbolTable().AddTmp(INT32, (e) =>
            {
            });
            ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, GetGEOpCode(ir.GetSymbolTable()[stepCopy.result].GetType().GetAtomTypeId(), INT32), stepCopy.result, zero, t1);

            // Patch GOTO LABEL Check
            gotoLabelCheck.PatchOp1(labelCheck.op1);

            // var > end
            var t2 = ir.GetSymbolTable().AddTmp(INT32, (e) =>
            {
            });
            ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, GetGTOpCode(ir.GetSymbolTable()[varInstr.result].GetType().GetAtomTypeId(), ir.GetSymbolTable()[endCopy.result].GetType().GetAtomTypeId()), varInstr.result, endCopy.result, t2);

            // (step >= 0 and var > end)
            var t3 = ir.GetSymbolTable().AddTmp(INT32, (e) =>
            {
            });
            ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.AND, t1, t2, t3);

            // step < 0
            var t4 = ir.GetSymbolTable().AddTmp(INT32, (e) =>
            {
            });
            ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, GetLTOpCode(ir.GetSymbolTable()[stepCopy.result].GetType().GetAtomTypeId(), INT32), stepCopy.result, zero, t4);

            // var < end
            var t5 = ir.GetSymbolTable().AddTmp(INT32, (e) =>
            {
            });
            ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, GetLTOpCode(ir.GetSymbolTable()[varInstr.result].GetType().GetAtomTypeId(), ir.GetSymbolTable()[endCopy.result].GetType().GetAtomTypeId()), varInstr.result, endCopy.result, t5);

            // (step < 0 and var < end)
            var t6 = ir.GetSymbolTable().AddTmp(INT32, (e) =>
            {
            });
            ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.AND, t4, t5, t6);
            var t7 = ir.GetSymbolTable().AddTmp(INT32, (e) =>
            {
            });
            ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.OR, t3, t6, t7);

            // if (true) GOTO after NEXT
            // set linenumber on exitNext().
            forLoopState.gotoAfterNext = ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.GOTO_LABEL_IF, t7, ir.GetSymbolTable().AddLabel(), NULL_ID);
            forLoopStateList.AddLast(forLoopState);
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        //
        // Expr
        //
        //
        // Functions
        //
        // n, x$, y$
        // x$, y$
        // x$, n, m
        // x$, n
        //
        // Stmt
        //
        // struct
        // array
        // scalar data type
        // struct
        // scalar data type
        // scalar
        //PuffinBasicAtomTypeId.lookup(compCtx.var2.getText());
        // array
        // list
        // struct
        // scalar data type
        // set
        // dict
        // struct
        // scalar data type
        // struct
        // throw
        // GOTO postFuncDecl
        // LABEL FuncStart
        // Push child scope
        // Copy expr to result
        // Pop declaration scope
        // GOTO Caller
        // LABEL postFuncDecl
        // Patch GOTO postFuncDecl
        // GOTO postFuncDecl
        // LABEL FuncStart
        // Push child scope
        // scalar
        //PuffinBasicAtomTypeId.lookup(compCtx.var2.getText());
        // list
        // struct
        // array
        // scalar data type
        // set
        // dict
        // struct
        // scalar data type
        // struct
        // array
        // throw
        // Copy expr to result
        // GOTO LABEL gotoCaller
        // Pop declaration scope
        // LABEL gotoCaller
        // GOTO Caller
        // LABEL postFuncDecl
        // Patch GOTO LABEL gotoCaller
        // Patch GOTO postFuncDecl
        // Unset current UDF state
        // LABEL beforeWhile
        // expr()
        // NOT expr()
        // If expr is false, GOTO afterWend
        // GOTO LABEL beforeWhile
        // LABEL afterWend
        // Patch GOTO afterWend
        // stepCopy = step or 1 (default)
        // var=init
        // endCopy=end
        // GOTO LABEL CHECK
        // APPLY STEP
        // JUMP here from NEXT
        // Add step
        // CHECK
        // If (step >= 0 and var > end) or (step < 0 and var < end) GOTO after "next"
        // step >= 0
        // Patch GOTO LABEL Check
        // var > end
        // (step >= 0 and var > end)
        // step < 0
        // var < end
        // (step < 0 and var < end)
        // if (true) GOTO after NEXT
        // set linenumber on exitNext().
        public override void ExitNextstmt(PuffinBasicParser.NextstmtContext ctx)
        {
            IList<ForLoopState> states = new List<ForLoopState>(1);
            if (!ctx.variable().Any())
            {
                if (forLoopStateList.Any())
                { 
                    states.Add(forLoopStateList.Last());
                    forLoopStateList.RemoveLast();
                }
                else
                {
                    throw new PuffinBasicSemanticError(NEXT_WITHOUT_FOR, GetCtxString(ctx), "NEXT without FOR");
                }
            }
            else
            {
                foreach (var varCtx in ctx.variable())
                {
                    if (varCtx.leafvariable() == null)
                    {
                        throw new PuffinBasicSemanticError(BAD_ARGUMENT, GetCtxString(ctx), "Bad variable!");
                    }

                    var varname = varCtx.leafvariable().varname().VARNAME().GetText();
                    var varsuffix = varCtx.leafvariable().varsuffix() != null ? varCtx.leafvariable().varsuffix().GetText() : null;
                    var dataType = ir.GetSymbolTable().GetDataTypeFor(varname, varsuffix);
                    var variableName = new VariableName(varname, dataType.GetRepr(), dataType);
                    int id = ir.GetSymbolTable().AddVariableOrUDF(variableName, (variableName1) => Variable.Of(variableName1, VariableKindHint.DERIVE_FROM_NAME, GetCtxString(ctx)), (id1, e1, v1) =>
                    {
                    });
                    var variable = ((STVariable)ir.GetSymbolTable()[id]).GetVariable();
                    if (!forLoopStateList.Any())
                    {
                        throw new PuffinBasicSemanticError(NEXT_WITHOUT_FOR, GetCtxString(ctx), "NEXT without FOR");
                    }

                    var state = forLoopStateList.Last();
                    forLoopStateList.RemoveLast();
                    if (state.variable.Equals(variable))
                    {
                        states.Add(state);
                    }
                    else
                    {
                        throw new PuffinBasicSemanticError(NEXT_WITHOUT_FOR, GetCtxString(ctx), "Next " + variable + " without FOR");
                    }
                }
            }

            foreach (ForLoopState state in states)
            {

                // GOTO APPLY STEP
                ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.GOTO_LABEL, state.labelApplyStep.op1, NULL_ID, NULL_ID);

                // LABEL afterNext
                var labelAfterNext = ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.LABEL, ir.GetSymbolTable().AddLabel(), NULL_ID, NULL_ID);
                state.gotoAfterNext.PatchOp2(labelAfterNext.op1);
            }
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        //
        // Expr
        //
        //
        // Functions
        //
        // n, x$, y$
        // x$, y$
        // x$, n, m
        // x$, n
        //
        // Stmt
        //
        // struct
        // array
        // scalar data type
        // struct
        // scalar data type
        // scalar
        //PuffinBasicAtomTypeId.lookup(compCtx.var2.getText());
        // array
        // list
        // struct
        // scalar data type
        // set
        // dict
        // struct
        // scalar data type
        // struct
        // throw
        // GOTO postFuncDecl
        // LABEL FuncStart
        // Push child scope
        // Copy expr to result
        // Pop declaration scope
        // GOTO Caller
        // LABEL postFuncDecl
        // Patch GOTO postFuncDecl
        // GOTO postFuncDecl
        // LABEL FuncStart
        // Push child scope
        // scalar
        //PuffinBasicAtomTypeId.lookup(compCtx.var2.getText());
        // list
        // struct
        // array
        // scalar data type
        // set
        // dict
        // struct
        // scalar data type
        // struct
        // array
        // throw
        // Copy expr to result
        // GOTO LABEL gotoCaller
        // Pop declaration scope
        // LABEL gotoCaller
        // GOTO Caller
        // LABEL postFuncDecl
        // Patch GOTO LABEL gotoCaller
        // Patch GOTO postFuncDecl
        // Unset current UDF state
        // LABEL beforeWhile
        // expr()
        // NOT expr()
        // If expr is false, GOTO afterWend
        // GOTO LABEL beforeWhile
        // LABEL afterWend
        // Patch GOTO afterWend
        // stepCopy = step or 1 (default)
        // var=init
        // endCopy=end
        // GOTO LABEL CHECK
        // APPLY STEP
        // JUMP here from NEXT
        // Add step
        // CHECK
        // If (step >= 0 and var > end) or (step < 0 and var < end) GOTO after "next"
        // step >= 0
        // Patch GOTO LABEL Check
        // var > end
        // (step >= 0 and var > end)
        // step < 0
        // var < end
        // (step < 0 and var < end)
        // if (true) GOTO after NEXT
        // set linenumber on exitNext().
        // GOTO APPLY STEP
        // LABEL afterNext
        /*
         * condition
         * GOTOIF condition labelBeforeThen
         * GOTO labelAfterThen|labelBeforeElse
         * labelBeforeThen
         * ThenStmts
         * GOTO labelAfterThen|labelAfterElse
         * labelAfterThen
         * ElseStmts
         * labelAfterElse
         */
        public override void EnterIfThenElse(PuffinBasicParser.IfThenElseContext ctx)
        {
            nodeToIfState.Put(ctx, new IfState());
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        //
        // Expr
        //
        //
        // Functions
        //
        // n, x$, y$
        // x$, y$
        // x$, n, m
        // x$, n
        //
        // Stmt
        //
        // struct
        // array
        // scalar data type
        // struct
        // scalar data type
        // scalar
        //PuffinBasicAtomTypeId.lookup(compCtx.var2.getText());
        // array
        // list
        // struct
        // scalar data type
        // set
        // dict
        // struct
        // scalar data type
        // struct
        // throw
        // GOTO postFuncDecl
        // LABEL FuncStart
        // Push child scope
        // Copy expr to result
        // Pop declaration scope
        // GOTO Caller
        // LABEL postFuncDecl
        // Patch GOTO postFuncDecl
        // GOTO postFuncDecl
        // LABEL FuncStart
        // Push child scope
        // scalar
        //PuffinBasicAtomTypeId.lookup(compCtx.var2.getText());
        // list
        // struct
        // array
        // scalar data type
        // set
        // dict
        // struct
        // scalar data type
        // struct
        // array
        // throw
        // Copy expr to result
        // GOTO LABEL gotoCaller
        // Pop declaration scope
        // LABEL gotoCaller
        // GOTO Caller
        // LABEL postFuncDecl
        // Patch GOTO LABEL gotoCaller
        // Patch GOTO postFuncDecl
        // Unset current UDF state
        // LABEL beforeWhile
        // expr()
        // NOT expr()
        // If expr is false, GOTO afterWend
        // GOTO LABEL beforeWhile
        // LABEL afterWend
        // Patch GOTO afterWend
        // stepCopy = step or 1 (default)
        // var=init
        // endCopy=end
        // GOTO LABEL CHECK
        // APPLY STEP
        // JUMP here from NEXT
        // Add step
        // CHECK
        // If (step >= 0 and var > end) or (step < 0 and var < end) GOTO after "next"
        // step >= 0
        // Patch GOTO LABEL Check
        // var > end
        // (step >= 0 and var > end)
        // step < 0
        // var < end
        // (step < 0 and var < end)
        // if (true) GOTO after NEXT
        // set linenumber on exitNext().
        // GOTO APPLY STEP
        // LABEL afterNext
        /*
         * condition
         * GOTOIF condition labelBeforeThen
         * GOTO labelAfterThen|labelBeforeElse
         * labelBeforeThen
         * ThenStmts
         * GOTO labelAfterThen|labelAfterElse
         * labelAfterThen
         * ElseStmts
         * labelAfterElse
         */
        public override void ExitIfThenElse(PuffinBasicParser.IfThenElseContext ctx)
        {
            var ifState = nodeToIfState.Get(ctx);
            bool noElseStmt = ifState.labelBeforeElse == null;
            var condition = LookupInstruction(ctx.expr());

            // Patch IF true: condition
            ifState.gotoIfConditionTrue.PatchOp1(condition.result);

            // Patch IF true: GOTO labelBeforeThen
            ifState.gotoIfConditionTrue.PatchOp2(ifState.labelBeforeThen.op1);

            // Patch IF false: GOTO labelAfterThen|labelBeforeElse
            ifState.gotoIfConditionFalse.PatchOp1(noElseStmt ? ifState.labelAfterThen.op1 : ifState.labelBeforeElse.op1);

            // Patch THEN: GOTO labelAfterThen|labelAfterElse
            ifState.gotoFromThenAfterIf.PatchOp1(noElseStmt ? ifState.labelAfterThen.op1 : ifState.labelAfterElse.op1);
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        //
        // Expr
        //
        //
        // Functions
        //
        // n, x$, y$
        // x$, y$
        // x$, n, m
        // x$, n
        //
        // Stmt
        //
        // struct
        // array
        // scalar data type
        // struct
        // scalar data type
        // scalar
        //PuffinBasicAtomTypeId.lookup(compCtx.var2.getText());
        // array
        // list
        // struct
        // scalar data type
        // set
        // dict
        // struct
        // scalar data type
        // struct
        // throw
        // GOTO postFuncDecl
        // LABEL FuncStart
        // Push child scope
        // Copy expr to result
        // Pop declaration scope
        // GOTO Caller
        // LABEL postFuncDecl
        // Patch GOTO postFuncDecl
        // GOTO postFuncDecl
        // LABEL FuncStart
        // Push child scope
        // scalar
        //PuffinBasicAtomTypeId.lookup(compCtx.var2.getText());
        // list
        // struct
        // array
        // scalar data type
        // set
        // dict
        // struct
        // scalar data type
        // struct
        // array
        // throw
        // Copy expr to result
        // GOTO LABEL gotoCaller
        // Pop declaration scope
        // LABEL gotoCaller
        // GOTO Caller
        // LABEL postFuncDecl
        // Patch GOTO LABEL gotoCaller
        // Patch GOTO postFuncDecl
        // Unset current UDF state
        // LABEL beforeWhile
        // expr()
        // NOT expr()
        // If expr is false, GOTO afterWend
        // GOTO LABEL beforeWhile
        // LABEL afterWend
        // Patch GOTO afterWend
        // stepCopy = step or 1 (default)
        // var=init
        // endCopy=end
        // GOTO LABEL CHECK
        // APPLY STEP
        // JUMP here from NEXT
        // Add step
        // CHECK
        // If (step >= 0 and var > end) or (step < 0 and var < end) GOTO after "next"
        // step >= 0
        // Patch GOTO LABEL Check
        // var > end
        // (step >= 0 and var > end)
        // step < 0
        // var < end
        // (step < 0 and var < end)
        // if (true) GOTO after NEXT
        // set linenumber on exitNext().
        // GOTO APPLY STEP
        // LABEL afterNext
        /*
         * condition
         * GOTOIF condition labelBeforeThen
         * GOTO labelAfterThen|labelBeforeElse
         * labelBeforeThen
         * ThenStmts
         * GOTO labelAfterThen|labelAfterElse
         * labelAfterThen
         * ElseStmts
         * labelAfterElse
         */
        // Patch IF true: condition
        // Patch IF true: GOTO labelBeforeThen
        // Patch IF false: GOTO labelAfterThen|labelBeforeElse
        // Patch THEN: GOTO labelAfterThen|labelAfterElse
        public override void EnterThen(PuffinBasicParser.ThenContext ctx)
        {
            var ifState = nodeToIfState.Get(ctx.Parent);

            // IF condition is true, GOTO labelBeforeThen
            ifState.gotoIfConditionTrue = ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.GOTO_LABEL_IF, ir.GetSymbolTable().AddGotoTarget(), NULL_ID, NULL_ID);

            // IF condition is false, GOTO labelAfterThen|labelBeforeElse
            ifState.gotoIfConditionFalse = ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.GOTO_LABEL, ir.GetSymbolTable().AddGotoTarget(), NULL_ID, NULL_ID);
            ifState.labelBeforeThen = ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.LABEL, ir.GetSymbolTable().AddLabel(), NULL_ID, NULL_ID);
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        //
        // Expr
        //
        //
        // Functions
        //
        // n, x$, y$
        // x$, y$
        // x$, n, m
        // x$, n
        //
        // Stmt
        //
        // struct
        // array
        // scalar data type
        // struct
        // scalar data type
        // scalar
        //PuffinBasicAtomTypeId.lookup(compCtx.var2.getText());
        // array
        // list
        // struct
        // scalar data type
        // set
        // dict
        // struct
        // scalar data type
        // struct
        // throw
        // GOTO postFuncDecl
        // LABEL FuncStart
        // Push child scope
        // Copy expr to result
        // Pop declaration scope
        // GOTO Caller
        // LABEL postFuncDecl
        // Patch GOTO postFuncDecl
        // GOTO postFuncDecl
        // LABEL FuncStart
        // Push child scope
        // scalar
        //PuffinBasicAtomTypeId.lookup(compCtx.var2.getText());
        // list
        // struct
        // array
        // scalar data type
        // set
        // dict
        // struct
        // scalar data type
        // struct
        // array
        // throw
        // Copy expr to result
        // GOTO LABEL gotoCaller
        // Pop declaration scope
        // LABEL gotoCaller
        // GOTO Caller
        // LABEL postFuncDecl
        // Patch GOTO LABEL gotoCaller
        // Patch GOTO postFuncDecl
        // Unset current UDF state
        // LABEL beforeWhile
        // expr()
        // NOT expr()
        // If expr is false, GOTO afterWend
        // GOTO LABEL beforeWhile
        // LABEL afterWend
        // Patch GOTO afterWend
        // stepCopy = step or 1 (default)
        // var=init
        // endCopy=end
        // GOTO LABEL CHECK
        // APPLY STEP
        // JUMP here from NEXT
        // Add step
        // CHECK
        // If (step >= 0 and var > end) or (step < 0 and var < end) GOTO after "next"
        // step >= 0
        // Patch GOTO LABEL Check
        // var > end
        // (step >= 0 and var > end)
        // step < 0
        // var < end
        // (step < 0 and var < end)
        // if (true) GOTO after NEXT
        // set linenumber on exitNext().
        // GOTO APPLY STEP
        // LABEL afterNext
        /*
         * condition
         * GOTOIF condition labelBeforeThen
         * GOTO labelAfterThen|labelBeforeElse
         * labelBeforeThen
         * ThenStmts
         * GOTO labelAfterThen|labelAfterElse
         * labelAfterThen
         * ElseStmts
         * labelAfterElse
         */
        // Patch IF true: condition
        // Patch IF true: GOTO labelBeforeThen
        // Patch IF false: GOTO labelAfterThen|labelBeforeElse
        // Patch THEN: GOTO labelAfterThen|labelAfterElse
        // IF condition is true, GOTO labelBeforeThen
        // IF condition is false, GOTO labelAfterThen|labelBeforeElse
        public override void ExitThen(PuffinBasicParser.ThenContext ctx)
        {

            // Add instruction for:
            // THEN GOTO linenum | THEN linenum
            if (ctx.linenum() != null)
            {
                var gotoLinenum = ParseLinenum(ctx.linenum().GetText());
                ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.GOTO_LINENUM, GetGotoLineNumberOp1(gotoLinenum), NULL_ID, NULL_ID);
            }

            var ifState = nodeToIfState.Get(ctx.Parent);

            // GOTO labelAfterThen|labelAfterElse
            ifState.gotoFromThenAfterIf = ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.GOTO_LABEL, ir.GetSymbolTable().AddLabel(), NULL_ID, NULL_ID);
            ifState.labelAfterThen = ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.LABEL, ir.GetSymbolTable().AddLabel(), NULL_ID, NULL_ID);
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        //
        // Expr
        //
        //
        // Functions
        //
        // n, x$, y$
        // x$, y$
        // x$, n, m
        // x$, n
        //
        // Stmt
        //
        // struct
        // array
        // scalar data type
        // struct
        // scalar data type
        // scalar
        //PuffinBasicAtomTypeId.lookup(compCtx.var2.getText());
        // array
        // list
        // struct
        // scalar data type
        // set
        // dict
        // struct
        // scalar data type
        // struct
        // throw
        // GOTO postFuncDecl
        // LABEL FuncStart
        // Push child scope
        // Copy expr to result
        // Pop declaration scope
        // GOTO Caller
        // LABEL postFuncDecl
        // Patch GOTO postFuncDecl
        // GOTO postFuncDecl
        // LABEL FuncStart
        // Push child scope
        // scalar
        //PuffinBasicAtomTypeId.lookup(compCtx.var2.getText());
        // list
        // struct
        // array
        // scalar data type
        // set
        // dict
        // struct
        // scalar data type
        // struct
        // array
        // throw
        // Copy expr to result
        // GOTO LABEL gotoCaller
        // Pop declaration scope
        // LABEL gotoCaller
        // GOTO Caller
        // LABEL postFuncDecl
        // Patch GOTO LABEL gotoCaller
        // Patch GOTO postFuncDecl
        // Unset current UDF state
        // LABEL beforeWhile
        // expr()
        // NOT expr()
        // If expr is false, GOTO afterWend
        // GOTO LABEL beforeWhile
        // LABEL afterWend
        // Patch GOTO afterWend
        // stepCopy = step or 1 (default)
        // var=init
        // endCopy=end
        // GOTO LABEL CHECK
        // APPLY STEP
        // JUMP here from NEXT
        // Add step
        // CHECK
        // If (step >= 0 and var > end) or (step < 0 and var < end) GOTO after "next"
        // step >= 0
        // Patch GOTO LABEL Check
        // var > end
        // (step >= 0 and var > end)
        // step < 0
        // var < end
        // (step < 0 and var < end)
        // if (true) GOTO after NEXT
        // set linenumber on exitNext().
        // GOTO APPLY STEP
        // LABEL afterNext
        /*
         * condition
         * GOTOIF condition labelBeforeThen
         * GOTO labelAfterThen|labelBeforeElse
         * labelBeforeThen
         * ThenStmts
         * GOTO labelAfterThen|labelAfterElse
         * labelAfterThen
         * ElseStmts
         * labelAfterElse
         */
        // Patch IF true: condition
        // Patch IF true: GOTO labelBeforeThen
        // Patch IF false: GOTO labelAfterThen|labelBeforeElse
        // Patch THEN: GOTO labelAfterThen|labelAfterElse
        // IF condition is true, GOTO labelBeforeThen
        // IF condition is false, GOTO labelAfterThen|labelBeforeElse
        // Add instruction for:
        // THEN GOTO linenum | THEN linenum
        // GOTO labelAfterThen|labelAfterElse
        public override void EnterElsestmt(PuffinBasicParser.ElsestmtContext ctx)
        {
            var ifState = nodeToIfState.Get(ctx.Parent);
            ifState.labelBeforeElse = ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.LABEL, ir.GetSymbolTable().AddLabel(), NULL_ID, NULL_ID);
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        //
        // Expr
        //
        //
        // Functions
        //
        // n, x$, y$
        // x$, y$
        // x$, n, m
        // x$, n
        //
        // Stmt
        //
        // struct
        // array
        // scalar data type
        // struct
        // scalar data type
        // scalar
        //PuffinBasicAtomTypeId.lookup(compCtx.var2.getText());
        // array
        // list
        // struct
        // scalar data type
        // set
        // dict
        // struct
        // scalar data type
        // struct
        // throw
        // GOTO postFuncDecl
        // LABEL FuncStart
        // Push child scope
        // Copy expr to result
        // Pop declaration scope
        // GOTO Caller
        // LABEL postFuncDecl
        // Patch GOTO postFuncDecl
        // GOTO postFuncDecl
        // LABEL FuncStart
        // Push child scope
        // scalar
        //PuffinBasicAtomTypeId.lookup(compCtx.var2.getText());
        // list
        // struct
        // array
        // scalar data type
        // set
        // dict
        // struct
        // scalar data type
        // struct
        // array
        // throw
        // Copy expr to result
        // GOTO LABEL gotoCaller
        // Pop declaration scope
        // LABEL gotoCaller
        // GOTO Caller
        // LABEL postFuncDecl
        // Patch GOTO LABEL gotoCaller
        // Patch GOTO postFuncDecl
        // Unset current UDF state
        // LABEL beforeWhile
        // expr()
        // NOT expr()
        // If expr is false, GOTO afterWend
        // GOTO LABEL beforeWhile
        // LABEL afterWend
        // Patch GOTO afterWend
        // stepCopy = step or 1 (default)
        // var=init
        // endCopy=end
        // GOTO LABEL CHECK
        // APPLY STEP
        // JUMP here from NEXT
        // Add step
        // CHECK
        // If (step >= 0 and var > end) or (step < 0 and var < end) GOTO after "next"
        // step >= 0
        // Patch GOTO LABEL Check
        // var > end
        // (step >= 0 and var > end)
        // step < 0
        // var < end
        // (step < 0 and var < end)
        // if (true) GOTO after NEXT
        // set linenumber on exitNext().
        // GOTO APPLY STEP
        // LABEL afterNext
        /*
         * condition
         * GOTOIF condition labelBeforeThen
         * GOTO labelAfterThen|labelBeforeElse
         * labelBeforeThen
         * ThenStmts
         * GOTO labelAfterThen|labelAfterElse
         * labelAfterThen
         * ElseStmts
         * labelAfterElse
         */
        // Patch IF true: condition
        // Patch IF true: GOTO labelBeforeThen
        // Patch IF false: GOTO labelAfterThen|labelBeforeElse
        // Patch THEN: GOTO labelAfterThen|labelAfterElse
        // IF condition is true, GOTO labelBeforeThen
        // IF condition is false, GOTO labelAfterThen|labelBeforeElse
        // Add instruction for:
        // THEN GOTO linenum | THEN linenum
        // GOTO labelAfterThen|labelAfterElse
        public override void ExitElsestmt(PuffinBasicParser.ElsestmtContext ctx)
        {

            // Add instruction for:
            // ELSE linenum
            if (ctx.linenum() != null)
            {
                var gotoLinenum = ParseLinenum(ctx.linenum().GetText());
                ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.GOTO_LINENUM, GetGotoLineNumberOp1(gotoLinenum), NULL_ID, NULL_ID);
            }

            var ifState = nodeToIfState.Get(ctx.Parent);
            ifState.labelAfterElse = ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.LABEL, ir.GetSymbolTable().AddLabel(), NULL_ID, NULL_ID);
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        //
        // Expr
        //
        //
        // Functions
        //
        // n, x$, y$
        // x$, y$
        // x$, n, m
        // x$, n
        //
        // Stmt
        //
        // struct
        // array
        // scalar data type
        // struct
        // scalar data type
        // scalar
        //PuffinBasicAtomTypeId.lookup(compCtx.var2.getText());
        // array
        // list
        // struct
        // scalar data type
        // set
        // dict
        // struct
        // scalar data type
        // struct
        // throw
        // GOTO postFuncDecl
        // LABEL FuncStart
        // Push child scope
        // Copy expr to result
        // Pop declaration scope
        // GOTO Caller
        // LABEL postFuncDecl
        // Patch GOTO postFuncDecl
        // GOTO postFuncDecl
        // LABEL FuncStart
        // Push child scope
        // scalar
        //PuffinBasicAtomTypeId.lookup(compCtx.var2.getText());
        // list
        // struct
        // array
        // scalar data type
        // set
        // dict
        // struct
        // scalar data type
        // struct
        // array
        // throw
        // Copy expr to result
        // GOTO LABEL gotoCaller
        // Pop declaration scope
        // LABEL gotoCaller
        // GOTO Caller
        // LABEL postFuncDecl
        // Patch GOTO LABEL gotoCaller
        // Patch GOTO postFuncDecl
        // Unset current UDF state
        // LABEL beforeWhile
        // expr()
        // NOT expr()
        // If expr is false, GOTO afterWend
        // GOTO LABEL beforeWhile
        // LABEL afterWend
        // Patch GOTO afterWend
        // stepCopy = step or 1 (default)
        // var=init
        // endCopy=end
        // GOTO LABEL CHECK
        // APPLY STEP
        // JUMP here from NEXT
        // Add step
        // CHECK
        // If (step >= 0 and var > end) or (step < 0 and var < end) GOTO after "next"
        // step >= 0
        // Patch GOTO LABEL Check
        // var > end
        // (step >= 0 and var > end)
        // step < 0
        // var < end
        // (step < 0 and var < end)
        // if (true) GOTO after NEXT
        // set linenumber on exitNext().
        // GOTO APPLY STEP
        // LABEL afterNext
        /*
         * condition
         * GOTOIF condition labelBeforeThen
         * GOTO labelAfterThen|labelBeforeElse
         * labelBeforeThen
         * ThenStmts
         * GOTO labelAfterThen|labelAfterElse
         * labelAfterThen
         * ElseStmts
         * labelAfterElse
         */
        // Patch IF true: condition
        // Patch IF true: GOTO labelBeforeThen
        // Patch IF false: GOTO labelAfterThen|labelBeforeElse
        // Patch THEN: GOTO labelAfterThen|labelAfterElse
        // IF condition is true, GOTO labelBeforeThen
        // IF condition is false, GOTO labelAfterThen|labelBeforeElse
        // Add instruction for:
        // THEN GOTO linenum | THEN linenum
        // GOTO labelAfterThen|labelAfterElse
        // Add instruction for:
        // ELSE linenum
        //
        // IF expr THEN BEGIN
        // ...
        // ELSE BEGIN
        // ...
        // END IF
        //
        public override void EnterIfthenbeginstmt(PuffinBasicParser.IfthenbeginstmtContext ctx)
        {
            var ifState = new IfState();
            nodeToIfState.Put(ctx, ifState);
            ifStateList.AddLast(ifState);
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        //
        // Expr
        //
        //
        // Functions
        //
        // n, x$, y$
        // x$, y$
        // x$, n, m
        // x$, n
        //
        // Stmt
        //
        // struct
        // array
        // scalar data type
        // struct
        // scalar data type
        // scalar
        //PuffinBasicAtomTypeId.lookup(compCtx.var2.getText());
        // array
        // list
        // struct
        // scalar data type
        // set
        // dict
        // struct
        // scalar data type
        // struct
        // throw
        // GOTO postFuncDecl
        // LABEL FuncStart
        // Push child scope
        // Copy expr to result
        // Pop declaration scope
        // GOTO Caller
        // LABEL postFuncDecl
        // Patch GOTO postFuncDecl
        // GOTO postFuncDecl
        // LABEL FuncStart
        // Push child scope
        // scalar
        //PuffinBasicAtomTypeId.lookup(compCtx.var2.getText());
        // list
        // struct
        // array
        // scalar data type
        // set
        // dict
        // struct
        // scalar data type
        // struct
        // array
        // throw
        // Copy expr to result
        // GOTO LABEL gotoCaller
        // Pop declaration scope
        // LABEL gotoCaller
        // GOTO Caller
        // LABEL postFuncDecl
        // Patch GOTO LABEL gotoCaller
        // Patch GOTO postFuncDecl
        // Unset current UDF state
        // LABEL beforeWhile
        // expr()
        // NOT expr()
        // If expr is false, GOTO afterWend
        // GOTO LABEL beforeWhile
        // LABEL afterWend
        // Patch GOTO afterWend
        // stepCopy = step or 1 (default)
        // var=init
        // endCopy=end
        // GOTO LABEL CHECK
        // APPLY STEP
        // JUMP here from NEXT
        // Add step
        // CHECK
        // If (step >= 0 and var > end) or (step < 0 and var < end) GOTO after "next"
        // step >= 0
        // Patch GOTO LABEL Check
        // var > end
        // (step >= 0 and var > end)
        // step < 0
        // var < end
        // (step < 0 and var < end)
        // if (true) GOTO after NEXT
        // set linenumber on exitNext().
        // GOTO APPLY STEP
        // LABEL afterNext
        /*
         * condition
         * GOTOIF condition labelBeforeThen
         * GOTO labelAfterThen|labelBeforeElse
         * labelBeforeThen
         * ThenStmts
         * GOTO labelAfterThen|labelAfterElse
         * labelAfterThen
         * ElseStmts
         * labelAfterElse
         */
        // Patch IF true: condition
        // Patch IF true: GOTO labelBeforeThen
        // Patch IF false: GOTO labelAfterThen|labelBeforeElse
        // Patch THEN: GOTO labelAfterThen|labelAfterElse
        // IF condition is true, GOTO labelBeforeThen
        // IF condition is false, GOTO labelAfterThen|labelBeforeElse
        // Add instruction for:
        // THEN GOTO linenum | THEN linenum
        // GOTO labelAfterThen|labelAfterElse
        // Add instruction for:
        // ELSE linenum
        //
        // IF expr THEN BEGIN
        // ...
        // ELSE BEGIN
        // ...
        // END IF
        //
        //
        // expr.result
        // GOTO labelBeforeThen IF expr.result is true
        // GOTO labelAfterThen|labelBeforeElse
        // labelBeforeThen (patch GOTOIF)
        // GOTO labelAfterThen|labelAfterElse (else begin)
        // labelAfterThen
        // labelBeforeElse
        //
        public override void ExitIfthenbeginstmt(PuffinBasicParser.IfthenbeginstmtContext ctx)
        {
            var ifState = nodeToIfState.Get(ctx);
            var condition = LookupInstruction(ctx.expr());

            // IF condition is true, GOTO labelBeforeThen
            ifState.gotoIfConditionTrue = ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.GOTO_LABEL_IF, condition.result, ir.GetSymbolTable().AddLabel(), NULL_ID);

            // IF condition is false, GOTO labelAfterThen|labelBeforeElse
            ifState.gotoIfConditionFalse = ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.GOTO_LABEL, ir.GetSymbolTable().AddGotoTarget(), NULL_ID, NULL_ID);

            // Add labelBeforeThen
            ifState.labelBeforeThen = ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.LABEL, ir.GetSymbolTable().AddLabel(), NULL_ID, NULL_ID);

            // Patch IF true: GOTO labelBeforeThen
            ifState.gotoIfConditionTrue.PatchOp2(ifState.labelBeforeThen.op1);
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        //
        // Expr
        //
        //
        // Functions
        //
        // n, x$, y$
        // x$, y$
        // x$, n, m
        // x$, n
        //
        // Stmt
        //
        // struct
        // array
        // scalar data type
        // struct
        // scalar data type
        // scalar
        //PuffinBasicAtomTypeId.lookup(compCtx.var2.getText());
        // array
        // list
        // struct
        // scalar data type
        // set
        // dict
        // struct
        // scalar data type
        // struct
        // throw
        // GOTO postFuncDecl
        // LABEL FuncStart
        // Push child scope
        // Copy expr to result
        // Pop declaration scope
        // GOTO Caller
        // LABEL postFuncDecl
        // Patch GOTO postFuncDecl
        // GOTO postFuncDecl
        // LABEL FuncStart
        // Push child scope
        // scalar
        //PuffinBasicAtomTypeId.lookup(compCtx.var2.getText());
        // list
        // struct
        // array
        // scalar data type
        // set
        // dict
        // struct
        // scalar data type
        // struct
        // array
        // throw
        // Copy expr to result
        // GOTO LABEL gotoCaller
        // Pop declaration scope
        // LABEL gotoCaller
        // GOTO Caller
        // LABEL postFuncDecl
        // Patch GOTO LABEL gotoCaller
        // Patch GOTO postFuncDecl
        // Unset current UDF state
        // LABEL beforeWhile
        // expr()
        // NOT expr()
        // If expr is false, GOTO afterWend
        // GOTO LABEL beforeWhile
        // LABEL afterWend
        // Patch GOTO afterWend
        // stepCopy = step or 1 (default)
        // var=init
        // endCopy=end
        // GOTO LABEL CHECK
        // APPLY STEP
        // JUMP here from NEXT
        // Add step
        // CHECK
        // If (step >= 0 and var > end) or (step < 0 and var < end) GOTO after "next"
        // step >= 0
        // Patch GOTO LABEL Check
        // var > end
        // (step >= 0 and var > end)
        // step < 0
        // var < end
        // (step < 0 and var < end)
        // if (true) GOTO after NEXT
        // set linenumber on exitNext().
        // GOTO APPLY STEP
        // LABEL afterNext
        /*
         * condition
         * GOTOIF condition labelBeforeThen
         * GOTO labelAfterThen|labelBeforeElse
         * labelBeforeThen
         * ThenStmts
         * GOTO labelAfterThen|labelAfterElse
         * labelAfterThen
         * ElseStmts
         * labelAfterElse
         */
        // Patch IF true: condition
        // Patch IF true: GOTO labelBeforeThen
        // Patch IF false: GOTO labelAfterThen|labelBeforeElse
        // Patch THEN: GOTO labelAfterThen|labelAfterElse
        // IF condition is true, GOTO labelBeforeThen
        // IF condition is false, GOTO labelAfterThen|labelBeforeElse
        // Add instruction for:
        // THEN GOTO linenum | THEN linenum
        // GOTO labelAfterThen|labelAfterElse
        // Add instruction for:
        // ELSE linenum
        //
        // IF expr THEN BEGIN
        // ...
        // ELSE BEGIN
        // ...
        // END IF
        //
        //
        // expr.result
        // GOTO labelBeforeThen IF expr.result is true
        // GOTO labelAfterThen|labelBeforeElse
        // labelBeforeThen (patch GOTOIF)
        // GOTO labelAfterThen|labelAfterElse (else begin)
        // labelAfterThen
        // labelBeforeElse
        //
        // IF condition is true, GOTO labelBeforeThen
        // IF condition is false, GOTO labelAfterThen|labelBeforeElse
        // Add labelBeforeThen
        // Patch IF true: GOTO labelBeforeThen
        public override void EnterElsebeginstmt(PuffinBasicParser.ElsebeginstmtContext ctx)
        {
            if (!ifStateList.Any())
            {
                throw new PuffinBasicSemanticError(MISMATCHED_ELSEBEGIN, GetCtxString(ctx), "ELSE BEGIN without IF THEN BEGIN");
            }

            var ifState = ifStateList.Last();

            // GOTO labelAfterThen|labelAfterElse
            ifState.gotoFromThenAfterIf = ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.GOTO_LABEL, ir.GetSymbolTable().AddLabel(), NULL_ID, NULL_ID);
            ifState.labelAfterThen = ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.LABEL, ir.GetSymbolTable().AddLabel(), NULL_ID, NULL_ID);
            ifState.labelBeforeElse = ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.LABEL, ir.GetSymbolTable().AddLabel(), NULL_ID, NULL_ID);
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        //
        // Expr
        //
        //
        // Functions
        //
        // n, x$, y$
        // x$, y$
        // x$, n, m
        // x$, n
        //
        // Stmt
        //
        // struct
        // array
        // scalar data type
        // struct
        // scalar data type
        // scalar
        //PuffinBasicAtomTypeId.lookup(compCtx.var2.getText());
        // array
        // list
        // struct
        // scalar data type
        // set
        // dict
        // struct
        // scalar data type
        // struct
        // throw
        // GOTO postFuncDecl
        // LABEL FuncStart
        // Push child scope
        // Copy expr to result
        // Pop declaration scope
        // GOTO Caller
        // LABEL postFuncDecl
        // Patch GOTO postFuncDecl
        // GOTO postFuncDecl
        // LABEL FuncStart
        // Push child scope
        // scalar
        //PuffinBasicAtomTypeId.lookup(compCtx.var2.getText());
        // list
        // struct
        // array
        // scalar data type
        // set
        // dict
        // struct
        // scalar data type
        // struct
        // array
        // throw
        // Copy expr to result
        // GOTO LABEL gotoCaller
        // Pop declaration scope
        // LABEL gotoCaller
        // GOTO Caller
        // LABEL postFuncDecl
        // Patch GOTO LABEL gotoCaller
        // Patch GOTO postFuncDecl
        // Unset current UDF state
        // LABEL beforeWhile
        // expr()
        // NOT expr()
        // If expr is false, GOTO afterWend
        // GOTO LABEL beforeWhile
        // LABEL afterWend
        // Patch GOTO afterWend
        // stepCopy = step or 1 (default)
        // var=init
        // endCopy=end
        // GOTO LABEL CHECK
        // APPLY STEP
        // JUMP here from NEXT
        // Add step
        // CHECK
        // If (step >= 0 and var > end) or (step < 0 and var < end) GOTO after "next"
        // step >= 0
        // Patch GOTO LABEL Check
        // var > end
        // (step >= 0 and var > end)
        // step < 0
        // var < end
        // (step < 0 and var < end)
        // if (true) GOTO after NEXT
        // set linenumber on exitNext().
        // GOTO APPLY STEP
        // LABEL afterNext
        /*
         * condition
         * GOTOIF condition labelBeforeThen
         * GOTO labelAfterThen|labelBeforeElse
         * labelBeforeThen
         * ThenStmts
         * GOTO labelAfterThen|labelAfterElse
         * labelAfterThen
         * ElseStmts
         * labelAfterElse
         */
        // Patch IF true: condition
        // Patch IF true: GOTO labelBeforeThen
        // Patch IF false: GOTO labelAfterThen|labelBeforeElse
        // Patch THEN: GOTO labelAfterThen|labelAfterElse
        // IF condition is true, GOTO labelBeforeThen
        // IF condition is false, GOTO labelAfterThen|labelBeforeElse
        // Add instruction for:
        // THEN GOTO linenum | THEN linenum
        // GOTO labelAfterThen|labelAfterElse
        // Add instruction for:
        // ELSE linenum
        //
        // IF expr THEN BEGIN
        // ...
        // ELSE BEGIN
        // ...
        // END IF
        //
        //
        // expr.result
        // GOTO labelBeforeThen IF expr.result is true
        // GOTO labelAfterThen|labelBeforeElse
        // labelBeforeThen (patch GOTOIF)
        // GOTO labelAfterThen|labelAfterElse (else begin)
        // labelAfterThen
        // labelBeforeElse
        //
        // IF condition is true, GOTO labelBeforeThen
        // IF condition is false, GOTO labelAfterThen|labelBeforeElse
        // Add labelBeforeThen
        // Patch IF true: GOTO labelBeforeThen
        // GOTO labelAfterThen|labelAfterElse
        public override void ExitEndifstmt(PuffinBasicParser.EndifstmtContext ctx)
        {
            if (!ifStateList.Any())
            {
                throw new PuffinBasicSemanticError(MISMATCHED_ENDIF, GetCtxString(ctx), "ENDIF without IF THEN BEGIN");
            }

            var ifState = ifStateList.Last();
            ifStateList.RemoveLast();
            bool noElseStmt = ifState.labelBeforeElse == null;
            if (noElseStmt)
            {

                // GOTO labelAfterThen|labelAfterElse
                ifState.gotoFromThenAfterIf = ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.GOTO_LABEL, ir.GetSymbolTable().AddLabel(), NULL_ID, NULL_ID);
                ifState.labelAfterThen = ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.LABEL, ir.GetSymbolTable().AddLabel(), NULL_ID, NULL_ID);
            }


            // Add labelAfterElse
            ifState.labelAfterElse = ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.LABEL, ir.GetSymbolTable().AddLabel(), NULL_ID, NULL_ID);

            // Patch IF true: GOTO labelBeforeThen
            ifState.gotoIfConditionTrue.PatchOp2(ifState.labelBeforeThen.op1);

            // Patch IF false: GOTO labelAfterThen|labelBeforeElse
            ifState.gotoIfConditionFalse.PatchOp1(noElseStmt ? ifState.labelAfterThen.op1 : ifState.labelBeforeElse.op1);

            // Patch THEN: GOTO labelAfterThen|labelAfterElse
            ifState.gotoFromThenAfterIf.PatchOp1(noElseStmt ? ifState.labelAfterThen.op1 : ifState.labelAfterElse.op1);
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        //
        // Expr
        //
        //
        // Functions
        //
        // n, x$, y$
        // x$, y$
        // x$, n, m
        // x$, n
        //
        // Stmt
        //
        // struct
        // array
        // scalar data type
        // struct
        // scalar data type
        // scalar
        //PuffinBasicAtomTypeId.lookup(compCtx.var2.getText());
        // array
        // list
        // struct
        // scalar data type
        // set
        // dict
        // struct
        // scalar data type
        // struct
        // throw
        // GOTO postFuncDecl
        // LABEL FuncStart
        // Push child scope
        // Copy expr to result
        // Pop declaration scope
        // GOTO Caller
        // LABEL postFuncDecl
        // Patch GOTO postFuncDecl
        // GOTO postFuncDecl
        // LABEL FuncStart
        // Push child scope
        // scalar
        //PuffinBasicAtomTypeId.lookup(compCtx.var2.getText());
        // list
        // struct
        // array
        // scalar data type
        // set
        // dict
        // struct
        // scalar data type
        // struct
        // array
        // throw
        // Copy expr to result
        // GOTO LABEL gotoCaller
        // Pop declaration scope
        // LABEL gotoCaller
        // GOTO Caller
        // LABEL postFuncDecl
        // Patch GOTO LABEL gotoCaller
        // Patch GOTO postFuncDecl
        // Unset current UDF state
        // LABEL beforeWhile
        // expr()
        // NOT expr()
        // If expr is false, GOTO afterWend
        // GOTO LABEL beforeWhile
        // LABEL afterWend
        // Patch GOTO afterWend
        // stepCopy = step or 1 (default)
        // var=init
        // endCopy=end
        // GOTO LABEL CHECK
        // APPLY STEP
        // JUMP here from NEXT
        // Add step
        // CHECK
        // If (step >= 0 and var > end) or (step < 0 and var < end) GOTO after "next"
        // step >= 0
        // Patch GOTO LABEL Check
        // var > end
        // (step >= 0 and var > end)
        // step < 0
        // var < end
        // (step < 0 and var < end)
        // if (true) GOTO after NEXT
        // set linenumber on exitNext().
        // GOTO APPLY STEP
        // LABEL afterNext
        /*
         * condition
         * GOTOIF condition labelBeforeThen
         * GOTO labelAfterThen|labelBeforeElse
         * labelBeforeThen
         * ThenStmts
         * GOTO labelAfterThen|labelAfterElse
         * labelAfterThen
         * ElseStmts
         * labelAfterElse
         */
        // Patch IF true: condition
        // Patch IF true: GOTO labelBeforeThen
        // Patch IF false: GOTO labelAfterThen|labelBeforeElse
        // Patch THEN: GOTO labelAfterThen|labelAfterElse
        // IF condition is true, GOTO labelBeforeThen
        // IF condition is false, GOTO labelAfterThen|labelBeforeElse
        // Add instruction for:
        // THEN GOTO linenum | THEN linenum
        // GOTO labelAfterThen|labelAfterElse
        // Add instruction for:
        // ELSE linenum
        //
        // IF expr THEN BEGIN
        // ...
        // ELSE BEGIN
        // ...
        // END IF
        //
        //
        // expr.result
        // GOTO labelBeforeThen IF expr.result is true
        // GOTO labelAfterThen|labelBeforeElse
        // labelBeforeThen (patch GOTOIF)
        // GOTO labelAfterThen|labelAfterElse (else begin)
        // labelAfterThen
        // labelBeforeElse
        //
        // IF condition is true, GOTO labelBeforeThen
        // IF condition is false, GOTO labelAfterThen|labelBeforeElse
        // Add labelBeforeThen
        // Patch IF true: GOTO labelBeforeThen
        // GOTO labelAfterThen|labelAfterElse
        // GOTO labelAfterThen|labelAfterElse
        // Add labelAfterElse
        // Patch IF true: GOTO labelBeforeThen
        // Patch IF false: GOTO labelAfterThen|labelBeforeElse
        // Patch THEN: GOTO labelAfterThen|labelAfterElse
        public override void ExitGosubstmt(PuffinBasicParser.GosubstmtContext ctx)
        {
            var gotoLinenum = ParseLinenum(ctx.linenum().GetText());
            var pushReturnLabel = ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.PUSH_RETLABEL, ir.GetSymbolTable().AddGotoTarget(), NULL_ID, NULL_ID);
            ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.GOTO_LINENUM, GetGotoLineNumberOp1(gotoLinenum), NULL_ID, NULL_ID);
            var labelReturn = ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.LABEL, ir.GetSymbolTable().AddLabel(), NULL_ID, NULL_ID);
            pushReturnLabel.PatchOp1(labelReturn.op1);
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        //
        // Expr
        //
        //
        // Functions
        //
        // n, x$, y$
        // x$, y$
        // x$, n, m
        // x$, n
        //
        // Stmt
        //
        // struct
        // array
        // scalar data type
        // struct
        // scalar data type
        // scalar
        //PuffinBasicAtomTypeId.lookup(compCtx.var2.getText());
        // array
        // list
        // struct
        // scalar data type
        // set
        // dict
        // struct
        // scalar data type
        // struct
        // throw
        // GOTO postFuncDecl
        // LABEL FuncStart
        // Push child scope
        // Copy expr to result
        // Pop declaration scope
        // GOTO Caller
        // LABEL postFuncDecl
        // Patch GOTO postFuncDecl
        // GOTO postFuncDecl
        // LABEL FuncStart
        // Push child scope
        // scalar
        //PuffinBasicAtomTypeId.lookup(compCtx.var2.getText());
        // list
        // struct
        // array
        // scalar data type
        // set
        // dict
        // struct
        // scalar data type
        // struct
        // array
        // throw
        // Copy expr to result
        // GOTO LABEL gotoCaller
        // Pop declaration scope
        // LABEL gotoCaller
        // GOTO Caller
        // LABEL postFuncDecl
        // Patch GOTO LABEL gotoCaller
        // Patch GOTO postFuncDecl
        // Unset current UDF state
        // LABEL beforeWhile
        // expr()
        // NOT expr()
        // If expr is false, GOTO afterWend
        // GOTO LABEL beforeWhile
        // LABEL afterWend
        // Patch GOTO afterWend
        // stepCopy = step or 1 (default)
        // var=init
        // endCopy=end
        // GOTO LABEL CHECK
        // APPLY STEP
        // JUMP here from NEXT
        // Add step
        // CHECK
        // If (step >= 0 and var > end) or (step < 0 and var < end) GOTO after "next"
        // step >= 0
        // Patch GOTO LABEL Check
        // var > end
        // (step >= 0 and var > end)
        // step < 0
        // var < end
        // (step < 0 and var < end)
        // if (true) GOTO after NEXT
        // set linenumber on exitNext().
        // GOTO APPLY STEP
        // LABEL afterNext
        /*
         * condition
         * GOTOIF condition labelBeforeThen
         * GOTO labelAfterThen|labelBeforeElse
         * labelBeforeThen
         * ThenStmts
         * GOTO labelAfterThen|labelAfterElse
         * labelAfterThen
         * ElseStmts
         * labelAfterElse
         */
        // Patch IF true: condition
        // Patch IF true: GOTO labelBeforeThen
        // Patch IF false: GOTO labelAfterThen|labelBeforeElse
        // Patch THEN: GOTO labelAfterThen|labelAfterElse
        // IF condition is true, GOTO labelBeforeThen
        // IF condition is false, GOTO labelAfterThen|labelBeforeElse
        // Add instruction for:
        // THEN GOTO linenum | THEN linenum
        // GOTO labelAfterThen|labelAfterElse
        // Add instruction for:
        // ELSE linenum
        //
        // IF expr THEN BEGIN
        // ...
        // ELSE BEGIN
        // ...
        // END IF
        //
        //
        // expr.result
        // GOTO labelBeforeThen IF expr.result is true
        // GOTO labelAfterThen|labelBeforeElse
        // labelBeforeThen (patch GOTOIF)
        // GOTO labelAfterThen|labelAfterElse (else begin)
        // labelAfterThen
        // labelBeforeElse
        //
        // IF condition is true, GOTO labelBeforeThen
        // IF condition is false, GOTO labelAfterThen|labelBeforeElse
        // Add labelBeforeThen
        // Patch IF true: GOTO labelBeforeThen
        // GOTO labelAfterThen|labelAfterElse
        // GOTO labelAfterThen|labelAfterElse
        // Add labelAfterElse
        // Patch IF true: GOTO labelBeforeThen
        // Patch IF false: GOTO labelAfterThen|labelBeforeElse
        // Patch THEN: GOTO labelAfterThen|labelAfterElse
        public override void ExitGosublabelstmt(PuffinBasicParser.GosublabelstmtContext ctx)
        {
            var gotoLabel = ir.GetSymbolTable().AddLabel(ctx.@string().STRING().GetText());
            var pushReturnLabel = ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.PUSH_RETLABEL, ir.GetSymbolTable().AddGotoTarget(), NULL_ID, NULL_ID);
            ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.GOTO_LABEL, gotoLabel, NULL_ID, NULL_ID);
            var labelReturn = ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.LABEL, ir.GetSymbolTable().AddLabel(), NULL_ID, NULL_ID);
            pushReturnLabel.PatchOp1(labelReturn.op1);
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        //
        // Expr
        //
        //
        // Functions
        //
        // n, x$, y$
        // x$, y$
        // x$, n, m
        // x$, n
        //
        // Stmt
        //
        // struct
        // array
        // scalar data type
        // struct
        // scalar data type
        // scalar
        //PuffinBasicAtomTypeId.lookup(compCtx.var2.getText());
        // array
        // list
        // struct
        // scalar data type
        // set
        // dict
        // struct
        // scalar data type
        // struct
        // throw
        // GOTO postFuncDecl
        // LABEL FuncStart
        // Push child scope
        // Copy expr to result
        // Pop declaration scope
        // GOTO Caller
        // LABEL postFuncDecl
        // Patch GOTO postFuncDecl
        // GOTO postFuncDecl
        // LABEL FuncStart
        // Push child scope
        // scalar
        //PuffinBasicAtomTypeId.lookup(compCtx.var2.getText());
        // list
        // struct
        // array
        // scalar data type
        // set
        // dict
        // struct
        // scalar data type
        // struct
        // array
        // throw
        // Copy expr to result
        // GOTO LABEL gotoCaller
        // Pop declaration scope
        // LABEL gotoCaller
        // GOTO Caller
        // LABEL postFuncDecl
        // Patch GOTO LABEL gotoCaller
        // Patch GOTO postFuncDecl
        // Unset current UDF state
        // LABEL beforeWhile
        // expr()
        // NOT expr()
        // If expr is false, GOTO afterWend
        // GOTO LABEL beforeWhile
        // LABEL afterWend
        // Patch GOTO afterWend
        // stepCopy = step or 1 (default)
        // var=init
        // endCopy=end
        // GOTO LABEL CHECK
        // APPLY STEP
        // JUMP here from NEXT
        // Add step
        // CHECK
        // If (step >= 0 and var > end) or (step < 0 and var < end) GOTO after "next"
        // step >= 0
        // Patch GOTO LABEL Check
        // var > end
        // (step >= 0 and var > end)
        // step < 0
        // var < end
        // (step < 0 and var < end)
        // if (true) GOTO after NEXT
        // set linenumber on exitNext().
        // GOTO APPLY STEP
        // LABEL afterNext
        /*
         * condition
         * GOTOIF condition labelBeforeThen
         * GOTO labelAfterThen|labelBeforeElse
         * labelBeforeThen
         * ThenStmts
         * GOTO labelAfterThen|labelAfterElse
         * labelAfterThen
         * ElseStmts
         * labelAfterElse
         */
        // Patch IF true: condition
        // Patch IF true: GOTO labelBeforeThen
        // Patch IF false: GOTO labelAfterThen|labelBeforeElse
        // Patch THEN: GOTO labelAfterThen|labelAfterElse
        // IF condition is true, GOTO labelBeforeThen
        // IF condition is false, GOTO labelAfterThen|labelBeforeElse
        // Add instruction for:
        // THEN GOTO linenum | THEN linenum
        // GOTO labelAfterThen|labelAfterElse
        // Add instruction for:
        // ELSE linenum
        //
        // IF expr THEN BEGIN
        // ...
        // ELSE BEGIN
        // ...
        // END IF
        //
        //
        // expr.result
        // GOTO labelBeforeThen IF expr.result is true
        // GOTO labelAfterThen|labelBeforeElse
        // labelBeforeThen (patch GOTOIF)
        // GOTO labelAfterThen|labelAfterElse (else begin)
        // labelAfterThen
        // labelBeforeElse
        //
        // IF condition is true, GOTO labelBeforeThen
        // IF condition is false, GOTO labelAfterThen|labelBeforeElse
        // Add labelBeforeThen
        // Patch IF true: GOTO labelBeforeThen
        // GOTO labelAfterThen|labelAfterElse
        // GOTO labelAfterThen|labelAfterElse
        // Add labelAfterElse
        // Patch IF true: GOTO labelBeforeThen
        // Patch IF false: GOTO labelAfterThen|labelBeforeElse
        // Patch THEN: GOTO labelAfterThen|labelAfterElse
        public override void ExitReturnstmt(PuffinBasicParser.ReturnstmtContext ctx)
        {
            ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.RETURN, NULL_ID, NULL_ID, NULL_ID);
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        //
        // Expr
        //
        //
        // Functions
        //
        // n, x$, y$
        // x$, y$
        // x$, n, m
        // x$, n
        //
        // Stmt
        //
        // struct
        // array
        // scalar data type
        // struct
        // scalar data type
        // scalar
        //PuffinBasicAtomTypeId.lookup(compCtx.var2.getText());
        // array
        // list
        // struct
        // scalar data type
        // set
        // dict
        // struct
        // scalar data type
        // struct
        // throw
        // GOTO postFuncDecl
        // LABEL FuncStart
        // Push child scope
        // Copy expr to result
        // Pop declaration scope
        // GOTO Caller
        // LABEL postFuncDecl
        // Patch GOTO postFuncDecl
        // GOTO postFuncDecl
        // LABEL FuncStart
        // Push child scope
        // scalar
        //PuffinBasicAtomTypeId.lookup(compCtx.var2.getText());
        // list
        // struct
        // array
        // scalar data type
        // set
        // dict
        // struct
        // scalar data type
        // struct
        // array
        // throw
        // Copy expr to result
        // GOTO LABEL gotoCaller
        // Pop declaration scope
        // LABEL gotoCaller
        // GOTO Caller
        // LABEL postFuncDecl
        // Patch GOTO LABEL gotoCaller
        // Patch GOTO postFuncDecl
        // Unset current UDF state
        // LABEL beforeWhile
        // expr()
        // NOT expr()
        // If expr is false, GOTO afterWend
        // GOTO LABEL beforeWhile
        // LABEL afterWend
        // Patch GOTO afterWend
        // stepCopy = step or 1 (default)
        // var=init
        // endCopy=end
        // GOTO LABEL CHECK
        // APPLY STEP
        // JUMP here from NEXT
        // Add step
        // CHECK
        // If (step >= 0 and var > end) or (step < 0 and var < end) GOTO after "next"
        // step >= 0
        // Patch GOTO LABEL Check
        // var > end
        // (step >= 0 and var > end)
        // step < 0
        // var < end
        // (step < 0 and var < end)
        // if (true) GOTO after NEXT
        // set linenumber on exitNext().
        // GOTO APPLY STEP
        // LABEL afterNext
        /*
         * condition
         * GOTOIF condition labelBeforeThen
         * GOTO labelAfterThen|labelBeforeElse
         * labelBeforeThen
         * ThenStmts
         * GOTO labelAfterThen|labelAfterElse
         * labelAfterThen
         * ElseStmts
         * labelAfterElse
         */
        // Patch IF true: condition
        // Patch IF true: GOTO labelBeforeThen
        // Patch IF false: GOTO labelAfterThen|labelBeforeElse
        // Patch THEN: GOTO labelAfterThen|labelAfterElse
        // IF condition is true, GOTO labelBeforeThen
        // IF condition is false, GOTO labelAfterThen|labelBeforeElse
        // Add instruction for:
        // THEN GOTO linenum | THEN linenum
        // GOTO labelAfterThen|labelAfterElse
        // Add instruction for:
        // ELSE linenum
        //
        // IF expr THEN BEGIN
        // ...
        // ELSE BEGIN
        // ...
        // END IF
        //
        //
        // expr.result
        // GOTO labelBeforeThen IF expr.result is true
        // GOTO labelAfterThen|labelBeforeElse
        // labelBeforeThen (patch GOTOIF)
        // GOTO labelAfterThen|labelAfterElse (else begin)
        // labelAfterThen
        // labelBeforeElse
        //
        // IF condition is true, GOTO labelBeforeThen
        // IF condition is false, GOTO labelAfterThen|labelBeforeElse
        // Add labelBeforeThen
        // Patch IF true: GOTO labelBeforeThen
        // GOTO labelAfterThen|labelAfterElse
        // GOTO labelAfterThen|labelAfterElse
        // Add labelAfterElse
        // Patch IF true: GOTO labelBeforeThen
        // Patch IF false: GOTO labelAfterThen|labelBeforeElse
        // Patch THEN: GOTO labelAfterThen|labelAfterElse
        public override void ExitGotostmt(PuffinBasicParser.GotostmtContext ctx)
        {
            var gotoLinenum = ParseLinenum(ctx.linenum().GetText());
            ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.GOTO_LINENUM, GetGotoLineNumberOp1(gotoLinenum), NULL_ID, NULL_ID);
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        //
        // Expr
        //
        //
        // Functions
        //
        // n, x$, y$
        // x$, y$
        // x$, n, m
        // x$, n
        //
        // Stmt
        //
        // struct
        // array
        // scalar data type
        // struct
        // scalar data type
        // scalar
        //PuffinBasicAtomTypeId.lookup(compCtx.var2.getText());
        // array
        // list
        // struct
        // scalar data type
        // set
        // dict
        // struct
        // scalar data type
        // struct
        // throw
        // GOTO postFuncDecl
        // LABEL FuncStart
        // Push child scope
        // Copy expr to result
        // Pop declaration scope
        // GOTO Caller
        // LABEL postFuncDecl
        // Patch GOTO postFuncDecl
        // GOTO postFuncDecl
        // LABEL FuncStart
        // Push child scope
        // scalar
        //PuffinBasicAtomTypeId.lookup(compCtx.var2.getText());
        // list
        // struct
        // array
        // scalar data type
        // set
        // dict
        // struct
        // scalar data type
        // struct
        // array
        // throw
        // Copy expr to result
        // GOTO LABEL gotoCaller
        // Pop declaration scope
        // LABEL gotoCaller
        // GOTO Caller
        // LABEL postFuncDecl
        // Patch GOTO LABEL gotoCaller
        // Patch GOTO postFuncDecl
        // Unset current UDF state
        // LABEL beforeWhile
        // expr()
        // NOT expr()
        // If expr is false, GOTO afterWend
        // GOTO LABEL beforeWhile
        // LABEL afterWend
        // Patch GOTO afterWend
        // stepCopy = step or 1 (default)
        // var=init
        // endCopy=end
        // GOTO LABEL CHECK
        // APPLY STEP
        // JUMP here from NEXT
        // Add step
        // CHECK
        // If (step >= 0 and var > end) or (step < 0 and var < end) GOTO after "next"
        // step >= 0
        // Patch GOTO LABEL Check
        // var > end
        // (step >= 0 and var > end)
        // step < 0
        // var < end
        // (step < 0 and var < end)
        // if (true) GOTO after NEXT
        // set linenumber on exitNext().
        // GOTO APPLY STEP
        // LABEL afterNext
        /*
         * condition
         * GOTOIF condition labelBeforeThen
         * GOTO labelAfterThen|labelBeforeElse
         * labelBeforeThen
         * ThenStmts
         * GOTO labelAfterThen|labelAfterElse
         * labelAfterThen
         * ElseStmts
         * labelAfterElse
         */
        // Patch IF true: condition
        // Patch IF true: GOTO labelBeforeThen
        // Patch IF false: GOTO labelAfterThen|labelBeforeElse
        // Patch THEN: GOTO labelAfterThen|labelAfterElse
        // IF condition is true, GOTO labelBeforeThen
        // IF condition is false, GOTO labelAfterThen|labelBeforeElse
        // Add instruction for:
        // THEN GOTO linenum | THEN linenum
        // GOTO labelAfterThen|labelAfterElse
        // Add instruction for:
        // ELSE linenum
        //
        // IF expr THEN BEGIN
        // ...
        // ELSE BEGIN
        // ...
        // END IF
        //
        //
        // expr.result
        // GOTO labelBeforeThen IF expr.result is true
        // GOTO labelAfterThen|labelBeforeElse
        // labelBeforeThen (patch GOTOIF)
        // GOTO labelAfterThen|labelAfterElse (else begin)
        // labelAfterThen
        // labelBeforeElse
        //
        // IF condition is true, GOTO labelBeforeThen
        // IF condition is false, GOTO labelAfterThen|labelBeforeElse
        // Add labelBeforeThen
        // Patch IF true: GOTO labelBeforeThen
        // GOTO labelAfterThen|labelAfterElse
        // GOTO labelAfterThen|labelAfterElse
        // Add labelAfterElse
        // Patch IF true: GOTO labelBeforeThen
        // Patch IF false: GOTO labelAfterThen|labelBeforeElse
        // Patch THEN: GOTO labelAfterThen|labelAfterElse
        public override void ExitGotolabelstmt(PuffinBasicParser.GotolabelstmtContext ctx)
        {
            var gotoLabel = ir.GetSymbolTable().AddLabel(ctx.@string().STRING().GetText());
            ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.GOTO_LABEL, gotoLabel, NULL_ID, NULL_ID);
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        //
        // Expr
        //
        //
        // Functions
        //
        // n, x$, y$
        // x$, y$
        // x$, n, m
        // x$, n
        //
        // Stmt
        //
        // struct
        // array
        // scalar data type
        // struct
        // scalar data type
        // scalar
        //PuffinBasicAtomTypeId.lookup(compCtx.var2.getText());
        // array
        // list
        // struct
        // scalar data type
        // set
        // dict
        // struct
        // scalar data type
        // struct
        // throw
        // GOTO postFuncDecl
        // LABEL FuncStart
        // Push child scope
        // Copy expr to result
        // Pop declaration scope
        // GOTO Caller
        // LABEL postFuncDecl
        // Patch GOTO postFuncDecl
        // GOTO postFuncDecl
        // LABEL FuncStart
        // Push child scope
        // scalar
        //PuffinBasicAtomTypeId.lookup(compCtx.var2.getText());
        // list
        // struct
        // array
        // scalar data type
        // set
        // dict
        // struct
        // scalar data type
        // struct
        // array
        // throw
        // Copy expr to result
        // GOTO LABEL gotoCaller
        // Pop declaration scope
        // LABEL gotoCaller
        // GOTO Caller
        // LABEL postFuncDecl
        // Patch GOTO LABEL gotoCaller
        // Patch GOTO postFuncDecl
        // Unset current UDF state
        // LABEL beforeWhile
        // expr()
        // NOT expr()
        // If expr is false, GOTO afterWend
        // GOTO LABEL beforeWhile
        // LABEL afterWend
        // Patch GOTO afterWend
        // stepCopy = step or 1 (default)
        // var=init
        // endCopy=end
        // GOTO LABEL CHECK
        // APPLY STEP
        // JUMP here from NEXT
        // Add step
        // CHECK
        // If (step >= 0 and var > end) or (step < 0 and var < end) GOTO after "next"
        // step >= 0
        // Patch GOTO LABEL Check
        // var > end
        // (step >= 0 and var > end)
        // step < 0
        // var < end
        // (step < 0 and var < end)
        // if (true) GOTO after NEXT
        // set linenumber on exitNext().
        // GOTO APPLY STEP
        // LABEL afterNext
        /*
         * condition
         * GOTOIF condition labelBeforeThen
         * GOTO labelAfterThen|labelBeforeElse
         * labelBeforeThen
         * ThenStmts
         * GOTO labelAfterThen|labelAfterElse
         * labelAfterThen
         * ElseStmts
         * labelAfterElse
         */
        // Patch IF true: condition
        // Patch IF true: GOTO labelBeforeThen
        // Patch IF false: GOTO labelAfterThen|labelBeforeElse
        // Patch THEN: GOTO labelAfterThen|labelAfterElse
        // IF condition is true, GOTO labelBeforeThen
        // IF condition is false, GOTO labelAfterThen|labelBeforeElse
        // Add instruction for:
        // THEN GOTO linenum | THEN linenum
        // GOTO labelAfterThen|labelAfterElse
        // Add instruction for:
        // ELSE linenum
        //
        // IF expr THEN BEGIN
        // ...
        // ELSE BEGIN
        // ...
        // END IF
        //
        //
        // expr.result
        // GOTO labelBeforeThen IF expr.result is true
        // GOTO labelAfterThen|labelBeforeElse
        // labelBeforeThen (patch GOTOIF)
        // GOTO labelAfterThen|labelAfterElse (else begin)
        // labelAfterThen
        // labelBeforeElse
        //
        // IF condition is true, GOTO labelBeforeThen
        // IF condition is false, GOTO labelAfterThen|labelBeforeElse
        // Add labelBeforeThen
        // Patch IF true: GOTO labelBeforeThen
        // GOTO labelAfterThen|labelAfterElse
        // GOTO labelAfterThen|labelAfterElse
        // Add labelAfterElse
        // Patch IF true: GOTO labelBeforeThen
        // Patch IF false: GOTO labelAfterThen|labelBeforeElse
        // Patch THEN: GOTO labelAfterThen|labelAfterElse
        public override void ExitSwapstmt(PuffinBasicParser.SwapstmtContext ctx)
        {
            var var1 = LookupInstruction(ctx.variable(0));
            var var2 = LookupInstruction(ctx.variable(1));
            var dt1 = ir.GetSymbolTable()[var1.result].GetType().GetAtomTypeId();
            var dt2 = ir.GetSymbolTable()[var2.result].GetType().GetAtomTypeId();
            if (dt1 != dt2)
            {
                throw new PuffinBasicSemanticError(DATA_TYPE_MISMATCH, GetCtxString(ctx), dt1 + " doesn't match " + dt2);
            }

            ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.SWAP, var1.result, var2.result, NULL_ID);
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        //
        // Expr
        //
        //
        // Functions
        //
        // n, x$, y$
        // x$, y$
        // x$, n, m
        // x$, n
        //
        // Stmt
        //
        // struct
        // array
        // scalar data type
        // struct
        // scalar data type
        // scalar
        //PuffinBasicAtomTypeId.lookup(compCtx.var2.getText());
        // array
        // list
        // struct
        // scalar data type
        // set
        // dict
        // struct
        // scalar data type
        // struct
        // throw
        // GOTO postFuncDecl
        // LABEL FuncStart
        // Push child scope
        // Copy expr to result
        // Pop declaration scope
        // GOTO Caller
        // LABEL postFuncDecl
        // Patch GOTO postFuncDecl
        // GOTO postFuncDecl
        // LABEL FuncStart
        // Push child scope
        // scalar
        //PuffinBasicAtomTypeId.lookup(compCtx.var2.getText());
        // list
        // struct
        // array
        // scalar data type
        // set
        // dict
        // struct
        // scalar data type
        // struct
        // array
        // throw
        // Copy expr to result
        // GOTO LABEL gotoCaller
        // Pop declaration scope
        // LABEL gotoCaller
        // GOTO Caller
        // LABEL postFuncDecl
        // Patch GOTO LABEL gotoCaller
        // Patch GOTO postFuncDecl
        // Unset current UDF state
        // LABEL beforeWhile
        // expr()
        // NOT expr()
        // If expr is false, GOTO afterWend
        // GOTO LABEL beforeWhile
        // LABEL afterWend
        // Patch GOTO afterWend
        // stepCopy = step or 1 (default)
        // var=init
        // endCopy=end
        // GOTO LABEL CHECK
        // APPLY STEP
        // JUMP here from NEXT
        // Add step
        // CHECK
        // If (step >= 0 and var > end) or (step < 0 and var < end) GOTO after "next"
        // step >= 0
        // Patch GOTO LABEL Check
        // var > end
        // (step >= 0 and var > end)
        // step < 0
        // var < end
        // (step < 0 and var < end)
        // if (true) GOTO after NEXT
        // set linenumber on exitNext().
        // GOTO APPLY STEP
        // LABEL afterNext
        /*
         * condition
         * GOTOIF condition labelBeforeThen
         * GOTO labelAfterThen|labelBeforeElse
         * labelBeforeThen
         * ThenStmts
         * GOTO labelAfterThen|labelAfterElse
         * labelAfterThen
         * ElseStmts
         * labelAfterElse
         */
        // Patch IF true: condition
        // Patch IF true: GOTO labelBeforeThen
        // Patch IF false: GOTO labelAfterThen|labelBeforeElse
        // Patch THEN: GOTO labelAfterThen|labelAfterElse
        // IF condition is true, GOTO labelBeforeThen
        // IF condition is false, GOTO labelAfterThen|labelBeforeElse
        // Add instruction for:
        // THEN GOTO linenum | THEN linenum
        // GOTO labelAfterThen|labelAfterElse
        // Add instruction for:
        // ELSE linenum
        //
        // IF expr THEN BEGIN
        // ...
        // ELSE BEGIN
        // ...
        // END IF
        //
        //
        // expr.result
        // GOTO labelBeforeThen IF expr.result is true
        // GOTO labelAfterThen|labelBeforeElse
        // labelBeforeThen (patch GOTOIF)
        // GOTO labelAfterThen|labelAfterElse (else begin)
        // labelAfterThen
        // labelBeforeElse
        //
        // IF condition is true, GOTO labelBeforeThen
        // IF condition is false, GOTO labelAfterThen|labelBeforeElse
        // Add labelBeforeThen
        // Patch IF true: GOTO labelBeforeThen
        // GOTO labelAfterThen|labelAfterElse
        // GOTO labelAfterThen|labelAfterElse
        // Add labelAfterElse
        // Patch IF true: GOTO labelBeforeThen
        // Patch IF false: GOTO labelAfterThen|labelBeforeElse
        // Patch THEN: GOTO labelAfterThen|labelAfterElse
        public override void ExitOpen1stmt(PuffinBasicParser.Open1stmtContext ctx)
        {
            var filenameInstr = LookupInstruction(ctx.filename);
            var fileOpenMode = GetFileOpenMode(ctx.filemode1());
            var accessMode = GetFileAccessMode(null);
            var lockMode = GetLockMode(null);
            var fileNumber = Numbers.ParseInt32(ctx.filenum.Text, GetCtxString(ctx));
            var recordLenInstrId = ctx.reclen != null ? 
                LookupInstruction(ctx.reclen).result : 
                ir.GetSymbolTable().AddTmp(INT32, (e) => e.GetValue().SetInt32(File.PuffinBasicFile.DEFAULT_RECORD_LEN));

            Types.AssertString(ir.GetSymbolTable()[filenameInstr.result].GetType().GetAtomTypeId(), GetCtxString(ctx));
            Types.AssertNumeric(ir.GetSymbolTable()[recordLenInstrId].GetType().GetAtomTypeId(), GetCtxString(ctx));

            // fileName, fileNumber
            ir.AddInstruction(
                sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, 
                OpCode.PARAM2, 
                filenameInstr.result, 
                ir.GetSymbolTable().AddTmp(INT32, (e) => e.GetValue().SetInt32(fileNumber)), 
                NULL_ID);

            // openMode, accessMode
            ir.AddInstruction(
                sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, 
                OpCode.PARAM2, 
                ir.GetSymbolTable().AddTmp(PuffinBasicAtomTypeId.STRING, (e) => e.GetValue().SetString(fileOpenMode.ToString())), 
                ir.GetSymbolTable().AddTmp(PuffinBasicAtomTypeId.STRING, (e) => e.GetValue().SetString(accessMode.ToString())), NULL_ID);

            // lockMode, recordLen
            ir.AddInstruction(
                sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, 
                OpCode.OPEN,
                ir.GetSymbolTable().AddTmp(PuffinBasicAtomTypeId.STRING, (e) => e.GetValue().SetString(lockMode.ToString())), 
                recordLenInstrId, 
                NULL_ID);
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        //
        // Expr
        //
        //
        // Functions
        //
        // n, x$, y$
        // x$, y$
        // x$, n, m
        // x$, n
        //
        // Stmt
        //
        // struct
        // array
        // scalar data type
        // struct
        // scalar data type
        // scalar
        //PuffinBasicAtomTypeId.lookup(compCtx.var2.getText());
        // array
        // list
        // struct
        // scalar data type
        // set
        // dict
        // struct
        // scalar data type
        // struct
        // throw
        // GOTO postFuncDecl
        // LABEL FuncStart
        // Push child scope
        // Copy expr to result
        // Pop declaration scope
        // GOTO Caller
        // LABEL postFuncDecl
        // Patch GOTO postFuncDecl
        // GOTO postFuncDecl
        // LABEL FuncStart
        // Push child scope
        // scalar
        //PuffinBasicAtomTypeId.lookup(compCtx.var2.getText());
        // list
        // struct
        // array
        // scalar data type
        // set
        // dict
        // struct
        // scalar data type
        // struct
        // array
        // throw
        // Copy expr to result
        // GOTO LABEL gotoCaller
        // Pop declaration scope
        // LABEL gotoCaller
        // GOTO Caller
        // LABEL postFuncDecl
        // Patch GOTO LABEL gotoCaller
        // Patch GOTO postFuncDecl
        // Unset current UDF state
        // LABEL beforeWhile
        // expr()
        // NOT expr()
        // If expr is false, GOTO afterWend
        // GOTO LABEL beforeWhile
        // LABEL afterWend
        // Patch GOTO afterWend
        // stepCopy = step or 1 (default)
        // var=init
        // endCopy=end
        // GOTO LABEL CHECK
        // APPLY STEP
        // JUMP here from NEXT
        // Add step
        // CHECK
        // If (step >= 0 and var > end) or (step < 0 and var < end) GOTO after "next"
        // step >= 0
        // Patch GOTO LABEL Check
        // var > end
        // (step >= 0 and var > end)
        // step < 0
        // var < end
        // (step < 0 and var < end)
        // if (true) GOTO after NEXT
        // set linenumber on exitNext().
        // GOTO APPLY STEP
        // LABEL afterNext
        /*
         * condition
         * GOTOIF condition labelBeforeThen
         * GOTO labelAfterThen|labelBeforeElse
         * labelBeforeThen
         * ThenStmts
         * GOTO labelAfterThen|labelAfterElse
         * labelAfterThen
         * ElseStmts
         * labelAfterElse
         */
        // Patch IF true: condition
        // Patch IF true: GOTO labelBeforeThen
        // Patch IF false: GOTO labelAfterThen|labelBeforeElse
        // Patch THEN: GOTO labelAfterThen|labelAfterElse
        // IF condition is true, GOTO labelBeforeThen
        // IF condition is false, GOTO labelAfterThen|labelBeforeElse
        // Add instruction for:
        // THEN GOTO linenum | THEN linenum
        // GOTO labelAfterThen|labelAfterElse
        // Add instruction for:
        // ELSE linenum
        //
        // IF expr THEN BEGIN
        // ...
        // ELSE BEGIN
        // ...
        // END IF
        //
        //
        // expr.result
        // GOTO labelBeforeThen IF expr.result is true
        // GOTO labelAfterThen|labelBeforeElse
        // labelBeforeThen (patch GOTOIF)
        // GOTO labelAfterThen|labelAfterElse (else begin)
        // labelAfterThen
        // labelBeforeElse
        //
        // IF condition is true, GOTO labelBeforeThen
        // IF condition is false, GOTO labelAfterThen|labelBeforeElse
        // Add labelBeforeThen
        // Patch IF true: GOTO labelBeforeThen
        // GOTO labelAfterThen|labelAfterElse
        // GOTO labelAfterThen|labelAfterElse
        // Add labelAfterElse
        // Patch IF true: GOTO labelBeforeThen
        // Patch IF false: GOTO labelAfterThen|labelBeforeElse
        // Patch THEN: GOTO labelAfterThen|labelAfterElse
        // fileName, fileNumber
        // openMode, accessMode
        // lockMode, recordLen
        public override void ExitOpen2stmt(PuffinBasicParser.Open2stmtContext ctx)
        {
            var filenameInstr = LookupInstruction(ctx.filename);
            var fileOpenMode = GetFileOpenMode(ctx.filemode2());
            var accessMode = GetFileAccessMode(ctx.access());
            var lockMode = GetLockMode(ctx.@lock());
            var fileNumber = Numbers.ParseInt32(ctx.filenum.Text, GetCtxString(ctx));
            var recordLenInstrId = ctx.reclen != null ? LookupInstruction(ctx.reclen).result : ir.GetSymbolTable().AddTmp(INT32, (e) => e.GetValue().SetInt32(File.PuffinBasicFile.DEFAULT_RECORD_LEN));
            Types.AssertString(ir.GetSymbolTable()[filenameInstr.result].GetType().GetAtomTypeId(), GetCtxString(ctx));
            Types.AssertNumeric(ir.GetSymbolTable()[recordLenInstrId].GetType().GetAtomTypeId(), GetCtxString(ctx));

            // fileName, fileNumber
            ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.PARAM2, filenameInstr.result, ir.GetSymbolTable().AddTmp(INT32, (e) => e.GetValue().SetInt32(fileNumber)), NULL_ID);

            // openMode, accessMode
            ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.PARAM2, ir.GetSymbolTable().AddTmp(PuffinBasicAtomTypeId.STRING, (e) => e.GetValue().SetString(fileOpenMode.ToString())), ir.GetSymbolTable().AddTmp(PuffinBasicAtomTypeId.STRING, (e) => e.GetValue().SetString(accessMode.ToString())), NULL_ID);

            // lockMode, recordLen
            ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.OPEN, ir.GetSymbolTable().AddTmp(PuffinBasicAtomTypeId.STRING, (e) => e.GetValue().SetString(lockMode.ToString())), recordLenInstrId, NULL_ID);
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        //
        // Expr
        //
        //
        // Functions
        //
        // n, x$, y$
        // x$, y$
        // x$, n, m
        // x$, n
        //
        // Stmt
        //
        // struct
        // array
        // scalar data type
        // struct
        // scalar data type
        // scalar
        //PuffinBasicAtomTypeId.lookup(compCtx.var2.getText());
        // array
        // list
        // struct
        // scalar data type
        // set
        // dict
        // struct
        // scalar data type
        // struct
        // throw
        // GOTO postFuncDecl
        // LABEL FuncStart
        // Push child scope
        // Copy expr to result
        // Pop declaration scope
        // GOTO Caller
        // LABEL postFuncDecl
        // Patch GOTO postFuncDecl
        // GOTO postFuncDecl
        // LABEL FuncStart
        // Push child scope
        // scalar
        //PuffinBasicAtomTypeId.lookup(compCtx.var2.getText());
        // list
        // struct
        // array
        // scalar data type
        // set
        // dict
        // struct
        // scalar data type
        // struct
        // array
        // throw
        // Copy expr to result
        // GOTO LABEL gotoCaller
        // Pop declaration scope
        // LABEL gotoCaller
        // GOTO Caller
        // LABEL postFuncDecl
        // Patch GOTO LABEL gotoCaller
        // Patch GOTO postFuncDecl
        // Unset current UDF state
        // LABEL beforeWhile
        // expr()
        // NOT expr()
        // If expr is false, GOTO afterWend
        // GOTO LABEL beforeWhile
        // LABEL afterWend
        // Patch GOTO afterWend
        // stepCopy = step or 1 (default)
        // var=init
        // endCopy=end
        // GOTO LABEL CHECK
        // APPLY STEP
        // JUMP here from NEXT
        // Add step
        // CHECK
        // If (step >= 0 and var > end) or (step < 0 and var < end) GOTO after "next"
        // step >= 0
        // Patch GOTO LABEL Check
        // var > end
        // (step >= 0 and var > end)
        // step < 0
        // var < end
        // (step < 0 and var < end)
        // if (true) GOTO after NEXT
        // set linenumber on exitNext().
        // GOTO APPLY STEP
        // LABEL afterNext
        /*
         * condition
         * GOTOIF condition labelBeforeThen
         * GOTO labelAfterThen|labelBeforeElse
         * labelBeforeThen
         * ThenStmts
         * GOTO labelAfterThen|labelAfterElse
         * labelAfterThen
         * ElseStmts
         * labelAfterElse
         */
        // Patch IF true: condition
        // Patch IF true: GOTO labelBeforeThen
        // Patch IF false: GOTO labelAfterThen|labelBeforeElse
        // Patch THEN: GOTO labelAfterThen|labelAfterElse
        // IF condition is true, GOTO labelBeforeThen
        // IF condition is false, GOTO labelAfterThen|labelBeforeElse
        // Add instruction for:
        // THEN GOTO linenum | THEN linenum
        // GOTO labelAfterThen|labelAfterElse
        // Add instruction for:
        // ELSE linenum
        //
        // IF expr THEN BEGIN
        // ...
        // ELSE BEGIN
        // ...
        // END IF
        //
        //
        // expr.result
        // GOTO labelBeforeThen IF expr.result is true
        // GOTO labelAfterThen|labelBeforeElse
        // labelBeforeThen (patch GOTOIF)
        // GOTO labelAfterThen|labelAfterElse (else begin)
        // labelAfterThen
        // labelBeforeElse
        //
        // IF condition is true, GOTO labelBeforeThen
        // IF condition is false, GOTO labelAfterThen|labelBeforeElse
        // Add labelBeforeThen
        // Patch IF true: GOTO labelBeforeThen
        // GOTO labelAfterThen|labelAfterElse
        // GOTO labelAfterThen|labelAfterElse
        // Add labelAfterElse
        // Patch IF true: GOTO labelBeforeThen
        // Patch IF false: GOTO labelAfterThen|labelBeforeElse
        // Patch THEN: GOTO labelAfterThen|labelAfterElse
        // fileName, fileNumber
        // openMode, accessMode
        // lockMode, recordLen
        // fileName, fileNumber
        // openMode, accessMode
        // lockMode, recordLen
        public override void ExitClosestmt(PuffinBasicParser.ClosestmtContext ctx)
        {
            //var fileNumbers = ctx.DECIMAL().Stream().Map((fileNumberCtx) => Numbers.ParseInt32(fileNumberCtx.GetText(), GetCtxString(ctx))).Collect(Collectors.ToList());
            var fileNumbers = ctx.DECIMAL().Select((fileNumberCtx) => Numbers.ParseInt32(fileNumberCtx.GetText(), GetCtxString(ctx)));
            if (fileNumbers.Any())
            {
                ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.CLOSE_ALL, NULL_ID, NULL_ID, NULL_ID);
            }
            else
            {
                //fileNumbers.ForEach((fileNumber) => ir.AddInstruction(sourceFile, 
                //    currentLineNumber, 
                //    ctx.Start.StartIndex, 
                //    ctx.Stop.StopIndex, 
                //    OpCode.CLOSE, 
                //    ir.GetSymbolTable().AddTmp(INT32, (e) => e.GetValue().SetInt32(fileNumber)), NULL_ID, NULL_ID));

                foreach (var fileNumber in fileNumbers)
                {
                    ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.CLOSE,
                        ir.GetSymbolTable().AddTmp(PuffinBasicAtomTypeId.INT32, (e) => e.GetValue().SetInt32(fileNumber)),
                        NULL_ID, NULL_ID);
                }
            }
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        //
        // Expr
        //
        //
        // Functions
        //
        // n, x$, y$
        // x$, y$
        // x$, n, m
        // x$, n
        //
        // Stmt
        //
        // struct
        // array
        // scalar data type
        // struct
        // scalar data type
        // scalar
        //PuffinBasicAtomTypeId.lookup(compCtx.var2.getText());
        // array
        // list
        // struct
        // scalar data type
        // set
        // dict
        // struct
        // scalar data type
        // struct
        // throw
        // GOTO postFuncDecl
        // LABEL FuncStart
        // Push child scope
        // Copy expr to result
        // Pop declaration scope
        // GOTO Caller
        // LABEL postFuncDecl
        // Patch GOTO postFuncDecl
        // GOTO postFuncDecl
        // LABEL FuncStart
        // Push child scope
        // scalar
        //PuffinBasicAtomTypeId.lookup(compCtx.var2.getText());
        // list
        // struct
        // array
        // scalar data type
        // set
        // dict
        // struct
        // scalar data type
        // struct
        // array
        // throw
        // Copy expr to result
        // GOTO LABEL gotoCaller
        // Pop declaration scope
        // LABEL gotoCaller
        // GOTO Caller
        // LABEL postFuncDecl
        // Patch GOTO LABEL gotoCaller
        // Patch GOTO postFuncDecl
        // Unset current UDF state
        // LABEL beforeWhile
        // expr()
        // NOT expr()
        // If expr is false, GOTO afterWend
        // GOTO LABEL beforeWhile
        // LABEL afterWend
        // Patch GOTO afterWend
        // stepCopy = step or 1 (default)
        // var=init
        // endCopy=end
        // GOTO LABEL CHECK
        // APPLY STEP
        // JUMP here from NEXT
        // Add step
        // CHECK
        // If (step >= 0 and var > end) or (step < 0 and var < end) GOTO after "next"
        // step >= 0
        // Patch GOTO LABEL Check
        // var > end
        // (step >= 0 and var > end)
        // step < 0
        // var < end
        // (step < 0 and var < end)
        // if (true) GOTO after NEXT
        // set linenumber on exitNext().
        // GOTO APPLY STEP
        // LABEL afterNext
        /*
         * condition
         * GOTOIF condition labelBeforeThen
         * GOTO labelAfterThen|labelBeforeElse
         * labelBeforeThen
         * ThenStmts
         * GOTO labelAfterThen|labelAfterElse
         * labelAfterThen
         * ElseStmts
         * labelAfterElse
         */
        // Patch IF true: condition
        // Patch IF true: GOTO labelBeforeThen
        // Patch IF false: GOTO labelAfterThen|labelBeforeElse
        // Patch THEN: GOTO labelAfterThen|labelAfterElse
        // IF condition is true, GOTO labelBeforeThen
        // IF condition is false, GOTO labelAfterThen|labelBeforeElse
        // Add instruction for:
        // THEN GOTO linenum | THEN linenum
        // GOTO labelAfterThen|labelAfterElse
        // Add instruction for:
        // ELSE linenum
        //
        // IF expr THEN BEGIN
        // ...
        // ELSE BEGIN
        // ...
        // END IF
        //
        //
        // expr.result
        // GOTO labelBeforeThen IF expr.result is true
        // GOTO labelAfterThen|labelBeforeElse
        // labelBeforeThen (patch GOTOIF)
        // GOTO labelAfterThen|labelAfterElse (else begin)
        // labelAfterThen
        // labelBeforeElse
        //
        // IF condition is true, GOTO labelBeforeThen
        // IF condition is false, GOTO labelAfterThen|labelBeforeElse
        // Add labelBeforeThen
        // Patch IF true: GOTO labelBeforeThen
        // GOTO labelAfterThen|labelAfterElse
        // GOTO labelAfterThen|labelAfterElse
        // Add labelAfterElse
        // Patch IF true: GOTO labelBeforeThen
        // Patch IF false: GOTO labelAfterThen|labelBeforeElse
        // Patch THEN: GOTO labelAfterThen|labelAfterElse
        // fileName, fileNumber
        // openMode, accessMode
        // lockMode, recordLen
        // fileName, fileNumber
        // openMode, accessMode
        // lockMode, recordLen
        public override void ExitFieldstmt(PuffinBasicParser.FieldstmtContext ctx)
        {
            var fileNumberInstr = LookupInstruction(ctx.filenum);
            Types.AssertNumeric(ir.GetSymbolTable()[fileNumberInstr.result].GetType().GetAtomTypeId(), GetCtxString(ctx));
            var numEntries = ctx.variable().Count();
            for (int i = 0; i < numEntries; i++)
            {
                var recordPartLen = Numbers.ParseInt32(ctx.DECIMAL(i).GetText(), GetCtxString(ctx));
                var varInstr = LookupInstruction(ctx.variable(i));
                AssertVariable(ir.GetSymbolTable()[varInstr.result], GetCtxString(ctx));
                ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.PARAM2, varInstr.result, ir.GetSymbolTable().AddTmp(INT32, (e) => e.GetValue().SetInt32(recordPartLen)), NULL_ID);
            }


            // FileNumber, #fields
            ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.FIELD, fileNumberInstr.result, ir.GetSymbolTable().AddTmp(INT32, (e) => e.GetValue().SetInt32(numEntries)), NULL_ID);
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        //
        // Expr
        //
        //
        // Functions
        //
        // n, x$, y$
        // x$, y$
        // x$, n, m
        // x$, n
        //
        // Stmt
        //
        // struct
        // array
        // scalar data type
        // struct
        // scalar data type
        // scalar
        //PuffinBasicAtomTypeId.lookup(compCtx.var2.getText());
        // array
        // list
        // struct
        // scalar data type
        // set
        // dict
        // struct
        // scalar data type
        // struct
        // throw
        // GOTO postFuncDecl
        // LABEL FuncStart
        // Push child scope
        // Copy expr to result
        // Pop declaration scope
        // GOTO Caller
        // LABEL postFuncDecl
        // Patch GOTO postFuncDecl
        // GOTO postFuncDecl
        // LABEL FuncStart
        // Push child scope
        // scalar
        //PuffinBasicAtomTypeId.lookup(compCtx.var2.getText());
        // list
        // struct
        // array
        // scalar data type
        // set
        // dict
        // struct
        // scalar data type
        // struct
        // array
        // throw
        // Copy expr to result
        // GOTO LABEL gotoCaller
        // Pop declaration scope
        // LABEL gotoCaller
        // GOTO Caller
        // LABEL postFuncDecl
        // Patch GOTO LABEL gotoCaller
        // Patch GOTO postFuncDecl
        // Unset current UDF state
        // LABEL beforeWhile
        // expr()
        // NOT expr()
        // If expr is false, GOTO afterWend
        // GOTO LABEL beforeWhile
        // LABEL afterWend
        // Patch GOTO afterWend
        // stepCopy = step or 1 (default)
        // var=init
        // endCopy=end
        // GOTO LABEL CHECK
        // APPLY STEP
        // JUMP here from NEXT
        // Add step
        // CHECK
        // If (step >= 0 and var > end) or (step < 0 and var < end) GOTO after "next"
        // step >= 0
        // Patch GOTO LABEL Check
        // var > end
        // (step >= 0 and var > end)
        // step < 0
        // var < end
        // (step < 0 and var < end)
        // if (true) GOTO after NEXT
        // set linenumber on exitNext().
        // GOTO APPLY STEP
        // LABEL afterNext
        /*
         * condition
         * GOTOIF condition labelBeforeThen
         * GOTO labelAfterThen|labelBeforeElse
         * labelBeforeThen
         * ThenStmts
         * GOTO labelAfterThen|labelAfterElse
         * labelAfterThen
         * ElseStmts
         * labelAfterElse
         */
        // Patch IF true: condition
        // Patch IF true: GOTO labelBeforeThen
        // Patch IF false: GOTO labelAfterThen|labelBeforeElse
        // Patch THEN: GOTO labelAfterThen|labelAfterElse
        // IF condition is true, GOTO labelBeforeThen
        // IF condition is false, GOTO labelAfterThen|labelBeforeElse
        // Add instruction for:
        // THEN GOTO linenum | THEN linenum
        // GOTO labelAfterThen|labelAfterElse
        // Add instruction for:
        // ELSE linenum
        //
        // IF expr THEN BEGIN
        // ...
        // ELSE BEGIN
        // ...
        // END IF
        //
        //
        // expr.result
        // GOTO labelBeforeThen IF expr.result is true
        // GOTO labelAfterThen|labelBeforeElse
        // labelBeforeThen (patch GOTOIF)
        // GOTO labelAfterThen|labelAfterElse (else begin)
        // labelAfterThen
        // labelBeforeElse
        //
        // IF condition is true, GOTO labelBeforeThen
        // IF condition is false, GOTO labelAfterThen|labelBeforeElse
        // Add labelBeforeThen
        // Patch IF true: GOTO labelBeforeThen
        // GOTO labelAfterThen|labelAfterElse
        // GOTO labelAfterThen|labelAfterElse
        // Add labelAfterElse
        // Patch IF true: GOTO labelBeforeThen
        // Patch IF false: GOTO labelAfterThen|labelBeforeElse
        // Patch THEN: GOTO labelAfterThen|labelAfterElse
        // fileName, fileNumber
        // openMode, accessMode
        // lockMode, recordLen
        // fileName, fileNumber
        // openMode, accessMode
        // lockMode, recordLen
        // FileNumber, #fields
        private void AssertVariable(ISTEntry entry, string line)
        {
            if (!entry.IsLValue())
            {
                throw new PuffinBasicSemanticError(BAD_ARGUMENT, line, "Expected variable, but found: " + entry);
            }
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        //
        // Expr
        //
        //
        // Functions
        //
        // n, x$, y$
        // x$, y$
        // x$, n, m
        // x$, n
        //
        // Stmt
        //
        // struct
        // array
        // scalar data type
        // struct
        // scalar data type
        // scalar
        //PuffinBasicAtomTypeId.lookup(compCtx.var2.getText());
        // array
        // list
        // struct
        // scalar data type
        // set
        // dict
        // struct
        // scalar data type
        // struct
        // throw
        // GOTO postFuncDecl
        // LABEL FuncStart
        // Push child scope
        // Copy expr to result
        // Pop declaration scope
        // GOTO Caller
        // LABEL postFuncDecl
        // Patch GOTO postFuncDecl
        // GOTO postFuncDecl
        // LABEL FuncStart
        // Push child scope
        // scalar
        //PuffinBasicAtomTypeId.lookup(compCtx.var2.getText());
        // list
        // struct
        // array
        // scalar data type
        // set
        // dict
        // struct
        // scalar data type
        // struct
        // array
        // throw
        // Copy expr to result
        // GOTO LABEL gotoCaller
        // Pop declaration scope
        // LABEL gotoCaller
        // GOTO Caller
        // LABEL postFuncDecl
        // Patch GOTO LABEL gotoCaller
        // Patch GOTO postFuncDecl
        // Unset current UDF state
        // LABEL beforeWhile
        // expr()
        // NOT expr()
        // If expr is false, GOTO afterWend
        // GOTO LABEL beforeWhile
        // LABEL afterWend
        // Patch GOTO afterWend
        // stepCopy = step or 1 (default)
        // var=init
        // endCopy=end
        // GOTO LABEL CHECK
        // APPLY STEP
        // JUMP here from NEXT
        // Add step
        // CHECK
        // If (step >= 0 and var > end) or (step < 0 and var < end) GOTO after "next"
        // step >= 0
        // Patch GOTO LABEL Check
        // var > end
        // (step >= 0 and var > end)
        // step < 0
        // var < end
        // (step < 0 and var < end)
        // if (true) GOTO after NEXT
        // set linenumber on exitNext().
        // GOTO APPLY STEP
        // LABEL afterNext
        /*
         * condition
         * GOTOIF condition labelBeforeThen
         * GOTO labelAfterThen|labelBeforeElse
         * labelBeforeThen
         * ThenStmts
         * GOTO labelAfterThen|labelAfterElse
         * labelAfterThen
         * ElseStmts
         * labelAfterElse
         */
        // Patch IF true: condition
        // Patch IF true: GOTO labelBeforeThen
        // Patch IF false: GOTO labelAfterThen|labelBeforeElse
        // Patch THEN: GOTO labelAfterThen|labelAfterElse
        // IF condition is true, GOTO labelBeforeThen
        // IF condition is false, GOTO labelAfterThen|labelBeforeElse
        // Add instruction for:
        // THEN GOTO linenum | THEN linenum
        // GOTO labelAfterThen|labelAfterElse
        // Add instruction for:
        // ELSE linenum
        //
        // IF expr THEN BEGIN
        // ...
        // ELSE BEGIN
        // ...
        // END IF
        //
        //
        // expr.result
        // GOTO labelBeforeThen IF expr.result is true
        // GOTO labelAfterThen|labelBeforeElse
        // labelBeforeThen (patch GOTOIF)
        // GOTO labelAfterThen|labelAfterElse (else begin)
        // labelAfterThen
        // labelBeforeElse
        //
        // IF condition is true, GOTO labelBeforeThen
        // IF condition is false, GOTO labelAfterThen|labelBeforeElse
        // Add labelBeforeThen
        // Patch IF true: GOTO labelBeforeThen
        // GOTO labelAfterThen|labelAfterElse
        // GOTO labelAfterThen|labelAfterElse
        // Add labelAfterElse
        // Patch IF true: GOTO labelBeforeThen
        // Patch IF false: GOTO labelAfterThen|labelBeforeElse
        // Patch THEN: GOTO labelAfterThen|labelAfterElse
        // fileName, fileNumber
        // openMode, accessMode
        // lockMode, recordLen
        // fileName, fileNumber
        // openMode, accessMode
        // lockMode, recordLen
        // FileNumber, #fields
        private void Assert1DArray(STVariable variableEntry, string line)
        {
            var variable = variableEntry.GetVariable();
            if (!variable.IsArray() || !((ArrayType)variable.GetType()).IsNDArray(1))
            {
                throw new PuffinBasicSemanticError(BAD_ARGUMENT, line, "Variable: " + variable.GetVariableName() + " is not array1d");
            }
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        //
        // Expr
        //
        //
        // Functions
        //
        // n, x$, y$
        // x$, y$
        // x$, n, m
        // x$, n
        //
        // Stmt
        //
        // struct
        // array
        // scalar data type
        // struct
        // scalar data type
        // scalar
        //PuffinBasicAtomTypeId.lookup(compCtx.var2.getText());
        // array
        // list
        // struct
        // scalar data type
        // set
        // dict
        // struct
        // scalar data type
        // struct
        // throw
        // GOTO postFuncDecl
        // LABEL FuncStart
        // Push child scope
        // Copy expr to result
        // Pop declaration scope
        // GOTO Caller
        // LABEL postFuncDecl
        // Patch GOTO postFuncDecl
        // GOTO postFuncDecl
        // LABEL FuncStart
        // Push child scope
        // scalar
        //PuffinBasicAtomTypeId.lookup(compCtx.var2.getText());
        // list
        // struct
        // array
        // scalar data type
        // set
        // dict
        // struct
        // scalar data type
        // struct
        // array
        // throw
        // Copy expr to result
        // GOTO LABEL gotoCaller
        // Pop declaration scope
        // LABEL gotoCaller
        // GOTO Caller
        // LABEL postFuncDecl
        // Patch GOTO LABEL gotoCaller
        // Patch GOTO postFuncDecl
        // Unset current UDF state
        // LABEL beforeWhile
        // expr()
        // NOT expr()
        // If expr is false, GOTO afterWend
        // GOTO LABEL beforeWhile
        // LABEL afterWend
        // Patch GOTO afterWend
        // stepCopy = step or 1 (default)
        // var=init
        // endCopy=end
        // GOTO LABEL CHECK
        // APPLY STEP
        // JUMP here from NEXT
        // Add step
        // CHECK
        // If (step >= 0 and var > end) or (step < 0 and var < end) GOTO after "next"
        // step >= 0
        // Patch GOTO LABEL Check
        // var > end
        // (step >= 0 and var > end)
        // step < 0
        // var < end
        // (step < 0 and var < end)
        // if (true) GOTO after NEXT
        // set linenumber on exitNext().
        // GOTO APPLY STEP
        // LABEL afterNext
        /*
         * condition
         * GOTOIF condition labelBeforeThen
         * GOTO labelAfterThen|labelBeforeElse
         * labelBeforeThen
         * ThenStmts
         * GOTO labelAfterThen|labelAfterElse
         * labelAfterThen
         * ElseStmts
         * labelAfterElse
         */
        // Patch IF true: condition
        // Patch IF true: GOTO labelBeforeThen
        // Patch IF false: GOTO labelAfterThen|labelBeforeElse
        // Patch THEN: GOTO labelAfterThen|labelAfterElse
        // IF condition is true, GOTO labelBeforeThen
        // IF condition is false, GOTO labelAfterThen|labelBeforeElse
        // Add instruction for:
        // THEN GOTO linenum | THEN linenum
        // GOTO labelAfterThen|labelAfterElse
        // Add instruction for:
        // ELSE linenum
        //
        // IF expr THEN BEGIN
        // ...
        // ELSE BEGIN
        // ...
        // END IF
        //
        //
        // expr.result
        // GOTO labelBeforeThen IF expr.result is true
        // GOTO labelAfterThen|labelBeforeElse
        // labelBeforeThen (patch GOTOIF)
        // GOTO labelAfterThen|labelAfterElse (else begin)
        // labelAfterThen
        // labelBeforeElse
        //
        // IF condition is true, GOTO labelBeforeThen
        // IF condition is false, GOTO labelAfterThen|labelBeforeElse
        // Add labelBeforeThen
        // Patch IF true: GOTO labelBeforeThen
        // GOTO labelAfterThen|labelAfterElse
        // GOTO labelAfterThen|labelAfterElse
        // Add labelAfterElse
        // Patch IF true: GOTO labelBeforeThen
        // Patch IF false: GOTO labelAfterThen|labelBeforeElse
        // Patch THEN: GOTO labelAfterThen|labelAfterElse
        // fileName, fileNumber
        // openMode, accessMode
        // lockMode, recordLen
        // fileName, fileNumber
        // openMode, accessMode
        // lockMode, recordLen
        // FileNumber, #fields
        private void Assert2DArray(STVariable variableEntry, string line)
        {
            var variable = variableEntry.GetVariable();
            if (!variable.IsArray() || !((ArrayType)variable.GetType()).IsNDArray(2))
            {
                throw new PuffinBasicSemanticError(BAD_ARGUMENT, line, "Variable: " + variable.GetVariableName() + " is not array2d");
            }
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        //
        // Expr
        //
        //
        // Functions
        //
        // n, x$, y$
        // x$, y$
        // x$, n, m
        // x$, n
        //
        // Stmt
        //
        // struct
        // array
        // scalar data type
        // struct
        // scalar data type
        // scalar
        //PuffinBasicAtomTypeId.lookup(compCtx.var2.getText());
        // array
        // list
        // struct
        // scalar data type
        // set
        // dict
        // struct
        // scalar data type
        // struct
        // throw
        // GOTO postFuncDecl
        // LABEL FuncStart
        // Push child scope
        // Copy expr to result
        // Pop declaration scope
        // GOTO Caller
        // LABEL postFuncDecl
        // Patch GOTO postFuncDecl
        // GOTO postFuncDecl
        // LABEL FuncStart
        // Push child scope
        // scalar
        //PuffinBasicAtomTypeId.lookup(compCtx.var2.getText());
        // list
        // struct
        // array
        // scalar data type
        // set
        // dict
        // struct
        // scalar data type
        // struct
        // array
        // throw
        // Copy expr to result
        // GOTO LABEL gotoCaller
        // Pop declaration scope
        // LABEL gotoCaller
        // GOTO Caller
        // LABEL postFuncDecl
        // Patch GOTO LABEL gotoCaller
        // Patch GOTO postFuncDecl
        // Unset current UDF state
        // LABEL beforeWhile
        // expr()
        // NOT expr()
        // If expr is false, GOTO afterWend
        // GOTO LABEL beforeWhile
        // LABEL afterWend
        // Patch GOTO afterWend
        // stepCopy = step or 1 (default)
        // var=init
        // endCopy=end
        // GOTO LABEL CHECK
        // APPLY STEP
        // JUMP here from NEXT
        // Add step
        // CHECK
        // If (step >= 0 and var > end) or (step < 0 and var < end) GOTO after "next"
        // step >= 0
        // Patch GOTO LABEL Check
        // var > end
        // (step >= 0 and var > end)
        // step < 0
        // var < end
        // (step < 0 and var < end)
        // if (true) GOTO after NEXT
        // set linenumber on exitNext().
        // GOTO APPLY STEP
        // LABEL afterNext
        /*
         * condition
         * GOTOIF condition labelBeforeThen
         * GOTO labelAfterThen|labelBeforeElse
         * labelBeforeThen
         * ThenStmts
         * GOTO labelAfterThen|labelAfterElse
         * labelAfterThen
         * ElseStmts
         * labelAfterElse
         */
        // Patch IF true: condition
        // Patch IF true: GOTO labelBeforeThen
        // Patch IF false: GOTO labelAfterThen|labelBeforeElse
        // Patch THEN: GOTO labelAfterThen|labelAfterElse
        // IF condition is true, GOTO labelBeforeThen
        // IF condition is false, GOTO labelAfterThen|labelBeforeElse
        // Add instruction for:
        // THEN GOTO linenum | THEN linenum
        // GOTO labelAfterThen|labelAfterElse
        // Add instruction for:
        // ELSE linenum
        //
        // IF expr THEN BEGIN
        // ...
        // ELSE BEGIN
        // ...
        // END IF
        //
        //
        // expr.result
        // GOTO labelBeforeThen IF expr.result is true
        // GOTO labelAfterThen|labelBeforeElse
        // labelBeforeThen (patch GOTOIF)
        // GOTO labelAfterThen|labelAfterElse (else begin)
        // labelAfterThen
        // labelBeforeElse
        //
        // IF condition is true, GOTO labelBeforeThen
        // IF condition is false, GOTO labelAfterThen|labelBeforeElse
        // Add labelBeforeThen
        // Patch IF true: GOTO labelBeforeThen
        // GOTO labelAfterThen|labelAfterElse
        // GOTO labelAfterThen|labelAfterElse
        // Add labelAfterElse
        // Patch IF true: GOTO labelBeforeThen
        // Patch IF false: GOTO labelAfterThen|labelBeforeElse
        // Patch THEN: GOTO labelAfterThen|labelAfterElse
        // fileName, fileNumber
        // openMode, accessMode
        // lockMode, recordLen
        // fileName, fileNumber
        // openMode, accessMode
        // lockMode, recordLen
        // FileNumber, #fields
        private void AssertNDArray(STVariable variable, string line)
        {
            if (!variable.GetVariable().IsArray())
            {
                throw new PuffinBasicSemanticError(BAD_ARGUMENT, line, "Variable: " + variable.GetVariable().GetVariableName() + " is not array");
            }
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        //
        // Expr
        //
        //
        // Functions
        //
        // n, x$, y$
        // x$, y$
        // x$, n, m
        // x$, n
        //
        // Stmt
        //
        // struct
        // array
        // scalar data type
        // struct
        // scalar data type
        // scalar
        //PuffinBasicAtomTypeId.lookup(compCtx.var2.getText());
        // array
        // list
        // struct
        // scalar data type
        // set
        // dict
        // struct
        // scalar data type
        // struct
        // throw
        // GOTO postFuncDecl
        // LABEL FuncStart
        // Push child scope
        // Copy expr to result
        // Pop declaration scope
        // GOTO Caller
        // LABEL postFuncDecl
        // Patch GOTO postFuncDecl
        // GOTO postFuncDecl
        // LABEL FuncStart
        // Push child scope
        // scalar
        //PuffinBasicAtomTypeId.lookup(compCtx.var2.getText());
        // list
        // struct
        // array
        // scalar data type
        // set
        // dict
        // struct
        // scalar data type
        // struct
        // array
        // throw
        // Copy expr to result
        // GOTO LABEL gotoCaller
        // Pop declaration scope
        // LABEL gotoCaller
        // GOTO Caller
        // LABEL postFuncDecl
        // Patch GOTO LABEL gotoCaller
        // Patch GOTO postFuncDecl
        // Unset current UDF state
        // LABEL beforeWhile
        // expr()
        // NOT expr()
        // If expr is false, GOTO afterWend
        // GOTO LABEL beforeWhile
        // LABEL afterWend
        // Patch GOTO afterWend
        // stepCopy = step or 1 (default)
        // var=init
        // endCopy=end
        // GOTO LABEL CHECK
        // APPLY STEP
        // JUMP here from NEXT
        // Add step
        // CHECK
        // If (step >= 0 and var > end) or (step < 0 and var < end) GOTO after "next"
        // step >= 0
        // Patch GOTO LABEL Check
        // var > end
        // (step >= 0 and var > end)
        // step < 0
        // var < end
        // (step < 0 and var < end)
        // if (true) GOTO after NEXT
        // set linenumber on exitNext().
        // GOTO APPLY STEP
        // LABEL afterNext
        /*
         * condition
         * GOTOIF condition labelBeforeThen
         * GOTO labelAfterThen|labelBeforeElse
         * labelBeforeThen
         * ThenStmts
         * GOTO labelAfterThen|labelAfterElse
         * labelAfterThen
         * ElseStmts
         * labelAfterElse
         */
        // Patch IF true: condition
        // Patch IF true: GOTO labelBeforeThen
        // Patch IF false: GOTO labelAfterThen|labelBeforeElse
        // Patch THEN: GOTO labelAfterThen|labelAfterElse
        // IF condition is true, GOTO labelBeforeThen
        // IF condition is false, GOTO labelAfterThen|labelBeforeElse
        // Add instruction for:
        // THEN GOTO linenum | THEN linenum
        // GOTO labelAfterThen|labelAfterElse
        // Add instruction for:
        // ELSE linenum
        //
        // IF expr THEN BEGIN
        // ...
        // ELSE BEGIN
        // ...
        // END IF
        //
        //
        // expr.result
        // GOTO labelBeforeThen IF expr.result is true
        // GOTO labelAfterThen|labelBeforeElse
        // labelBeforeThen (patch GOTOIF)
        // GOTO labelAfterThen|labelAfterElse (else begin)
        // labelAfterThen
        // labelBeforeElse
        //
        // IF condition is true, GOTO labelBeforeThen
        // IF condition is false, GOTO labelAfterThen|labelBeforeElse
        // Add labelBeforeThen
        // Patch IF true: GOTO labelBeforeThen
        // GOTO labelAfterThen|labelAfterElse
        // GOTO labelAfterThen|labelAfterElse
        // Add labelAfterElse
        // Patch IF true: GOTO labelBeforeThen
        // Patch IF false: GOTO labelAfterThen|labelBeforeElse
        // Patch THEN: GOTO labelAfterThen|labelAfterElse
        // fileName, fileNumber
        // openMode, accessMode
        // lockMode, recordLen
        // fileName, fileNumber
        // openMode, accessMode
        // lockMode, recordLen
        // FileNumber, #fields
        public override void ExitPutstmt(PuffinBasicParser.PutstmtContext ctx)
        {
            var fileNumberInstr = Numbers.ParseInt32(ctx.filenum.Text, GetCtxString(ctx));
            int exprId;
            if (ctx.expr() != null)
            {
                exprId = LookupInstruction(ctx.expr()).result;
                Types.AssertNumeric(ir.GetSymbolTable()[exprId].GetType().GetAtomTypeId(), GetCtxString(ctx));
            }
            else
            {
                exprId = NULL_ID;
            }

            ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.PUTF, ir.GetSymbolTable().AddTmp(INT32, (e) => e.GetValue().SetInt32(fileNumberInstr)), exprId, NULL_ID);
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        //
        // Expr
        //
        //
        // Functions
        //
        // n, x$, y$
        // x$, y$
        // x$, n, m
        // x$, n
        //
        // Stmt
        //
        // struct
        // array
        // scalar data type
        // struct
        // scalar data type
        // scalar
        //PuffinBasicAtomTypeId.lookup(compCtx.var2.getText());
        // array
        // list
        // struct
        // scalar data type
        // set
        // dict
        // struct
        // scalar data type
        // struct
        // throw
        // GOTO postFuncDecl
        // LABEL FuncStart
        // Push child scope
        // Copy expr to result
        // Pop declaration scope
        // GOTO Caller
        // LABEL postFuncDecl
        // Patch GOTO postFuncDecl
        // GOTO postFuncDecl
        // LABEL FuncStart
        // Push child scope
        // scalar
        //PuffinBasicAtomTypeId.lookup(compCtx.var2.getText());
        // list
        // struct
        // array
        // scalar data type
        // set
        // dict
        // struct
        // scalar data type
        // struct
        // array
        // throw
        // Copy expr to result
        // GOTO LABEL gotoCaller
        // Pop declaration scope
        // LABEL gotoCaller
        // GOTO Caller
        // LABEL postFuncDecl
        // Patch GOTO LABEL gotoCaller
        // Patch GOTO postFuncDecl
        // Unset current UDF state
        // LABEL beforeWhile
        // expr()
        // NOT expr()
        // If expr is false, GOTO afterWend
        // GOTO LABEL beforeWhile
        // LABEL afterWend
        // Patch GOTO afterWend
        // stepCopy = step or 1 (default)
        // var=init
        // endCopy=end
        // GOTO LABEL CHECK
        // APPLY STEP
        // JUMP here from NEXT
        // Add step
        // CHECK
        // If (step >= 0 and var > end) or (step < 0 and var < end) GOTO after "next"
        // step >= 0
        // Patch GOTO LABEL Check
        // var > end
        // (step >= 0 and var > end)
        // step < 0
        // var < end
        // (step < 0 and var < end)
        // if (true) GOTO after NEXT
        // set linenumber on exitNext().
        // GOTO APPLY STEP
        // LABEL afterNext
        /*
         * condition
         * GOTOIF condition labelBeforeThen
         * GOTO labelAfterThen|labelBeforeElse
         * labelBeforeThen
         * ThenStmts
         * GOTO labelAfterThen|labelAfterElse
         * labelAfterThen
         * ElseStmts
         * labelAfterElse
         */
        // Patch IF true: condition
        // Patch IF true: GOTO labelBeforeThen
        // Patch IF false: GOTO labelAfterThen|labelBeforeElse
        // Patch THEN: GOTO labelAfterThen|labelAfterElse
        // IF condition is true, GOTO labelBeforeThen
        // IF condition is false, GOTO labelAfterThen|labelBeforeElse
        // Add instruction for:
        // THEN GOTO linenum | THEN linenum
        // GOTO labelAfterThen|labelAfterElse
        // Add instruction for:
        // ELSE linenum
        //
        // IF expr THEN BEGIN
        // ...
        // ELSE BEGIN
        // ...
        // END IF
        //
        //
        // expr.result
        // GOTO labelBeforeThen IF expr.result is true
        // GOTO labelAfterThen|labelBeforeElse
        // labelBeforeThen (patch GOTOIF)
        // GOTO labelAfterThen|labelAfterElse (else begin)
        // labelAfterThen
        // labelBeforeElse
        //
        // IF condition is true, GOTO labelBeforeThen
        // IF condition is false, GOTO labelAfterThen|labelBeforeElse
        // Add labelBeforeThen
        // Patch IF true: GOTO labelBeforeThen
        // GOTO labelAfterThen|labelAfterElse
        // GOTO labelAfterThen|labelAfterElse
        // Add labelAfterElse
        // Patch IF true: GOTO labelBeforeThen
        // Patch IF false: GOTO labelAfterThen|labelBeforeElse
        // Patch THEN: GOTO labelAfterThen|labelAfterElse
        // fileName, fileNumber
        // openMode, accessMode
        // lockMode, recordLen
        // fileName, fileNumber
        // openMode, accessMode
        // lockMode, recordLen
        // FileNumber, #fields
        public override void ExitMiddlrstmt(PuffinBasicParser.MiddlrstmtContext ctx)
        {
            var varInstr = LookupInstruction(ctx.variable());
            var nInstr = LookupInstruction(ctx.expr(0));
            var mInstrId = ctx.expr().Count()== 3 ? LookupInstruction(ctx.expr(1)).result : ir.GetSymbolTable().AddTmp(INT32, (e) => e.GetValue().SetInt32(-1));
            var replacement = ctx.expr().Count()== 3 ? LookupInstruction(ctx.expr(2)) : LookupInstruction(ctx.expr(1));
            Types.AssertString(ir.GetSymbolTable()[varInstr.result].GetType().GetAtomTypeId(), GetCtxString(ctx));
            Types.AssertString(ir.GetSymbolTable()[replacement.result].GetType().GetAtomTypeId(), GetCtxString(ctx));
            AssertVariable(ir.GetSymbolTable()[varInstr.result], GetCtxString(ctx));
            Types.AssertNumeric(ir.GetSymbolTable()[nInstr.result].GetType().GetAtomTypeId(), GetCtxString(ctx));
            Types.AssertNumeric(ir.GetSymbolTable()[mInstrId].GetType().GetAtomTypeId(), GetCtxString(ctx));
            ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.PARAM2, varInstr.result, nInstr.result, NULL_ID);
            ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.MIDDLR_STMT, mInstrId, replacement.result, NULL_ID);
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        //
        // Expr
        //
        //
        // Functions
        //
        // n, x$, y$
        // x$, y$
        // x$, n, m
        // x$, n
        //
        // Stmt
        //
        // struct
        // array
        // scalar data type
        // struct
        // scalar data type
        // scalar
        //PuffinBasicAtomTypeId.lookup(compCtx.var2.getText());
        // array
        // list
        // struct
        // scalar data type
        // set
        // dict
        // struct
        // scalar data type
        // struct
        // throw
        // GOTO postFuncDecl
        // LABEL FuncStart
        // Push child scope
        // Copy expr to result
        // Pop declaration scope
        // GOTO Caller
        // LABEL postFuncDecl
        // Patch GOTO postFuncDecl
        // GOTO postFuncDecl
        // LABEL FuncStart
        // Push child scope
        // scalar
        //PuffinBasicAtomTypeId.lookup(compCtx.var2.getText());
        // list
        // struct
        // array
        // scalar data type
        // set
        // dict
        // struct
        // scalar data type
        // struct
        // array
        // throw
        // Copy expr to result
        // GOTO LABEL gotoCaller
        // Pop declaration scope
        // LABEL gotoCaller
        // GOTO Caller
        // LABEL postFuncDecl
        // Patch GOTO LABEL gotoCaller
        // Patch GOTO postFuncDecl
        // Unset current UDF state
        // LABEL beforeWhile
        // expr()
        // NOT expr()
        // If expr is false, GOTO afterWend
        // GOTO LABEL beforeWhile
        // LABEL afterWend
        // Patch GOTO afterWend
        // stepCopy = step or 1 (default)
        // var=init
        // endCopy=end
        // GOTO LABEL CHECK
        // APPLY STEP
        // JUMP here from NEXT
        // Add step
        // CHECK
        // If (step >= 0 and var > end) or (step < 0 and var < end) GOTO after "next"
        // step >= 0
        // Patch GOTO LABEL Check
        // var > end
        // (step >= 0 and var > end)
        // step < 0
        // var < end
        // (step < 0 and var < end)
        // if (true) GOTO after NEXT
        // set linenumber on exitNext().
        // GOTO APPLY STEP
        // LABEL afterNext
        /*
         * condition
         * GOTOIF condition labelBeforeThen
         * GOTO labelAfterThen|labelBeforeElse
         * labelBeforeThen
         * ThenStmts
         * GOTO labelAfterThen|labelAfterElse
         * labelAfterThen
         * ElseStmts
         * labelAfterElse
         */
        // Patch IF true: condition
        // Patch IF true: GOTO labelBeforeThen
        // Patch IF false: GOTO labelAfterThen|labelBeforeElse
        // Patch THEN: GOTO labelAfterThen|labelAfterElse
        // IF condition is true, GOTO labelBeforeThen
        // IF condition is false, GOTO labelAfterThen|labelBeforeElse
        // Add instruction for:
        // THEN GOTO linenum | THEN linenum
        // GOTO labelAfterThen|labelAfterElse
        // Add instruction for:
        // ELSE linenum
        //
        // IF expr THEN BEGIN
        // ...
        // ELSE BEGIN
        // ...
        // END IF
        //
        //
        // expr.result
        // GOTO labelBeforeThen IF expr.result is true
        // GOTO labelAfterThen|labelBeforeElse
        // labelBeforeThen (patch GOTOIF)
        // GOTO labelAfterThen|labelAfterElse (else begin)
        // labelAfterThen
        // labelBeforeElse
        //
        // IF condition is true, GOTO labelBeforeThen
        // IF condition is false, GOTO labelAfterThen|labelBeforeElse
        // Add labelBeforeThen
        // Patch IF true: GOTO labelBeforeThen
        // GOTO labelAfterThen|labelAfterElse
        // GOTO labelAfterThen|labelAfterElse
        // Add labelAfterElse
        // Patch IF true: GOTO labelBeforeThen
        // Patch IF false: GOTO labelAfterThen|labelBeforeElse
        // Patch THEN: GOTO labelAfterThen|labelAfterElse
        // fileName, fileNumber
        // openMode, accessMode
        // lockMode, recordLen
        // fileName, fileNumber
        // openMode, accessMode
        // lockMode, recordLen
        // FileNumber, #fields
        public override void ExitGetstmt(PuffinBasicParser.GetstmtContext ctx)
        {
            var fileNumberInstr = Numbers.ParseInt32(ctx.filenum.Text, GetCtxString(ctx));
            int exprId;
            if (ctx.expr() != null)
            {
                exprId = LookupInstruction(ctx.expr()).result;
                Types.AssertNumeric(ir.GetSymbolTable()[exprId].GetType().GetAtomTypeId(), GetCtxString(ctx));
            }
            else
            {
                exprId = NULL_ID;
            }

            ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.GETF, ir.GetSymbolTable().AddTmp(INT32, (e) => e.GetValue().SetInt32(fileNumberInstr)), exprId, NULL_ID);
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        //
        // Expr
        //
        //
        // Functions
        //
        // n, x$, y$
        // x$, y$
        // x$, n, m
        // x$, n
        //
        // Stmt
        //
        // struct
        // array
        // scalar data type
        // struct
        // scalar data type
        // scalar
        //PuffinBasicAtomTypeId.lookup(compCtx.var2.getText());
        // array
        // list
        // struct
        // scalar data type
        // set
        // dict
        // struct
        // scalar data type
        // struct
        // throw
        // GOTO postFuncDecl
        // LABEL FuncStart
        // Push child scope
        // Copy expr to result
        // Pop declaration scope
        // GOTO Caller
        // LABEL postFuncDecl
        // Patch GOTO postFuncDecl
        // GOTO postFuncDecl
        // LABEL FuncStart
        // Push child scope
        // scalar
        //PuffinBasicAtomTypeId.lookup(compCtx.var2.getText());
        // list
        // struct
        // array
        // scalar data type
        // set
        // dict
        // struct
        // scalar data type
        // struct
        // array
        // throw
        // Copy expr to result
        // GOTO LABEL gotoCaller
        // Pop declaration scope
        // LABEL gotoCaller
        // GOTO Caller
        // LABEL postFuncDecl
        // Patch GOTO LABEL gotoCaller
        // Patch GOTO postFuncDecl
        // Unset current UDF state
        // LABEL beforeWhile
        // expr()
        // NOT expr()
        // If expr is false, GOTO afterWend
        // GOTO LABEL beforeWhile
        // LABEL afterWend
        // Patch GOTO afterWend
        // stepCopy = step or 1 (default)
        // var=init
        // endCopy=end
        // GOTO LABEL CHECK
        // APPLY STEP
        // JUMP here from NEXT
        // Add step
        // CHECK
        // If (step >= 0 and var > end) or (step < 0 and var < end) GOTO after "next"
        // step >= 0
        // Patch GOTO LABEL Check
        // var > end
        // (step >= 0 and var > end)
        // step < 0
        // var < end
        // (step < 0 and var < end)
        // if (true) GOTO after NEXT
        // set linenumber on exitNext().
        // GOTO APPLY STEP
        // LABEL afterNext
        /*
         * condition
         * GOTOIF condition labelBeforeThen
         * GOTO labelAfterThen|labelBeforeElse
         * labelBeforeThen
         * ThenStmts
         * GOTO labelAfterThen|labelAfterElse
         * labelAfterThen
         * ElseStmts
         * labelAfterElse
         */
        // Patch IF true: condition
        // Patch IF true: GOTO labelBeforeThen
        // Patch IF false: GOTO labelAfterThen|labelBeforeElse
        // Patch THEN: GOTO labelAfterThen|labelAfterElse
        // IF condition is true, GOTO labelBeforeThen
        // IF condition is false, GOTO labelAfterThen|labelBeforeElse
        // Add instruction for:
        // THEN GOTO linenum | THEN linenum
        // GOTO labelAfterThen|labelAfterElse
        // Add instruction for:
        // ELSE linenum
        //
        // IF expr THEN BEGIN
        // ...
        // ELSE BEGIN
        // ...
        // END IF
        //
        //
        // expr.result
        // GOTO labelBeforeThen IF expr.result is true
        // GOTO labelAfterThen|labelBeforeElse
        // labelBeforeThen (patch GOTOIF)
        // GOTO labelAfterThen|labelAfterElse (else begin)
        // labelAfterThen
        // labelBeforeElse
        //
        // IF condition is true, GOTO labelBeforeThen
        // IF condition is false, GOTO labelAfterThen|labelBeforeElse
        // Add labelBeforeThen
        // Patch IF true: GOTO labelBeforeThen
        // GOTO labelAfterThen|labelAfterElse
        // GOTO labelAfterThen|labelAfterElse
        // Add labelAfterElse
        // Patch IF true: GOTO labelBeforeThen
        // Patch IF false: GOTO labelAfterThen|labelBeforeElse
        // Patch THEN: GOTO labelAfterThen|labelAfterElse
        // fileName, fileNumber
        // openMode, accessMode
        // lockMode, recordLen
        // fileName, fileNumber
        // openMode, accessMode
        // lockMode, recordLen
        // FileNumber, #fields
        public override void ExitRandomizestmt(PuffinBasicParser.RandomizestmtContext ctx)
        {
            var exprId = LookupInstruction(ctx.expr()).result;
            Types.AssertNumeric(ir.GetSymbolTable()[exprId].GetType().GetAtomTypeId(), GetCtxString(ctx));
            ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.RANDOMIZE, exprId, NULL_ID, NULL_ID);
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        //
        // Expr
        //
        //
        // Functions
        //
        // n, x$, y$
        // x$, y$
        // x$, n, m
        // x$, n
        //
        // Stmt
        //
        // struct
        // array
        // scalar data type
        // struct
        // scalar data type
        // scalar
        //PuffinBasicAtomTypeId.lookup(compCtx.var2.getText());
        // array
        // list
        // struct
        // scalar data type
        // set
        // dict
        // struct
        // scalar data type
        // struct
        // throw
        // GOTO postFuncDecl
        // LABEL FuncStart
        // Push child scope
        // Copy expr to result
        // Pop declaration scope
        // GOTO Caller
        // LABEL postFuncDecl
        // Patch GOTO postFuncDecl
        // GOTO postFuncDecl
        // LABEL FuncStart
        // Push child scope
        // scalar
        //PuffinBasicAtomTypeId.lookup(compCtx.var2.getText());
        // list
        // struct
        // array
        // scalar data type
        // set
        // dict
        // struct
        // scalar data type
        // struct
        // array
        // throw
        // Copy expr to result
        // GOTO LABEL gotoCaller
        // Pop declaration scope
        // LABEL gotoCaller
        // GOTO Caller
        // LABEL postFuncDecl
        // Patch GOTO LABEL gotoCaller
        // Patch GOTO postFuncDecl
        // Unset current UDF state
        // LABEL beforeWhile
        // expr()
        // NOT expr()
        // If expr is false, GOTO afterWend
        // GOTO LABEL beforeWhile
        // LABEL afterWend
        // Patch GOTO afterWend
        // stepCopy = step or 1 (default)
        // var=init
        // endCopy=end
        // GOTO LABEL CHECK
        // APPLY STEP
        // JUMP here from NEXT
        // Add step
        // CHECK
        // If (step >= 0 and var > end) or (step < 0 and var < end) GOTO after "next"
        // step >= 0
        // Patch GOTO LABEL Check
        // var > end
        // (step >= 0 and var > end)
        // step < 0
        // var < end
        // (step < 0 and var < end)
        // if (true) GOTO after NEXT
        // set linenumber on exitNext().
        // GOTO APPLY STEP
        // LABEL afterNext
        /*
         * condition
         * GOTOIF condition labelBeforeThen
         * GOTO labelAfterThen|labelBeforeElse
         * labelBeforeThen
         * ThenStmts
         * GOTO labelAfterThen|labelAfterElse
         * labelAfterThen
         * ElseStmts
         * labelAfterElse
         */
        // Patch IF true: condition
        // Patch IF true: GOTO labelBeforeThen
        // Patch IF false: GOTO labelAfterThen|labelBeforeElse
        // Patch THEN: GOTO labelAfterThen|labelAfterElse
        // IF condition is true, GOTO labelBeforeThen
        // IF condition is false, GOTO labelAfterThen|labelBeforeElse
        // Add instruction for:
        // THEN GOTO linenum | THEN linenum
        // GOTO labelAfterThen|labelAfterElse
        // Add instruction for:
        // ELSE linenum
        //
        // IF expr THEN BEGIN
        // ...
        // ELSE BEGIN
        // ...
        // END IF
        //
        //
        // expr.result
        // GOTO labelBeforeThen IF expr.result is true
        // GOTO labelAfterThen|labelBeforeElse
        // labelBeforeThen (patch GOTOIF)
        // GOTO labelAfterThen|labelAfterElse (else begin)
        // labelAfterThen
        // labelBeforeElse
        //
        // IF condition is true, GOTO labelBeforeThen
        // IF condition is false, GOTO labelAfterThen|labelBeforeElse
        // Add labelBeforeThen
        // Patch IF true: GOTO labelBeforeThen
        // GOTO labelAfterThen|labelAfterElse
        // GOTO labelAfterThen|labelAfterElse
        // Add labelAfterElse
        // Patch IF true: GOTO labelBeforeThen
        // Patch IF false: GOTO labelAfterThen|labelBeforeElse
        // Patch THEN: GOTO labelAfterThen|labelAfterElse
        // fileName, fileNumber
        // openMode, accessMode
        // lockMode, recordLen
        // fileName, fileNumber
        // openMode, accessMode
        // lockMode, recordLen
        // FileNumber, #fields
        public override void ExitRandomizetimerstmt(PuffinBasicParser.RandomizetimerstmtContext ctx)
        {
            ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.RANDOMIZE_TIMER, NULL_ID, NULL_ID, NULL_ID);
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        //
        // Expr
        //
        //
        // Functions
        //
        // n, x$, y$
        // x$, y$
        // x$, n, m
        // x$, n
        //
        // Stmt
        //
        // struct
        // array
        // scalar data type
        // struct
        // scalar data type
        // scalar
        //PuffinBasicAtomTypeId.lookup(compCtx.var2.getText());
        // array
        // list
        // struct
        // scalar data type
        // set
        // dict
        // struct
        // scalar data type
        // struct
        // throw
        // GOTO postFuncDecl
        // LABEL FuncStart
        // Push child scope
        // Copy expr to result
        // Pop declaration scope
        // GOTO Caller
        // LABEL postFuncDecl
        // Patch GOTO postFuncDecl
        // GOTO postFuncDecl
        // LABEL FuncStart
        // Push child scope
        // scalar
        //PuffinBasicAtomTypeId.lookup(compCtx.var2.getText());
        // list
        // struct
        // array
        // scalar data type
        // set
        // dict
        // struct
        // scalar data type
        // struct
        // array
        // throw
        // Copy expr to result
        // GOTO LABEL gotoCaller
        // Pop declaration scope
        // LABEL gotoCaller
        // GOTO Caller
        // LABEL postFuncDecl
        // Patch GOTO LABEL gotoCaller
        // Patch GOTO postFuncDecl
        // Unset current UDF state
        // LABEL beforeWhile
        // expr()
        // NOT expr()
        // If expr is false, GOTO afterWend
        // GOTO LABEL beforeWhile
        // LABEL afterWend
        // Patch GOTO afterWend
        // stepCopy = step or 1 (default)
        // var=init
        // endCopy=end
        // GOTO LABEL CHECK
        // APPLY STEP
        // JUMP here from NEXT
        // Add step
        // CHECK
        // If (step >= 0 and var > end) or (step < 0 and var < end) GOTO after "next"
        // step >= 0
        // Patch GOTO LABEL Check
        // var > end
        // (step >= 0 and var > end)
        // step < 0
        // var < end
        // (step < 0 and var < end)
        // if (true) GOTO after NEXT
        // set linenumber on exitNext().
        // GOTO APPLY STEP
        // LABEL afterNext
        /*
         * condition
         * GOTOIF condition labelBeforeThen
         * GOTO labelAfterThen|labelBeforeElse
         * labelBeforeThen
         * ThenStmts
         * GOTO labelAfterThen|labelAfterElse
         * labelAfterThen
         * ElseStmts
         * labelAfterElse
         */
        // Patch IF true: condition
        // Patch IF true: GOTO labelBeforeThen
        // Patch IF false: GOTO labelAfterThen|labelBeforeElse
        // Patch THEN: GOTO labelAfterThen|labelAfterElse
        // IF condition is true, GOTO labelBeforeThen
        // IF condition is false, GOTO labelAfterThen|labelBeforeElse
        // Add instruction for:
        // THEN GOTO linenum | THEN linenum
        // GOTO labelAfterThen|labelAfterElse
        // Add instruction for:
        // ELSE linenum
        //
        // IF expr THEN BEGIN
        // ...
        // ELSE BEGIN
        // ...
        // END IF
        //
        //
        // expr.result
        // GOTO labelBeforeThen IF expr.result is true
        // GOTO labelAfterThen|labelBeforeElse
        // labelBeforeThen (patch GOTOIF)
        // GOTO labelAfterThen|labelAfterElse (else begin)
        // labelAfterThen
        // labelBeforeElse
        //
        // IF condition is true, GOTO labelBeforeThen
        // IF condition is false, GOTO labelAfterThen|labelBeforeElse
        // Add labelBeforeThen
        // Patch IF true: GOTO labelBeforeThen
        // GOTO labelAfterThen|labelAfterElse
        // GOTO labelAfterThen|labelAfterElse
        // Add labelAfterElse
        // Patch IF true: GOTO labelBeforeThen
        // Patch IF false: GOTO labelAfterThen|labelBeforeElse
        // Patch THEN: GOTO labelAfterThen|labelAfterElse
        // fileName, fileNumber
        // openMode, accessMode
        // lockMode, recordLen
        // fileName, fileNumber
        // openMode, accessMode
        // lockMode, recordLen
        // FileNumber, #fields
        public override void ExitDefintstmt(PuffinBasicParser.DefintstmtContext ctx)
        {
            HandleDefTypeStmt(ctx.LETTERRANGE(), INT32);
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        //
        // Expr
        //
        //
        // Functions
        //
        // n, x$, y$
        // x$, y$
        // x$, n, m
        // x$, n
        //
        // Stmt
        //
        // struct
        // array
        // scalar data type
        // struct
        // scalar data type
        // scalar
        //PuffinBasicAtomTypeId.lookup(compCtx.var2.getText());
        // array
        // list
        // struct
        // scalar data type
        // set
        // dict
        // struct
        // scalar data type
        // struct
        // throw
        // GOTO postFuncDecl
        // LABEL FuncStart
        // Push child scope
        // Copy expr to result
        // Pop declaration scope
        // GOTO Caller
        // LABEL postFuncDecl
        // Patch GOTO postFuncDecl
        // GOTO postFuncDecl
        // LABEL FuncStart
        // Push child scope
        // scalar
        //PuffinBasicAtomTypeId.lookup(compCtx.var2.getText());
        // list
        // struct
        // array
        // scalar data type
        // set
        // dict
        // struct
        // scalar data type
        // struct
        // array
        // throw
        // Copy expr to result
        // GOTO LABEL gotoCaller
        // Pop declaration scope
        // LABEL gotoCaller
        // GOTO Caller
        // LABEL postFuncDecl
        // Patch GOTO LABEL gotoCaller
        // Patch GOTO postFuncDecl
        // Unset current UDF state
        // LABEL beforeWhile
        // expr()
        // NOT expr()
        // If expr is false, GOTO afterWend
        // GOTO LABEL beforeWhile
        // LABEL afterWend
        // Patch GOTO afterWend
        // stepCopy = step or 1 (default)
        // var=init
        // endCopy=end
        // GOTO LABEL CHECK
        // APPLY STEP
        // JUMP here from NEXT
        // Add step
        // CHECK
        // If (step >= 0 and var > end) or (step < 0 and var < end) GOTO after "next"
        // step >= 0
        // Patch GOTO LABEL Check
        // var > end
        // (step >= 0 and var > end)
        // step < 0
        // var < end
        // (step < 0 and var < end)
        // if (true) GOTO after NEXT
        // set linenumber on exitNext().
        // GOTO APPLY STEP
        // LABEL afterNext
        /*
         * condition
         * GOTOIF condition labelBeforeThen
         * GOTO labelAfterThen|labelBeforeElse
         * labelBeforeThen
         * ThenStmts
         * GOTO labelAfterThen|labelAfterElse
         * labelAfterThen
         * ElseStmts
         * labelAfterElse
         */
        // Patch IF true: condition
        // Patch IF true: GOTO labelBeforeThen
        // Patch IF false: GOTO labelAfterThen|labelBeforeElse
        // Patch THEN: GOTO labelAfterThen|labelAfterElse
        // IF condition is true, GOTO labelBeforeThen
        // IF condition is false, GOTO labelAfterThen|labelBeforeElse
        // Add instruction for:
        // THEN GOTO linenum | THEN linenum
        // GOTO labelAfterThen|labelAfterElse
        // Add instruction for:
        // ELSE linenum
        //
        // IF expr THEN BEGIN
        // ...
        // ELSE BEGIN
        // ...
        // END IF
        //
        //
        // expr.result
        // GOTO labelBeforeThen IF expr.result is true
        // GOTO labelAfterThen|labelBeforeElse
        // labelBeforeThen (patch GOTOIF)
        // GOTO labelAfterThen|labelAfterElse (else begin)
        // labelAfterThen
        // labelBeforeElse
        //
        // IF condition is true, GOTO labelBeforeThen
        // IF condition is false, GOTO labelAfterThen|labelBeforeElse
        // Add labelBeforeThen
        // Patch IF true: GOTO labelBeforeThen
        // GOTO labelAfterThen|labelAfterElse
        // GOTO labelAfterThen|labelAfterElse
        // Add labelAfterElse
        // Patch IF true: GOTO labelBeforeThen
        // Patch IF false: GOTO labelAfterThen|labelBeforeElse
        // Patch THEN: GOTO labelAfterThen|labelAfterElse
        // fileName, fileNumber
        // openMode, accessMode
        // lockMode, recordLen
        // fileName, fileNumber
        // openMode, accessMode
        // lockMode, recordLen
        // FileNumber, #fields
        public override void ExitDeflngstmt(PuffinBasicParser.DeflngstmtContext ctx)
        {
            HandleDefTypeStmt(ctx.LETTERRANGE(), INT64);
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        //
        // Expr
        //
        //
        // Functions
        //
        // n, x$, y$
        // x$, y$
        // x$, n, m
        // x$, n
        //
        // Stmt
        //
        // struct
        // array
        // scalar data type
        // struct
        // scalar data type
        // scalar
        //PuffinBasicAtomTypeId.lookup(compCtx.var2.getText());
        // array
        // list
        // struct
        // scalar data type
        // set
        // dict
        // struct
        // scalar data type
        // struct
        // throw
        // GOTO postFuncDecl
        // LABEL FuncStart
        // Push child scope
        // Copy expr to result
        // Pop declaration scope
        // GOTO Caller
        // LABEL postFuncDecl
        // Patch GOTO postFuncDecl
        // GOTO postFuncDecl
        // LABEL FuncStart
        // Push child scope
        // scalar
        //PuffinBasicAtomTypeId.lookup(compCtx.var2.getText());
        // list
        // struct
        // array
        // scalar data type
        // set
        // dict
        // struct
        // scalar data type
        // struct
        // array
        // throw
        // Copy expr to result
        // GOTO LABEL gotoCaller
        // Pop declaration scope
        // LABEL gotoCaller
        // GOTO Caller
        // LABEL postFuncDecl
        // Patch GOTO LABEL gotoCaller
        // Patch GOTO postFuncDecl
        // Unset current UDF state
        // LABEL beforeWhile
        // expr()
        // NOT expr()
        // If expr is false, GOTO afterWend
        // GOTO LABEL beforeWhile
        // LABEL afterWend
        // Patch GOTO afterWend
        // stepCopy = step or 1 (default)
        // var=init
        // endCopy=end
        // GOTO LABEL CHECK
        // APPLY STEP
        // JUMP here from NEXT
        // Add step
        // CHECK
        // If (step >= 0 and var > end) or (step < 0 and var < end) GOTO after "next"
        // step >= 0
        // Patch GOTO LABEL Check
        // var > end
        // (step >= 0 and var > end)
        // step < 0
        // var < end
        // (step < 0 and var < end)
        // if (true) GOTO after NEXT
        // set linenumber on exitNext().
        // GOTO APPLY STEP
        // LABEL afterNext
        /*
         * condition
         * GOTOIF condition labelBeforeThen
         * GOTO labelAfterThen|labelBeforeElse
         * labelBeforeThen
         * ThenStmts
         * GOTO labelAfterThen|labelAfterElse
         * labelAfterThen
         * ElseStmts
         * labelAfterElse
         */
        // Patch IF true: condition
        // Patch IF true: GOTO labelBeforeThen
        // Patch IF false: GOTO labelAfterThen|labelBeforeElse
        // Patch THEN: GOTO labelAfterThen|labelAfterElse
        // IF condition is true, GOTO labelBeforeThen
        // IF condition is false, GOTO labelAfterThen|labelBeforeElse
        // Add instruction for:
        // THEN GOTO linenum | THEN linenum
        // GOTO labelAfterThen|labelAfterElse
        // Add instruction for:
        // ELSE linenum
        //
        // IF expr THEN BEGIN
        // ...
        // ELSE BEGIN
        // ...
        // END IF
        //
        //
        // expr.result
        // GOTO labelBeforeThen IF expr.result is true
        // GOTO labelAfterThen|labelBeforeElse
        // labelBeforeThen (patch GOTOIF)
        // GOTO labelAfterThen|labelAfterElse (else begin)
        // labelAfterThen
        // labelBeforeElse
        //
        // IF condition is true, GOTO labelBeforeThen
        // IF condition is false, GOTO labelAfterThen|labelBeforeElse
        // Add labelBeforeThen
        // Patch IF true: GOTO labelBeforeThen
        // GOTO labelAfterThen|labelAfterElse
        // GOTO labelAfterThen|labelAfterElse
        // Add labelAfterElse
        // Patch IF true: GOTO labelBeforeThen
        // Patch IF false: GOTO labelAfterThen|labelBeforeElse
        // Patch THEN: GOTO labelAfterThen|labelAfterElse
        // fileName, fileNumber
        // openMode, accessMode
        // lockMode, recordLen
        // fileName, fileNumber
        // openMode, accessMode
        // lockMode, recordLen
        // FileNumber, #fields
        public override void ExitDefsngstmt(PuffinBasicParser.DefsngstmtContext ctx)
        {
            HandleDefTypeStmt(ctx.LETTERRANGE(), PuffinBasicAtomTypeId.FLOAT);
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        //
        // Expr
        //
        //
        // Functions
        //
        // n, x$, y$
        // x$, y$
        // x$, n, m
        // x$, n
        //
        // Stmt
        //
        // struct
        // array
        // scalar data type
        // struct
        // scalar data type
        // scalar
        //PuffinBasicAtomTypeId.lookup(compCtx.var2.getText());
        // array
        // list
        // struct
        // scalar data type
        // set
        // dict
        // struct
        // scalar data type
        // struct
        // throw
        // GOTO postFuncDecl
        // LABEL FuncStart
        // Push child scope
        // Copy expr to result
        // Pop declaration scope
        // GOTO Caller
        // LABEL postFuncDecl
        // Patch GOTO postFuncDecl
        // GOTO postFuncDecl
        // LABEL FuncStart
        // Push child scope
        // scalar
        //PuffinBasicAtomTypeId.lookup(compCtx.var2.getText());
        // list
        // struct
        // array
        // scalar data type
        // set
        // dict
        // struct
        // scalar data type
        // struct
        // array
        // throw
        // Copy expr to result
        // GOTO LABEL gotoCaller
        // Pop declaration scope
        // LABEL gotoCaller
        // GOTO Caller
        // LABEL postFuncDecl
        // Patch GOTO LABEL gotoCaller
        // Patch GOTO postFuncDecl
        // Unset current UDF state
        // LABEL beforeWhile
        // expr()
        // NOT expr()
        // If expr is false, GOTO afterWend
        // GOTO LABEL beforeWhile
        // LABEL afterWend
        // Patch GOTO afterWend
        // stepCopy = step or 1 (default)
        // var=init
        // endCopy=end
        // GOTO LABEL CHECK
        // APPLY STEP
        // JUMP here from NEXT
        // Add step
        // CHECK
        // If (step >= 0 and var > end) or (step < 0 and var < end) GOTO after "next"
        // step >= 0
        // Patch GOTO LABEL Check
        // var > end
        // (step >= 0 and var > end)
        // step < 0
        // var < end
        // (step < 0 and var < end)
        // if (true) GOTO after NEXT
        // set linenumber on exitNext().
        // GOTO APPLY STEP
        // LABEL afterNext
        /*
         * condition
         * GOTOIF condition labelBeforeThen
         * GOTO labelAfterThen|labelBeforeElse
         * labelBeforeThen
         * ThenStmts
         * GOTO labelAfterThen|labelAfterElse
         * labelAfterThen
         * ElseStmts
         * labelAfterElse
         */
        // Patch IF true: condition
        // Patch IF true: GOTO labelBeforeThen
        // Patch IF false: GOTO labelAfterThen|labelBeforeElse
        // Patch THEN: GOTO labelAfterThen|labelAfterElse
        // IF condition is true, GOTO labelBeforeThen
        // IF condition is false, GOTO labelAfterThen|labelBeforeElse
        // Add instruction for:
        // THEN GOTO linenum | THEN linenum
        // GOTO labelAfterThen|labelAfterElse
        // Add instruction for:
        // ELSE linenum
        //
        // IF expr THEN BEGIN
        // ...
        // ELSE BEGIN
        // ...
        // END IF
        //
        //
        // expr.result
        // GOTO labelBeforeThen IF expr.result is true
        // GOTO labelAfterThen|labelBeforeElse
        // labelBeforeThen (patch GOTOIF)
        // GOTO labelAfterThen|labelAfterElse (else begin)
        // labelAfterThen
        // labelBeforeElse
        //
        // IF condition is true, GOTO labelBeforeThen
        // IF condition is false, GOTO labelAfterThen|labelBeforeElse
        // Add labelBeforeThen
        // Patch IF true: GOTO labelBeforeThen
        // GOTO labelAfterThen|labelAfterElse
        // GOTO labelAfterThen|labelAfterElse
        // Add labelAfterElse
        // Patch IF true: GOTO labelBeforeThen
        // Patch IF false: GOTO labelAfterThen|labelBeforeElse
        // Patch THEN: GOTO labelAfterThen|labelAfterElse
        // fileName, fileNumber
        // openMode, accessMode
        // lockMode, recordLen
        // fileName, fileNumber
        // openMode, accessMode
        // lockMode, recordLen
        // FileNumber, #fields
        public override void ExitDefdblstmt(PuffinBasicParser.DefdblstmtContext ctx)
        {
            HandleDefTypeStmt(ctx.LETTERRANGE(), PuffinBasicAtomTypeId.DOUBLE);
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        //
        // Expr
        //
        //
        // Functions
        //
        // n, x$, y$
        // x$, y$
        // x$, n, m
        // x$, n
        //
        // Stmt
        //
        // struct
        // array
        // scalar data type
        // struct
        // scalar data type
        // scalar
        //PuffinBasicAtomTypeId.lookup(compCtx.var2.getText());
        // array
        // list
        // struct
        // scalar data type
        // set
        // dict
        // struct
        // scalar data type
        // struct
        // throw
        // GOTO postFuncDecl
        // LABEL FuncStart
        // Push child scope
        // Copy expr to result
        // Pop declaration scope
        // GOTO Caller
        // LABEL postFuncDecl
        // Patch GOTO postFuncDecl
        // GOTO postFuncDecl
        // LABEL FuncStart
        // Push child scope
        // scalar
        //PuffinBasicAtomTypeId.lookup(compCtx.var2.getText());
        // list
        // struct
        // array
        // scalar data type
        // set
        // dict
        // struct
        // scalar data type
        // struct
        // array
        // throw
        // Copy expr to result
        // GOTO LABEL gotoCaller
        // Pop declaration scope
        // LABEL gotoCaller
        // GOTO Caller
        // LABEL postFuncDecl
        // Patch GOTO LABEL gotoCaller
        // Patch GOTO postFuncDecl
        // Unset current UDF state
        // LABEL beforeWhile
        // expr()
        // NOT expr()
        // If expr is false, GOTO afterWend
        // GOTO LABEL beforeWhile
        // LABEL afterWend
        // Patch GOTO afterWend
        // stepCopy = step or 1 (default)
        // var=init
        // endCopy=end
        // GOTO LABEL CHECK
        // APPLY STEP
        // JUMP here from NEXT
        // Add step
        // CHECK
        // If (step >= 0 and var > end) or (step < 0 and var < end) GOTO after "next"
        // step >= 0
        // Patch GOTO LABEL Check
        // var > end
        // (step >= 0 and var > end)
        // step < 0
        // var < end
        // (step < 0 and var < end)
        // if (true) GOTO after NEXT
        // set linenumber on exitNext().
        // GOTO APPLY STEP
        // LABEL afterNext
        /*
         * condition
         * GOTOIF condition labelBeforeThen
         * GOTO labelAfterThen|labelBeforeElse
         * labelBeforeThen
         * ThenStmts
         * GOTO labelAfterThen|labelAfterElse
         * labelAfterThen
         * ElseStmts
         * labelAfterElse
         */
        // Patch IF true: condition
        // Patch IF true: GOTO labelBeforeThen
        // Patch IF false: GOTO labelAfterThen|labelBeforeElse
        // Patch THEN: GOTO labelAfterThen|labelAfterElse
        // IF condition is true, GOTO labelBeforeThen
        // IF condition is false, GOTO labelAfterThen|labelBeforeElse
        // Add instruction for:
        // THEN GOTO linenum | THEN linenum
        // GOTO labelAfterThen|labelAfterElse
        // Add instruction for:
        // ELSE linenum
        //
        // IF expr THEN BEGIN
        // ...
        // ELSE BEGIN
        // ...
        // END IF
        //
        //
        // expr.result
        // GOTO labelBeforeThen IF expr.result is true
        // GOTO labelAfterThen|labelBeforeElse
        // labelBeforeThen (patch GOTOIF)
        // GOTO labelAfterThen|labelAfterElse (else begin)
        // labelAfterThen
        // labelBeforeElse
        //
        // IF condition is true, GOTO labelBeforeThen
        // IF condition is false, GOTO labelAfterThen|labelBeforeElse
        // Add labelBeforeThen
        // Patch IF true: GOTO labelBeforeThen
        // GOTO labelAfterThen|labelAfterElse
        // GOTO labelAfterThen|labelAfterElse
        // Add labelAfterElse
        // Patch IF true: GOTO labelBeforeThen
        // Patch IF false: GOTO labelAfterThen|labelBeforeElse
        // Patch THEN: GOTO labelAfterThen|labelAfterElse
        // fileName, fileNumber
        // openMode, accessMode
        // lockMode, recordLen
        // fileName, fileNumber
        // openMode, accessMode
        // lockMode, recordLen
        // FileNumber, #fields
        public override void ExitDefstrstmt(PuffinBasicParser.DefstrstmtContext ctx)
        {
            HandleDefTypeStmt(ctx.LETTERRANGE(), PuffinBasicAtomTypeId.STRING);
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        //
        // Expr
        //
        //
        // Functions
        //
        // n, x$, y$
        // x$, y$
        // x$, n, m
        // x$, n
        //
        // Stmt
        //
        // struct
        // array
        // scalar data type
        // struct
        // scalar data type
        // scalar
        //PuffinBasicAtomTypeId.lookup(compCtx.var2.getText());
        // array
        // list
        // struct
        // scalar data type
        // set
        // dict
        // struct
        // scalar data type
        // struct
        // throw
        // GOTO postFuncDecl
        // LABEL FuncStart
        // Push child scope
        // Copy expr to result
        // Pop declaration scope
        // GOTO Caller
        // LABEL postFuncDecl
        // Patch GOTO postFuncDecl
        // GOTO postFuncDecl
        // LABEL FuncStart
        // Push child scope
        // scalar
        //PuffinBasicAtomTypeId.lookup(compCtx.var2.getText());
        // list
        // struct
        // array
        // scalar data type
        // set
        // dict
        // struct
        // scalar data type
        // struct
        // array
        // throw
        // Copy expr to result
        // GOTO LABEL gotoCaller
        // Pop declaration scope
        // LABEL gotoCaller
        // GOTO Caller
        // LABEL postFuncDecl
        // Patch GOTO LABEL gotoCaller
        // Patch GOTO postFuncDecl
        // Unset current UDF state
        // LABEL beforeWhile
        // expr()
        // NOT expr()
        // If expr is false, GOTO afterWend
        // GOTO LABEL beforeWhile
        // LABEL afterWend
        // Patch GOTO afterWend
        // stepCopy = step or 1 (default)
        // var=init
        // endCopy=end
        // GOTO LABEL CHECK
        // APPLY STEP
        // JUMP here from NEXT
        // Add step
        // CHECK
        // If (step >= 0 and var > end) or (step < 0 and var < end) GOTO after "next"
        // step >= 0
        // Patch GOTO LABEL Check
        // var > end
        // (step >= 0 and var > end)
        // step < 0
        // var < end
        // (step < 0 and var < end)
        // if (true) GOTO after NEXT
        // set linenumber on exitNext().
        // GOTO APPLY STEP
        // LABEL afterNext
        /*
         * condition
         * GOTOIF condition labelBeforeThen
         * GOTO labelAfterThen|labelBeforeElse
         * labelBeforeThen
         * ThenStmts
         * GOTO labelAfterThen|labelAfterElse
         * labelAfterThen
         * ElseStmts
         * labelAfterElse
         */
        // Patch IF true: condition
        // Patch IF true: GOTO labelBeforeThen
        // Patch IF false: GOTO labelAfterThen|labelBeforeElse
        // Patch THEN: GOTO labelAfterThen|labelAfterElse
        // IF condition is true, GOTO labelBeforeThen
        // IF condition is false, GOTO labelAfterThen|labelBeforeElse
        // Add instruction for:
        // THEN GOTO linenum | THEN linenum
        // GOTO labelAfterThen|labelAfterElse
        // Add instruction for:
        // ELSE linenum
        //
        // IF expr THEN BEGIN
        // ...
        // ELSE BEGIN
        // ...
        // END IF
        //
        //
        // expr.result
        // GOTO labelBeforeThen IF expr.result is true
        // GOTO labelAfterThen|labelBeforeElse
        // labelBeforeThen (patch GOTOIF)
        // GOTO labelAfterThen|labelAfterElse (else begin)
        // labelAfterThen
        // labelBeforeElse
        //
        // IF condition is true, GOTO labelBeforeThen
        // IF condition is false, GOTO labelAfterThen|labelBeforeElse
        // Add labelBeforeThen
        // Patch IF true: GOTO labelBeforeThen
        // GOTO labelAfterThen|labelAfterElse
        // GOTO labelAfterThen|labelAfterElse
        // Add labelAfterElse
        // Patch IF true: GOTO labelBeforeThen
        // Patch IF false: GOTO labelAfterThen|labelBeforeElse
        // Patch THEN: GOTO labelAfterThen|labelAfterElse
        // fileName, fileNumber
        // openMode, accessMode
        // lockMode, recordLen
        // fileName, fileNumber
        // openMode, accessMode
        // lockMode, recordLen
        // FileNumber, #fields
        public override void ExitLsetstmt(PuffinBasicParser.LsetstmtContext ctx)
        {
            var varInstr = LookupInstruction(ctx.variable());
            var exprInstr = LookupInstruction(ctx.expr());
            var varEntry = ir.GetSymbolTable()[varInstr.result];
            AssertVariable(varEntry, GetCtxString(ctx));
            Types.AssertString(varEntry.GetType().GetAtomTypeId(), GetCtxString(ctx));
            Types.AssertString(ir.GetSymbolTable()[exprInstr.result].GetType().GetAtomTypeId(), GetCtxString(ctx));
            ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.LSET, varInstr.result, exprInstr.result, NULL_ID);
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        //
        // Expr
        //
        //
        // Functions
        //
        // n, x$, y$
        // x$, y$
        // x$, n, m
        // x$, n
        //
        // Stmt
        //
        // struct
        // array
        // scalar data type
        // struct
        // scalar data type
        // scalar
        //PuffinBasicAtomTypeId.lookup(compCtx.var2.getText());
        // array
        // list
        // struct
        // scalar data type
        // set
        // dict
        // struct
        // scalar data type
        // struct
        // throw
        // GOTO postFuncDecl
        // LABEL FuncStart
        // Push child scope
        // Copy expr to result
        // Pop declaration scope
        // GOTO Caller
        // LABEL postFuncDecl
        // Patch GOTO postFuncDecl
        // GOTO postFuncDecl
        // LABEL FuncStart
        // Push child scope
        // scalar
        //PuffinBasicAtomTypeId.lookup(compCtx.var2.getText());
        // list
        // struct
        // array
        // scalar data type
        // set
        // dict
        // struct
        // scalar data type
        // struct
        // array
        // throw
        // Copy expr to result
        // GOTO LABEL gotoCaller
        // Pop declaration scope
        // LABEL gotoCaller
        // GOTO Caller
        // LABEL postFuncDecl
        // Patch GOTO LABEL gotoCaller
        // Patch GOTO postFuncDecl
        // Unset current UDF state
        // LABEL beforeWhile
        // expr()
        // NOT expr()
        // If expr is false, GOTO afterWend
        // GOTO LABEL beforeWhile
        // LABEL afterWend
        // Patch GOTO afterWend
        // stepCopy = step or 1 (default)
        // var=init
        // endCopy=end
        // GOTO LABEL CHECK
        // APPLY STEP
        // JUMP here from NEXT
        // Add step
        // CHECK
        // If (step >= 0 and var > end) or (step < 0 and var < end) GOTO after "next"
        // step >= 0
        // Patch GOTO LABEL Check
        // var > end
        // (step >= 0 and var > end)
        // step < 0
        // var < end
        // (step < 0 and var < end)
        // if (true) GOTO after NEXT
        // set linenumber on exitNext().
        // GOTO APPLY STEP
        // LABEL afterNext
        /*
         * condition
         * GOTOIF condition labelBeforeThen
         * GOTO labelAfterThen|labelBeforeElse
         * labelBeforeThen
         * ThenStmts
         * GOTO labelAfterThen|labelAfterElse
         * labelAfterThen
         * ElseStmts
         * labelAfterElse
         */
        // Patch IF true: condition
        // Patch IF true: GOTO labelBeforeThen
        // Patch IF false: GOTO labelAfterThen|labelBeforeElse
        // Patch THEN: GOTO labelAfterThen|labelAfterElse
        // IF condition is true, GOTO labelBeforeThen
        // IF condition is false, GOTO labelAfterThen|labelBeforeElse
        // Add instruction for:
        // THEN GOTO linenum | THEN linenum
        // GOTO labelAfterThen|labelAfterElse
        // Add instruction for:
        // ELSE linenum
        //
        // IF expr THEN BEGIN
        // ...
        // ELSE BEGIN
        // ...
        // END IF
        //
        //
        // expr.result
        // GOTO labelBeforeThen IF expr.result is true
        // GOTO labelAfterThen|labelBeforeElse
        // labelBeforeThen (patch GOTOIF)
        // GOTO labelAfterThen|labelAfterElse (else begin)
        // labelAfterThen
        // labelBeforeElse
        //
        // IF condition is true, GOTO labelBeforeThen
        // IF condition is false, GOTO labelAfterThen|labelBeforeElse
        // Add labelBeforeThen
        // Patch IF true: GOTO labelBeforeThen
        // GOTO labelAfterThen|labelAfterElse
        // GOTO labelAfterThen|labelAfterElse
        // Add labelAfterElse
        // Patch IF true: GOTO labelBeforeThen
        // Patch IF false: GOTO labelAfterThen|labelBeforeElse
        // Patch THEN: GOTO labelAfterThen|labelAfterElse
        // fileName, fileNumber
        // openMode, accessMode
        // lockMode, recordLen
        // fileName, fileNumber
        // openMode, accessMode
        // lockMode, recordLen
        // FileNumber, #fields
        public override void ExitRsetstmt(PuffinBasicParser.RsetstmtContext ctx)
        {
            var varInstr = LookupInstruction(ctx.variable());
            var exprInstr = LookupInstruction(ctx.expr());
            var varEntry = ir.GetSymbolTable()[varInstr.result];
            AssertVariable(varEntry, GetCtxString(ctx));
            Types.AssertString(varEntry.GetType().GetAtomTypeId(), GetCtxString(ctx));
            Types.AssertString(ir.GetSymbolTable()[exprInstr.result].GetType().GetAtomTypeId(), GetCtxString(ctx));
            ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.RSET, varInstr.result, exprInstr.result, NULL_ID);
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        //
        // Expr
        //
        //
        // Functions
        //
        // n, x$, y$
        // x$, y$
        // x$, n, m
        // x$, n
        //
        // Stmt
        //
        // struct
        // array
        // scalar data type
        // struct
        // scalar data type
        // scalar
        //PuffinBasicAtomTypeId.lookup(compCtx.var2.getText());
        // array
        // list
        // struct
        // scalar data type
        // set
        // dict
        // struct
        // scalar data type
        // struct
        // throw
        // GOTO postFuncDecl
        // LABEL FuncStart
        // Push child scope
        // Copy expr to result
        // Pop declaration scope
        // GOTO Caller
        // LABEL postFuncDecl
        // Patch GOTO postFuncDecl
        // GOTO postFuncDecl
        // LABEL FuncStart
        // Push child scope
        // scalar
        //PuffinBasicAtomTypeId.lookup(compCtx.var2.getText());
        // list
        // struct
        // array
        // scalar data type
        // set
        // dict
        // struct
        // scalar data type
        // struct
        // array
        // throw
        // Copy expr to result
        // GOTO LABEL gotoCaller
        // Pop declaration scope
        // LABEL gotoCaller
        // GOTO Caller
        // LABEL postFuncDecl
        // Patch GOTO LABEL gotoCaller
        // Patch GOTO postFuncDecl
        // Unset current UDF state
        // LABEL beforeWhile
        // expr()
        // NOT expr()
        // If expr is false, GOTO afterWend
        // GOTO LABEL beforeWhile
        // LABEL afterWend
        // Patch GOTO afterWend
        // stepCopy = step or 1 (default)
        // var=init
        // endCopy=end
        // GOTO LABEL CHECK
        // APPLY STEP
        // JUMP here from NEXT
        // Add step
        // CHECK
        // If (step >= 0 and var > end) or (step < 0 and var < end) GOTO after "next"
        // step >= 0
        // Patch GOTO LABEL Check
        // var > end
        // (step >= 0 and var > end)
        // step < 0
        // var < end
        // (step < 0 and var < end)
        // if (true) GOTO after NEXT
        // set linenumber on exitNext().
        // GOTO APPLY STEP
        // LABEL afterNext
        /*
         * condition
         * GOTOIF condition labelBeforeThen
         * GOTO labelAfterThen|labelBeforeElse
         * labelBeforeThen
         * ThenStmts
         * GOTO labelAfterThen|labelAfterElse
         * labelAfterThen
         * ElseStmts
         * labelAfterElse
         */
        // Patch IF true: condition
        // Patch IF true: GOTO labelBeforeThen
        // Patch IF false: GOTO labelAfterThen|labelBeforeElse
        // Patch THEN: GOTO labelAfterThen|labelAfterElse
        // IF condition is true, GOTO labelBeforeThen
        // IF condition is false, GOTO labelAfterThen|labelBeforeElse
        // Add instruction for:
        // THEN GOTO linenum | THEN linenum
        // GOTO labelAfterThen|labelAfterElse
        // Add instruction for:
        // ELSE linenum
        //
        // IF expr THEN BEGIN
        // ...
        // ELSE BEGIN
        // ...
        // END IF
        //
        //
        // expr.result
        // GOTO labelBeforeThen IF expr.result is true
        // GOTO labelAfterThen|labelBeforeElse
        // labelBeforeThen (patch GOTOIF)
        // GOTO labelAfterThen|labelAfterElse (else begin)
        // labelAfterThen
        // labelBeforeElse
        //
        // IF condition is true, GOTO labelBeforeThen
        // IF condition is false, GOTO labelAfterThen|labelBeforeElse
        // Add labelBeforeThen
        // Patch IF true: GOTO labelBeforeThen
        // GOTO labelAfterThen|labelAfterElse
        // GOTO labelAfterThen|labelAfterElse
        // Add labelAfterElse
        // Patch IF true: GOTO labelBeforeThen
        // Patch IF false: GOTO labelAfterThen|labelBeforeElse
        // Patch THEN: GOTO labelAfterThen|labelAfterElse
        // fileName, fileNumber
        // openMode, accessMode
        // lockMode, recordLen
        // fileName, fileNumber
        // openMode, accessMode
        // lockMode, recordLen
        // FileNumber, #fields
        public override void ExitInputstmt(PuffinBasicParser.InputstmtContext ctx)
        {
            foreach (var varCtx in ctx.variable())
            {
                var varInstr = LookupInstruction(varCtx);
                AssertVariable(ir.GetSymbolTable()[varInstr.result], GetCtxString(ctx));
                ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.PARAM1, varInstr.result, NULL_ID, NULL_ID);
            }

            int promptId;
            if (ctx.expr() != null)
            {
                promptId = LookupInstruction(ctx.expr()).result;
                Types.AssertString(ir.GetSymbolTable()[promptId].GetType().GetAtomTypeId(), GetCtxString(ctx));
            }
            else
            {
                promptId = ir.GetSymbolTable().AddTmp(PuffinBasicAtomTypeId.STRING, (e) => e.GetValue().SetString("?"));
            }

            ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.INPUT, promptId, NULL_ID, NULL_ID);
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        //
        // Expr
        //
        //
        // Functions
        //
        // n, x$, y$
        // x$, y$
        // x$, n, m
        // x$, n
        //
        // Stmt
        //
        // struct
        // array
        // scalar data type
        // struct
        // scalar data type
        // scalar
        //PuffinBasicAtomTypeId.lookup(compCtx.var2.getText());
        // array
        // list
        // struct
        // scalar data type
        // set
        // dict
        // struct
        // scalar data type
        // struct
        // throw
        // GOTO postFuncDecl
        // LABEL FuncStart
        // Push child scope
        // Copy expr to result
        // Pop declaration scope
        // GOTO Caller
        // LABEL postFuncDecl
        // Patch GOTO postFuncDecl
        // GOTO postFuncDecl
        // LABEL FuncStart
        // Push child scope
        // scalar
        //PuffinBasicAtomTypeId.lookup(compCtx.var2.getText());
        // list
        // struct
        // array
        // scalar data type
        // set
        // dict
        // struct
        // scalar data type
        // struct
        // array
        // throw
        // Copy expr to result
        // GOTO LABEL gotoCaller
        // Pop declaration scope
        // LABEL gotoCaller
        // GOTO Caller
        // LABEL postFuncDecl
        // Patch GOTO LABEL gotoCaller
        // Patch GOTO postFuncDecl
        // Unset current UDF state
        // LABEL beforeWhile
        // expr()
        // NOT expr()
        // If expr is false, GOTO afterWend
        // GOTO LABEL beforeWhile
        // LABEL afterWend
        // Patch GOTO afterWend
        // stepCopy = step or 1 (default)
        // var=init
        // endCopy=end
        // GOTO LABEL CHECK
        // APPLY STEP
        // JUMP here from NEXT
        // Add step
        // CHECK
        // If (step >= 0 and var > end) or (step < 0 and var < end) GOTO after "next"
        // step >= 0
        // Patch GOTO LABEL Check
        // var > end
        // (step >= 0 and var > end)
        // step < 0
        // var < end
        // (step < 0 and var < end)
        // if (true) GOTO after NEXT
        // set linenumber on exitNext().
        // GOTO APPLY STEP
        // LABEL afterNext
        /*
         * condition
         * GOTOIF condition labelBeforeThen
         * GOTO labelAfterThen|labelBeforeElse
         * labelBeforeThen
         * ThenStmts
         * GOTO labelAfterThen|labelAfterElse
         * labelAfterThen
         * ElseStmts
         * labelAfterElse
         */
        // Patch IF true: condition
        // Patch IF true: GOTO labelBeforeThen
        // Patch IF false: GOTO labelAfterThen|labelBeforeElse
        // Patch THEN: GOTO labelAfterThen|labelAfterElse
        // IF condition is true, GOTO labelBeforeThen
        // IF condition is false, GOTO labelAfterThen|labelBeforeElse
        // Add instruction for:
        // THEN GOTO linenum | THEN linenum
        // GOTO labelAfterThen|labelAfterElse
        // Add instruction for:
        // ELSE linenum
        //
        // IF expr THEN BEGIN
        // ...
        // ELSE BEGIN
        // ...
        // END IF
        //
        //
        // expr.result
        // GOTO labelBeforeThen IF expr.result is true
        // GOTO labelAfterThen|labelBeforeElse
        // labelBeforeThen (patch GOTOIF)
        // GOTO labelAfterThen|labelAfterElse (else begin)
        // labelAfterThen
        // labelBeforeElse
        //
        // IF condition is true, GOTO labelBeforeThen
        // IF condition is false, GOTO labelAfterThen|labelBeforeElse
        // Add labelBeforeThen
        // Patch IF true: GOTO labelBeforeThen
        // GOTO labelAfterThen|labelAfterElse
        // GOTO labelAfterThen|labelAfterElse
        // Add labelAfterElse
        // Patch IF true: GOTO labelBeforeThen
        // Patch IF false: GOTO labelAfterThen|labelBeforeElse
        // Patch THEN: GOTO labelAfterThen|labelAfterElse
        // fileName, fileNumber
        // openMode, accessMode
        // lockMode, recordLen
        // fileName, fileNumber
        // openMode, accessMode
        // lockMode, recordLen
        // FileNumber, #fields
        public override void ExitInputhashstmt(PuffinBasicParser.InputhashstmtContext ctx)
        {
            foreach (var varCtx in ctx.variable())
            {
                var varInstr = LookupInstruction(varCtx);
                AssertVariable(ir.GetSymbolTable()[varInstr.result], GetCtxString(ctx));
                ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.PARAM1, varInstr.result, NULL_ID, NULL_ID);
            }

            var fileNumInstr = LookupInstruction(ctx.filenum);
            Types.AssertNumeric(ir.GetSymbolTable()[fileNumInstr.result].GetType().GetAtomTypeId(), GetCtxString(ctx));
            ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.INPUT, NULL_ID, fileNumInstr.result, NULL_ID);
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        //
        // Expr
        //
        //
        // Functions
        //
        // n, x$, y$
        // x$, y$
        // x$, n, m
        // x$, n
        //
        // Stmt
        //
        // struct
        // array
        // scalar data type
        // struct
        // scalar data type
        // scalar
        //PuffinBasicAtomTypeId.lookup(compCtx.var2.getText());
        // array
        // list
        // struct
        // scalar data type
        // set
        // dict
        // struct
        // scalar data type
        // struct
        // throw
        // GOTO postFuncDecl
        // LABEL FuncStart
        // Push child scope
        // Copy expr to result
        // Pop declaration scope
        // GOTO Caller
        // LABEL postFuncDecl
        // Patch GOTO postFuncDecl
        // GOTO postFuncDecl
        // LABEL FuncStart
        // Push child scope
        // scalar
        //PuffinBasicAtomTypeId.lookup(compCtx.var2.getText());
        // list
        // struct
        // array
        // scalar data type
        // set
        // dict
        // struct
        // scalar data type
        // struct
        // array
        // throw
        // Copy expr to result
        // GOTO LABEL gotoCaller
        // Pop declaration scope
        // LABEL gotoCaller
        // GOTO Caller
        // LABEL postFuncDecl
        // Patch GOTO LABEL gotoCaller
        // Patch GOTO postFuncDecl
        // Unset current UDF state
        // LABEL beforeWhile
        // expr()
        // NOT expr()
        // If expr is false, GOTO afterWend
        // GOTO LABEL beforeWhile
        // LABEL afterWend
        // Patch GOTO afterWend
        // stepCopy = step or 1 (default)
        // var=init
        // endCopy=end
        // GOTO LABEL CHECK
        // APPLY STEP
        // JUMP here from NEXT
        // Add step
        // CHECK
        // If (step >= 0 and var > end) or (step < 0 and var < end) GOTO after "next"
        // step >= 0
        // Patch GOTO LABEL Check
        // var > end
        // (step >= 0 and var > end)
        // step < 0
        // var < end
        // (step < 0 and var < end)
        // if (true) GOTO after NEXT
        // set linenumber on exitNext().
        // GOTO APPLY STEP
        // LABEL afterNext
        /*
         * condition
         * GOTOIF condition labelBeforeThen
         * GOTO labelAfterThen|labelBeforeElse
         * labelBeforeThen
         * ThenStmts
         * GOTO labelAfterThen|labelAfterElse
         * labelAfterThen
         * ElseStmts
         * labelAfterElse
         */
        // Patch IF true: condition
        // Patch IF true: GOTO labelBeforeThen
        // Patch IF false: GOTO labelAfterThen|labelBeforeElse
        // Patch THEN: GOTO labelAfterThen|labelAfterElse
        // IF condition is true, GOTO labelBeforeThen
        // IF condition is false, GOTO labelAfterThen|labelBeforeElse
        // Add instruction for:
        // THEN GOTO linenum | THEN linenum
        // GOTO labelAfterThen|labelAfterElse
        // Add instruction for:
        // ELSE linenum
        //
        // IF expr THEN BEGIN
        // ...
        // ELSE BEGIN
        // ...
        // END IF
        //
        //
        // expr.result
        // GOTO labelBeforeThen IF expr.result is true
        // GOTO labelAfterThen|labelBeforeElse
        // labelBeforeThen (patch GOTOIF)
        // GOTO labelAfterThen|labelAfterElse (else begin)
        // labelAfterThen
        // labelBeforeElse
        //
        // IF condition is true, GOTO labelBeforeThen
        // IF condition is false, GOTO labelAfterThen|labelBeforeElse
        // Add labelBeforeThen
        // Patch IF true: GOTO labelBeforeThen
        // GOTO labelAfterThen|labelAfterElse
        // GOTO labelAfterThen|labelAfterElse
        // Add labelAfterElse
        // Patch IF true: GOTO labelBeforeThen
        // Patch IF false: GOTO labelAfterThen|labelBeforeElse
        // Patch THEN: GOTO labelAfterThen|labelAfterElse
        // fileName, fileNumber
        // openMode, accessMode
        // lockMode, recordLen
        // fileName, fileNumber
        // openMode, accessMode
        // lockMode, recordLen
        // FileNumber, #fields
        public override void ExitLineinputstmt(PuffinBasicParser.LineinputstmtContext ctx)
        {
            var varInstr = LookupInstruction(ctx.variable());
            AssertVariable(ir.GetSymbolTable()[varInstr.result], GetCtxString(ctx));
            ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.PARAM1, varInstr.result, NULL_ID, NULL_ID);
            int promptId;
            if (ctx.expr() != null)
            {
                promptId = LookupInstruction(ctx.expr()).result;
                Types.AssertString(ir.GetSymbolTable()[promptId].GetType().GetAtomTypeId(), GetCtxString(ctx));
            }
            else
            {
                promptId = ir.GetSymbolTable().AddTmp(PuffinBasicAtomTypeId.STRING, (e) => e.GetValue().SetString(""));
            }

            ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.LINE_INPUT, promptId, NULL_ID, NULL_ID);
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        //
        // Expr
        //
        //
        // Functions
        //
        // n, x$, y$
        // x$, y$
        // x$, n, m
        // x$, n
        //
        // Stmt
        //
        // struct
        // array
        // scalar data type
        // struct
        // scalar data type
        // scalar
        //PuffinBasicAtomTypeId.lookup(compCtx.var2.getText());
        // array
        // list
        // struct
        // scalar data type
        // set
        // dict
        // struct
        // scalar data type
        // struct
        // throw
        // GOTO postFuncDecl
        // LABEL FuncStart
        // Push child scope
        // Copy expr to result
        // Pop declaration scope
        // GOTO Caller
        // LABEL postFuncDecl
        // Patch GOTO postFuncDecl
        // GOTO postFuncDecl
        // LABEL FuncStart
        // Push child scope
        // scalar
        //PuffinBasicAtomTypeId.lookup(compCtx.var2.getText());
        // list
        // struct
        // array
        // scalar data type
        // set
        // dict
        // struct
        // scalar data type
        // struct
        // array
        // throw
        // Copy expr to result
        // GOTO LABEL gotoCaller
        // Pop declaration scope
        // LABEL gotoCaller
        // GOTO Caller
        // LABEL postFuncDecl
        // Patch GOTO LABEL gotoCaller
        // Patch GOTO postFuncDecl
        // Unset current UDF state
        // LABEL beforeWhile
        // expr()
        // NOT expr()
        // If expr is false, GOTO afterWend
        // GOTO LABEL beforeWhile
        // LABEL afterWend
        // Patch GOTO afterWend
        // stepCopy = step or 1 (default)
        // var=init
        // endCopy=end
        // GOTO LABEL CHECK
        // APPLY STEP
        // JUMP here from NEXT
        // Add step
        // CHECK
        // If (step >= 0 and var > end) or (step < 0 and var < end) GOTO after "next"
        // step >= 0
        // Patch GOTO LABEL Check
        // var > end
        // (step >= 0 and var > end)
        // step < 0
        // var < end
        // (step < 0 and var < end)
        // if (true) GOTO after NEXT
        // set linenumber on exitNext().
        // GOTO APPLY STEP
        // LABEL afterNext
        /*
         * condition
         * GOTOIF condition labelBeforeThen
         * GOTO labelAfterThen|labelBeforeElse
         * labelBeforeThen
         * ThenStmts
         * GOTO labelAfterThen|labelAfterElse
         * labelAfterThen
         * ElseStmts
         * labelAfterElse
         */
        // Patch IF true: condition
        // Patch IF true: GOTO labelBeforeThen
        // Patch IF false: GOTO labelAfterThen|labelBeforeElse
        // Patch THEN: GOTO labelAfterThen|labelAfterElse
        // IF condition is true, GOTO labelBeforeThen
        // IF condition is false, GOTO labelAfterThen|labelBeforeElse
        // Add instruction for:
        // THEN GOTO linenum | THEN linenum
        // GOTO labelAfterThen|labelAfterElse
        // Add instruction for:
        // ELSE linenum
        //
        // IF expr THEN BEGIN
        // ...
        // ELSE BEGIN
        // ...
        // END IF
        //
        //
        // expr.result
        // GOTO labelBeforeThen IF expr.result is true
        // GOTO labelAfterThen|labelBeforeElse
        // labelBeforeThen (patch GOTOIF)
        // GOTO labelAfterThen|labelAfterElse (else begin)
        // labelAfterThen
        // labelBeforeElse
        //
        // IF condition is true, GOTO labelBeforeThen
        // IF condition is false, GOTO labelAfterThen|labelBeforeElse
        // Add labelBeforeThen
        // Patch IF true: GOTO labelBeforeThen
        // GOTO labelAfterThen|labelAfterElse
        // GOTO labelAfterThen|labelAfterElse
        // Add labelAfterElse
        // Patch IF true: GOTO labelBeforeThen
        // Patch IF false: GOTO labelAfterThen|labelBeforeElse
        // Patch THEN: GOTO labelAfterThen|labelAfterElse
        // fileName, fileNumber
        // openMode, accessMode
        // lockMode, recordLen
        // fileName, fileNumber
        // openMode, accessMode
        // lockMode, recordLen
        // FileNumber, #fields
        public override void ExitLineinputhashstmt(PuffinBasicParser.LineinputhashstmtContext ctx)
        {
            var varInstr = LookupInstruction(ctx.variable());
            AssertVariable(ir.GetSymbolTable()[varInstr.result], GetCtxString(ctx));
            ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.PARAM1, varInstr.result, NULL_ID, NULL_ID);
            var fileNumInstr = LookupInstruction(ctx.filenum);
            Types.AssertNumeric(ir.GetSymbolTable()[fileNumInstr.result].GetType().GetAtomTypeId(), GetCtxString(ctx));
            ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.LINE_INPUT, NULL_ID, fileNumInstr.result, NULL_ID);
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        //
        // Expr
        //
        //
        // Functions
        //
        // n, x$, y$
        // x$, y$
        // x$, n, m
        // x$, n
        //
        // Stmt
        //
        // struct
        // array
        // scalar data type
        // struct
        // scalar data type
        // scalar
        //PuffinBasicAtomTypeId.lookup(compCtx.var2.getText());
        // array
        // list
        // struct
        // scalar data type
        // set
        // dict
        // struct
        // scalar data type
        // struct
        // throw
        // GOTO postFuncDecl
        // LABEL FuncStart
        // Push child scope
        // Copy expr to result
        // Pop declaration scope
        // GOTO Caller
        // LABEL postFuncDecl
        // Patch GOTO postFuncDecl
        // GOTO postFuncDecl
        // LABEL FuncStart
        // Push child scope
        // scalar
        //PuffinBasicAtomTypeId.lookup(compCtx.var2.getText());
        // list
        // struct
        // array
        // scalar data type
        // set
        // dict
        // struct
        // scalar data type
        // struct
        // array
        // throw
        // Copy expr to result
        // GOTO LABEL gotoCaller
        // Pop declaration scope
        // LABEL gotoCaller
        // GOTO Caller
        // LABEL postFuncDecl
        // Patch GOTO LABEL gotoCaller
        // Patch GOTO postFuncDecl
        // Unset current UDF state
        // LABEL beforeWhile
        // expr()
        // NOT expr()
        // If expr is false, GOTO afterWend
        // GOTO LABEL beforeWhile
        // LABEL afterWend
        // Patch GOTO afterWend
        // stepCopy = step or 1 (default)
        // var=init
        // endCopy=end
        // GOTO LABEL CHECK
        // APPLY STEP
        // JUMP here from NEXT
        // Add step
        // CHECK
        // If (step >= 0 and var > end) or (step < 0 and var < end) GOTO after "next"
        // step >= 0
        // Patch GOTO LABEL Check
        // var > end
        // (step >= 0 and var > end)
        // step < 0
        // var < end
        // (step < 0 and var < end)
        // if (true) GOTO after NEXT
        // set linenumber on exitNext().
        // GOTO APPLY STEP
        // LABEL afterNext
        /*
         * condition
         * GOTOIF condition labelBeforeThen
         * GOTO labelAfterThen|labelBeforeElse
         * labelBeforeThen
         * ThenStmts
         * GOTO labelAfterThen|labelAfterElse
         * labelAfterThen
         * ElseStmts
         * labelAfterElse
         */
        // Patch IF true: condition
        // Patch IF true: GOTO labelBeforeThen
        // Patch IF false: GOTO labelAfterThen|labelBeforeElse
        // Patch THEN: GOTO labelAfterThen|labelAfterElse
        // IF condition is true, GOTO labelBeforeThen
        // IF condition is false, GOTO labelAfterThen|labelBeforeElse
        // Add instruction for:
        // THEN GOTO linenum | THEN linenum
        // GOTO labelAfterThen|labelAfterElse
        // Add instruction for:
        // ELSE linenum
        //
        // IF expr THEN BEGIN
        // ...
        // ELSE BEGIN
        // ...
        // END IF
        //
        //
        // expr.result
        // GOTO labelBeforeThen IF expr.result is true
        // GOTO labelAfterThen|labelBeforeElse
        // labelBeforeThen (patch GOTOIF)
        // GOTO labelAfterThen|labelAfterElse (else begin)
        // labelAfterThen
        // labelBeforeElse
        //
        // IF condition is true, GOTO labelBeforeThen
        // IF condition is false, GOTO labelAfterThen|labelBeforeElse
        // Add labelBeforeThen
        // Patch IF true: GOTO labelBeforeThen
        // GOTO labelAfterThen|labelAfterElse
        // GOTO labelAfterThen|labelAfterElse
        // Add labelAfterElse
        // Patch IF true: GOTO labelBeforeThen
        // Patch IF false: GOTO labelAfterThen|labelBeforeElse
        // Patch THEN: GOTO labelAfterThen|labelAfterElse
        // fileName, fileNumber
        // openMode, accessMode
        // lockMode, recordLen
        // fileName, fileNumber
        // openMode, accessMode
        // lockMode, recordLen
        // FileNumber, #fields
        public override void ExitWritestmt(PuffinBasicParser.WritestmtContext ctx)
        {
            HandleWritestmt(ctx, ctx.expr(), null);
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        //
        // Expr
        //
        //
        // Functions
        //
        // n, x$, y$
        // x$, y$
        // x$, n, m
        // x$, n
        //
        // Stmt
        //
        // struct
        // array
        // scalar data type
        // struct
        // scalar data type
        // scalar
        //PuffinBasicAtomTypeId.lookup(compCtx.var2.getText());
        // array
        // list
        // struct
        // scalar data type
        // set
        // dict
        // struct
        // scalar data type
        // struct
        // throw
        // GOTO postFuncDecl
        // LABEL FuncStart
        // Push child scope
        // Copy expr to result
        // Pop declaration scope
        // GOTO Caller
        // LABEL postFuncDecl
        // Patch GOTO postFuncDecl
        // GOTO postFuncDecl
        // LABEL FuncStart
        // Push child scope
        // scalar
        //PuffinBasicAtomTypeId.lookup(compCtx.var2.getText());
        // list
        // struct
        // array
        // scalar data type
        // set
        // dict
        // struct
        // scalar data type
        // struct
        // array
        // throw
        // Copy expr to result
        // GOTO LABEL gotoCaller
        // Pop declaration scope
        // LABEL gotoCaller
        // GOTO Caller
        // LABEL postFuncDecl
        // Patch GOTO LABEL gotoCaller
        // Patch GOTO postFuncDecl
        // Unset current UDF state
        // LABEL beforeWhile
        // expr()
        // NOT expr()
        // If expr is false, GOTO afterWend
        // GOTO LABEL beforeWhile
        // LABEL afterWend
        // Patch GOTO afterWend
        // stepCopy = step or 1 (default)
        // var=init
        // endCopy=end
        // GOTO LABEL CHECK
        // APPLY STEP
        // JUMP here from NEXT
        // Add step
        // CHECK
        // If (step >= 0 and var > end) or (step < 0 and var < end) GOTO after "next"
        // step >= 0
        // Patch GOTO LABEL Check
        // var > end
        // (step >= 0 and var > end)
        // step < 0
        // var < end
        // (step < 0 and var < end)
        // if (true) GOTO after NEXT
        // set linenumber on exitNext().
        // GOTO APPLY STEP
        // LABEL afterNext
        /*
         * condition
         * GOTOIF condition labelBeforeThen
         * GOTO labelAfterThen|labelBeforeElse
         * labelBeforeThen
         * ThenStmts
         * GOTO labelAfterThen|labelAfterElse
         * labelAfterThen
         * ElseStmts
         * labelAfterElse
         */
        // Patch IF true: condition
        // Patch IF true: GOTO labelBeforeThen
        // Patch IF false: GOTO labelAfterThen|labelBeforeElse
        // Patch THEN: GOTO labelAfterThen|labelAfterElse
        // IF condition is true, GOTO labelBeforeThen
        // IF condition is false, GOTO labelAfterThen|labelBeforeElse
        // Add instruction for:
        // THEN GOTO linenum | THEN linenum
        // GOTO labelAfterThen|labelAfterElse
        // Add instruction for:
        // ELSE linenum
        //
        // IF expr THEN BEGIN
        // ...
        // ELSE BEGIN
        // ...
        // END IF
        //
        //
        // expr.result
        // GOTO labelBeforeThen IF expr.result is true
        // GOTO labelAfterThen|labelBeforeElse
        // labelBeforeThen (patch GOTOIF)
        // GOTO labelAfterThen|labelAfterElse (else begin)
        // labelAfterThen
        // labelBeforeElse
        //
        // IF condition is true, GOTO labelBeforeThen
        // IF condition is false, GOTO labelAfterThen|labelBeforeElse
        // Add labelBeforeThen
        // Patch IF true: GOTO labelBeforeThen
        // GOTO labelAfterThen|labelAfterElse
        // GOTO labelAfterThen|labelAfterElse
        // Add labelAfterElse
        // Patch IF true: GOTO labelBeforeThen
        // Patch IF false: GOTO labelAfterThen|labelBeforeElse
        // Patch THEN: GOTO labelAfterThen|labelAfterElse
        // fileName, fileNumber
        // openMode, accessMode
        // lockMode, recordLen
        // fileName, fileNumber
        // openMode, accessMode
        // lockMode, recordLen
        // FileNumber, #fields
        public override void ExitWritehashstmt(PuffinBasicParser.WritehashstmtContext ctx)
        {
            var fileNumInstr = LookupInstruction(ctx.filenum);
            HandleWritestmt(ctx, ctx.expr(), fileNumInstr);
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        //
        // Expr
        //
        //
        // Functions
        //
        // n, x$, y$
        // x$, y$
        // x$, n, m
        // x$, n
        //
        // Stmt
        //
        // struct
        // array
        // scalar data type
        // struct
        // scalar data type
        // scalar
        //PuffinBasicAtomTypeId.lookup(compCtx.var2.getText());
        // array
        // list
        // struct
        // scalar data type
        // set
        // dict
        // struct
        // scalar data type
        // struct
        // throw
        // GOTO postFuncDecl
        // LABEL FuncStart
        // Push child scope
        // Copy expr to result
        // Pop declaration scope
        // GOTO Caller
        // LABEL postFuncDecl
        // Patch GOTO postFuncDecl
        // GOTO postFuncDecl
        // LABEL FuncStart
        // Push child scope
        // scalar
        //PuffinBasicAtomTypeId.lookup(compCtx.var2.getText());
        // list
        // struct
        // array
        // scalar data type
        // set
        // dict
        // struct
        // scalar data type
        // struct
        // array
        // throw
        // Copy expr to result
        // GOTO LABEL gotoCaller
        // Pop declaration scope
        // LABEL gotoCaller
        // GOTO Caller
        // LABEL postFuncDecl
        // Patch GOTO LABEL gotoCaller
        // Patch GOTO postFuncDecl
        // Unset current UDF state
        // LABEL beforeWhile
        // expr()
        // NOT expr()
        // If expr is false, GOTO afterWend
        // GOTO LABEL beforeWhile
        // LABEL afterWend
        // Patch GOTO afterWend
        // stepCopy = step or 1 (default)
        // var=init
        // endCopy=end
        // GOTO LABEL CHECK
        // APPLY STEP
        // JUMP here from NEXT
        // Add step
        // CHECK
        // If (step >= 0 and var > end) or (step < 0 and var < end) GOTO after "next"
        // step >= 0
        // Patch GOTO LABEL Check
        // var > end
        // (step >= 0 and var > end)
        // step < 0
        // var < end
        // (step < 0 and var < end)
        // if (true) GOTO after NEXT
        // set linenumber on exitNext().
        // GOTO APPLY STEP
        // LABEL afterNext
        /*
         * condition
         * GOTOIF condition labelBeforeThen
         * GOTO labelAfterThen|labelBeforeElse
         * labelBeforeThen
         * ThenStmts
         * GOTO labelAfterThen|labelAfterElse
         * labelAfterThen
         * ElseStmts
         * labelAfterElse
         */
        // Patch IF true: condition
        // Patch IF true: GOTO labelBeforeThen
        // Patch IF false: GOTO labelAfterThen|labelBeforeElse
        // Patch THEN: GOTO labelAfterThen|labelAfterElse
        // IF condition is true, GOTO labelBeforeThen
        // IF condition is false, GOTO labelAfterThen|labelBeforeElse
        // Add instruction for:
        // THEN GOTO linenum | THEN linenum
        // GOTO labelAfterThen|labelAfterElse
        // Add instruction for:
        // ELSE linenum
        //
        // IF expr THEN BEGIN
        // ...
        // ELSE BEGIN
        // ...
        // END IF
        //
        //
        // expr.result
        // GOTO labelBeforeThen IF expr.result is true
        // GOTO labelAfterThen|labelBeforeElse
        // labelBeforeThen (patch GOTOIF)
        // GOTO labelAfterThen|labelAfterElse (else begin)
        // labelAfterThen
        // labelBeforeElse
        //
        // IF condition is true, GOTO labelBeforeThen
        // IF condition is false, GOTO labelAfterThen|labelBeforeElse
        // Add labelBeforeThen
        // Patch IF true: GOTO labelBeforeThen
        // GOTO labelAfterThen|labelAfterElse
        // GOTO labelAfterThen|labelAfterElse
        // Add labelAfterElse
        // Patch IF true: GOTO labelBeforeThen
        // Patch IF false: GOTO labelAfterThen|labelBeforeElse
        // Patch THEN: GOTO labelAfterThen|labelAfterElse
        // fileName, fileNumber
        // openMode, accessMode
        // lockMode, recordLen
        // fileName, fileNumber
        // openMode, accessMode
        // lockMode, recordLen
        // FileNumber, #fields
        public virtual void HandleWritestmt(ParserRuleContext ctx, IList<PuffinBasicParser.ExprContext> exprs, Instruction fileNumber)
        {

            // if fileNumber != null, skip first instruction
            for (int i = fileNumber == null ? 0 : 1; i < exprs.Count; i++)
            {
                var exprCtx = exprs[i];
                var exprInstr = LookupInstruction(exprCtx);
                ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.WRITE, exprInstr.result, NULL_ID, NULL_ID);
                if (i + 1 < exprs.Count)
                {
                    var commaId = ir.GetSymbolTable().AddTmp(PuffinBasicAtomTypeId.STRING, (entry) => entry.GetValue().SetString(","));
                    ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.PRINT, commaId, NULL_ID, NULL_ID);
                }
            }

            var newlineId = ir.GetSymbolTable().AddTmp(PuffinBasicAtomTypeId.STRING, (entry) => entry.GetValue().SetString(Environment.NewLine));
            ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.PRINT, newlineId, NULL_ID, NULL_ID);
            int fileNumberId;
            if (fileNumber != null)
            {
                Types.AssertNumeric(ir.GetSymbolTable()[fileNumber.result].GetType().GetAtomTypeId(), GetCtxString(ctx));
                fileNumberId = fileNumber.result;
            }
            else
            {
                fileNumberId = NULL_ID;
            }

            ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.FLUSH, fileNumberId, NULL_ID, NULL_ID);
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        //
        // Expr
        //
        //
        // Functions
        //
        // n, x$, y$
        // x$, y$
        // x$, n, m
        // x$, n
        //
        // Stmt
        //
        // struct
        // array
        // scalar data type
        // struct
        // scalar data type
        // scalar
        //PuffinBasicAtomTypeId.lookup(compCtx.var2.getText());
        // array
        // list
        // struct
        // scalar data type
        // set
        // dict
        // struct
        // scalar data type
        // struct
        // throw
        // GOTO postFuncDecl
        // LABEL FuncStart
        // Push child scope
        // Copy expr to result
        // Pop declaration scope
        // GOTO Caller
        // LABEL postFuncDecl
        // Patch GOTO postFuncDecl
        // GOTO postFuncDecl
        // LABEL FuncStart
        // Push child scope
        // scalar
        //PuffinBasicAtomTypeId.lookup(compCtx.var2.getText());
        // list
        // struct
        // array
        // scalar data type
        // set
        // dict
        // struct
        // scalar data type
        // struct
        // array
        // throw
        // Copy expr to result
        // GOTO LABEL gotoCaller
        // Pop declaration scope
        // LABEL gotoCaller
        // GOTO Caller
        // LABEL postFuncDecl
        // Patch GOTO LABEL gotoCaller
        // Patch GOTO postFuncDecl
        // Unset current UDF state
        // LABEL beforeWhile
        // expr()
        // NOT expr()
        // If expr is false, GOTO afterWend
        // GOTO LABEL beforeWhile
        // LABEL afterWend
        // Patch GOTO afterWend
        // stepCopy = step or 1 (default)
        // var=init
        // endCopy=end
        // GOTO LABEL CHECK
        // APPLY STEP
        // JUMP here from NEXT
        // Add step
        // CHECK
        // If (step >= 0 and var > end) or (step < 0 and var < end) GOTO after "next"
        // step >= 0
        // Patch GOTO LABEL Check
        // var > end
        // (step >= 0 and var > end)
        // step < 0
        // var < end
        // (step < 0 and var < end)
        // if (true) GOTO after NEXT
        // set linenumber on exitNext().
        // GOTO APPLY STEP
        // LABEL afterNext
        /*
         * condition
         * GOTOIF condition labelBeforeThen
         * GOTO labelAfterThen|labelBeforeElse
         * labelBeforeThen
         * ThenStmts
         * GOTO labelAfterThen|labelAfterElse
         * labelAfterThen
         * ElseStmts
         * labelAfterElse
         */
        // Patch IF true: condition
        // Patch IF true: GOTO labelBeforeThen
        // Patch IF false: GOTO labelAfterThen|labelBeforeElse
        // Patch THEN: GOTO labelAfterThen|labelAfterElse
        // IF condition is true, GOTO labelBeforeThen
        // IF condition is false, GOTO labelAfterThen|labelBeforeElse
        // Add instruction for:
        // THEN GOTO linenum | THEN linenum
        // GOTO labelAfterThen|labelAfterElse
        // Add instruction for:
        // ELSE linenum
        //
        // IF expr THEN BEGIN
        // ...
        // ELSE BEGIN
        // ...
        // END IF
        //
        //
        // expr.result
        // GOTO labelBeforeThen IF expr.result is true
        // GOTO labelAfterThen|labelBeforeElse
        // labelBeforeThen (patch GOTOIF)
        // GOTO labelAfterThen|labelAfterElse (else begin)
        // labelAfterThen
        // labelBeforeElse
        //
        // IF condition is true, GOTO labelBeforeThen
        // IF condition is false, GOTO labelAfterThen|labelBeforeElse
        // Add labelBeforeThen
        // Patch IF true: GOTO labelBeforeThen
        // GOTO labelAfterThen|labelAfterElse
        // GOTO labelAfterThen|labelAfterElse
        // Add labelAfterElse
        // Patch IF true: GOTO labelBeforeThen
        // Patch IF false: GOTO labelAfterThen|labelBeforeElse
        // Patch THEN: GOTO labelAfterThen|labelAfterElse
        // fileName, fileNumber
        // openMode, accessMode
        // lockMode, recordLen
        // fileName, fileNumber
        // openMode, accessMode
        // lockMode, recordLen
        // FileNumber, #fields
        // if fileNumber != null, skip first instruction
        public override void ExitReadstmt(PuffinBasicParser.ReadstmtContext ctx)
        {
            foreach (var varCtx in ctx.variable())
            {
                var varInstr = LookupInstruction(varCtx);
                AssertVariable(ir.GetSymbolTable()[varInstr.result], GetCtxString(ctx));
                ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.READ, varInstr.result, NULL_ID, NULL_ID);
            }
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        //
        // Expr
        //
        //
        // Functions
        //
        // n, x$, y$
        // x$, y$
        // x$, n, m
        // x$, n
        //
        // Stmt
        //
        // struct
        // array
        // scalar data type
        // struct
        // scalar data type
        // scalar
        //PuffinBasicAtomTypeId.lookup(compCtx.var2.getText());
        // array
        // list
        // struct
        // scalar data type
        // set
        // dict
        // struct
        // scalar data type
        // struct
        // throw
        // GOTO postFuncDecl
        // LABEL FuncStart
        // Push child scope
        // Copy expr to result
        // Pop declaration scope
        // GOTO Caller
        // LABEL postFuncDecl
        // Patch GOTO postFuncDecl
        // GOTO postFuncDecl
        // LABEL FuncStart
        // Push child scope
        // scalar
        //PuffinBasicAtomTypeId.lookup(compCtx.var2.getText());
        // list
        // struct
        // array
        // scalar data type
        // set
        // dict
        // struct
        // scalar data type
        // struct
        // array
        // throw
        // Copy expr to result
        // GOTO LABEL gotoCaller
        // Pop declaration scope
        // LABEL gotoCaller
        // GOTO Caller
        // LABEL postFuncDecl
        // Patch GOTO LABEL gotoCaller
        // Patch GOTO postFuncDecl
        // Unset current UDF state
        // LABEL beforeWhile
        // expr()
        // NOT expr()
        // If expr is false, GOTO afterWend
        // GOTO LABEL beforeWhile
        // LABEL afterWend
        // Patch GOTO afterWend
        // stepCopy = step or 1 (default)
        // var=init
        // endCopy=end
        // GOTO LABEL CHECK
        // APPLY STEP
        // JUMP here from NEXT
        // Add step
        // CHECK
        // If (step >= 0 and var > end) or (step < 0 and var < end) GOTO after "next"
        // step >= 0
        // Patch GOTO LABEL Check
        // var > end
        // (step >= 0 and var > end)
        // step < 0
        // var < end
        // (step < 0 and var < end)
        // if (true) GOTO after NEXT
        // set linenumber on exitNext().
        // GOTO APPLY STEP
        // LABEL afterNext
        /*
         * condition
         * GOTOIF condition labelBeforeThen
         * GOTO labelAfterThen|labelBeforeElse
         * labelBeforeThen
         * ThenStmts
         * GOTO labelAfterThen|labelAfterElse
         * labelAfterThen
         * ElseStmts
         * labelAfterElse
         */
        // Patch IF true: condition
        // Patch IF true: GOTO labelBeforeThen
        // Patch IF false: GOTO labelAfterThen|labelBeforeElse
        // Patch THEN: GOTO labelAfterThen|labelAfterElse
        // IF condition is true, GOTO labelBeforeThen
        // IF condition is false, GOTO labelAfterThen|labelBeforeElse
        // Add instruction for:
        // THEN GOTO linenum | THEN linenum
        // GOTO labelAfterThen|labelAfterElse
        // Add instruction for:
        // ELSE linenum
        //
        // IF expr THEN BEGIN
        // ...
        // ELSE BEGIN
        // ...
        // END IF
        //
        //
        // expr.result
        // GOTO labelBeforeThen IF expr.result is true
        // GOTO labelAfterThen|labelBeforeElse
        // labelBeforeThen (patch GOTOIF)
        // GOTO labelAfterThen|labelAfterElse (else begin)
        // labelAfterThen
        // labelBeforeElse
        //
        // IF condition is true, GOTO labelBeforeThen
        // IF condition is false, GOTO labelAfterThen|labelBeforeElse
        // Add labelBeforeThen
        // Patch IF true: GOTO labelBeforeThen
        // GOTO labelAfterThen|labelAfterElse
        // GOTO labelAfterThen|labelAfterElse
        // Add labelAfterElse
        // Patch IF true: GOTO labelBeforeThen
        // Patch IF false: GOTO labelAfterThen|labelBeforeElse
        // Patch THEN: GOTO labelAfterThen|labelAfterElse
        // fileName, fileNumber
        // openMode, accessMode
        // lockMode, recordLen
        // fileName, fileNumber
        // openMode, accessMode
        // lockMode, recordLen
        // FileNumber, #fields
        // if fileNumber != null, skip first instruction
        public override void ExitRestorestmt(PuffinBasicParser.RestorestmtContext ctx)
        {
            ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.RESTORE, NULL_ID, NULL_ID, NULL_ID);
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        //
        // Expr
        //
        //
        // Functions
        //
        // n, x$, y$
        // x$, y$
        // x$, n, m
        // x$, n
        //
        // Stmt
        //
        // struct
        // array
        // scalar data type
        // struct
        // scalar data type
        // scalar
        //PuffinBasicAtomTypeId.lookup(compCtx.var2.getText());
        // array
        // list
        // struct
        // scalar data type
        // set
        // dict
        // struct
        // scalar data type
        // struct
        // throw
        // GOTO postFuncDecl
        // LABEL FuncStart
        // Push child scope
        // Copy expr to result
        // Pop declaration scope
        // GOTO Caller
        // LABEL postFuncDecl
        // Patch GOTO postFuncDecl
        // GOTO postFuncDecl
        // LABEL FuncStart
        // Push child scope
        // scalar
        //PuffinBasicAtomTypeId.lookup(compCtx.var2.getText());
        // list
        // struct
        // array
        // scalar data type
        // set
        // dict
        // struct
        // scalar data type
        // struct
        // array
        // throw
        // Copy expr to result
        // GOTO LABEL gotoCaller
        // Pop declaration scope
        // LABEL gotoCaller
        // GOTO Caller
        // LABEL postFuncDecl
        // Patch GOTO LABEL gotoCaller
        // Patch GOTO postFuncDecl
        // Unset current UDF state
        // LABEL beforeWhile
        // expr()
        // NOT expr()
        // If expr is false, GOTO afterWend
        // GOTO LABEL beforeWhile
        // LABEL afterWend
        // Patch GOTO afterWend
        // stepCopy = step or 1 (default)
        // var=init
        // endCopy=end
        // GOTO LABEL CHECK
        // APPLY STEP
        // JUMP here from NEXT
        // Add step
        // CHECK
        // If (step >= 0 and var > end) or (step < 0 and var < end) GOTO after "next"
        // step >= 0
        // Patch GOTO LABEL Check
        // var > end
        // (step >= 0 and var > end)
        // step < 0
        // var < end
        // (step < 0 and var < end)
        // if (true) GOTO after NEXT
        // set linenumber on exitNext().
        // GOTO APPLY STEP
        // LABEL afterNext
        /*
         * condition
         * GOTOIF condition labelBeforeThen
         * GOTO labelAfterThen|labelBeforeElse
         * labelBeforeThen
         * ThenStmts
         * GOTO labelAfterThen|labelAfterElse
         * labelAfterThen
         * ElseStmts
         * labelAfterElse
         */
        // Patch IF true: condition
        // Patch IF true: GOTO labelBeforeThen
        // Patch IF false: GOTO labelAfterThen|labelBeforeElse
        // Patch THEN: GOTO labelAfterThen|labelAfterElse
        // IF condition is true, GOTO labelBeforeThen
        // IF condition is false, GOTO labelAfterThen|labelBeforeElse
        // Add instruction for:
        // THEN GOTO linenum | THEN linenum
        // GOTO labelAfterThen|labelAfterElse
        // Add instruction for:
        // ELSE linenum
        //
        // IF expr THEN BEGIN
        // ...
        // ELSE BEGIN
        // ...
        // END IF
        //
        //
        // expr.result
        // GOTO labelBeforeThen IF expr.result is true
        // GOTO labelAfterThen|labelBeforeElse
        // labelBeforeThen (patch GOTOIF)
        // GOTO labelAfterThen|labelAfterElse (else begin)
        // labelAfterThen
        // labelBeforeElse
        //
        // IF condition is true, GOTO labelBeforeThen
        // IF condition is false, GOTO labelAfterThen|labelBeforeElse
        // Add labelBeforeThen
        // Patch IF true: GOTO labelBeforeThen
        // GOTO labelAfterThen|labelAfterElse
        // GOTO labelAfterThen|labelAfterElse
        // Add labelAfterElse
        // Patch IF true: GOTO labelBeforeThen
        // Patch IF false: GOTO labelAfterThen|labelBeforeElse
        // Patch THEN: GOTO labelAfterThen|labelAfterElse
        // fileName, fileNumber
        // openMode, accessMode
        // lockMode, recordLen
        // fileName, fileNumber
        // openMode, accessMode
        // lockMode, recordLen
        // FileNumber, #fields
        // if fileNumber != null, skip first instruction
        public override void ExitDatastmt(PuffinBasicParser.DatastmtContext ctx)
        {
            var children = ctx.children;
            for (int i = 1; i < children.Count; i += 2)
            {
                var child = children[i];
                int valueId;
                if (child is PuffinBasicParser.NumberContext)
                {
                    valueId = LookupInstruction((PuffinBasicParser.NumberContext)child).result;
                }
                else
                {
                    var text = Unquote(child.GetText());
                    valueId = ir.GetSymbolTable().AddTmp(PuffinBasicAtomTypeId.STRING, (e) => e.GetValue().SetString(text));
                }

                ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.DATA, valueId, NULL_ID, NULL_ID);
            }
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        //
        // Expr
        //
        //
        // Functions
        //
        // n, x$, y$
        // x$, y$
        // x$, n, m
        // x$, n
        //
        // Stmt
        //
        // struct
        // array
        // scalar data type
        // struct
        // scalar data type
        // scalar
        //PuffinBasicAtomTypeId.lookup(compCtx.var2.getText());
        // array
        // list
        // struct
        // scalar data type
        // set
        // dict
        // struct
        // scalar data type
        // struct
        // throw
        // GOTO postFuncDecl
        // LABEL FuncStart
        // Push child scope
        // Copy expr to result
        // Pop declaration scope
        // GOTO Caller
        // LABEL postFuncDecl
        // Patch GOTO postFuncDecl
        // GOTO postFuncDecl
        // LABEL FuncStart
        // Push child scope
        // scalar
        //PuffinBasicAtomTypeId.lookup(compCtx.var2.getText());
        // list
        // struct
        // array
        // scalar data type
        // set
        // dict
        // struct
        // scalar data type
        // struct
        // array
        // throw
        // Copy expr to result
        // GOTO LABEL gotoCaller
        // Pop declaration scope
        // LABEL gotoCaller
        // GOTO Caller
        // LABEL postFuncDecl
        // Patch GOTO LABEL gotoCaller
        // Patch GOTO postFuncDecl
        // Unset current UDF state
        // LABEL beforeWhile
        // expr()
        // NOT expr()
        // If expr is false, GOTO afterWend
        // GOTO LABEL beforeWhile
        // LABEL afterWend
        // Patch GOTO afterWend
        // stepCopy = step or 1 (default)
        // var=init
        // endCopy=end
        // GOTO LABEL CHECK
        // APPLY STEP
        // JUMP here from NEXT
        // Add step
        // CHECK
        // If (step >= 0 and var > end) or (step < 0 and var < end) GOTO after "next"
        // step >= 0
        // Patch GOTO LABEL Check
        // var > end
        // (step >= 0 and var > end)
        // step < 0
        // var < end
        // (step < 0 and var < end)
        // if (true) GOTO after NEXT
        // set linenumber on exitNext().
        // GOTO APPLY STEP
        // LABEL afterNext
        /*
         * condition
         * GOTOIF condition labelBeforeThen
         * GOTO labelAfterThen|labelBeforeElse
         * labelBeforeThen
         * ThenStmts
         * GOTO labelAfterThen|labelAfterElse
         * labelAfterThen
         * ElseStmts
         * labelAfterElse
         */
        // Patch IF true: condition
        // Patch IF true: GOTO labelBeforeThen
        // Patch IF false: GOTO labelAfterThen|labelBeforeElse
        // Patch THEN: GOTO labelAfterThen|labelAfterElse
        // IF condition is true, GOTO labelBeforeThen
        // IF condition is false, GOTO labelAfterThen|labelBeforeElse
        // Add instruction for:
        // THEN GOTO linenum | THEN linenum
        // GOTO labelAfterThen|labelAfterElse
        // Add instruction for:
        // ELSE linenum
        //
        // IF expr THEN BEGIN
        // ...
        // ELSE BEGIN
        // ...
        // END IF
        //
        //
        // expr.result
        // GOTO labelBeforeThen IF expr.result is true
        // GOTO labelAfterThen|labelBeforeElse
        // labelBeforeThen (patch GOTOIF)
        // GOTO labelAfterThen|labelAfterElse (else begin)
        // labelAfterThen
        // labelBeforeElse
        //
        // IF condition is true, GOTO labelBeforeThen
        // IF condition is false, GOTO labelAfterThen|labelBeforeElse
        // Add labelBeforeThen
        // Patch IF true: GOTO labelBeforeThen
        // GOTO labelAfterThen|labelAfterElse
        // GOTO labelAfterThen|labelAfterElse
        // Add labelAfterElse
        // Patch IF true: GOTO labelBeforeThen
        // Patch IF false: GOTO labelAfterThen|labelBeforeElse
        // Patch THEN: GOTO labelAfterThen|labelAfterElse
        // fileName, fileNumber
        // openMode, accessMode
        // lockMode, recordLen
        // fileName, fileNumber
        // openMode, accessMode
        // lockMode, recordLen
        // FileNumber, #fields
        // if fileNumber != null, skip first instruction
        public override void ExitLabelstmt(PuffinBasicParser.LabelstmtContext ctx)
        {
            var label = ctx.@string().STRING().GetText();
            ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.LABEL, ir.GetSymbolTable().AddLabel(label), NULL_ID, NULL_ID);
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        //
        // Expr
        //
        //
        // Functions
        //
        // n, x$, y$
        // x$, y$
        // x$, n, m
        // x$, n
        //
        // Stmt
        //
        // struct
        // array
        // scalar data type
        // struct
        // scalar data type
        // scalar
        //PuffinBasicAtomTypeId.lookup(compCtx.var2.getText());
        // array
        // list
        // struct
        // scalar data type
        // set
        // dict
        // struct
        // scalar data type
        // struct
        // throw
        // GOTO postFuncDecl
        // LABEL FuncStart
        // Push child scope
        // Copy expr to result
        // Pop declaration scope
        // GOTO Caller
        // LABEL postFuncDecl
        // Patch GOTO postFuncDecl
        // GOTO postFuncDecl
        // LABEL FuncStart
        // Push child scope
        // scalar
        //PuffinBasicAtomTypeId.lookup(compCtx.var2.getText());
        // list
        // struct
        // array
        // scalar data type
        // set
        // dict
        // struct
        // scalar data type
        // struct
        // array
        // throw
        // Copy expr to result
        // GOTO LABEL gotoCaller
        // Pop declaration scope
        // LABEL gotoCaller
        // GOTO Caller
        // LABEL postFuncDecl
        // Patch GOTO LABEL gotoCaller
        // Patch GOTO postFuncDecl
        // Unset current UDF state
        // LABEL beforeWhile
        // expr()
        // NOT expr()
        // If expr is false, GOTO afterWend
        // GOTO LABEL beforeWhile
        // LABEL afterWend
        // Patch GOTO afterWend
        // stepCopy = step or 1 (default)
        // var=init
        // endCopy=end
        // GOTO LABEL CHECK
        // APPLY STEP
        // JUMP here from NEXT
        // Add step
        // CHECK
        // If (step >= 0 and var > end) or (step < 0 and var < end) GOTO after "next"
        // step >= 0
        // Patch GOTO LABEL Check
        // var > end
        // (step >= 0 and var > end)
        // step < 0
        // var < end
        // (step < 0 and var < end)
        // if (true) GOTO after NEXT
        // set linenumber on exitNext().
        // GOTO APPLY STEP
        // LABEL afterNext
        /*
         * condition
         * GOTOIF condition labelBeforeThen
         * GOTO labelAfterThen|labelBeforeElse
         * labelBeforeThen
         * ThenStmts
         * GOTO labelAfterThen|labelAfterElse
         * labelAfterThen
         * ElseStmts
         * labelAfterElse
         */
        // Patch IF true: condition
        // Patch IF true: GOTO labelBeforeThen
        // Patch IF false: GOTO labelAfterThen|labelBeforeElse
        // Patch THEN: GOTO labelAfterThen|labelAfterElse
        // IF condition is true, GOTO labelBeforeThen
        // IF condition is false, GOTO labelAfterThen|labelBeforeElse
        // Add instruction for:
        // THEN GOTO linenum | THEN linenum
        // GOTO labelAfterThen|labelAfterElse
        // Add instruction for:
        // ELSE linenum
        //
        // IF expr THEN BEGIN
        // ...
        // ELSE BEGIN
        // ...
        // END IF
        //
        //
        // expr.result
        // GOTO labelBeforeThen IF expr.result is true
        // GOTO labelAfterThen|labelBeforeElse
        // labelBeforeThen (patch GOTOIF)
        // GOTO labelAfterThen|labelAfterElse (else begin)
        // labelAfterThen
        // labelBeforeElse
        //
        // IF condition is true, GOTO labelBeforeThen
        // IF condition is false, GOTO labelAfterThen|labelBeforeElse
        // Add labelBeforeThen
        // Patch IF true: GOTO labelBeforeThen
        // GOTO labelAfterThen|labelAfterElse
        // GOTO labelAfterThen|labelAfterElse
        // Add labelAfterElse
        // Patch IF true: GOTO labelBeforeThen
        // Patch IF false: GOTO labelAfterThen|labelBeforeElse
        // Patch THEN: GOTO labelAfterThen|labelAfterElse
        // fileName, fileNumber
        // openMode, accessMode
        // lockMode, recordLen
        // fileName, fileNumber
        // openMode, accessMode
        // lockMode, recordLen
        // FileNumber, #fields
        // if fileNumber != null, skip first instruction
        // GraphicsRuntime
        public override void ExitScreenstmt(PuffinBasicParser.ScreenstmtContext ctx)
        {
            AssertGraphics();
            var title = LookupInstruction(ctx.expr(0));
            var w = LookupInstruction(ctx.expr(1));
            var h = LookupInstruction(ctx.expr(2));
            var iw = ctx.expr().Count()== 5 ? LookupInstruction(ctx.expr(3)) : w;
            var ih = ctx.expr().Count()== 5 ? LookupInstruction(ctx.expr(4)) : h;
            var manualRepaintFlag = ctx.mr != null;
            var doubleBufferFlag = ctx.db != null;
            Types.AssertString(ir.GetSymbolTable()[title.result].GetType().GetAtomTypeId(), GetCtxString(ctx));
            Types.AssertNumeric(ir.GetSymbolTable()[w.result].GetType().GetAtomTypeId(), GetCtxString(ctx));
            Types.AssertNumeric(ir.GetSymbolTable()[h.result].GetType().GetAtomTypeId(), GetCtxString(ctx));
            Types.AssertNumeric(ir.GetSymbolTable()[iw.result].GetType().GetAtomTypeId(), GetCtxString(ctx));
            Types.AssertNumeric(ir.GetSymbolTable()[ih.result].GetType().GetAtomTypeId(), GetCtxString(ctx));
            ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.PARAM2, w.result, h.result, NULL_ID);
            ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.PARAM2, iw.result, ih.result, NULL_ID);
            var repaint = ir.GetSymbolTable().AddTmp(INT32, (e) => e.GetValue().SetInt32(manualRepaintFlag ? 0 : -1));
            var doubleBuffer = ir.GetSymbolTable().AddTmp(INT32, (e) => e.GetValue().SetInt32(doubleBufferFlag ? -1 : 0));
            ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.PARAM2, repaint, doubleBuffer, NULL_ID);
            ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.SCREEN, title.result, NULL_ID, NULL_ID);
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        //
        // Expr
        //
        //
        // Functions
        //
        // n, x$, y$
        // x$, y$
        // x$, n, m
        // x$, n
        //
        // Stmt
        //
        // struct
        // array
        // scalar data type
        // struct
        // scalar data type
        // scalar
        //PuffinBasicAtomTypeId.lookup(compCtx.var2.getText());
        // array
        // list
        // struct
        // scalar data type
        // set
        // dict
        // struct
        // scalar data type
        // struct
        // throw
        // GOTO postFuncDecl
        // LABEL FuncStart
        // Push child scope
        // Copy expr to result
        // Pop declaration scope
        // GOTO Caller
        // LABEL postFuncDecl
        // Patch GOTO postFuncDecl
        // GOTO postFuncDecl
        // LABEL FuncStart
        // Push child scope
        // scalar
        //PuffinBasicAtomTypeId.lookup(compCtx.var2.getText());
        // list
        // struct
        // array
        // scalar data type
        // set
        // dict
        // struct
        // scalar data type
        // struct
        // array
        // throw
        // Copy expr to result
        // GOTO LABEL gotoCaller
        // Pop declaration scope
        // LABEL gotoCaller
        // GOTO Caller
        // LABEL postFuncDecl
        // Patch GOTO LABEL gotoCaller
        // Patch GOTO postFuncDecl
        // Unset current UDF state
        // LABEL beforeWhile
        // expr()
        // NOT expr()
        // If expr is false, GOTO afterWend
        // GOTO LABEL beforeWhile
        // LABEL afterWend
        // Patch GOTO afterWend
        // stepCopy = step or 1 (default)
        // var=init
        // endCopy=end
        // GOTO LABEL CHECK
        // APPLY STEP
        // JUMP here from NEXT
        // Add step
        // CHECK
        // If (step >= 0 and var > end) or (step < 0 and var < end) GOTO after "next"
        // step >= 0
        // Patch GOTO LABEL Check
        // var > end
        // (step >= 0 and var > end)
        // step < 0
        // var < end
        // (step < 0 and var < end)
        // if (true) GOTO after NEXT
        // set linenumber on exitNext().
        // GOTO APPLY STEP
        // LABEL afterNext
        /*
         * condition
         * GOTOIF condition labelBeforeThen
         * GOTO labelAfterThen|labelBeforeElse
         * labelBeforeThen
         * ThenStmts
         * GOTO labelAfterThen|labelAfterElse
         * labelAfterThen
         * ElseStmts
         * labelAfterElse
         */
        // Patch IF true: condition
        // Patch IF true: GOTO labelBeforeThen
        // Patch IF false: GOTO labelAfterThen|labelBeforeElse
        // Patch THEN: GOTO labelAfterThen|labelAfterElse
        // IF condition is true, GOTO labelBeforeThen
        // IF condition is false, GOTO labelAfterThen|labelBeforeElse
        // Add instruction for:
        // THEN GOTO linenum | THEN linenum
        // GOTO labelAfterThen|labelAfterElse
        // Add instruction for:
        // ELSE linenum
        //
        // IF expr THEN BEGIN
        // ...
        // ELSE BEGIN
        // ...
        // END IF
        //
        //
        // expr.result
        // GOTO labelBeforeThen IF expr.result is true
        // GOTO labelAfterThen|labelBeforeElse
        // labelBeforeThen (patch GOTOIF)
        // GOTO labelAfterThen|labelAfterElse (else begin)
        // labelAfterThen
        // labelBeforeElse
        //
        // IF condition is true, GOTO labelBeforeThen
        // IF condition is false, GOTO labelAfterThen|labelBeforeElse
        // Add labelBeforeThen
        // Patch IF true: GOTO labelBeforeThen
        // GOTO labelAfterThen|labelAfterElse
        // GOTO labelAfterThen|labelAfterElse
        // Add labelAfterElse
        // Patch IF true: GOTO labelBeforeThen
        // Patch IF false: GOTO labelAfterThen|labelBeforeElse
        // Patch THEN: GOTO labelAfterThen|labelAfterElse
        // fileName, fileNumber
        // openMode, accessMode
        // lockMode, recordLen
        // fileName, fileNumber
        // openMode, accessMode
        // lockMode, recordLen
        // FileNumber, #fields
        // if fileNumber != null, skip first instruction
        // GraphicsRuntime
        public override void ExitRepaintstmt(PuffinBasicParser.RepaintstmtContext ctx)
        {
            AssertGraphics();
            ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.REPAINT, NULL_ID, NULL_ID, NULL_ID);
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        //
        // Expr
        //
        //
        // Functions
        //
        // n, x$, y$
        // x$, y$
        // x$, n, m
        // x$, n
        //
        // Stmt
        //
        // struct
        // array
        // scalar data type
        // struct
        // scalar data type
        // scalar
        //PuffinBasicAtomTypeId.lookup(compCtx.var2.getText());
        // array
        // list
        // struct
        // scalar data type
        // set
        // dict
        // struct
        // scalar data type
        // struct
        // throw
        // GOTO postFuncDecl
        // LABEL FuncStart
        // Push child scope
        // Copy expr to result
        // Pop declaration scope
        // GOTO Caller
        // LABEL postFuncDecl
        // Patch GOTO postFuncDecl
        // GOTO postFuncDecl
        // LABEL FuncStart
        // Push child scope
        // scalar
        //PuffinBasicAtomTypeId.lookup(compCtx.var2.getText());
        // list
        // struct
        // array
        // scalar data type
        // set
        // dict
        // struct
        // scalar data type
        // struct
        // array
        // throw
        // Copy expr to result
        // GOTO LABEL gotoCaller
        // Pop declaration scope
        // LABEL gotoCaller
        // GOTO Caller
        // LABEL postFuncDecl
        // Patch GOTO LABEL gotoCaller
        // Patch GOTO postFuncDecl
        // Unset current UDF state
        // LABEL beforeWhile
        // expr()
        // NOT expr()
        // If expr is false, GOTO afterWend
        // GOTO LABEL beforeWhile
        // LABEL afterWend
        // Patch GOTO afterWend
        // stepCopy = step or 1 (default)
        // var=init
        // endCopy=end
        // GOTO LABEL CHECK
        // APPLY STEP
        // JUMP here from NEXT
        // Add step
        // CHECK
        // If (step >= 0 and var > end) or (step < 0 and var < end) GOTO after "next"
        // step >= 0
        // Patch GOTO LABEL Check
        // var > end
        // (step >= 0 and var > end)
        // step < 0
        // var < end
        // (step < 0 and var < end)
        // if (true) GOTO after NEXT
        // set linenumber on exitNext().
        // GOTO APPLY STEP
        // LABEL afterNext
        /*
         * condition
         * GOTOIF condition labelBeforeThen
         * GOTO labelAfterThen|labelBeforeElse
         * labelBeforeThen
         * ThenStmts
         * GOTO labelAfterThen|labelAfterElse
         * labelAfterThen
         * ElseStmts
         * labelAfterElse
         */
        // Patch IF true: condition
        // Patch IF true: GOTO labelBeforeThen
        // Patch IF false: GOTO labelAfterThen|labelBeforeElse
        // Patch THEN: GOTO labelAfterThen|labelAfterElse
        // IF condition is true, GOTO labelBeforeThen
        // IF condition is false, GOTO labelAfterThen|labelBeforeElse
        // Add instruction for:
        // THEN GOTO linenum | THEN linenum
        // GOTO labelAfterThen|labelAfterElse
        // Add instruction for:
        // ELSE linenum
        //
        // IF expr THEN BEGIN
        // ...
        // ELSE BEGIN
        // ...
        // END IF
        //
        //
        // expr.result
        // GOTO labelBeforeThen IF expr.result is true
        // GOTO labelAfterThen|labelBeforeElse
        // labelBeforeThen (patch GOTOIF)
        // GOTO labelAfterThen|labelAfterElse (else begin)
        // labelAfterThen
        // labelBeforeElse
        //
        // IF condition is true, GOTO labelBeforeThen
        // IF condition is false, GOTO labelAfterThen|labelBeforeElse
        // Add labelBeforeThen
        // Patch IF true: GOTO labelBeforeThen
        // GOTO labelAfterThen|labelAfterElse
        // GOTO labelAfterThen|labelAfterElse
        // Add labelAfterElse
        // Patch IF true: GOTO labelBeforeThen
        // Patch IF false: GOTO labelAfterThen|labelBeforeElse
        // Patch THEN: GOTO labelAfterThen|labelAfterElse
        // fileName, fileNumber
        // openMode, accessMode
        // lockMode, recordLen
        // fileName, fileNumber
        // openMode, accessMode
        // lockMode, recordLen
        // FileNumber, #fields
        // if fileNumber != null, skip first instruction
        // GraphicsRuntime
        public override void ExitCirclestmt(PuffinBasicParser.CirclestmtContext ctx)
        {
            AssertGraphics();
            var x = LookupInstruction(ctx.x);
            var y = LookupInstruction(ctx.y);
            var r1 = LookupInstruction(ctx.r1);
            var r2 = LookupInstruction(ctx.r2);
            var s = ctx.s != null ? LookupInstruction(ctx.s) : null;
            var e = ctx.e != null ? LookupInstruction(ctx.e) : null;
            var fill = ctx.fill != null ? LookupInstruction(ctx.fill) : null;
            Types.AssertNumeric(ir.GetSymbolTable()[x.result].GetType().GetAtomTypeId(), GetCtxString(ctx));
            Types.AssertNumeric(ir.GetSymbolTable()[y.result].GetType().GetAtomTypeId(), GetCtxString(ctx));
            Types.AssertNumeric(ir.GetSymbolTable()[r1.result].GetType().GetAtomTypeId(), GetCtxString(ctx));
            Types.AssertNumeric(ir.GetSymbolTable()[r2.result].GetType().GetAtomTypeId(), GetCtxString(ctx));
            if (s != null)
            {
                Types.AssertNumeric(ir.GetSymbolTable()[s.result].GetType().GetAtomTypeId(), GetCtxString(ctx));
            }

            if (e != null)
            {
                Types.AssertNumeric(ir.GetSymbolTable()[e.result].GetType().GetAtomTypeId(), GetCtxString(ctx));
            }

            if (fill != null)
            {
                Types.AssertString(ir.GetSymbolTable()[fill.result].GetType().GetAtomTypeId(), GetCtxString(ctx));
            }

            ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.PARAM2, x.result, y.result, NULL_ID);
            ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.PARAM2, s != null ? s.result : NULL_ID, e != null ? e.result : NULL_ID, NULL_ID);
            ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.PARAM1, fill != null ? fill.result : NULL_ID, NULL_ID, NULL_ID);
            ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.CIRCLE, r1.result, r2.result, NULL_ID);
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        //
        // Expr
        //
        //
        // Functions
        //
        // n, x$, y$
        // x$, y$
        // x$, n, m
        // x$, n
        //
        // Stmt
        //
        // struct
        // array
        // scalar data type
        // struct
        // scalar data type
        // scalar
        //PuffinBasicAtomTypeId.lookup(compCtx.var2.getText());
        // array
        // list
        // struct
        // scalar data type
        // set
        // dict
        // struct
        // scalar data type
        // struct
        // throw
        // GOTO postFuncDecl
        // LABEL FuncStart
        // Push child scope
        // Copy expr to result
        // Pop declaration scope
        // GOTO Caller
        // LABEL postFuncDecl
        // Patch GOTO postFuncDecl
        // GOTO postFuncDecl
        // LABEL FuncStart
        // Push child scope
        // scalar
        //PuffinBasicAtomTypeId.lookup(compCtx.var2.getText());
        // list
        // struct
        // array
        // scalar data type
        // set
        // dict
        // struct
        // scalar data type
        // struct
        // array
        // throw
        // Copy expr to result
        // GOTO LABEL gotoCaller
        // Pop declaration scope
        // LABEL gotoCaller
        // GOTO Caller
        // LABEL postFuncDecl
        // Patch GOTO LABEL gotoCaller
        // Patch GOTO postFuncDecl
        // Unset current UDF state
        // LABEL beforeWhile
        // expr()
        // NOT expr()
        // If expr is false, GOTO afterWend
        // GOTO LABEL beforeWhile
        // LABEL afterWend
        // Patch GOTO afterWend
        // stepCopy = step or 1 (default)
        // var=init
        // endCopy=end
        // GOTO LABEL CHECK
        // APPLY STEP
        // JUMP here from NEXT
        // Add step
        // CHECK
        // If (step >= 0 and var > end) or (step < 0 and var < end) GOTO after "next"
        // step >= 0
        // Patch GOTO LABEL Check
        // var > end
        // (step >= 0 and var > end)
        // step < 0
        // var < end
        // (step < 0 and var < end)
        // if (true) GOTO after NEXT
        // set linenumber on exitNext().
        // GOTO APPLY STEP
        // LABEL afterNext
        /*
         * condition
         * GOTOIF condition labelBeforeThen
         * GOTO labelAfterThen|labelBeforeElse
         * labelBeforeThen
         * ThenStmts
         * GOTO labelAfterThen|labelAfterElse
         * labelAfterThen
         * ElseStmts
         * labelAfterElse
         */
        // Patch IF true: condition
        // Patch IF true: GOTO labelBeforeThen
        // Patch IF false: GOTO labelAfterThen|labelBeforeElse
        // Patch THEN: GOTO labelAfterThen|labelAfterElse
        // IF condition is true, GOTO labelBeforeThen
        // IF condition is false, GOTO labelAfterThen|labelBeforeElse
        // Add instruction for:
        // THEN GOTO linenum | THEN linenum
        // GOTO labelAfterThen|labelAfterElse
        // Add instruction for:
        // ELSE linenum
        //
        // IF expr THEN BEGIN
        // ...
        // ELSE BEGIN
        // ...
        // END IF
        //
        //
        // expr.result
        // GOTO labelBeforeThen IF expr.result is true
        // GOTO labelAfterThen|labelBeforeElse
        // labelBeforeThen (patch GOTOIF)
        // GOTO labelAfterThen|labelAfterElse (else begin)
        // labelAfterThen
        // labelBeforeElse
        //
        // IF condition is true, GOTO labelBeforeThen
        // IF condition is false, GOTO labelAfterThen|labelBeforeElse
        // Add labelBeforeThen
        // Patch IF true: GOTO labelBeforeThen
        // GOTO labelAfterThen|labelAfterElse
        // GOTO labelAfterThen|labelAfterElse
        // Add labelAfterElse
        // Patch IF true: GOTO labelBeforeThen
        // Patch IF false: GOTO labelAfterThen|labelBeforeElse
        // Patch THEN: GOTO labelAfterThen|labelAfterElse
        // fileName, fileNumber
        // openMode, accessMode
        // lockMode, recordLen
        // fileName, fileNumber
        // openMode, accessMode
        // lockMode, recordLen
        // FileNumber, #fields
        // if fileNumber != null, skip first instruction
        // GraphicsRuntime
        public override void ExitLinestmt(PuffinBasicParser.LinestmtContext ctx)
        {
            AssertGraphics();
            var x1 = LookupInstruction(ctx.x1);
            var y1 = LookupInstruction(ctx.y1);
            var x2 = LookupInstruction(ctx.x2);
            var y2 = LookupInstruction(ctx.y2);
            Types.AssertNumeric(ir.GetSymbolTable()[x1.result].GetType().GetAtomTypeId(), GetCtxString(ctx));
            Types.AssertNumeric(ir.GetSymbolTable()[y1.result].GetType().GetAtomTypeId(), GetCtxString(ctx));
            Types.AssertNumeric(ir.GetSymbolTable()[x2.result].GetType().GetAtomTypeId(), GetCtxString(ctx));
            Types.AssertNumeric(ir.GetSymbolTable()[y2.result].GetType().GetAtomTypeId(), GetCtxString(ctx));
            Instruction bf = null;
            if (ctx.bf != null)
            {
                bf = LookupInstruction(ctx.bf);
                Types.AssertString(ir.GetSymbolTable()[bf.result].GetType().GetAtomTypeId(), GetCtxString(ctx));
            }

            ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.PARAM2, x1.result, y1.result, NULL_ID);
            ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.PARAM2, x2.result, y2.result, NULL_ID);
            ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.LINE, bf != null ? bf.result : NULL_ID, NULL_ID, NULL_ID);
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        //
        // Expr
        //
        //
        // Functions
        //
        // n, x$, y$
        // x$, y$
        // x$, n, m
        // x$, n
        //
        // Stmt
        //
        // struct
        // array
        // scalar data type
        // struct
        // scalar data type
        // scalar
        //PuffinBasicAtomTypeId.lookup(compCtx.var2.getText());
        // array
        // list
        // struct
        // scalar data type
        // set
        // dict
        // struct
        // scalar data type
        // struct
        // throw
        // GOTO postFuncDecl
        // LABEL FuncStart
        // Push child scope
        // Copy expr to result
        // Pop declaration scope
        // GOTO Caller
        // LABEL postFuncDecl
        // Patch GOTO postFuncDecl
        // GOTO postFuncDecl
        // LABEL FuncStart
        // Push child scope
        // scalar
        //PuffinBasicAtomTypeId.lookup(compCtx.var2.getText());
        // list
        // struct
        // array
        // scalar data type
        // set
        // dict
        // struct
        // scalar data type
        // struct
        // array
        // throw
        // Copy expr to result
        // GOTO LABEL gotoCaller
        // Pop declaration scope
        // LABEL gotoCaller
        // GOTO Caller
        // LABEL postFuncDecl
        // Patch GOTO LABEL gotoCaller
        // Patch GOTO postFuncDecl
        // Unset current UDF state
        // LABEL beforeWhile
        // expr()
        // NOT expr()
        // If expr is false, GOTO afterWend
        // GOTO LABEL beforeWhile
        // LABEL afterWend
        // Patch GOTO afterWend
        // stepCopy = step or 1 (default)
        // var=init
        // endCopy=end
        // GOTO LABEL CHECK
        // APPLY STEP
        // JUMP here from NEXT
        // Add step
        // CHECK
        // If (step >= 0 and var > end) or (step < 0 and var < end) GOTO after "next"
        // step >= 0
        // Patch GOTO LABEL Check
        // var > end
        // (step >= 0 and var > end)
        // step < 0
        // var < end
        // (step < 0 and var < end)
        // if (true) GOTO after NEXT
        // set linenumber on exitNext().
        // GOTO APPLY STEP
        // LABEL afterNext
        /*
         * condition
         * GOTOIF condition labelBeforeThen
         * GOTO labelAfterThen|labelBeforeElse
         * labelBeforeThen
         * ThenStmts
         * GOTO labelAfterThen|labelAfterElse
         * labelAfterThen
         * ElseStmts
         * labelAfterElse
         */
        // Patch IF true: condition
        // Patch IF true: GOTO labelBeforeThen
        // Patch IF false: GOTO labelAfterThen|labelBeforeElse
        // Patch THEN: GOTO labelAfterThen|labelAfterElse
        // IF condition is true, GOTO labelBeforeThen
        // IF condition is false, GOTO labelAfterThen|labelBeforeElse
        // Add instruction for:
        // THEN GOTO linenum | THEN linenum
        // GOTO labelAfterThen|labelAfterElse
        // Add instruction for:
        // ELSE linenum
        //
        // IF expr THEN BEGIN
        // ...
        // ELSE BEGIN
        // ...
        // END IF
        //
        //
        // expr.result
        // GOTO labelBeforeThen IF expr.result is true
        // GOTO labelAfterThen|labelBeforeElse
        // labelBeforeThen (patch GOTOIF)
        // GOTO labelAfterThen|labelAfterElse (else begin)
        // labelAfterThen
        // labelBeforeElse
        //
        // IF condition is true, GOTO labelBeforeThen
        // IF condition is false, GOTO labelAfterThen|labelBeforeElse
        // Add labelBeforeThen
        // Patch IF true: GOTO labelBeforeThen
        // GOTO labelAfterThen|labelAfterElse
        // GOTO labelAfterThen|labelAfterElse
        // Add labelAfterElse
        // Patch IF true: GOTO labelBeforeThen
        // Patch IF false: GOTO labelAfterThen|labelBeforeElse
        // Patch THEN: GOTO labelAfterThen|labelAfterElse
        // fileName, fileNumber
        // openMode, accessMode
        // lockMode, recordLen
        // fileName, fileNumber
        // openMode, accessMode
        // lockMode, recordLen
        // FileNumber, #fields
        // if fileNumber != null, skip first instruction
        // GraphicsRuntime
        public override void ExitColorstmt(PuffinBasicParser.ColorstmtContext ctx)
        {
            var r = LookupInstruction(ctx.r);
            var g = LookupInstruction(ctx.g);
            var b = LookupInstruction(ctx.b);
            Types.AssertNumeric(ir.GetSymbolTable()[r.result].GetType().GetAtomTypeId(), GetCtxString(ctx));
            Types.AssertNumeric(ir.GetSymbolTable()[g.result].GetType().GetAtomTypeId(), GetCtxString(ctx));
            Types.AssertNumeric(ir.GetSymbolTable()[b.result].GetType().GetAtomTypeId(), GetCtxString(ctx));
            ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.PARAM2, r.result, g.result, NULL_ID);
            ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.COLOR, b.result, NULL_ID, NULL_ID);
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        //
        // Expr
        //
        //
        // Functions
        //
        // n, x$, y$
        // x$, y$
        // x$, n, m
        // x$, n
        //
        // Stmt
        //
        // struct
        // array
        // scalar data type
        // struct
        // scalar data type
        // scalar
        //PuffinBasicAtomTypeId.lookup(compCtx.var2.getText());
        // array
        // list
        // struct
        // scalar data type
        // set
        // dict
        // struct
        // scalar data type
        // struct
        // throw
        // GOTO postFuncDecl
        // LABEL FuncStart
        // Push child scope
        // Copy expr to result
        // Pop declaration scope
        // GOTO Caller
        // LABEL postFuncDecl
        // Patch GOTO postFuncDecl
        // GOTO postFuncDecl
        // LABEL FuncStart
        // Push child scope
        // scalar
        //PuffinBasicAtomTypeId.lookup(compCtx.var2.getText());
        // list
        // struct
        // array
        // scalar data type
        // set
        // dict
        // struct
        // scalar data type
        // struct
        // array
        // throw
        // Copy expr to result
        // GOTO LABEL gotoCaller
        // Pop declaration scope
        // LABEL gotoCaller
        // GOTO Caller
        // LABEL postFuncDecl
        // Patch GOTO LABEL gotoCaller
        // Patch GOTO postFuncDecl
        // Unset current UDF state
        // LABEL beforeWhile
        // expr()
        // NOT expr()
        // If expr is false, GOTO afterWend
        // GOTO LABEL beforeWhile
        // LABEL afterWend
        // Patch GOTO afterWend
        // stepCopy = step or 1 (default)
        // var=init
        // endCopy=end
        // GOTO LABEL CHECK
        // APPLY STEP
        // JUMP here from NEXT
        // Add step
        // CHECK
        // If (step >= 0 and var > end) or (step < 0 and var < end) GOTO after "next"
        // step >= 0
        // Patch GOTO LABEL Check
        // var > end
        // (step >= 0 and var > end)
        // step < 0
        // var < end
        // (step < 0 and var < end)
        // if (true) GOTO after NEXT
        // set linenumber on exitNext().
        // GOTO APPLY STEP
        // LABEL afterNext
        /*
         * condition
         * GOTOIF condition labelBeforeThen
         * GOTO labelAfterThen|labelBeforeElse
         * labelBeforeThen
         * ThenStmts
         * GOTO labelAfterThen|labelAfterElse
         * labelAfterThen
         * ElseStmts
         * labelAfterElse
         */
        // Patch IF true: condition
        // Patch IF true: GOTO labelBeforeThen
        // Patch IF false: GOTO labelAfterThen|labelBeforeElse
        // Patch THEN: GOTO labelAfterThen|labelAfterElse
        // IF condition is true, GOTO labelBeforeThen
        // IF condition is false, GOTO labelAfterThen|labelBeforeElse
        // Add instruction for:
        // THEN GOTO linenum | THEN linenum
        // GOTO labelAfterThen|labelAfterElse
        // Add instruction for:
        // ELSE linenum
        //
        // IF expr THEN BEGIN
        // ...
        // ELSE BEGIN
        // ...
        // END IF
        //
        //
        // expr.result
        // GOTO labelBeforeThen IF expr.result is true
        // GOTO labelAfterThen|labelBeforeElse
        // labelBeforeThen (patch GOTOIF)
        // GOTO labelAfterThen|labelAfterElse (else begin)
        // labelAfterThen
        // labelBeforeElse
        //
        // IF condition is true, GOTO labelBeforeThen
        // IF condition is false, GOTO labelAfterThen|labelBeforeElse
        // Add labelBeforeThen
        // Patch IF true: GOTO labelBeforeThen
        // GOTO labelAfterThen|labelAfterElse
        // GOTO labelAfterThen|labelAfterElse
        // Add labelAfterElse
        // Patch IF true: GOTO labelBeforeThen
        // Patch IF false: GOTO labelAfterThen|labelBeforeElse
        // Patch THEN: GOTO labelAfterThen|labelAfterElse
        // fileName, fileNumber
        // openMode, accessMode
        // lockMode, recordLen
        // fileName, fileNumber
        // openMode, accessMode
        // lockMode, recordLen
        // FileNumber, #fields
        // if fileNumber != null, skip first instruction
        // GraphicsRuntime
        public override void ExitPaintstmt(PuffinBasicParser.PaintstmtContext ctx)
        {
            AssertGraphics();
            var x = LookupInstruction(ctx.x);
            var y = LookupInstruction(ctx.y);
            var r = LookupInstruction(ctx.r);
            var g = LookupInstruction(ctx.g);
            var b = LookupInstruction(ctx.b);
            Types.AssertNumeric(ir.GetSymbolTable()[x.result].GetType().GetAtomTypeId(), GetCtxString(ctx));
            Types.AssertNumeric(ir.GetSymbolTable()[y.result].GetType().GetAtomTypeId(), GetCtxString(ctx));
            Types.AssertNumeric(ir.GetSymbolTable()[r.result].GetType().GetAtomTypeId(), GetCtxString(ctx));
            Types.AssertNumeric(ir.GetSymbolTable()[g.result].GetType().GetAtomTypeId(), GetCtxString(ctx));
            Types.AssertNumeric(ir.GetSymbolTable()[b.result].GetType().GetAtomTypeId(), GetCtxString(ctx));
            ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.PARAM2, r.result, g.result, NULL_ID);
            ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.PARAM1, b.result, NULL_ID, NULL_ID);
            ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.PAINT, x.result, y.result, NULL_ID);
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        //
        // Expr
        //
        //
        // Functions
        //
        // n, x$, y$
        // x$, y$
        // x$, n, m
        // x$, n
        //
        // Stmt
        //
        // struct
        // array
        // scalar data type
        // struct
        // scalar data type
        // scalar
        //PuffinBasicAtomTypeId.lookup(compCtx.var2.getText());
        // array
        // list
        // struct
        // scalar data type
        // set
        // dict
        // struct
        // scalar data type
        // struct
        // throw
        // GOTO postFuncDecl
        // LABEL FuncStart
        // Push child scope
        // Copy expr to result
        // Pop declaration scope
        // GOTO Caller
        // LABEL postFuncDecl
        // Patch GOTO postFuncDecl
        // GOTO postFuncDecl
        // LABEL FuncStart
        // Push child scope
        // scalar
        //PuffinBasicAtomTypeId.lookup(compCtx.var2.getText());
        // list
        // struct
        // array
        // scalar data type
        // set
        // dict
        // struct
        // scalar data type
        // struct
        // array
        // throw
        // Copy expr to result
        // GOTO LABEL gotoCaller
        // Pop declaration scope
        // LABEL gotoCaller
        // GOTO Caller
        // LABEL postFuncDecl
        // Patch GOTO LABEL gotoCaller
        // Patch GOTO postFuncDecl
        // Unset current UDF state
        // LABEL beforeWhile
        // expr()
        // NOT expr()
        // If expr is false, GOTO afterWend
        // GOTO LABEL beforeWhile
        // LABEL afterWend
        // Patch GOTO afterWend
        // stepCopy = step or 1 (default)
        // var=init
        // endCopy=end
        // GOTO LABEL CHECK
        // APPLY STEP
        // JUMP here from NEXT
        // Add step
        // CHECK
        // If (step >= 0 and var > end) or (step < 0 and var < end) GOTO after "next"
        // step >= 0
        // Patch GOTO LABEL Check
        // var > end
        // (step >= 0 and var > end)
        // step < 0
        // var < end
        // (step < 0 and var < end)
        // if (true) GOTO after NEXT
        // set linenumber on exitNext().
        // GOTO APPLY STEP
        // LABEL afterNext
        /*
         * condition
         * GOTOIF condition labelBeforeThen
         * GOTO labelAfterThen|labelBeforeElse
         * labelBeforeThen
         * ThenStmts
         * GOTO labelAfterThen|labelAfterElse
         * labelAfterThen
         * ElseStmts
         * labelAfterElse
         */
        // Patch IF true: condition
        // Patch IF true: GOTO labelBeforeThen
        // Patch IF false: GOTO labelAfterThen|labelBeforeElse
        // Patch THEN: GOTO labelAfterThen|labelAfterElse
        // IF condition is true, GOTO labelBeforeThen
        // IF condition is false, GOTO labelAfterThen|labelBeforeElse
        // Add instruction for:
        // THEN GOTO linenum | THEN linenum
        // GOTO labelAfterThen|labelAfterElse
        // Add instruction for:
        // ELSE linenum
        //
        // IF expr THEN BEGIN
        // ...
        // ELSE BEGIN
        // ...
        // END IF
        //
        //
        // expr.result
        // GOTO labelBeforeThen IF expr.result is true
        // GOTO labelAfterThen|labelBeforeElse
        // labelBeforeThen (patch GOTOIF)
        // GOTO labelAfterThen|labelAfterElse (else begin)
        // labelAfterThen
        // labelBeforeElse
        //
        // IF condition is true, GOTO labelBeforeThen
        // IF condition is false, GOTO labelAfterThen|labelBeforeElse
        // Add labelBeforeThen
        // Patch IF true: GOTO labelBeforeThen
        // GOTO labelAfterThen|labelAfterElse
        // GOTO labelAfterThen|labelAfterElse
        // Add labelAfterElse
        // Patch IF true: GOTO labelBeforeThen
        // Patch IF false: GOTO labelAfterThen|labelBeforeElse
        // Patch THEN: GOTO labelAfterThen|labelAfterElse
        // fileName, fileNumber
        // openMode, accessMode
        // lockMode, recordLen
        // fileName, fileNumber
        // openMode, accessMode
        // lockMode, recordLen
        // FileNumber, #fields
        // if fileNumber != null, skip first instruction
        // GraphicsRuntime
        public override void ExitPsetstmt(PuffinBasicParser.PsetstmtContext ctx)
        {
            AssertGraphics();
            var x = LookupInstruction(ctx.x);
            var y = LookupInstruction(ctx.y);
            int rId = NULL_ID, gId = NULL_ID, bId = NULL_ID;
            if (ctx.r != null)
            {
                rId = LookupInstruction(ctx.r).result;
                Types.AssertNumeric(ir.GetSymbolTable()[rId].GetType().GetAtomTypeId(), GetCtxString(ctx));
            }

            if (ctx.g != null)
            {
                gId = LookupInstruction(ctx.g).result;
                Types.AssertNumeric(ir.GetSymbolTable()[gId].GetType().GetAtomTypeId(), GetCtxString(ctx));
            }

            if (ctx.b != null)
            {
                bId = LookupInstruction(ctx.b).result;
                Types.AssertNumeric(ir.GetSymbolTable()[bId].GetType().GetAtomTypeId(), GetCtxString(ctx));
            }

            Types.AssertNumeric(ir.GetSymbolTable()[x.result].GetType().GetAtomTypeId(), GetCtxString(ctx));
            Types.AssertNumeric(ir.GetSymbolTable()[y.result].GetType().GetAtomTypeId(), GetCtxString(ctx));
            ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.PARAM2, rId, gId, NULL_ID);
            ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.PARAM1, bId, NULL_ID, NULL_ID);
            ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.PSET, x.result, y.result, NULL_ID);
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        //
        // Expr
        //
        //
        // Functions
        //
        // n, x$, y$
        // x$, y$
        // x$, n, m
        // x$, n
        //
        // Stmt
        //
        // struct
        // array
        // scalar data type
        // struct
        // scalar data type
        // scalar
        //PuffinBasicAtomTypeId.lookup(compCtx.var2.getText());
        // array
        // list
        // struct
        // scalar data type
        // set
        // dict
        // struct
        // scalar data type
        // struct
        // throw
        // GOTO postFuncDecl
        // LABEL FuncStart
        // Push child scope
        // Copy expr to result
        // Pop declaration scope
        // GOTO Caller
        // LABEL postFuncDecl
        // Patch GOTO postFuncDecl
        // GOTO postFuncDecl
        // LABEL FuncStart
        // Push child scope
        // scalar
        //PuffinBasicAtomTypeId.lookup(compCtx.var2.getText());
        // list
        // struct
        // array
        // scalar data type
        // set
        // dict
        // struct
        // scalar data type
        // struct
        // array
        // throw
        // Copy expr to result
        // GOTO LABEL gotoCaller
        // Pop declaration scope
        // LABEL gotoCaller
        // GOTO Caller
        // LABEL postFuncDecl
        // Patch GOTO LABEL gotoCaller
        // Patch GOTO postFuncDecl
        // Unset current UDF state
        // LABEL beforeWhile
        // expr()
        // NOT expr()
        // If expr is false, GOTO afterWend
        // GOTO LABEL beforeWhile
        // LABEL afterWend
        // Patch GOTO afterWend
        // stepCopy = step or 1 (default)
        // var=init
        // endCopy=end
        // GOTO LABEL CHECK
        // APPLY STEP
        // JUMP here from NEXT
        // Add step
        // CHECK
        // If (step >= 0 and var > end) or (step < 0 and var < end) GOTO after "next"
        // step >= 0
        // Patch GOTO LABEL Check
        // var > end
        // (step >= 0 and var > end)
        // step < 0
        // var < end
        // (step < 0 and var < end)
        // if (true) GOTO after NEXT
        // set linenumber on exitNext().
        // GOTO APPLY STEP
        // LABEL afterNext
        /*
         * condition
         * GOTOIF condition labelBeforeThen
         * GOTO labelAfterThen|labelBeforeElse
         * labelBeforeThen
         * ThenStmts
         * GOTO labelAfterThen|labelAfterElse
         * labelAfterThen
         * ElseStmts
         * labelAfterElse
         */
        // Patch IF true: condition
        // Patch IF true: GOTO labelBeforeThen
        // Patch IF false: GOTO labelAfterThen|labelBeforeElse
        // Patch THEN: GOTO labelAfterThen|labelAfterElse
        // IF condition is true, GOTO labelBeforeThen
        // IF condition is false, GOTO labelAfterThen|labelBeforeElse
        // Add instruction for:
        // THEN GOTO linenum | THEN linenum
        // GOTO labelAfterThen|labelAfterElse
        // Add instruction for:
        // ELSE linenum
        //
        // IF expr THEN BEGIN
        // ...
        // ELSE BEGIN
        // ...
        // END IF
        //
        //
        // expr.result
        // GOTO labelBeforeThen IF expr.result is true
        // GOTO labelAfterThen|labelBeforeElse
        // labelBeforeThen (patch GOTOIF)
        // GOTO labelAfterThen|labelAfterElse (else begin)
        // labelAfterThen
        // labelBeforeElse
        //
        // IF condition is true, GOTO labelBeforeThen
        // IF condition is false, GOTO labelAfterThen|labelBeforeElse
        // Add labelBeforeThen
        // Patch IF true: GOTO labelBeforeThen
        // GOTO labelAfterThen|labelAfterElse
        // GOTO labelAfterThen|labelAfterElse
        // Add labelAfterElse
        // Patch IF true: GOTO labelBeforeThen
        // Patch IF false: GOTO labelAfterThen|labelBeforeElse
        // Patch THEN: GOTO labelAfterThen|labelAfterElse
        // fileName, fileNumber
        // openMode, accessMode
        // lockMode, recordLen
        // fileName, fileNumber
        // openMode, accessMode
        // lockMode, recordLen
        // FileNumber, #fields
        // if fileNumber != null, skip first instruction
        // GraphicsRuntime
        public override void ExitGraphicsgetstmt(PuffinBasicParser.GraphicsgetstmtContext ctx)
        {
            AssertGraphics();
            var x1 = LookupInstruction(ctx.x1);
            var y1 = LookupInstruction(ctx.y1);
            var x2 = LookupInstruction(ctx.x2);
            var y2 = LookupInstruction(ctx.y2);
            var varInstr = LookupInstruction(ctx.variable());
            int bufferNumber = ctx.BACK1() != null ? GraphicsUtil.BUFFER_NUM_BACK1 : GraphicsUtil.BUFFER_NUM_FRONT;
            Types.AssertNumeric(ir.GetSymbolTable()[x1.result].GetType().GetAtomTypeId(), GetCtxString(ctx));
            Types.AssertNumeric(ir.GetSymbolTable()[y1.result].GetType().GetAtomTypeId(), GetCtxString(ctx));
            Types.AssertNumeric(ir.GetSymbolTable()[x2.result].GetType().GetAtomTypeId(), GetCtxString(ctx));
            Types.AssertNumeric(ir.GetSymbolTable()[y2.result].GetType().GetAtomTypeId(), GetCtxString(ctx));
            AssertVariable(ir.GetSymbolTable()[varInstr.result], GetCtxString(ctx));
            ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.PARAM2, x1.result, y1.result, NULL_ID);
            ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.PARAM2, x2.result, y2.result, NULL_ID);
            ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.GGET, varInstr.result, ir.GetSymbolTable().AddTmp(INT32, (e) => e.GetValue().SetInt32(bufferNumber)), NULL_ID);
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        //
        // Expr
        //
        //
        // Functions
        //
        // n, x$, y$
        // x$, y$
        // x$, n, m
        // x$, n
        //
        // Stmt
        //
        // struct
        // array
        // scalar data type
        // struct
        // scalar data type
        // scalar
        //PuffinBasicAtomTypeId.lookup(compCtx.var2.getText());
        // array
        // list
        // struct
        // scalar data type
        // set
        // dict
        // struct
        // scalar data type
        // struct
        // throw
        // GOTO postFuncDecl
        // LABEL FuncStart
        // Push child scope
        // Copy expr to result
        // Pop declaration scope
        // GOTO Caller
        // LABEL postFuncDecl
        // Patch GOTO postFuncDecl
        // GOTO postFuncDecl
        // LABEL FuncStart
        // Push child scope
        // scalar
        //PuffinBasicAtomTypeId.lookup(compCtx.var2.getText());
        // list
        // struct
        // array
        // scalar data type
        // set
        // dict
        // struct
        // scalar data type
        // struct
        // array
        // throw
        // Copy expr to result
        // GOTO LABEL gotoCaller
        // Pop declaration scope
        // LABEL gotoCaller
        // GOTO Caller
        // LABEL postFuncDecl
        // Patch GOTO LABEL gotoCaller
        // Patch GOTO postFuncDecl
        // Unset current UDF state
        // LABEL beforeWhile
        // expr()
        // NOT expr()
        // If expr is false, GOTO afterWend
        // GOTO LABEL beforeWhile
        // LABEL afterWend
        // Patch GOTO afterWend
        // stepCopy = step or 1 (default)
        // var=init
        // endCopy=end
        // GOTO LABEL CHECK
        // APPLY STEP
        // JUMP here from NEXT
        // Add step
        // CHECK
        // If (step >= 0 and var > end) or (step < 0 and var < end) GOTO after "next"
        // step >= 0
        // Patch GOTO LABEL Check
        // var > end
        // (step >= 0 and var > end)
        // step < 0
        // var < end
        // (step < 0 and var < end)
        // if (true) GOTO after NEXT
        // set linenumber on exitNext().
        // GOTO APPLY STEP
        // LABEL afterNext
        /*
         * condition
         * GOTOIF condition labelBeforeThen
         * GOTO labelAfterThen|labelBeforeElse
         * labelBeforeThen
         * ThenStmts
         * GOTO labelAfterThen|labelAfterElse
         * labelAfterThen
         * ElseStmts
         * labelAfterElse
         */
        // Patch IF true: condition
        // Patch IF true: GOTO labelBeforeThen
        // Patch IF false: GOTO labelAfterThen|labelBeforeElse
        // Patch THEN: GOTO labelAfterThen|labelAfterElse
        // IF condition is true, GOTO labelBeforeThen
        // IF condition is false, GOTO labelAfterThen|labelBeforeElse
        // Add instruction for:
        // THEN GOTO linenum | THEN linenum
        // GOTO labelAfterThen|labelAfterElse
        // Add instruction for:
        // ELSE linenum
        //
        // IF expr THEN BEGIN
        // ...
        // ELSE BEGIN
        // ...
        // END IF
        //
        //
        // expr.result
        // GOTO labelBeforeThen IF expr.result is true
        // GOTO labelAfterThen|labelBeforeElse
        // labelBeforeThen (patch GOTOIF)
        // GOTO labelAfterThen|labelAfterElse (else begin)
        // labelAfterThen
        // labelBeforeElse
        //
        // IF condition is true, GOTO labelBeforeThen
        // IF condition is false, GOTO labelAfterThen|labelBeforeElse
        // Add labelBeforeThen
        // Patch IF true: GOTO labelBeforeThen
        // GOTO labelAfterThen|labelAfterElse
        // GOTO labelAfterThen|labelAfterElse
        // Add labelAfterElse
        // Patch IF true: GOTO labelBeforeThen
        // Patch IF false: GOTO labelAfterThen|labelBeforeElse
        // Patch THEN: GOTO labelAfterThen|labelAfterElse
        // fileName, fileNumber
        // openMode, accessMode
        // lockMode, recordLen
        // fileName, fileNumber
        // openMode, accessMode
        // lockMode, recordLen
        // FileNumber, #fields
        // if fileNumber != null, skip first instruction
        // GraphicsRuntime
        public override void ExitGraphicsputstmt(PuffinBasicParser.GraphicsputstmtContext ctx)
        {
            AssertGraphics();
            var x = LookupInstruction(ctx.x);
            var y = LookupInstruction(ctx.y);
            var varInstr = LookupInstruction(ctx.variable());
            var action = ctx.action != null ? LookupInstruction(ctx.action) : null;
            int bufferNumber = ctx.FRONT() == null ? GraphicsUtil.BUFFER_NUM_BACK1 : GraphicsUtil.BUFFER_NUM_FRONT;
            Types.AssertNumeric(ir.GetSymbolTable()[x.result].GetType().GetAtomTypeId(), GetCtxString(ctx));
            Types.AssertNumeric(ir.GetSymbolTable()[y.result].GetType().GetAtomTypeId(), GetCtxString(ctx));
            AssertVariable(ir.GetSymbolTable()[varInstr.result], GetCtxString(ctx));
            if (action != null)
            {
                Types.AssertString(ir.GetSymbolTable()[action.result].GetType().GetAtomTypeId(), GetCtxString(ctx));
            }

            ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.PARAM2, x.result, y.result, NULL_ID);
            ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.PARAM1, ir.GetSymbolTable().AddTmp(INT32, (e) => e.GetValue().SetInt32(bufferNumber)), NULL_ID, NULL_ID);
            ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.GPUT, action != null ? action.result : NULL_ID, varInstr.result, NULL_ID);
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        //
        // Expr
        //
        //
        // Functions
        //
        // n, x$, y$
        // x$, y$
        // x$, n, m
        // x$, n
        //
        // Stmt
        //
        // struct
        // array
        // scalar data type
        // struct
        // scalar data type
        // scalar
        //PuffinBasicAtomTypeId.lookup(compCtx.var2.getText());
        // array
        // list
        // struct
        // scalar data type
        // set
        // dict
        // struct
        // scalar data type
        // struct
        // throw
        // GOTO postFuncDecl
        // LABEL FuncStart
        // Push child scope
        // Copy expr to result
        // Pop declaration scope
        // GOTO Caller
        // LABEL postFuncDecl
        // Patch GOTO postFuncDecl
        // GOTO postFuncDecl
        // LABEL FuncStart
        // Push child scope
        // scalar
        //PuffinBasicAtomTypeId.lookup(compCtx.var2.getText());
        // list
        // struct
        // array
        // scalar data type
        // set
        // dict
        // struct
        // scalar data type
        // struct
        // array
        // throw
        // Copy expr to result
        // GOTO LABEL gotoCaller
        // Pop declaration scope
        // LABEL gotoCaller
        // GOTO Caller
        // LABEL postFuncDecl
        // Patch GOTO LABEL gotoCaller
        // Patch GOTO postFuncDecl
        // Unset current UDF state
        // LABEL beforeWhile
        // expr()
        // NOT expr()
        // If expr is false, GOTO afterWend
        // GOTO LABEL beforeWhile
        // LABEL afterWend
        // Patch GOTO afterWend
        // stepCopy = step or 1 (default)
        // var=init
        // endCopy=end
        // GOTO LABEL CHECK
        // APPLY STEP
        // JUMP here from NEXT
        // Add step
        // CHECK
        // If (step >= 0 and var > end) or (step < 0 and var < end) GOTO after "next"
        // step >= 0
        // Patch GOTO LABEL Check
        // var > end
        // (step >= 0 and var > end)
        // step < 0
        // var < end
        // (step < 0 and var < end)
        // if (true) GOTO after NEXT
        // set linenumber on exitNext().
        // GOTO APPLY STEP
        // LABEL afterNext
        /*
         * condition
         * GOTOIF condition labelBeforeThen
         * GOTO labelAfterThen|labelBeforeElse
         * labelBeforeThen
         * ThenStmts
         * GOTO labelAfterThen|labelAfterElse
         * labelAfterThen
         * ElseStmts
         * labelAfterElse
         */
        // Patch IF true: condition
        // Patch IF true: GOTO labelBeforeThen
        // Patch IF false: GOTO labelAfterThen|labelBeforeElse
        // Patch THEN: GOTO labelAfterThen|labelAfterElse
        // IF condition is true, GOTO labelBeforeThen
        // IF condition is false, GOTO labelAfterThen|labelBeforeElse
        // Add instruction for:
        // THEN GOTO linenum | THEN linenum
        // GOTO labelAfterThen|labelAfterElse
        // Add instruction for:
        // ELSE linenum
        //
        // IF expr THEN BEGIN
        // ...
        // ELSE BEGIN
        // ...
        // END IF
        //
        //
        // expr.result
        // GOTO labelBeforeThen IF expr.result is true
        // GOTO labelAfterThen|labelBeforeElse
        // labelBeforeThen (patch GOTOIF)
        // GOTO labelAfterThen|labelAfterElse (else begin)
        // labelAfterThen
        // labelBeforeElse
        //
        // IF condition is true, GOTO labelBeforeThen
        // IF condition is false, GOTO labelAfterThen|labelBeforeElse
        // Add labelBeforeThen
        // Patch IF true: GOTO labelBeforeThen
        // GOTO labelAfterThen|labelAfterElse
        // GOTO labelAfterThen|labelAfterElse
        // Add labelAfterElse
        // Patch IF true: GOTO labelBeforeThen
        // Patch IF false: GOTO labelAfterThen|labelBeforeElse
        // Patch THEN: GOTO labelAfterThen|labelAfterElse
        // fileName, fileNumber
        // openMode, accessMode
        // lockMode, recordLen
        // fileName, fileNumber
        // openMode, accessMode
        // lockMode, recordLen
        // FileNumber, #fields
        // if fileNumber != null, skip first instruction
        // GraphicsRuntime
        public override void ExitGraphicsbuffercopyhorstmt(PuffinBasicParser.GraphicsbuffercopyhorstmtContext ctx)
        {
            AssertGraphics();
            var srcx = LookupInstruction(ctx.srcx);
            var dstx = LookupInstruction(ctx.dstx);
            var w = LookupInstruction(ctx.w);
            Types.AssertNumeric(ir.GetSymbolTable()[srcx.result].GetType().GetAtomTypeId(), GetCtxString(ctx));
            Types.AssertNumeric(ir.GetSymbolTable()[dstx.result].GetType().GetAtomTypeId(), GetCtxString(ctx));
            Types.AssertNumeric(ir.GetSymbolTable()[w.result].GetType().GetAtomTypeId(), GetCtxString(ctx));
            ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.PARAM2, srcx.result, dstx.result, NULL_ID);
            ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.BUFFERCOPYHOR, w.result, NULL_ID, NULL_ID);
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        //
        // Expr
        //
        //
        // Functions
        //
        // n, x$, y$
        // x$, y$
        // x$, n, m
        // x$, n
        //
        // Stmt
        //
        // struct
        // array
        // scalar data type
        // struct
        // scalar data type
        // scalar
        //PuffinBasicAtomTypeId.lookup(compCtx.var2.getText());
        // array
        // list
        // struct
        // scalar data type
        // set
        // dict
        // struct
        // scalar data type
        // struct
        // throw
        // GOTO postFuncDecl
        // LABEL FuncStart
        // Push child scope
        // Copy expr to result
        // Pop declaration scope
        // GOTO Caller
        // LABEL postFuncDecl
        // Patch GOTO postFuncDecl
        // GOTO postFuncDecl
        // LABEL FuncStart
        // Push child scope
        // scalar
        //PuffinBasicAtomTypeId.lookup(compCtx.var2.getText());
        // list
        // struct
        // array
        // scalar data type
        // set
        // dict
        // struct
        // scalar data type
        // struct
        // array
        // throw
        // Copy expr to result
        // GOTO LABEL gotoCaller
        // Pop declaration scope
        // LABEL gotoCaller
        // GOTO Caller
        // LABEL postFuncDecl
        // Patch GOTO LABEL gotoCaller
        // Patch GOTO postFuncDecl
        // Unset current UDF state
        // LABEL beforeWhile
        // expr()
        // NOT expr()
        // If expr is false, GOTO afterWend
        // GOTO LABEL beforeWhile
        // LABEL afterWend
        // Patch GOTO afterWend
        // stepCopy = step or 1 (default)
        // var=init
        // endCopy=end
        // GOTO LABEL CHECK
        // APPLY STEP
        // JUMP here from NEXT
        // Add step
        // CHECK
        // If (step >= 0 and var > end) or (step < 0 and var < end) GOTO after "next"
        // step >= 0
        // Patch GOTO LABEL Check
        // var > end
        // (step >= 0 and var > end)
        // step < 0
        // var < end
        // (step < 0 and var < end)
        // if (true) GOTO after NEXT
        // set linenumber on exitNext().
        // GOTO APPLY STEP
        // LABEL afterNext
        /*
         * condition
         * GOTOIF condition labelBeforeThen
         * GOTO labelAfterThen|labelBeforeElse
         * labelBeforeThen
         * ThenStmts
         * GOTO labelAfterThen|labelAfterElse
         * labelAfterThen
         * ElseStmts
         * labelAfterElse
         */
        // Patch IF true: condition
        // Patch IF true: GOTO labelBeforeThen
        // Patch IF false: GOTO labelAfterThen|labelBeforeElse
        // Patch THEN: GOTO labelAfterThen|labelAfterElse
        // IF condition is true, GOTO labelBeforeThen
        // IF condition is false, GOTO labelAfterThen|labelBeforeElse
        // Add instruction for:
        // THEN GOTO linenum | THEN linenum
        // GOTO labelAfterThen|labelAfterElse
        // Add instruction for:
        // ELSE linenum
        //
        // IF expr THEN BEGIN
        // ...
        // ELSE BEGIN
        // ...
        // END IF
        //
        //
        // expr.result
        // GOTO labelBeforeThen IF expr.result is true
        // GOTO labelAfterThen|labelBeforeElse
        // labelBeforeThen (patch GOTOIF)
        // GOTO labelAfterThen|labelAfterElse (else begin)
        // labelAfterThen
        // labelBeforeElse
        //
        // IF condition is true, GOTO labelBeforeThen
        // IF condition is false, GOTO labelAfterThen|labelBeforeElse
        // Add labelBeforeThen
        // Patch IF true: GOTO labelBeforeThen
        // GOTO labelAfterThen|labelAfterElse
        // GOTO labelAfterThen|labelAfterElse
        // Add labelAfterElse
        // Patch IF true: GOTO labelBeforeThen
        // Patch IF false: GOTO labelAfterThen|labelBeforeElse
        // Patch THEN: GOTO labelAfterThen|labelAfterElse
        // fileName, fileNumber
        // openMode, accessMode
        // lockMode, recordLen
        // fileName, fileNumber
        // openMode, accessMode
        // lockMode, recordLen
        // FileNumber, #fields
        // if fileNumber != null, skip first instruction
        // GraphicsRuntime
        public override void ExitDrawstmt(PuffinBasicParser.DrawstmtContext ctx)
        {
            AssertGraphics();
            var str = LookupInstruction(ctx.expr());
            Types.AssertString(ir.GetSymbolTable()[str.result].GetType().GetAtomTypeId(), GetCtxString(ctx));
            ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.DRAW, str.result, NULL_ID, NULL_ID);
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        //
        // Expr
        //
        //
        // Functions
        //
        // n, x$, y$
        // x$, y$
        // x$, n, m
        // x$, n
        //
        // Stmt
        //
        // struct
        // array
        // scalar data type
        // struct
        // scalar data type
        // scalar
        //PuffinBasicAtomTypeId.lookup(compCtx.var2.getText());
        // array
        // list
        // struct
        // scalar data type
        // set
        // dict
        // struct
        // scalar data type
        // struct
        // throw
        // GOTO postFuncDecl
        // LABEL FuncStart
        // Push child scope
        // Copy expr to result
        // Pop declaration scope
        // GOTO Caller
        // LABEL postFuncDecl
        // Patch GOTO postFuncDecl
        // GOTO postFuncDecl
        // LABEL FuncStart
        // Push child scope
        // scalar
        //PuffinBasicAtomTypeId.lookup(compCtx.var2.getText());
        // list
        // struct
        // array
        // scalar data type
        // set
        // dict
        // struct
        // scalar data type
        // struct
        // array
        // throw
        // Copy expr to result
        // GOTO LABEL gotoCaller
        // Pop declaration scope
        // LABEL gotoCaller
        // GOTO Caller
        // LABEL postFuncDecl
        // Patch GOTO LABEL gotoCaller
        // Patch GOTO postFuncDecl
        // Unset current UDF state
        // LABEL beforeWhile
        // expr()
        // NOT expr()
        // If expr is false, GOTO afterWend
        // GOTO LABEL beforeWhile
        // LABEL afterWend
        // Patch GOTO afterWend
        // stepCopy = step or 1 (default)
        // var=init
        // endCopy=end
        // GOTO LABEL CHECK
        // APPLY STEP
        // JUMP here from NEXT
        // Add step
        // CHECK
        // If (step >= 0 and var > end) or (step < 0 and var < end) GOTO after "next"
        // step >= 0
        // Patch GOTO LABEL Check
        // var > end
        // (step >= 0 and var > end)
        // step < 0
        // var < end
        // (step < 0 and var < end)
        // if (true) GOTO after NEXT
        // set linenumber on exitNext().
        // GOTO APPLY STEP
        // LABEL afterNext
        /*
         * condition
         * GOTOIF condition labelBeforeThen
         * GOTO labelAfterThen|labelBeforeElse
         * labelBeforeThen
         * ThenStmts
         * GOTO labelAfterThen|labelAfterElse
         * labelAfterThen
         * ElseStmts
         * labelAfterElse
         */
        // Patch IF true: condition
        // Patch IF true: GOTO labelBeforeThen
        // Patch IF false: GOTO labelAfterThen|labelBeforeElse
        // Patch THEN: GOTO labelAfterThen|labelAfterElse
        // IF condition is true, GOTO labelBeforeThen
        // IF condition is false, GOTO labelAfterThen|labelBeforeElse
        // Add instruction for:
        // THEN GOTO linenum | THEN linenum
        // GOTO labelAfterThen|labelAfterElse
        // Add instruction for:
        // ELSE linenum
        //
        // IF expr THEN BEGIN
        // ...
        // ELSE BEGIN
        // ...
        // END IF
        //
        //
        // expr.result
        // GOTO labelBeforeThen IF expr.result is true
        // GOTO labelAfterThen|labelBeforeElse
        // labelBeforeThen (patch GOTOIF)
        // GOTO labelAfterThen|labelAfterElse (else begin)
        // labelAfterThen
        // labelBeforeElse
        //
        // IF condition is true, GOTO labelBeforeThen
        // IF condition is false, GOTO labelAfterThen|labelBeforeElse
        // Add labelBeforeThen
        // Patch IF true: GOTO labelBeforeThen
        // GOTO labelAfterThen|labelAfterElse
        // GOTO labelAfterThen|labelAfterElse
        // Add labelAfterElse
        // Patch IF true: GOTO labelBeforeThen
        // Patch IF false: GOTO labelAfterThen|labelBeforeElse
        // Patch THEN: GOTO labelAfterThen|labelAfterElse
        // fileName, fileNumber
        // openMode, accessMode
        // lockMode, recordLen
        // fileName, fileNumber
        // openMode, accessMode
        // lockMode, recordLen
        // FileNumber, #fields
        // if fileNumber != null, skip first instruction
        // GraphicsRuntime
        public override void ExitFontstmt(PuffinBasicParser.FontstmtContext ctx)
        {
            AssertGraphics();
            var name = LookupInstruction(ctx.name);
            var style = LookupInstruction(ctx.style);
            var size = LookupInstruction(ctx.size);
            Types.AssertString(ir.GetSymbolTable()[style.result].GetType().GetAtomTypeId(), GetCtxString(ctx));
            Types.AssertNumeric(ir.GetSymbolTable()[size.result].GetType().GetAtomTypeId(), GetCtxString(ctx));
            Types.AssertString(ir.GetSymbolTable()[name.result].GetType().GetAtomTypeId(), GetCtxString(ctx));
            ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.PARAM2, style.result, size.result, NULL_ID);
            ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.FONT, name.result, NULL_ID, NULL_ID);
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        //
        // Expr
        //
        //
        // Functions
        //
        // n, x$, y$
        // x$, y$
        // x$, n, m
        // x$, n
        //
        // Stmt
        //
        // struct
        // array
        // scalar data type
        // struct
        // scalar data type
        // scalar
        //PuffinBasicAtomTypeId.lookup(compCtx.var2.getText());
        // array
        // list
        // struct
        // scalar data type
        // set
        // dict
        // struct
        // scalar data type
        // struct
        // throw
        // GOTO postFuncDecl
        // LABEL FuncStart
        // Push child scope
        // Copy expr to result
        // Pop declaration scope
        // GOTO Caller
        // LABEL postFuncDecl
        // Patch GOTO postFuncDecl
        // GOTO postFuncDecl
        // LABEL FuncStart
        // Push child scope
        // scalar
        //PuffinBasicAtomTypeId.lookup(compCtx.var2.getText());
        // list
        // struct
        // array
        // scalar data type
        // set
        // dict
        // struct
        // scalar data type
        // struct
        // array
        // throw
        // Copy expr to result
        // GOTO LABEL gotoCaller
        // Pop declaration scope
        // LABEL gotoCaller
        // GOTO Caller
        // LABEL postFuncDecl
        // Patch GOTO LABEL gotoCaller
        // Patch GOTO postFuncDecl
        // Unset current UDF state
        // LABEL beforeWhile
        // expr()
        // NOT expr()
        // If expr is false, GOTO afterWend
        // GOTO LABEL beforeWhile
        // LABEL afterWend
        // Patch GOTO afterWend
        // stepCopy = step or 1 (default)
        // var=init
        // endCopy=end
        // GOTO LABEL CHECK
        // APPLY STEP
        // JUMP here from NEXT
        // Add step
        // CHECK
        // If (step >= 0 and var > end) or (step < 0 and var < end) GOTO after "next"
        // step >= 0
        // Patch GOTO LABEL Check
        // var > end
        // (step >= 0 and var > end)
        // step < 0
        // var < end
        // (step < 0 and var < end)
        // if (true) GOTO after NEXT
        // set linenumber on exitNext().
        // GOTO APPLY STEP
        // LABEL afterNext
        /*
         * condition
         * GOTOIF condition labelBeforeThen
         * GOTO labelAfterThen|labelBeforeElse
         * labelBeforeThen
         * ThenStmts
         * GOTO labelAfterThen|labelAfterElse
         * labelAfterThen
         * ElseStmts
         * labelAfterElse
         */
        // Patch IF true: condition
        // Patch IF true: GOTO labelBeforeThen
        // Patch IF false: GOTO labelAfterThen|labelBeforeElse
        // Patch THEN: GOTO labelAfterThen|labelAfterElse
        // IF condition is true, GOTO labelBeforeThen
        // IF condition is false, GOTO labelAfterThen|labelBeforeElse
        // Add instruction for:
        // THEN GOTO linenum | THEN linenum
        // GOTO labelAfterThen|labelAfterElse
        // Add instruction for:
        // ELSE linenum
        //
        // IF expr THEN BEGIN
        // ...
        // ELSE BEGIN
        // ...
        // END IF
        //
        //
        // expr.result
        // GOTO labelBeforeThen IF expr.result is true
        // GOTO labelAfterThen|labelBeforeElse
        // labelBeforeThen (patch GOTOIF)
        // GOTO labelAfterThen|labelAfterElse (else begin)
        // labelAfterThen
        // labelBeforeElse
        //
        // IF condition is true, GOTO labelBeforeThen
        // IF condition is false, GOTO labelAfterThen|labelBeforeElse
        // Add labelBeforeThen
        // Patch IF true: GOTO labelBeforeThen
        // GOTO labelAfterThen|labelAfterElse
        // GOTO labelAfterThen|labelAfterElse
        // Add labelAfterElse
        // Patch IF true: GOTO labelBeforeThen
        // Patch IF false: GOTO labelAfterThen|labelBeforeElse
        // Patch THEN: GOTO labelAfterThen|labelAfterElse
        // fileName, fileNumber
        // openMode, accessMode
        // lockMode, recordLen
        // fileName, fileNumber
        // openMode, accessMode
        // lockMode, recordLen
        // FileNumber, #fields
        // if fileNumber != null, skip first instruction
        // GraphicsRuntime
        public override void ExitDrawstrstmt(PuffinBasicParser.DrawstrstmtContext ctx)
        {
            var str = LookupInstruction(ctx.str);
            var x = LookupInstruction(ctx.x);
            var y = LookupInstruction(ctx.y);
            Types.AssertNumeric(ir.GetSymbolTable()[x.result].GetType().GetAtomTypeId(), GetCtxString(ctx));
            Types.AssertNumeric(ir.GetSymbolTable()[y.result].GetType().GetAtomTypeId(), GetCtxString(ctx));
            Types.AssertString(ir.GetSymbolTable()[str.result].GetType().GetAtomTypeId(), GetCtxString(ctx));
            ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.PARAM2, x.result, y.result, NULL_ID);
            ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.DRAWSTR, str.result, NULL_ID, NULL_ID);
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        //
        // Expr
        //
        //
        // Functions
        //
        // n, x$, y$
        // x$, y$
        // x$, n, m
        // x$, n
        //
        // Stmt
        //
        // struct
        // array
        // scalar data type
        // struct
        // scalar data type
        // scalar
        //PuffinBasicAtomTypeId.lookup(compCtx.var2.getText());
        // array
        // list
        // struct
        // scalar data type
        // set
        // dict
        // struct
        // scalar data type
        // struct
        // throw
        // GOTO postFuncDecl
        // LABEL FuncStart
        // Push child scope
        // Copy expr to result
        // Pop declaration scope
        // GOTO Caller
        // LABEL postFuncDecl
        // Patch GOTO postFuncDecl
        // GOTO postFuncDecl
        // LABEL FuncStart
        // Push child scope
        // scalar
        //PuffinBasicAtomTypeId.lookup(compCtx.var2.getText());
        // list
        // struct
        // array
        // scalar data type
        // set
        // dict
        // struct
        // scalar data type
        // struct
        // array
        // throw
        // Copy expr to result
        // GOTO LABEL gotoCaller
        // Pop declaration scope
        // LABEL gotoCaller
        // GOTO Caller
        // LABEL postFuncDecl
        // Patch GOTO LABEL gotoCaller
        // Patch GOTO postFuncDecl
        // Unset current UDF state
        // LABEL beforeWhile
        // expr()
        // NOT expr()
        // If expr is false, GOTO afterWend
        // GOTO LABEL beforeWhile
        // LABEL afterWend
        // Patch GOTO afterWend
        // stepCopy = step or 1 (default)
        // var=init
        // endCopy=end
        // GOTO LABEL CHECK
        // APPLY STEP
        // JUMP here from NEXT
        // Add step
        // CHECK
        // If (step >= 0 and var > end) or (step < 0 and var < end) GOTO after "next"
        // step >= 0
        // Patch GOTO LABEL Check
        // var > end
        // (step >= 0 and var > end)
        // step < 0
        // var < end
        // (step < 0 and var < end)
        // if (true) GOTO after NEXT
        // set linenumber on exitNext().
        // GOTO APPLY STEP
        // LABEL afterNext
        /*
         * condition
         * GOTOIF condition labelBeforeThen
         * GOTO labelAfterThen|labelBeforeElse
         * labelBeforeThen
         * ThenStmts
         * GOTO labelAfterThen|labelAfterElse
         * labelAfterThen
         * ElseStmts
         * labelAfterElse
         */
        // Patch IF true: condition
        // Patch IF true: GOTO labelBeforeThen
        // Patch IF false: GOTO labelAfterThen|labelBeforeElse
        // Patch THEN: GOTO labelAfterThen|labelAfterElse
        // IF condition is true, GOTO labelBeforeThen
        // IF condition is false, GOTO labelAfterThen|labelBeforeElse
        // Add instruction for:
        // THEN GOTO linenum | THEN linenum
        // GOTO labelAfterThen|labelAfterElse
        // Add instruction for:
        // ELSE linenum
        //
        // IF expr THEN BEGIN
        // ...
        // ELSE BEGIN
        // ...
        // END IF
        //
        //
        // expr.result
        // GOTO labelBeforeThen IF expr.result is true
        // GOTO labelAfterThen|labelBeforeElse
        // labelBeforeThen (patch GOTOIF)
        // GOTO labelAfterThen|labelAfterElse (else begin)
        // labelAfterThen
        // labelBeforeElse
        //
        // IF condition is true, GOTO labelBeforeThen
        // IF condition is false, GOTO labelAfterThen|labelBeforeElse
        // Add labelBeforeThen
        // Patch IF true: GOTO labelBeforeThen
        // GOTO labelAfterThen|labelAfterElse
        // GOTO labelAfterThen|labelAfterElse
        // Add labelAfterElse
        // Patch IF true: GOTO labelBeforeThen
        // Patch IF false: GOTO labelAfterThen|labelBeforeElse
        // Patch THEN: GOTO labelAfterThen|labelAfterElse
        // fileName, fileNumber
        // openMode, accessMode
        // lockMode, recordLen
        // fileName, fileNumber
        // openMode, accessMode
        // lockMode, recordLen
        // FileNumber, #fields
        // if fileNumber != null, skip first instruction
        // GraphicsRuntime
        public override void ExitLoadimgstmt(PuffinBasicParser.LoadimgstmtContext ctx)
        {
            AssertGraphics();
            var path = LookupInstruction(ctx.path);
            var varInstr = LookupInstruction(ctx.variable());
            Types.AssertString(ir.GetSymbolTable()[path.result].GetType().GetAtomTypeId(), GetCtxString(ctx));
            AssertVariable(ir.GetSymbolTable()[varInstr.result], GetCtxString(ctx));
            ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.LOADIMG, path.result, varInstr.result, NULL_ID);
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        //
        // Expr
        //
        //
        // Functions
        //
        // n, x$, y$
        // x$, y$
        // x$, n, m
        // x$, n
        //
        // Stmt
        //
        // struct
        // array
        // scalar data type
        // struct
        // scalar data type
        // scalar
        //PuffinBasicAtomTypeId.lookup(compCtx.var2.getText());
        // array
        // list
        // struct
        // scalar data type
        // set
        // dict
        // struct
        // scalar data type
        // struct
        // throw
        // GOTO postFuncDecl
        // LABEL FuncStart
        // Push child scope
        // Copy expr to result
        // Pop declaration scope
        // GOTO Caller
        // LABEL postFuncDecl
        // Patch GOTO postFuncDecl
        // GOTO postFuncDecl
        // LABEL FuncStart
        // Push child scope
        // scalar
        //PuffinBasicAtomTypeId.lookup(compCtx.var2.getText());
        // list
        // struct
        // array
        // scalar data type
        // set
        // dict
        // struct
        // scalar data type
        // struct
        // array
        // throw
        // Copy expr to result
        // GOTO LABEL gotoCaller
        // Pop declaration scope
        // LABEL gotoCaller
        // GOTO Caller
        // LABEL postFuncDecl
        // Patch GOTO LABEL gotoCaller
        // Patch GOTO postFuncDecl
        // Unset current UDF state
        // LABEL beforeWhile
        // expr()
        // NOT expr()
        // If expr is false, GOTO afterWend
        // GOTO LABEL beforeWhile
        // LABEL afterWend
        // Patch GOTO afterWend
        // stepCopy = step or 1 (default)
        // var=init
        // endCopy=end
        // GOTO LABEL CHECK
        // APPLY STEP
        // JUMP here from NEXT
        // Add step
        // CHECK
        // If (step >= 0 and var > end) or (step < 0 and var < end) GOTO after "next"
        // step >= 0
        // Patch GOTO LABEL Check
        // var > end
        // (step >= 0 and var > end)
        // step < 0
        // var < end
        // (step < 0 and var < end)
        // if (true) GOTO after NEXT
        // set linenumber on exitNext().
        // GOTO APPLY STEP
        // LABEL afterNext
        /*
         * condition
         * GOTOIF condition labelBeforeThen
         * GOTO labelAfterThen|labelBeforeElse
         * labelBeforeThen
         * ThenStmts
         * GOTO labelAfterThen|labelAfterElse
         * labelAfterThen
         * ElseStmts
         * labelAfterElse
         */
        // Patch IF true: condition
        // Patch IF true: GOTO labelBeforeThen
        // Patch IF false: GOTO labelAfterThen|labelBeforeElse
        // Patch THEN: GOTO labelAfterThen|labelAfterElse
        // IF condition is true, GOTO labelBeforeThen
        // IF condition is false, GOTO labelAfterThen|labelBeforeElse
        // Add instruction for:
        // THEN GOTO linenum | THEN linenum
        // GOTO labelAfterThen|labelAfterElse
        // Add instruction for:
        // ELSE linenum
        //
        // IF expr THEN BEGIN
        // ...
        // ELSE BEGIN
        // ...
        // END IF
        //
        //
        // expr.result
        // GOTO labelBeforeThen IF expr.result is true
        // GOTO labelAfterThen|labelBeforeElse
        // labelBeforeThen (patch GOTOIF)
        // GOTO labelAfterThen|labelAfterElse (else begin)
        // labelAfterThen
        // labelBeforeElse
        //
        // IF condition is true, GOTO labelBeforeThen
        // IF condition is false, GOTO labelAfterThen|labelBeforeElse
        // Add labelBeforeThen
        // Patch IF true: GOTO labelBeforeThen
        // GOTO labelAfterThen|labelAfterElse
        // GOTO labelAfterThen|labelAfterElse
        // Add labelAfterElse
        // Patch IF true: GOTO labelBeforeThen
        // Patch IF false: GOTO labelAfterThen|labelBeforeElse
        // Patch THEN: GOTO labelAfterThen|labelAfterElse
        // fileName, fileNumber
        // openMode, accessMode
        // lockMode, recordLen
        // fileName, fileNumber
        // openMode, accessMode
        // lockMode, recordLen
        // FileNumber, #fields
        // if fileNumber != null, skip first instruction
        // GraphicsRuntime
        public override void ExitSaveimgstmt(PuffinBasicParser.SaveimgstmtContext ctx)
        {
            AssertGraphics();
            var path = LookupInstruction(ctx.path);
            var varInstr = LookupInstruction(ctx.variable());
            Types.AssertString(ir.GetSymbolTable()[path.result].GetType().GetAtomTypeId(), GetCtxString(ctx));
            AssertVariable(ir.GetSymbolTable()[varInstr.result], GetCtxString(ctx));
            ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.SAVEIMG, path.result, varInstr.result, NULL_ID);
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        //
        // Expr
        //
        //
        // Functions
        //
        // n, x$, y$
        // x$, y$
        // x$, n, m
        // x$, n
        //
        // Stmt
        //
        // struct
        // array
        // scalar data type
        // struct
        // scalar data type
        // scalar
        //PuffinBasicAtomTypeId.lookup(compCtx.var2.getText());
        // array
        // list
        // struct
        // scalar data type
        // set
        // dict
        // struct
        // scalar data type
        // struct
        // throw
        // GOTO postFuncDecl
        // LABEL FuncStart
        // Push child scope
        // Copy expr to result
        // Pop declaration scope
        // GOTO Caller
        // LABEL postFuncDecl
        // Patch GOTO postFuncDecl
        // GOTO postFuncDecl
        // LABEL FuncStart
        // Push child scope
        // scalar
        //PuffinBasicAtomTypeId.lookup(compCtx.var2.getText());
        // list
        // struct
        // array
        // scalar data type
        // set
        // dict
        // struct
        // scalar data type
        // struct
        // array
        // throw
        // Copy expr to result
        // GOTO LABEL gotoCaller
        // Pop declaration scope
        // LABEL gotoCaller
        // GOTO Caller
        // LABEL postFuncDecl
        // Patch GOTO LABEL gotoCaller
        // Patch GOTO postFuncDecl
        // Unset current UDF state
        // LABEL beforeWhile
        // expr()
        // NOT expr()
        // If expr is false, GOTO afterWend
        // GOTO LABEL beforeWhile
        // LABEL afterWend
        // Patch GOTO afterWend
        // stepCopy = step or 1 (default)
        // var=init
        // endCopy=end
        // GOTO LABEL CHECK
        // APPLY STEP
        // JUMP here from NEXT
        // Add step
        // CHECK
        // If (step >= 0 and var > end) or (step < 0 and var < end) GOTO after "next"
        // step >= 0
        // Patch GOTO LABEL Check
        // var > end
        // (step >= 0 and var > end)
        // step < 0
        // var < end
        // (step < 0 and var < end)
        // if (true) GOTO after NEXT
        // set linenumber on exitNext().
        // GOTO APPLY STEP
        // LABEL afterNext
        /*
         * condition
         * GOTOIF condition labelBeforeThen
         * GOTO labelAfterThen|labelBeforeElse
         * labelBeforeThen
         * ThenStmts
         * GOTO labelAfterThen|labelAfterElse
         * labelAfterThen
         * ElseStmts
         * labelAfterElse
         */
        // Patch IF true: condition
        // Patch IF true: GOTO labelBeforeThen
        // Patch IF false: GOTO labelAfterThen|labelBeforeElse
        // Patch THEN: GOTO labelAfterThen|labelAfterElse
        // IF condition is true, GOTO labelBeforeThen
        // IF condition is false, GOTO labelAfterThen|labelBeforeElse
        // Add instruction for:
        // THEN GOTO linenum | THEN linenum
        // GOTO labelAfterThen|labelAfterElse
        // Add instruction for:
        // ELSE linenum
        //
        // IF expr THEN BEGIN
        // ...
        // ELSE BEGIN
        // ...
        // END IF
        //
        //
        // expr.result
        // GOTO labelBeforeThen IF expr.result is true
        // GOTO labelAfterThen|labelBeforeElse
        // labelBeforeThen (patch GOTOIF)
        // GOTO labelAfterThen|labelAfterElse (else begin)
        // labelAfterThen
        // labelBeforeElse
        //
        // IF condition is true, GOTO labelBeforeThen
        // IF condition is false, GOTO labelAfterThen|labelBeforeElse
        // Add labelBeforeThen
        // Patch IF true: GOTO labelBeforeThen
        // GOTO labelAfterThen|labelAfterElse
        // GOTO labelAfterThen|labelAfterElse
        // Add labelAfterElse
        // Patch IF true: GOTO labelBeforeThen
        // Patch IF false: GOTO labelAfterThen|labelBeforeElse
        // Patch THEN: GOTO labelAfterThen|labelAfterElse
        // fileName, fileNumber
        // openMode, accessMode
        // lockMode, recordLen
        // fileName, fileNumber
        // openMode, accessMode
        // lockMode, recordLen
        // FileNumber, #fields
        // if fileNumber != null, skip first instruction
        // GraphicsRuntime
        public override void ExitClsstmt(PuffinBasicParser.ClsstmtContext ctx)
        {
            AssertGraphics();
            ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.CLS, NULL_ID, NULL_ID, NULL_ID);
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        //
        // Expr
        //
        //
        // Functions
        //
        // n, x$, y$
        // x$, y$
        // x$, n, m
        // x$, n
        //
        // Stmt
        //
        // struct
        // array
        // scalar data type
        // struct
        // scalar data type
        // scalar
        //PuffinBasicAtomTypeId.lookup(compCtx.var2.getText());
        // array
        // list
        // struct
        // scalar data type
        // set
        // dict
        // struct
        // scalar data type
        // struct
        // throw
        // GOTO postFuncDecl
        // LABEL FuncStart
        // Push child scope
        // Copy expr to result
        // Pop declaration scope
        // GOTO Caller
        // LABEL postFuncDecl
        // Patch GOTO postFuncDecl
        // GOTO postFuncDecl
        // LABEL FuncStart
        // Push child scope
        // scalar
        //PuffinBasicAtomTypeId.lookup(compCtx.var2.getText());
        // list
        // struct
        // array
        // scalar data type
        // set
        // dict
        // struct
        // scalar data type
        // struct
        // array
        // throw
        // Copy expr to result
        // GOTO LABEL gotoCaller
        // Pop declaration scope
        // LABEL gotoCaller
        // GOTO Caller
        // LABEL postFuncDecl
        // Patch GOTO LABEL gotoCaller
        // Patch GOTO postFuncDecl
        // Unset current UDF state
        // LABEL beforeWhile
        // expr()
        // NOT expr()
        // If expr is false, GOTO afterWend
        // GOTO LABEL beforeWhile
        // LABEL afterWend
        // Patch GOTO afterWend
        // stepCopy = step or 1 (default)
        // var=init
        // endCopy=end
        // GOTO LABEL CHECK
        // APPLY STEP
        // JUMP here from NEXT
        // Add step
        // CHECK
        // If (step >= 0 and var > end) or (step < 0 and var < end) GOTO after "next"
        // step >= 0
        // Patch GOTO LABEL Check
        // var > end
        // (step >= 0 and var > end)
        // step < 0
        // var < end
        // (step < 0 and var < end)
        // if (true) GOTO after NEXT
        // set linenumber on exitNext().
        // GOTO APPLY STEP
        // LABEL afterNext
        /*
         * condition
         * GOTOIF condition labelBeforeThen
         * GOTO labelAfterThen|labelBeforeElse
         * labelBeforeThen
         * ThenStmts
         * GOTO labelAfterThen|labelAfterElse
         * labelAfterThen
         * ElseStmts
         * labelAfterElse
         */
        // Patch IF true: condition
        // Patch IF true: GOTO labelBeforeThen
        // Patch IF false: GOTO labelAfterThen|labelBeforeElse
        // Patch THEN: GOTO labelAfterThen|labelAfterElse
        // IF condition is true, GOTO labelBeforeThen
        // IF condition is false, GOTO labelAfterThen|labelBeforeElse
        // Add instruction for:
        // THEN GOTO linenum | THEN linenum
        // GOTO labelAfterThen|labelAfterElse
        // Add instruction for:
        // ELSE linenum
        //
        // IF expr THEN BEGIN
        // ...
        // ELSE BEGIN
        // ...
        // END IF
        //
        //
        // expr.result
        // GOTO labelBeforeThen IF expr.result is true
        // GOTO labelAfterThen|labelBeforeElse
        // labelBeforeThen (patch GOTOIF)
        // GOTO labelAfterThen|labelAfterElse (else begin)
        // labelAfterThen
        // labelBeforeElse
        //
        // IF condition is true, GOTO labelBeforeThen
        // IF condition is false, GOTO labelAfterThen|labelBeforeElse
        // Add labelBeforeThen
        // Patch IF true: GOTO labelBeforeThen
        // GOTO labelAfterThen|labelAfterElse
        // GOTO labelAfterThen|labelAfterElse
        // Add labelAfterElse
        // Patch IF true: GOTO labelBeforeThen
        // Patch IF false: GOTO labelAfterThen|labelBeforeElse
        // Patch THEN: GOTO labelAfterThen|labelAfterElse
        // fileName, fileNumber
        // openMode, accessMode
        // lockMode, recordLen
        // fileName, fileNumber
        // openMode, accessMode
        // lockMode, recordLen
        // FileNumber, #fields
        // if fileNumber != null, skip first instruction
        // GraphicsRuntime
        public override void ExitLoadwavstmt(PuffinBasicParser.LoadwavstmtContext ctx)
        {
            AssertGraphics();
            var path = LookupInstruction(ctx.path);
            var varInstr = LookupInstruction(ctx.variable());
            Types.AssertString(ir.GetSymbolTable()[path.result].GetType().GetAtomTypeId(), GetCtxString(ctx));
            AssertVariable(ir.GetSymbolTable()[varInstr.result], GetCtxString(ctx));
            ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.LOADWAV, path.result, varInstr.result, NULL_ID);
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        //
        // Expr
        //
        //
        // Functions
        //
        // n, x$, y$
        // x$, y$
        // x$, n, m
        // x$, n
        //
        // Stmt
        //
        // struct
        // array
        // scalar data type
        // struct
        // scalar data type
        // scalar
        //PuffinBasicAtomTypeId.lookup(compCtx.var2.getText());
        // array
        // list
        // struct
        // scalar data type
        // set
        // dict
        // struct
        // scalar data type
        // struct
        // throw
        // GOTO postFuncDecl
        // LABEL FuncStart
        // Push child scope
        // Copy expr to result
        // Pop declaration scope
        // GOTO Caller
        // LABEL postFuncDecl
        // Patch GOTO postFuncDecl
        // GOTO postFuncDecl
        // LABEL FuncStart
        // Push child scope
        // scalar
        //PuffinBasicAtomTypeId.lookup(compCtx.var2.getText());
        // list
        // struct
        // array
        // scalar data type
        // set
        // dict
        // struct
        // scalar data type
        // struct
        // array
        // throw
        // Copy expr to result
        // GOTO LABEL gotoCaller
        // Pop declaration scope
        // LABEL gotoCaller
        // GOTO Caller
        // LABEL postFuncDecl
        // Patch GOTO LABEL gotoCaller
        // Patch GOTO postFuncDecl
        // Unset current UDF state
        // LABEL beforeWhile
        // expr()
        // NOT expr()
        // If expr is false, GOTO afterWend
        // GOTO LABEL beforeWhile
        // LABEL afterWend
        // Patch GOTO afterWend
        // stepCopy = step or 1 (default)
        // var=init
        // endCopy=end
        // GOTO LABEL CHECK
        // APPLY STEP
        // JUMP here from NEXT
        // Add step
        // CHECK
        // If (step >= 0 and var > end) or (step < 0 and var < end) GOTO after "next"
        // step >= 0
        // Patch GOTO LABEL Check
        // var > end
        // (step >= 0 and var > end)
        // step < 0
        // var < end
        // (step < 0 and var < end)
        // if (true) GOTO after NEXT
        // set linenumber on exitNext().
        // GOTO APPLY STEP
        // LABEL afterNext
        /*
         * condition
         * GOTOIF condition labelBeforeThen
         * GOTO labelAfterThen|labelBeforeElse
         * labelBeforeThen
         * ThenStmts
         * GOTO labelAfterThen|labelAfterElse
         * labelAfterThen
         * ElseStmts
         * labelAfterElse
         */
        // Patch IF true: condition
        // Patch IF true: GOTO labelBeforeThen
        // Patch IF false: GOTO labelAfterThen|labelBeforeElse
        // Patch THEN: GOTO labelAfterThen|labelAfterElse
        // IF condition is true, GOTO labelBeforeThen
        // IF condition is false, GOTO labelAfterThen|labelBeforeElse
        // Add instruction for:
        // THEN GOTO linenum | THEN linenum
        // GOTO labelAfterThen|labelAfterElse
        // Add instruction for:
        // ELSE linenum
        //
        // IF expr THEN BEGIN
        // ...
        // ELSE BEGIN
        // ...
        // END IF
        //
        //
        // expr.result
        // GOTO labelBeforeThen IF expr.result is true
        // GOTO labelAfterThen|labelBeforeElse
        // labelBeforeThen (patch GOTOIF)
        // GOTO labelAfterThen|labelAfterElse (else begin)
        // labelAfterThen
        // labelBeforeElse
        //
        // IF condition is true, GOTO labelBeforeThen
        // IF condition is false, GOTO labelAfterThen|labelBeforeElse
        // Add labelBeforeThen
        // Patch IF true: GOTO labelBeforeThen
        // GOTO labelAfterThen|labelAfterElse
        // GOTO labelAfterThen|labelAfterElse
        // Add labelAfterElse
        // Patch IF true: GOTO labelBeforeThen
        // Patch IF false: GOTO labelAfterThen|labelBeforeElse
        // Patch THEN: GOTO labelAfterThen|labelAfterElse
        // fileName, fileNumber
        // openMode, accessMode
        // lockMode, recordLen
        // fileName, fileNumber
        // openMode, accessMode
        // lockMode, recordLen
        // FileNumber, #fields
        // if fileNumber != null, skip first instruction
        // GraphicsRuntime
        public override void ExitPlaywavstmt(PuffinBasicParser.PlaywavstmtContext ctx)
        {
            AssertGraphics();
            var varInstr = LookupInstruction(ctx.variable());
            AssertVariable(ir.GetSymbolTable()[varInstr.result], GetCtxString(ctx));
            ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.PLAYWAV, varInstr.result, NULL_ID, NULL_ID);
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        //
        // Expr
        //
        //
        // Functions
        //
        // n, x$, y$
        // x$, y$
        // x$, n, m
        // x$, n
        //
        // Stmt
        //
        // struct
        // array
        // scalar data type
        // struct
        // scalar data type
        // scalar
        //PuffinBasicAtomTypeId.lookup(compCtx.var2.getText());
        // array
        // list
        // struct
        // scalar data type
        // set
        // dict
        // struct
        // scalar data type
        // struct
        // throw
        // GOTO postFuncDecl
        // LABEL FuncStart
        // Push child scope
        // Copy expr to result
        // Pop declaration scope
        // GOTO Caller
        // LABEL postFuncDecl
        // Patch GOTO postFuncDecl
        // GOTO postFuncDecl
        // LABEL FuncStart
        // Push child scope
        // scalar
        //PuffinBasicAtomTypeId.lookup(compCtx.var2.getText());
        // list
        // struct
        // array
        // scalar data type
        // set
        // dict
        // struct
        // scalar data type
        // struct
        // array
        // throw
        // Copy expr to result
        // GOTO LABEL gotoCaller
        // Pop declaration scope
        // LABEL gotoCaller
        // GOTO Caller
        // LABEL postFuncDecl
        // Patch GOTO LABEL gotoCaller
        // Patch GOTO postFuncDecl
        // Unset current UDF state
        // LABEL beforeWhile
        // expr()
        // NOT expr()
        // If expr is false, GOTO afterWend
        // GOTO LABEL beforeWhile
        // LABEL afterWend
        // Patch GOTO afterWend
        // stepCopy = step or 1 (default)
        // var=init
        // endCopy=end
        // GOTO LABEL CHECK
        // APPLY STEP
        // JUMP here from NEXT
        // Add step
        // CHECK
        // If (step >= 0 and var > end) or (step < 0 and var < end) GOTO after "next"
        // step >= 0
        // Patch GOTO LABEL Check
        // var > end
        // (step >= 0 and var > end)
        // step < 0
        // var < end
        // (step < 0 and var < end)
        // if (true) GOTO after NEXT
        // set linenumber on exitNext().
        // GOTO APPLY STEP
        // LABEL afterNext
        /*
         * condition
         * GOTOIF condition labelBeforeThen
         * GOTO labelAfterThen|labelBeforeElse
         * labelBeforeThen
         * ThenStmts
         * GOTO labelAfterThen|labelAfterElse
         * labelAfterThen
         * ElseStmts
         * labelAfterElse
         */
        // Patch IF true: condition
        // Patch IF true: GOTO labelBeforeThen
        // Patch IF false: GOTO labelAfterThen|labelBeforeElse
        // Patch THEN: GOTO labelAfterThen|labelAfterElse
        // IF condition is true, GOTO labelBeforeThen
        // IF condition is false, GOTO labelAfterThen|labelBeforeElse
        // Add instruction for:
        // THEN GOTO linenum | THEN linenum
        // GOTO labelAfterThen|labelAfterElse
        // Add instruction for:
        // ELSE linenum
        //
        // IF expr THEN BEGIN
        // ...
        // ELSE BEGIN
        // ...
        // END IF
        //
        //
        // expr.result
        // GOTO labelBeforeThen IF expr.result is true
        // GOTO labelAfterThen|labelBeforeElse
        // labelBeforeThen (patch GOTOIF)
        // GOTO labelAfterThen|labelAfterElse (else begin)
        // labelAfterThen
        // labelBeforeElse
        //
        // IF condition is true, GOTO labelBeforeThen
        // IF condition is false, GOTO labelAfterThen|labelBeforeElse
        // Add labelBeforeThen
        // Patch IF true: GOTO labelBeforeThen
        // GOTO labelAfterThen|labelAfterElse
        // GOTO labelAfterThen|labelAfterElse
        // Add labelAfterElse
        // Patch IF true: GOTO labelBeforeThen
        // Patch IF false: GOTO labelAfterThen|labelBeforeElse
        // Patch THEN: GOTO labelAfterThen|labelAfterElse
        // fileName, fileNumber
        // openMode, accessMode
        // lockMode, recordLen
        // fileName, fileNumber
        // openMode, accessMode
        // lockMode, recordLen
        // FileNumber, #fields
        // if fileNumber != null, skip first instruction
        // GraphicsRuntime
        public override void ExitStopwavstmt(PuffinBasicParser.StopwavstmtContext ctx)
        {
            AssertGraphics();
            var varInstr = LookupInstruction(ctx.variable());
            AssertVariable(ir.GetSymbolTable()[varInstr.result], GetCtxString(ctx));
            ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.STOPWAV, varInstr.result, NULL_ID, NULL_ID);
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        //
        // Expr
        //
        //
        // Functions
        //
        // n, x$, y$
        // x$, y$
        // x$, n, m
        // x$, n
        //
        // Stmt
        //
        // struct
        // array
        // scalar data type
        // struct
        // scalar data type
        // scalar
        //PuffinBasicAtomTypeId.lookup(compCtx.var2.getText());
        // array
        // list
        // struct
        // scalar data type
        // set
        // dict
        // struct
        // scalar data type
        // struct
        // throw
        // GOTO postFuncDecl
        // LABEL FuncStart
        // Push child scope
        // Copy expr to result
        // Pop declaration scope
        // GOTO Caller
        // LABEL postFuncDecl
        // Patch GOTO postFuncDecl
        // GOTO postFuncDecl
        // LABEL FuncStart
        // Push child scope
        // scalar
        //PuffinBasicAtomTypeId.lookup(compCtx.var2.getText());
        // list
        // struct
        // array
        // scalar data type
        // set
        // dict
        // struct
        // scalar data type
        // struct
        // array
        // throw
        // Copy expr to result
        // GOTO LABEL gotoCaller
        // Pop declaration scope
        // LABEL gotoCaller
        // GOTO Caller
        // LABEL postFuncDecl
        // Patch GOTO LABEL gotoCaller
        // Patch GOTO postFuncDecl
        // Unset current UDF state
        // LABEL beforeWhile
        // expr()
        // NOT expr()
        // If expr is false, GOTO afterWend
        // GOTO LABEL beforeWhile
        // LABEL afterWend
        // Patch GOTO afterWend
        // stepCopy = step or 1 (default)
        // var=init
        // endCopy=end
        // GOTO LABEL CHECK
        // APPLY STEP
        // JUMP here from NEXT
        // Add step
        // CHECK
        // If (step >= 0 and var > end) or (step < 0 and var < end) GOTO after "next"
        // step >= 0
        // Patch GOTO LABEL Check
        // var > end
        // (step >= 0 and var > end)
        // step < 0
        // var < end
        // (step < 0 and var < end)
        // if (true) GOTO after NEXT
        // set linenumber on exitNext().
        // GOTO APPLY STEP
        // LABEL afterNext
        /*
         * condition
         * GOTOIF condition labelBeforeThen
         * GOTO labelAfterThen|labelBeforeElse
         * labelBeforeThen
         * ThenStmts
         * GOTO labelAfterThen|labelAfterElse
         * labelAfterThen
         * ElseStmts
         * labelAfterElse
         */
        // Patch IF true: condition
        // Patch IF true: GOTO labelBeforeThen
        // Patch IF false: GOTO labelAfterThen|labelBeforeElse
        // Patch THEN: GOTO labelAfterThen|labelAfterElse
        // IF condition is true, GOTO labelBeforeThen
        // IF condition is false, GOTO labelAfterThen|labelBeforeElse
        // Add instruction for:
        // THEN GOTO linenum | THEN linenum
        // GOTO labelAfterThen|labelAfterElse
        // Add instruction for:
        // ELSE linenum
        //
        // IF expr THEN BEGIN
        // ...
        // ELSE BEGIN
        // ...
        // END IF
        //
        //
        // expr.result
        // GOTO labelBeforeThen IF expr.result is true
        // GOTO labelAfterThen|labelBeforeElse
        // labelBeforeThen (patch GOTOIF)
        // GOTO labelAfterThen|labelAfterElse (else begin)
        // labelAfterThen
        // labelBeforeElse
        //
        // IF condition is true, GOTO labelBeforeThen
        // IF condition is false, GOTO labelAfterThen|labelBeforeElse
        // Add labelBeforeThen
        // Patch IF true: GOTO labelBeforeThen
        // GOTO labelAfterThen|labelAfterElse
        // GOTO labelAfterThen|labelAfterElse
        // Add labelAfterElse
        // Patch IF true: GOTO labelBeforeThen
        // Patch IF false: GOTO labelAfterThen|labelBeforeElse
        // Patch THEN: GOTO labelAfterThen|labelAfterElse
        // fileName, fileNumber
        // openMode, accessMode
        // lockMode, recordLen
        // fileName, fileNumber
        // openMode, accessMode
        // lockMode, recordLen
        // FileNumber, #fields
        // if fileNumber != null, skip first instruction
        // GraphicsRuntime
        public override void ExitLoopwavstmt(PuffinBasicParser.LoopwavstmtContext ctx)
        {
            AssertGraphics();
            var varInstr = LookupInstruction(ctx.variable());
            AssertVariable(ir.GetSymbolTable()[varInstr.result], GetCtxString(ctx));
            ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.LOOPWAV, varInstr.result, NULL_ID, NULL_ID);
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        //
        // Expr
        //
        //
        // Functions
        //
        // n, x$, y$
        // x$, y$
        // x$, n, m
        // x$, n
        //
        // Stmt
        //
        // struct
        // array
        // scalar data type
        // struct
        // scalar data type
        // scalar
        //PuffinBasicAtomTypeId.lookup(compCtx.var2.getText());
        // array
        // list
        // struct
        // scalar data type
        // set
        // dict
        // struct
        // scalar data type
        // struct
        // throw
        // GOTO postFuncDecl
        // LABEL FuncStart
        // Push child scope
        // Copy expr to result
        // Pop declaration scope
        // GOTO Caller
        // LABEL postFuncDecl
        // Patch GOTO postFuncDecl
        // GOTO postFuncDecl
        // LABEL FuncStart
        // Push child scope
        // scalar
        //PuffinBasicAtomTypeId.lookup(compCtx.var2.getText());
        // list
        // struct
        // array
        // scalar data type
        // set
        // dict
        // struct
        // scalar data type
        // struct
        // array
        // throw
        // Copy expr to result
        // GOTO LABEL gotoCaller
        // Pop declaration scope
        // LABEL gotoCaller
        // GOTO Caller
        // LABEL postFuncDecl
        // Patch GOTO LABEL gotoCaller
        // Patch GOTO postFuncDecl
        // Unset current UDF state
        // LABEL beforeWhile
        // expr()
        // NOT expr()
        // If expr is false, GOTO afterWend
        // GOTO LABEL beforeWhile
        // LABEL afterWend
        // Patch GOTO afterWend
        // stepCopy = step or 1 (default)
        // var=init
        // endCopy=end
        // GOTO LABEL CHECK
        // APPLY STEP
        // JUMP here from NEXT
        // Add step
        // CHECK
        // If (step >= 0 and var > end) or (step < 0 and var < end) GOTO after "next"
        // step >= 0
        // Patch GOTO LABEL Check
        // var > end
        // (step >= 0 and var > end)
        // step < 0
        // var < end
        // (step < 0 and var < end)
        // if (true) GOTO after NEXT
        // set linenumber on exitNext().
        // GOTO APPLY STEP
        // LABEL afterNext
        /*
         * condition
         * GOTOIF condition labelBeforeThen
         * GOTO labelAfterThen|labelBeforeElse
         * labelBeforeThen
         * ThenStmts
         * GOTO labelAfterThen|labelAfterElse
         * labelAfterThen
         * ElseStmts
         * labelAfterElse
         */
        // Patch IF true: condition
        // Patch IF true: GOTO labelBeforeThen
        // Patch IF false: GOTO labelAfterThen|labelBeforeElse
        // Patch THEN: GOTO labelAfterThen|labelAfterElse
        // IF condition is true, GOTO labelBeforeThen
        // IF condition is false, GOTO labelAfterThen|labelBeforeElse
        // Add instruction for:
        // THEN GOTO linenum | THEN linenum
        // GOTO labelAfterThen|labelAfterElse
        // Add instruction for:
        // ELSE linenum
        //
        // IF expr THEN BEGIN
        // ...
        // ELSE BEGIN
        // ...
        // END IF
        //
        //
        // expr.result
        // GOTO labelBeforeThen IF expr.result is true
        // GOTO labelAfterThen|labelBeforeElse
        // labelBeforeThen (patch GOTOIF)
        // GOTO labelAfterThen|labelAfterElse (else begin)
        // labelAfterThen
        // labelBeforeElse
        //
        // IF condition is true, GOTO labelBeforeThen
        // IF condition is false, GOTO labelAfterThen|labelBeforeElse
        // Add labelBeforeThen
        // Patch IF true: GOTO labelBeforeThen
        // GOTO labelAfterThen|labelAfterElse
        // GOTO labelAfterThen|labelAfterElse
        // Add labelAfterElse
        // Patch IF true: GOTO labelBeforeThen
        // Patch IF false: GOTO labelAfterThen|labelBeforeElse
        // Patch THEN: GOTO labelAfterThen|labelAfterElse
        // fileName, fileNumber
        // openMode, accessMode
        // lockMode, recordLen
        // fileName, fileNumber
        // openMode, accessMode
        // lockMode, recordLen
        // FileNumber, #fields
        // if fileNumber != null, skip first instruction
        // GraphicsRuntime
        public override void ExitSleepstmt(PuffinBasicParser.SleepstmtContext ctx)
        {
            var millis = LookupInstruction(ctx.expr());
            Types.AssertNumeric(ir.GetSymbolTable()[millis.result].GetType().GetAtomTypeId(), GetCtxString(ctx));
            ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.SLEEP, millis.result, NULL_ID, NULL_ID);
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        //
        // Expr
        //
        //
        // Functions
        //
        // n, x$, y$
        // x$, y$
        // x$, n, m
        // x$, n
        //
        // Stmt
        //
        // struct
        // array
        // scalar data type
        // struct
        // scalar data type
        // scalar
        //PuffinBasicAtomTypeId.lookup(compCtx.var2.getText());
        // array
        // list
        // struct
        // scalar data type
        // set
        // dict
        // struct
        // scalar data type
        // struct
        // throw
        // GOTO postFuncDecl
        // LABEL FuncStart
        // Push child scope
        // Copy expr to result
        // Pop declaration scope
        // GOTO Caller
        // LABEL postFuncDecl
        // Patch GOTO postFuncDecl
        // GOTO postFuncDecl
        // LABEL FuncStart
        // Push child scope
        // scalar
        //PuffinBasicAtomTypeId.lookup(compCtx.var2.getText());
        // list
        // struct
        // array
        // scalar data type
        // set
        // dict
        // struct
        // scalar data type
        // struct
        // array
        // throw
        // Copy expr to result
        // GOTO LABEL gotoCaller
        // Pop declaration scope
        // LABEL gotoCaller
        // GOTO Caller
        // LABEL postFuncDecl
        // Patch GOTO LABEL gotoCaller
        // Patch GOTO postFuncDecl
        // Unset current UDF state
        // LABEL beforeWhile
        // expr()
        // NOT expr()
        // If expr is false, GOTO afterWend
        // GOTO LABEL beforeWhile
        // LABEL afterWend
        // Patch GOTO afterWend
        // stepCopy = step or 1 (default)
        // var=init
        // endCopy=end
        // GOTO LABEL CHECK
        // APPLY STEP
        // JUMP here from NEXT
        // Add step
        // CHECK
        // If (step >= 0 and var > end) or (step < 0 and var < end) GOTO after "next"
        // step >= 0
        // Patch GOTO LABEL Check
        // var > end
        // (step >= 0 and var > end)
        // step < 0
        // var < end
        // (step < 0 and var < end)
        // if (true) GOTO after NEXT
        // set linenumber on exitNext().
        // GOTO APPLY STEP
        // LABEL afterNext
        /*
         * condition
         * GOTOIF condition labelBeforeThen
         * GOTO labelAfterThen|labelBeforeElse
         * labelBeforeThen
         * ThenStmts
         * GOTO labelAfterThen|labelAfterElse
         * labelAfterThen
         * ElseStmts
         * labelAfterElse
         */
        // Patch IF true: condition
        // Patch IF true: GOTO labelBeforeThen
        // Patch IF false: GOTO labelAfterThen|labelBeforeElse
        // Patch THEN: GOTO labelAfterThen|labelAfterElse
        // IF condition is true, GOTO labelBeforeThen
        // IF condition is false, GOTO labelAfterThen|labelBeforeElse
        // Add instruction for:
        // THEN GOTO linenum | THEN linenum
        // GOTO labelAfterThen|labelAfterElse
        // Add instruction for:
        // ELSE linenum
        //
        // IF expr THEN BEGIN
        // ...
        // ELSE BEGIN
        // ...
        // END IF
        //
        //
        // expr.result
        // GOTO labelBeforeThen IF expr.result is true
        // GOTO labelAfterThen|labelBeforeElse
        // labelBeforeThen (patch GOTOIF)
        // GOTO labelAfterThen|labelAfterElse (else begin)
        // labelAfterThen
        // labelBeforeElse
        //
        // IF condition is true, GOTO labelBeforeThen
        // IF condition is false, GOTO labelAfterThen|labelBeforeElse
        // Add labelBeforeThen
        // Patch IF true: GOTO labelBeforeThen
        // GOTO labelAfterThen|labelAfterElse
        // GOTO labelAfterThen|labelAfterElse
        // Add labelAfterElse
        // Patch IF true: GOTO labelBeforeThen
        // Patch IF false: GOTO labelAfterThen|labelBeforeElse
        // Patch THEN: GOTO labelAfterThen|labelAfterElse
        // fileName, fileNumber
        // openMode, accessMode
        // lockMode, recordLen
        // fileName, fileNumber
        // openMode, accessMode
        // lockMode, recordLen
        // FileNumber, #fields
        // if fileNumber != null, skip first instruction
        // GraphicsRuntime
        public override void ExitBeepstmt(PuffinBasicParser.BeepstmtContext ctx)
        {
            AssertGraphics();
            ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.BEEP, NULL_ID, NULL_ID, NULL_ID);
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        //
        // Expr
        //
        //
        // Functions
        //
        // n, x$, y$
        // x$, y$
        // x$, n, m
        // x$, n
        //
        // Stmt
        //
        // struct
        // array
        // scalar data type
        // struct
        // scalar data type
        // scalar
        //PuffinBasicAtomTypeId.lookup(compCtx.var2.getText());
        // array
        // list
        // struct
        // scalar data type
        // set
        // dict
        // struct
        // scalar data type
        // struct
        // throw
        // GOTO postFuncDecl
        // LABEL FuncStart
        // Push child scope
        // Copy expr to result
        // Pop declaration scope
        // GOTO Caller
        // LABEL postFuncDecl
        // Patch GOTO postFuncDecl
        // GOTO postFuncDecl
        // LABEL FuncStart
        // Push child scope
        // scalar
        //PuffinBasicAtomTypeId.lookup(compCtx.var2.getText());
        // list
        // struct
        // array
        // scalar data type
        // set
        // dict
        // struct
        // scalar data type
        // struct
        // array
        // throw
        // Copy expr to result
        // GOTO LABEL gotoCaller
        // Pop declaration scope
        // LABEL gotoCaller
        // GOTO Caller
        // LABEL postFuncDecl
        // Patch GOTO LABEL gotoCaller
        // Patch GOTO postFuncDecl
        // Unset current UDF state
        // LABEL beforeWhile
        // expr()
        // NOT expr()
        // If expr is false, GOTO afterWend
        // GOTO LABEL beforeWhile
        // LABEL afterWend
        // Patch GOTO afterWend
        // stepCopy = step or 1 (default)
        // var=init
        // endCopy=end
        // GOTO LABEL CHECK
        // APPLY STEP
        // JUMP here from NEXT
        // Add step
        // CHECK
        // If (step >= 0 and var > end) or (step < 0 and var < end) GOTO after "next"
        // step >= 0
        // Patch GOTO LABEL Check
        // var > end
        // (step >= 0 and var > end)
        // step < 0
        // var < end
        // (step < 0 and var < end)
        // if (true) GOTO after NEXT
        // set linenumber on exitNext().
        // GOTO APPLY STEP
        // LABEL afterNext
        /*
         * condition
         * GOTOIF condition labelBeforeThen
         * GOTO labelAfterThen|labelBeforeElse
         * labelBeforeThen
         * ThenStmts
         * GOTO labelAfterThen|labelAfterElse
         * labelAfterThen
         * ElseStmts
         * labelAfterElse
         */
        // Patch IF true: condition
        // Patch IF true: GOTO labelBeforeThen
        // Patch IF false: GOTO labelAfterThen|labelBeforeElse
        // Patch THEN: GOTO labelAfterThen|labelAfterElse
        // IF condition is true, GOTO labelBeforeThen
        // IF condition is false, GOTO labelAfterThen|labelBeforeElse
        // Add instruction for:
        // THEN GOTO linenum | THEN linenum
        // GOTO labelAfterThen|labelAfterElse
        // Add instruction for:
        // ELSE linenum
        //
        // IF expr THEN BEGIN
        // ...
        // ELSE BEGIN
        // ...
        // END IF
        //
        //
        // expr.result
        // GOTO labelBeforeThen IF expr.result is true
        // GOTO labelAfterThen|labelBeforeElse
        // labelBeforeThen (patch GOTOIF)
        // GOTO labelAfterThen|labelAfterElse (else begin)
        // labelAfterThen
        // labelBeforeElse
        //
        // IF condition is true, GOTO labelBeforeThen
        // IF condition is false, GOTO labelAfterThen|labelBeforeElse
        // Add labelBeforeThen
        // Patch IF true: GOTO labelBeforeThen
        // GOTO labelAfterThen|labelAfterElse
        // GOTO labelAfterThen|labelAfterElse
        // Add labelAfterElse
        // Patch IF true: GOTO labelBeforeThen
        // Patch IF false: GOTO labelAfterThen|labelBeforeElse
        // Patch THEN: GOTO labelAfterThen|labelAfterElse
        // fileName, fileNumber
        // openMode, accessMode
        // lockMode, recordLen
        // fileName, fileNumber
        // openMode, accessMode
        // lockMode, recordLen
        // FileNumber, #fields
        // if fileNumber != null, skip first instruction
        // GraphicsRuntime
        public override void ExitArray1dsortstmt(PuffinBasicParser.Array1dsortstmtContext ctx)
        {
            var var1Instr = GetArray1dVariableInstruction(ctx, ctx.variable(), false);
            ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.ARRAY1DSORT, var1Instr.result, NULL_ID, NULL_ID);
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        //
        // Expr
        //
        //
        // Functions
        //
        // n, x$, y$
        // x$, y$
        // x$, n, m
        // x$, n
        //
        // Stmt
        //
        // struct
        // array
        // scalar data type
        // struct
        // scalar data type
        // scalar
        //PuffinBasicAtomTypeId.lookup(compCtx.var2.getText());
        // array
        // list
        // struct
        // scalar data type
        // set
        // dict
        // struct
        // scalar data type
        // struct
        // throw
        // GOTO postFuncDecl
        // LABEL FuncStart
        // Push child scope
        // Copy expr to result
        // Pop declaration scope
        // GOTO Caller
        // LABEL postFuncDecl
        // Patch GOTO postFuncDecl
        // GOTO postFuncDecl
        // LABEL FuncStart
        // Push child scope
        // scalar
        //PuffinBasicAtomTypeId.lookup(compCtx.var2.getText());
        // list
        // struct
        // array
        // scalar data type
        // set
        // dict
        // struct
        // scalar data type
        // struct
        // array
        // throw
        // Copy expr to result
        // GOTO LABEL gotoCaller
        // Pop declaration scope
        // LABEL gotoCaller
        // GOTO Caller
        // LABEL postFuncDecl
        // Patch GOTO LABEL gotoCaller
        // Patch GOTO postFuncDecl
        // Unset current UDF state
        // LABEL beforeWhile
        // expr()
        // NOT expr()
        // If expr is false, GOTO afterWend
        // GOTO LABEL beforeWhile
        // LABEL afterWend
        // Patch GOTO afterWend
        // stepCopy = step or 1 (default)
        // var=init
        // endCopy=end
        // GOTO LABEL CHECK
        // APPLY STEP
        // JUMP here from NEXT
        // Add step
        // CHECK
        // If (step >= 0 and var > end) or (step < 0 and var < end) GOTO after "next"
        // step >= 0
        // Patch GOTO LABEL Check
        // var > end
        // (step >= 0 and var > end)
        // step < 0
        // var < end
        // (step < 0 and var < end)
        // if (true) GOTO after NEXT
        // set linenumber on exitNext().
        // GOTO APPLY STEP
        // LABEL afterNext
        /*
         * condition
         * GOTOIF condition labelBeforeThen
         * GOTO labelAfterThen|labelBeforeElse
         * labelBeforeThen
         * ThenStmts
         * GOTO labelAfterThen|labelAfterElse
         * labelAfterThen
         * ElseStmts
         * labelAfterElse
         */
        // Patch IF true: condition
        // Patch IF true: GOTO labelBeforeThen
        // Patch IF false: GOTO labelAfterThen|labelBeforeElse
        // Patch THEN: GOTO labelAfterThen|labelAfterElse
        // IF condition is true, GOTO labelBeforeThen
        // IF condition is false, GOTO labelAfterThen|labelBeforeElse
        // Add instruction for:
        // THEN GOTO linenum | THEN linenum
        // GOTO labelAfterThen|labelAfterElse
        // Add instruction for:
        // ELSE linenum
        //
        // IF expr THEN BEGIN
        // ...
        // ELSE BEGIN
        // ...
        // END IF
        //
        //
        // expr.result
        // GOTO labelBeforeThen IF expr.result is true
        // GOTO labelAfterThen|labelBeforeElse
        // labelBeforeThen (patch GOTOIF)
        // GOTO labelAfterThen|labelAfterElse (else begin)
        // labelAfterThen
        // labelBeforeElse
        //
        // IF condition is true, GOTO labelBeforeThen
        // IF condition is false, GOTO labelAfterThen|labelBeforeElse
        // Add labelBeforeThen
        // Patch IF true: GOTO labelBeforeThen
        // GOTO labelAfterThen|labelAfterElse
        // GOTO labelAfterThen|labelAfterElse
        // Add labelAfterElse
        // Patch IF true: GOTO labelBeforeThen
        // Patch IF false: GOTO labelAfterThen|labelBeforeElse
        // Patch THEN: GOTO labelAfterThen|labelAfterElse
        // fileName, fileNumber
        // openMode, accessMode
        // lockMode, recordLen
        // fileName, fileNumber
        // openMode, accessMode
        // lockMode, recordLen
        // FileNumber, #fields
        // if fileNumber != null, skip first instruction
        // GraphicsRuntime
        public override void ExitArraycopystmt(PuffinBasicParser.ArraycopystmtContext ctx)
        {
            var var1Instr = GetArrayNdVariableInstruction(ctx, ctx.variable(0));
            var var2Instr = GetArrayNdVariableInstruction(ctx, ctx.variable(1));
            ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.ARRAYCOPY, var1Instr.result, var2Instr.result, NULL_ID);
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        //
        // Expr
        //
        //
        // Functions
        //
        // n, x$, y$
        // x$, y$
        // x$, n, m
        // x$, n
        //
        // Stmt
        //
        // struct
        // array
        // scalar data type
        // struct
        // scalar data type
        // scalar
        //PuffinBasicAtomTypeId.lookup(compCtx.var2.getText());
        // array
        // list
        // struct
        // scalar data type
        // set
        // dict
        // struct
        // scalar data type
        // struct
        // throw
        // GOTO postFuncDecl
        // LABEL FuncStart
        // Push child scope
        // Copy expr to result
        // Pop declaration scope
        // GOTO Caller
        // LABEL postFuncDecl
        // Patch GOTO postFuncDecl
        // GOTO postFuncDecl
        // LABEL FuncStart
        // Push child scope
        // scalar
        //PuffinBasicAtomTypeId.lookup(compCtx.var2.getText());
        // list
        // struct
        // array
        // scalar data type
        // set
        // dict
        // struct
        // scalar data type
        // struct
        // array
        // throw
        // Copy expr to result
        // GOTO LABEL gotoCaller
        // Pop declaration scope
        // LABEL gotoCaller
        // GOTO Caller
        // LABEL postFuncDecl
        // Patch GOTO LABEL gotoCaller
        // Patch GOTO postFuncDecl
        // Unset current UDF state
        // LABEL beforeWhile
        // expr()
        // NOT expr()
        // If expr is false, GOTO afterWend
        // GOTO LABEL beforeWhile
        // LABEL afterWend
        // Patch GOTO afterWend
        // stepCopy = step or 1 (default)
        // var=init
        // endCopy=end
        // GOTO LABEL CHECK
        // APPLY STEP
        // JUMP here from NEXT
        // Add step
        // CHECK
        // If (step >= 0 and var > end) or (step < 0 and var < end) GOTO after "next"
        // step >= 0
        // Patch GOTO LABEL Check
        // var > end
        // (step >= 0 and var > end)
        // step < 0
        // var < end
        // (step < 0 and var < end)
        // if (true) GOTO after NEXT
        // set linenumber on exitNext().
        // GOTO APPLY STEP
        // LABEL afterNext
        /*
         * condition
         * GOTOIF condition labelBeforeThen
         * GOTO labelAfterThen|labelBeforeElse
         * labelBeforeThen
         * ThenStmts
         * GOTO labelAfterThen|labelAfterElse
         * labelAfterThen
         * ElseStmts
         * labelAfterElse
         */
        // Patch IF true: condition
        // Patch IF true: GOTO labelBeforeThen
        // Patch IF false: GOTO labelAfterThen|labelBeforeElse
        // Patch THEN: GOTO labelAfterThen|labelAfterElse
        // IF condition is true, GOTO labelBeforeThen
        // IF condition is false, GOTO labelAfterThen|labelBeforeElse
        // Add instruction for:
        // THEN GOTO linenum | THEN linenum
        // GOTO labelAfterThen|labelAfterElse
        // Add instruction for:
        // ELSE linenum
        //
        // IF expr THEN BEGIN
        // ...
        // ELSE BEGIN
        // ...
        // END IF
        //
        //
        // expr.result
        // GOTO labelBeforeThen IF expr.result is true
        // GOTO labelAfterThen|labelBeforeElse
        // labelBeforeThen (patch GOTOIF)
        // GOTO labelAfterThen|labelAfterElse (else begin)
        // labelAfterThen
        // labelBeforeElse
        //
        // IF condition is true, GOTO labelBeforeThen
        // IF condition is false, GOTO labelAfterThen|labelBeforeElse
        // Add labelBeforeThen
        // Patch IF true: GOTO labelBeforeThen
        // GOTO labelAfterThen|labelAfterElse
        // GOTO labelAfterThen|labelAfterElse
        // Add labelAfterElse
        // Patch IF true: GOTO labelBeforeThen
        // Patch IF false: GOTO labelAfterThen|labelBeforeElse
        // Patch THEN: GOTO labelAfterThen|labelAfterElse
        // fileName, fileNumber
        // openMode, accessMode
        // lockMode, recordLen
        // fileName, fileNumber
        // openMode, accessMode
        // lockMode, recordLen
        // FileNumber, #fields
        // if fileNumber != null, skip first instruction
        // GraphicsRuntime
        public override void ExitArray1dcopystmt(PuffinBasicParser.Array1dcopystmtContext ctx)
        {
            var var1Instr = GetArray1dVariableInstruction(ctx, ctx.variable(0), false);
            var var2Instr = GetArray1dVariableInstruction(ctx, ctx.variable(1), false);
            var src0 = LookupInstruction(ctx.src0);
            Types.AssertNumeric(ir.GetSymbolTable()[src0.result].GetType().GetAtomTypeId(), GetCtxString(ctx));
            var dst0 = LookupInstruction(ctx.dst0);
            Types.AssertNumeric(ir.GetSymbolTable()[dst0.result].GetType().GetAtomTypeId(), GetCtxString(ctx));
            var len = LookupInstruction(ctx.len);
            Types.AssertNumeric(ir.GetSymbolTable()[len.result].GetType().GetAtomTypeId(), GetCtxString(ctx));
            ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.PARAM2, var1Instr.result, src0.result, NULL_ID);
            ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.PARAM2, var2Instr.result, dst0.result, NULL_ID);
            ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.ARRAY1DCOPY, len.result, NULL_ID, NULL_ID);
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        //
        // Expr
        //
        //
        // Functions
        //
        // n, x$, y$
        // x$, y$
        // x$, n, m
        // x$, n
        //
        // Stmt
        //
        // struct
        // array
        // scalar data type
        // struct
        // scalar data type
        // scalar
        //PuffinBasicAtomTypeId.lookup(compCtx.var2.getText());
        // array
        // list
        // struct
        // scalar data type
        // set
        // dict
        // struct
        // scalar data type
        // struct
        // throw
        // GOTO postFuncDecl
        // LABEL FuncStart
        // Push child scope
        // Copy expr to result
        // Pop declaration scope
        // GOTO Caller
        // LABEL postFuncDecl
        // Patch GOTO postFuncDecl
        // GOTO postFuncDecl
        // LABEL FuncStart
        // Push child scope
        // scalar
        //PuffinBasicAtomTypeId.lookup(compCtx.var2.getText());
        // list
        // struct
        // array
        // scalar data type
        // set
        // dict
        // struct
        // scalar data type
        // struct
        // array
        // throw
        // Copy expr to result
        // GOTO LABEL gotoCaller
        // Pop declaration scope
        // LABEL gotoCaller
        // GOTO Caller
        // LABEL postFuncDecl
        // Patch GOTO LABEL gotoCaller
        // Patch GOTO postFuncDecl
        // Unset current UDF state
        // LABEL beforeWhile
        // expr()
        // NOT expr()
        // If expr is false, GOTO afterWend
        // GOTO LABEL beforeWhile
        // LABEL afterWend
        // Patch GOTO afterWend
        // stepCopy = step or 1 (default)
        // var=init
        // endCopy=end
        // GOTO LABEL CHECK
        // APPLY STEP
        // JUMP here from NEXT
        // Add step
        // CHECK
        // If (step >= 0 and var > end) or (step < 0 and var < end) GOTO after "next"
        // step >= 0
        // Patch GOTO LABEL Check
        // var > end
        // (step >= 0 and var > end)
        // step < 0
        // var < end
        // (step < 0 and var < end)
        // if (true) GOTO after NEXT
        // set linenumber on exitNext().
        // GOTO APPLY STEP
        // LABEL afterNext
        /*
         * condition
         * GOTOIF condition labelBeforeThen
         * GOTO labelAfterThen|labelBeforeElse
         * labelBeforeThen
         * ThenStmts
         * GOTO labelAfterThen|labelAfterElse
         * labelAfterThen
         * ElseStmts
         * labelAfterElse
         */
        // Patch IF true: condition
        // Patch IF true: GOTO labelBeforeThen
        // Patch IF false: GOTO labelAfterThen|labelBeforeElse
        // Patch THEN: GOTO labelAfterThen|labelAfterElse
        // IF condition is true, GOTO labelBeforeThen
        // IF condition is false, GOTO labelAfterThen|labelBeforeElse
        // Add instruction for:
        // THEN GOTO linenum | THEN linenum
        // GOTO labelAfterThen|labelAfterElse
        // Add instruction for:
        // ELSE linenum
        //
        // IF expr THEN BEGIN
        // ...
        // ELSE BEGIN
        // ...
        // END IF
        //
        //
        // expr.result
        // GOTO labelBeforeThen IF expr.result is true
        // GOTO labelAfterThen|labelBeforeElse
        // labelBeforeThen (patch GOTOIF)
        // GOTO labelAfterThen|labelAfterElse (else begin)
        // labelAfterThen
        // labelBeforeElse
        //
        // IF condition is true, GOTO labelBeforeThen
        // IF condition is false, GOTO labelAfterThen|labelBeforeElse
        // Add labelBeforeThen
        // Patch IF true: GOTO labelBeforeThen
        // GOTO labelAfterThen|labelAfterElse
        // GOTO labelAfterThen|labelAfterElse
        // Add labelAfterElse
        // Patch IF true: GOTO labelBeforeThen
        // Patch IF false: GOTO labelAfterThen|labelBeforeElse
        // Patch THEN: GOTO labelAfterThen|labelAfterElse
        // fileName, fileNumber
        // openMode, accessMode
        // lockMode, recordLen
        // fileName, fileNumber
        // openMode, accessMode
        // lockMode, recordLen
        // FileNumber, #fields
        // if fileNumber != null, skip first instruction
        // GraphicsRuntime
        public override void ExitArray2dshifthorstmt(PuffinBasicParser.Array2dshifthorstmtContext ctx)
        {
            var varInstr = GetArray2dVariableInstruction(ctx, ctx.variable());
            var expr = LookupInstruction(ctx.expr());
            Types.AssertNumeric(ir.GetSymbolTable()[expr.result].GetType().GetAtomTypeId(), GetCtxString(ctx));
            ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.ARRAY2DSHIFTHOR, varInstr.result, expr.result, NULL_ID);
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        //
        // Expr
        //
        //
        // Functions
        //
        // n, x$, y$
        // x$, y$
        // x$, n, m
        // x$, n
        //
        // Stmt
        //
        // struct
        // array
        // scalar data type
        // struct
        // scalar data type
        // scalar
        //PuffinBasicAtomTypeId.lookup(compCtx.var2.getText());
        // array
        // list
        // struct
        // scalar data type
        // set
        // dict
        // struct
        // scalar data type
        // struct
        // throw
        // GOTO postFuncDecl
        // LABEL FuncStart
        // Push child scope
        // Copy expr to result
        // Pop declaration scope
        // GOTO Caller
        // LABEL postFuncDecl
        // Patch GOTO postFuncDecl
        // GOTO postFuncDecl
        // LABEL FuncStart
        // Push child scope
        // scalar
        //PuffinBasicAtomTypeId.lookup(compCtx.var2.getText());
        // list
        // struct
        // array
        // scalar data type
        // set
        // dict
        // struct
        // scalar data type
        // struct
        // array
        // throw
        // Copy expr to result
        // GOTO LABEL gotoCaller
        // Pop declaration scope
        // LABEL gotoCaller
        // GOTO Caller
        // LABEL postFuncDecl
        // Patch GOTO LABEL gotoCaller
        // Patch GOTO postFuncDecl
        // Unset current UDF state
        // LABEL beforeWhile
        // expr()
        // NOT expr()
        // If expr is false, GOTO afterWend
        // GOTO LABEL beforeWhile
        // LABEL afterWend
        // Patch GOTO afterWend
        // stepCopy = step or 1 (default)
        // var=init
        // endCopy=end
        // GOTO LABEL CHECK
        // APPLY STEP
        // JUMP here from NEXT
        // Add step
        // CHECK
        // If (step >= 0 and var > end) or (step < 0 and var < end) GOTO after "next"
        // step >= 0
        // Patch GOTO LABEL Check
        // var > end
        // (step >= 0 and var > end)
        // step < 0
        // var < end
        // (step < 0 and var < end)
        // if (true) GOTO after NEXT
        // set linenumber on exitNext().
        // GOTO APPLY STEP
        // LABEL afterNext
        /*
         * condition
         * GOTOIF condition labelBeforeThen
         * GOTO labelAfterThen|labelBeforeElse
         * labelBeforeThen
         * ThenStmts
         * GOTO labelAfterThen|labelAfterElse
         * labelAfterThen
         * ElseStmts
         * labelAfterElse
         */
        // Patch IF true: condition
        // Patch IF true: GOTO labelBeforeThen
        // Patch IF false: GOTO labelAfterThen|labelBeforeElse
        // Patch THEN: GOTO labelAfterThen|labelAfterElse
        // IF condition is true, GOTO labelBeforeThen
        // IF condition is false, GOTO labelAfterThen|labelBeforeElse
        // Add instruction for:
        // THEN GOTO linenum | THEN linenum
        // GOTO labelAfterThen|labelAfterElse
        // Add instruction for:
        // ELSE linenum
        //
        // IF expr THEN BEGIN
        // ...
        // ELSE BEGIN
        // ...
        // END IF
        //
        //
        // expr.result
        // GOTO labelBeforeThen IF expr.result is true
        // GOTO labelAfterThen|labelBeforeElse
        // labelBeforeThen (patch GOTOIF)
        // GOTO labelAfterThen|labelAfterElse (else begin)
        // labelAfterThen
        // labelBeforeElse
        //
        // IF condition is true, GOTO labelBeforeThen
        // IF condition is false, GOTO labelAfterThen|labelBeforeElse
        // Add labelBeforeThen
        // Patch IF true: GOTO labelBeforeThen
        // GOTO labelAfterThen|labelAfterElse
        // GOTO labelAfterThen|labelAfterElse
        // Add labelAfterElse
        // Patch IF true: GOTO labelBeforeThen
        // Patch IF false: GOTO labelAfterThen|labelBeforeElse
        // Patch THEN: GOTO labelAfterThen|labelAfterElse
        // fileName, fileNumber
        // openMode, accessMode
        // lockMode, recordLen
        // fileName, fileNumber
        // openMode, accessMode
        // lockMode, recordLen
        // FileNumber, #fields
        // if fileNumber != null, skip first instruction
        // GraphicsRuntime
        public override void ExitArray2dshiftverstmt(PuffinBasicParser.Array2dshiftverstmtContext ctx)
        {
            var varInstr = GetArray2dVariableInstruction(ctx, ctx.variable());
            var expr = LookupInstruction(ctx.expr());
            Types.AssertNumeric(ir.GetSymbolTable()[expr.result].GetType().GetAtomTypeId(), GetCtxString(ctx));
            ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.ARRAY2DSHIFTVER, varInstr.result, expr.result, NULL_ID);
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        //
        // Expr
        //
        //
        // Functions
        //
        // n, x$, y$
        // x$, y$
        // x$, n, m
        // x$, n
        //
        // Stmt
        //
        // struct
        // array
        // scalar data type
        // struct
        // scalar data type
        // scalar
        //PuffinBasicAtomTypeId.lookup(compCtx.var2.getText());
        // array
        // list
        // struct
        // scalar data type
        // set
        // dict
        // struct
        // scalar data type
        // struct
        // throw
        // GOTO postFuncDecl
        // LABEL FuncStart
        // Push child scope
        // Copy expr to result
        // Pop declaration scope
        // GOTO Caller
        // LABEL postFuncDecl
        // Patch GOTO postFuncDecl
        // GOTO postFuncDecl
        // LABEL FuncStart
        // Push child scope
        // scalar
        //PuffinBasicAtomTypeId.lookup(compCtx.var2.getText());
        // list
        // struct
        // array
        // scalar data type
        // set
        // dict
        // struct
        // scalar data type
        // struct
        // array
        // throw
        // Copy expr to result
        // GOTO LABEL gotoCaller
        // Pop declaration scope
        // LABEL gotoCaller
        // GOTO Caller
        // LABEL postFuncDecl
        // Patch GOTO LABEL gotoCaller
        // Patch GOTO postFuncDecl
        // Unset current UDF state
        // LABEL beforeWhile
        // expr()
        // NOT expr()
        // If expr is false, GOTO afterWend
        // GOTO LABEL beforeWhile
        // LABEL afterWend
        // Patch GOTO afterWend
        // stepCopy = step or 1 (default)
        // var=init
        // endCopy=end
        // GOTO LABEL CHECK
        // APPLY STEP
        // JUMP here from NEXT
        // Add step
        // CHECK
        // If (step >= 0 and var > end) or (step < 0 and var < end) GOTO after "next"
        // step >= 0
        // Patch GOTO LABEL Check
        // var > end
        // (step >= 0 and var > end)
        // step < 0
        // var < end
        // (step < 0 and var < end)
        // if (true) GOTO after NEXT
        // set linenumber on exitNext().
        // GOTO APPLY STEP
        // LABEL afterNext
        /*
         * condition
         * GOTOIF condition labelBeforeThen
         * GOTO labelAfterThen|labelBeforeElse
         * labelBeforeThen
         * ThenStmts
         * GOTO labelAfterThen|labelAfterElse
         * labelAfterThen
         * ElseStmts
         * labelAfterElse
         */
        // Patch IF true: condition
        // Patch IF true: GOTO labelBeforeThen
        // Patch IF false: GOTO labelAfterThen|labelBeforeElse
        // Patch THEN: GOTO labelAfterThen|labelAfterElse
        // IF condition is true, GOTO labelBeforeThen
        // IF condition is false, GOTO labelAfterThen|labelBeforeElse
        // Add instruction for:
        // THEN GOTO linenum | THEN linenum
        // GOTO labelAfterThen|labelAfterElse
        // Add instruction for:
        // ELSE linenum
        //
        // IF expr THEN BEGIN
        // ...
        // ELSE BEGIN
        // ...
        // END IF
        //
        //
        // expr.result
        // GOTO labelBeforeThen IF expr.result is true
        // GOTO labelAfterThen|labelBeforeElse
        // labelBeforeThen (patch GOTOIF)
        // GOTO labelAfterThen|labelAfterElse (else begin)
        // labelAfterThen
        // labelBeforeElse
        //
        // IF condition is true, GOTO labelBeforeThen
        // IF condition is false, GOTO labelAfterThen|labelBeforeElse
        // Add labelBeforeThen
        // Patch IF true: GOTO labelBeforeThen
        // GOTO labelAfterThen|labelAfterElse
        // GOTO labelAfterThen|labelAfterElse
        // Add labelAfterElse
        // Patch IF true: GOTO labelBeforeThen
        // Patch IF false: GOTO labelAfterThen|labelBeforeElse
        // Patch THEN: GOTO labelAfterThen|labelAfterElse
        // fileName, fileNumber
        // openMode, accessMode
        // lockMode, recordLen
        // fileName, fileNumber
        // openMode, accessMode
        // lockMode, recordLen
        // FileNumber, #fields
        // if fileNumber != null, skip first instruction
        // GraphicsRuntime
        public override void ExitArrayfillstmt(PuffinBasicParser.ArrayfillstmtContext ctx)
        {
            var varInstr = GetArrayNdVariableInstruction(ctx, ctx.variable());
            var expr = LookupInstruction(ctx.expr());
            Types.AssertNumeric(ir.GetSymbolTable()[expr.result].GetType().GetAtomTypeId(), GetCtxString(ctx));
            ir.AddInstruction(sourceFile, currentLineNumber, ctx.Start.StartIndex, ctx.Stop.StopIndex, OpCode.ARRAYFILL, varInstr.result, expr.result, NULL_ID);
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        //
        // Expr
        //
        //
        // Functions
        //
        // n, x$, y$
        // x$, y$
        // x$, n, m
        // x$, n
        //
        // Stmt
        //
        // struct
        // array
        // scalar data type
        // struct
        // scalar data type
        // scalar
        //PuffinBasicAtomTypeId.lookup(compCtx.var2.getText());
        // array
        // list
        // struct
        // scalar data type
        // set
        // dict
        // struct
        // scalar data type
        // struct
        // throw
        // GOTO postFuncDecl
        // LABEL FuncStart
        // Push child scope
        // Copy expr to result
        // Pop declaration scope
        // GOTO Caller
        // LABEL postFuncDecl
        // Patch GOTO postFuncDecl
        // GOTO postFuncDecl
        // LABEL FuncStart
        // Push child scope
        // scalar
        //PuffinBasicAtomTypeId.lookup(compCtx.var2.getText());
        // list
        // struct
        // array
        // scalar data type
        // set
        // dict
        // struct
        // scalar data type
        // struct
        // array
        // throw
        // Copy expr to result
        // GOTO LABEL gotoCaller
        // Pop declaration scope
        // LABEL gotoCaller
        // GOTO Caller
        // LABEL postFuncDecl
        // Patch GOTO LABEL gotoCaller
        // Patch GOTO postFuncDecl
        // Unset current UDF state
        // LABEL beforeWhile
        // expr()
        // NOT expr()
        // If expr is false, GOTO afterWend
        // GOTO LABEL beforeWhile
        // LABEL afterWend
        // Patch GOTO afterWend
        // stepCopy = step or 1 (default)
        // var=init
        // endCopy=end
        // GOTO LABEL CHECK
        // APPLY STEP
        // JUMP here from NEXT
        // Add step
        // CHECK
        // If (step >= 0 and var > end) or (step < 0 and var < end) GOTO after "next"
        // step >= 0
        // Patch GOTO LABEL Check
        // var > end
        // (step >= 0 and var > end)
        // step < 0
        // var < end
        // (step < 0 and var < end)
        // if (true) GOTO after NEXT
        // set linenumber on exitNext().
        // GOTO APPLY STEP
        // LABEL afterNext
        /*
         * condition
         * GOTOIF condition labelBeforeThen
         * GOTO labelAfterThen|labelBeforeElse
         * labelBeforeThen
         * ThenStmts
         * GOTO labelAfterThen|labelAfterElse
         * labelAfterThen
         * ElseStmts
         * labelAfterElse
         */
        // Patch IF true: condition
        // Patch IF true: GOTO labelBeforeThen
        // Patch IF false: GOTO labelAfterThen|labelBeforeElse
        // Patch THEN: GOTO labelAfterThen|labelAfterElse
        // IF condition is true, GOTO labelBeforeThen
        // IF condition is false, GOTO labelAfterThen|labelBeforeElse
        // Add instruction for:
        // THEN GOTO linenum | THEN linenum
        // GOTO labelAfterThen|labelAfterElse
        // Add instruction for:
        // ELSE linenum
        //
        // IF expr THEN BEGIN
        // ...
        // ELSE BEGIN
        // ...
        // END IF
        //
        //
        // expr.result
        // GOTO labelBeforeThen IF expr.result is true
        // GOTO labelAfterThen|labelBeforeElse
        // labelBeforeThen (patch GOTOIF)
        // GOTO labelAfterThen|labelAfterElse (else begin)
        // labelAfterThen
        // labelBeforeElse
        //
        // IF condition is true, GOTO labelBeforeThen
        // IF condition is false, GOTO labelAfterThen|labelBeforeElse
        // Add labelBeforeThen
        // Patch IF true: GOTO labelBeforeThen
        // GOTO labelAfterThen|labelAfterElse
        // GOTO labelAfterThen|labelAfterElse
        // Add labelAfterElse
        // Patch IF true: GOTO labelBeforeThen
        // Patch IF false: GOTO labelAfterThen|labelBeforeElse
        // Patch THEN: GOTO labelAfterThen|labelAfterElse
        // fileName, fileNumber
        // openMode, accessMode
        // lockMode, recordLen
        // fileName, fileNumber
        // openMode, accessMode
        // lockMode, recordLen
        // FileNumber, #fields
        // if fileNumber != null, skip first instruction
        // GraphicsRuntime
        private void AssertGraphics()
        {
            if (!graphics)
            {
                throw new PuffinBasicInternalError("GraphicsRuntime is not enabled!");
            }
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        //
        // Expr
        //
        //
        // Functions
        //
        // n, x$, y$
        // x$, y$
        // x$, n, m
        // x$, n
        //
        // Stmt
        //
        // struct
        // array
        // scalar data type
        // struct
        // scalar data type
        // scalar
        //PuffinBasicAtomTypeId.lookup(compCtx.var2.getText());
        // array
        // list
        // struct
        // scalar data type
        // set
        // dict
        // struct
        // scalar data type
        // struct
        // throw
        // GOTO postFuncDecl
        // LABEL FuncStart
        // Push child scope
        // Copy expr to result
        // Pop declaration scope
        // GOTO Caller
        // LABEL postFuncDecl
        // Patch GOTO postFuncDecl
        // GOTO postFuncDecl
        // LABEL FuncStart
        // Push child scope
        // scalar
        //PuffinBasicAtomTypeId.lookup(compCtx.var2.getText());
        // list
        // struct
        // array
        // scalar data type
        // set
        // dict
        // struct
        // scalar data type
        // struct
        // array
        // throw
        // Copy expr to result
        // GOTO LABEL gotoCaller
        // Pop declaration scope
        // LABEL gotoCaller
        // GOTO Caller
        // LABEL postFuncDecl
        // Patch GOTO LABEL gotoCaller
        // Patch GOTO postFuncDecl
        // Unset current UDF state
        // LABEL beforeWhile
        // expr()
        // NOT expr()
        // If expr is false, GOTO afterWend
        // GOTO LABEL beforeWhile
        // LABEL afterWend
        // Patch GOTO afterWend
        // stepCopy = step or 1 (default)
        // var=init
        // endCopy=end
        // GOTO LABEL CHECK
        // APPLY STEP
        // JUMP here from NEXT
        // Add step
        // CHECK
        // If (step >= 0 and var > end) or (step < 0 and var < end) GOTO after "next"
        // step >= 0
        // Patch GOTO LABEL Check
        // var > end
        // (step >= 0 and var > end)
        // step < 0
        // var < end
        // (step < 0 and var < end)
        // if (true) GOTO after NEXT
        // set linenumber on exitNext().
        // GOTO APPLY STEP
        // LABEL afterNext
        /*
         * condition
         * GOTOIF condition labelBeforeThen
         * GOTO labelAfterThen|labelBeforeElse
         * labelBeforeThen
         * ThenStmts
         * GOTO labelAfterThen|labelAfterElse
         * labelAfterThen
         * ElseStmts
         * labelAfterElse
         */
        // Patch IF true: condition
        // Patch IF true: GOTO labelBeforeThen
        // Patch IF false: GOTO labelAfterThen|labelBeforeElse
        // Patch THEN: GOTO labelAfterThen|labelAfterElse
        // IF condition is true, GOTO labelBeforeThen
        // IF condition is false, GOTO labelAfterThen|labelBeforeElse
        // Add instruction for:
        // THEN GOTO linenum | THEN linenum
        // GOTO labelAfterThen|labelAfterElse
        // Add instruction for:
        // ELSE linenum
        //
        // IF expr THEN BEGIN
        // ...
        // ELSE BEGIN
        // ...
        // END IF
        //
        //
        // expr.result
        // GOTO labelBeforeThen IF expr.result is true
        // GOTO labelAfterThen|labelBeforeElse
        // labelBeforeThen (patch GOTOIF)
        // GOTO labelAfterThen|labelAfterElse (else begin)
        // labelAfterThen
        // labelBeforeElse
        //
        // IF condition is true, GOTO labelBeforeThen
        // IF condition is false, GOTO labelAfterThen|labelBeforeElse
        // Add labelBeforeThen
        // Patch IF true: GOTO labelBeforeThen
        // GOTO labelAfterThen|labelAfterElse
        // GOTO labelAfterThen|labelAfterElse
        // Add labelAfterElse
        // Patch IF true: GOTO labelBeforeThen
        // Patch IF false: GOTO labelAfterThen|labelBeforeElse
        // Patch THEN: GOTO labelAfterThen|labelAfterElse
        // fileName, fileNumber
        // openMode, accessMode
        // lockMode, recordLen
        // fileName, fileNumber
        // openMode, accessMode
        // lockMode, recordLen
        // FileNumber, #fields
        // if fileNumber != null, skip first instruction
        // GraphicsRuntime
        private void HandleDefTypeStmt(IList<ITerminalNode> letterRanges, PuffinBasicAtomTypeId dataType)
        {
            IList<char> defs = new List<char>();
            foreach (var lr  in letterRanges.Select(x => x.GetText())) {
                for (char i = lr[0]; i < lr[2]; i++)
                    defs.Add(i);
            }

            foreach (var def in defs)
                ir.GetSymbolTable().SetDefaultDataType(def, dataType);
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        //
        // Expr
        //
        //
        // Functions
        //
        // n, x$, y$
        // x$, y$
        // x$, n, m
        // x$, n
        //
        // Stmt
        //
        // struct
        // array
        // scalar data type
        // struct
        // scalar data type
        // scalar
        //PuffinBasicAtomTypeId.lookup(compCtx.var2.getText());
        // array
        // list
        // struct
        // scalar data type
        // set
        // dict
        // struct
        // scalar data type
        // struct
        // throw
        // GOTO postFuncDecl
        // LABEL FuncStart
        // Push child scope
        // Copy expr to result
        // Pop declaration scope
        // GOTO Caller
        // LABEL postFuncDecl
        // Patch GOTO postFuncDecl
        // GOTO postFuncDecl
        // LABEL FuncStart
        // Push child scope
        // scalar
        //PuffinBasicAtomTypeId.lookup(compCtx.var2.getText());
        // list
        // struct
        // array
        // scalar data type
        // set
        // dict
        // struct
        // scalar data type
        // struct
        // array
        // throw
        // Copy expr to result
        // GOTO LABEL gotoCaller
        // Pop declaration scope
        // LABEL gotoCaller
        // GOTO Caller
        // LABEL postFuncDecl
        // Patch GOTO LABEL gotoCaller
        // Patch GOTO postFuncDecl
        // Unset current UDF state
        // LABEL beforeWhile
        // expr()
        // NOT expr()
        // If expr is false, GOTO afterWend
        // GOTO LABEL beforeWhile
        // LABEL afterWend
        // Patch GOTO afterWend
        // stepCopy = step or 1 (default)
        // var=init
        // endCopy=end
        // GOTO LABEL CHECK
        // APPLY STEP
        // JUMP here from NEXT
        // Add step
        // CHECK
        // If (step >= 0 and var > end) or (step < 0 and var < end) GOTO after "next"
        // step >= 0
        // Patch GOTO LABEL Check
        // var > end
        // (step >= 0 and var > end)
        // step < 0
        // var < end
        // (step < 0 and var < end)
        // if (true) GOTO after NEXT
        // set linenumber on exitNext().
        // GOTO APPLY STEP
        // LABEL afterNext
        /*
         * condition
         * GOTOIF condition labelBeforeThen
         * GOTO labelAfterThen|labelBeforeElse
         * labelBeforeThen
         * ThenStmts
         * GOTO labelAfterThen|labelAfterElse
         * labelAfterThen
         * ElseStmts
         * labelAfterElse
         */
        // Patch IF true: condition
        // Patch IF true: GOTO labelBeforeThen
        // Patch IF false: GOTO labelAfterThen|labelBeforeElse
        // Patch THEN: GOTO labelAfterThen|labelAfterElse
        // IF condition is true, GOTO labelBeforeThen
        // IF condition is false, GOTO labelAfterThen|labelBeforeElse
        // Add instruction for:
        // THEN GOTO linenum | THEN linenum
        // GOTO labelAfterThen|labelAfterElse
        // Add instruction for:
        // ELSE linenum
        //
        // IF expr THEN BEGIN
        // ...
        // ELSE BEGIN
        // ...
        // END IF
        //
        //
        // expr.result
        // GOTO labelBeforeThen IF expr.result is true
        // GOTO labelAfterThen|labelBeforeElse
        // labelBeforeThen (patch GOTOIF)
        // GOTO labelAfterThen|labelAfterElse (else begin)
        // labelAfterThen
        // labelBeforeElse
        //
        // IF condition is true, GOTO labelBeforeThen
        // IF condition is false, GOTO labelAfterThen|labelBeforeElse
        // Add labelBeforeThen
        // Patch IF true: GOTO labelBeforeThen
        // GOTO labelAfterThen|labelAfterElse
        // GOTO labelAfterThen|labelAfterElse
        // Add labelAfterElse
        // Patch IF true: GOTO labelBeforeThen
        // Patch IF false: GOTO labelAfterThen|labelBeforeElse
        // Patch THEN: GOTO labelAfterThen|labelAfterElse
        // fileName, fileNumber
        // openMode, accessMode
        // lockMode, recordLen
        // fileName, fileNumber
        // openMode, accessMode
        // lockMode, recordLen
        // FileNumber, #fields
        // if fileNumber != null, skip first instruction
        // GraphicsRuntime
        private static FileOpenMode GetFileOpenMode(PuffinBasicParser.Filemode1Context filemode1)
        {
            var mode = filemode1 != null ? Unquote(filemode1.GetText()) : null;
            if (mode == null || mode.Equals("r", StringComparison.OrdinalIgnoreCase))
            {
                return FileOpenMode.RANDOM;
            }
            else if (mode.Equals("i", StringComparison.OrdinalIgnoreCase))
            {
                return FileOpenMode.INPUT;
            }
            else if (mode.Equals("o", StringComparison.OrdinalIgnoreCase))
            {
                return FileOpenMode.OUTPUT;
            }
            else
            {
                return FileOpenMode.APPEND;
            }
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        //
        // Expr
        //
        //
        // Functions
        //
        // n, x$, y$
        // x$, y$
        // x$, n, m
        // x$, n
        //
        // Stmt
        //
        // struct
        // array
        // scalar data type
        // struct
        // scalar data type
        // scalar
        //PuffinBasicAtomTypeId.lookup(compCtx.var2.getText());
        // array
        // list
        // struct
        // scalar data type
        // set
        // dict
        // struct
        // scalar data type
        // struct
        // throw
        // GOTO postFuncDecl
        // LABEL FuncStart
        // Push child scope
        // Copy expr to result
        // Pop declaration scope
        // GOTO Caller
        // LABEL postFuncDecl
        // Patch GOTO postFuncDecl
        // GOTO postFuncDecl
        // LABEL FuncStart
        // Push child scope
        // scalar
        //PuffinBasicAtomTypeId.lookup(compCtx.var2.getText());
        // list
        // struct
        // array
        // scalar data type
        // set
        // dict
        // struct
        // scalar data type
        // struct
        // array
        // throw
        // Copy expr to result
        // GOTO LABEL gotoCaller
        // Pop declaration scope
        // LABEL gotoCaller
        // GOTO Caller
        // LABEL postFuncDecl
        // Patch GOTO LABEL gotoCaller
        // Patch GOTO postFuncDecl
        // Unset current UDF state
        // LABEL beforeWhile
        // expr()
        // NOT expr()
        // If expr is false, GOTO afterWend
        // GOTO LABEL beforeWhile
        // LABEL afterWend
        // Patch GOTO afterWend
        // stepCopy = step or 1 (default)
        // var=init
        // endCopy=end
        // GOTO LABEL CHECK
        // APPLY STEP
        // JUMP here from NEXT
        // Add step
        // CHECK
        // If (step >= 0 and var > end) or (step < 0 and var < end) GOTO after "next"
        // step >= 0
        // Patch GOTO LABEL Check
        // var > end
        // (step >= 0 and var > end)
        // step < 0
        // var < end
        // (step < 0 and var < end)
        // if (true) GOTO after NEXT
        // set linenumber on exitNext().
        // GOTO APPLY STEP
        // LABEL afterNext
        /*
         * condition
         * GOTOIF condition labelBeforeThen
         * GOTO labelAfterThen|labelBeforeElse
         * labelBeforeThen
         * ThenStmts
         * GOTO labelAfterThen|labelAfterElse
         * labelAfterThen
         * ElseStmts
         * labelAfterElse
         */
        // Patch IF true: condition
        // Patch IF true: GOTO labelBeforeThen
        // Patch IF false: GOTO labelAfterThen|labelBeforeElse
        // Patch THEN: GOTO labelAfterThen|labelAfterElse
        // IF condition is true, GOTO labelBeforeThen
        // IF condition is false, GOTO labelAfterThen|labelBeforeElse
        // Add instruction for:
        // THEN GOTO linenum | THEN linenum
        // GOTO labelAfterThen|labelAfterElse
        // Add instruction for:
        // ELSE linenum
        //
        // IF expr THEN BEGIN
        // ...
        // ELSE BEGIN
        // ...
        // END IF
        //
        //
        // expr.result
        // GOTO labelBeforeThen IF expr.result is true
        // GOTO labelAfterThen|labelBeforeElse
        // labelBeforeThen (patch GOTOIF)
        // GOTO labelAfterThen|labelAfterElse (else begin)
        // labelAfterThen
        // labelBeforeElse
        //
        // IF condition is true, GOTO labelBeforeThen
        // IF condition is false, GOTO labelAfterThen|labelBeforeElse
        // Add labelBeforeThen
        // Patch IF true: GOTO labelBeforeThen
        // GOTO labelAfterThen|labelAfterElse
        // GOTO labelAfterThen|labelAfterElse
        // Add labelAfterElse
        // Patch IF true: GOTO labelBeforeThen
        // Patch IF false: GOTO labelAfterThen|labelBeforeElse
        // Patch THEN: GOTO labelAfterThen|labelAfterElse
        // fileName, fileNumber
        // openMode, accessMode
        // lockMode, recordLen
        // fileName, fileNumber
        // openMode, accessMode
        // lockMode, recordLen
        // FileNumber, #fields
        // if fileNumber != null, skip first instruction
        // GraphicsRuntime
        private static FileOpenMode GetFileOpenMode(PuffinBasicParser.Filemode2Context filemode2)
        {
            if (filemode2 == null || filemode2.RANDOM() != null)
            {
                return FileOpenMode.RANDOM;
            }
            else if (filemode2.INPUT() != null)
            {
                return FileOpenMode.INPUT;
            }
            else if (filemode2.OUTPUT() != null)
            {
                return FileOpenMode.OUTPUT;
            }
            else
            {
                return FileOpenMode.APPEND;
            }
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        //
        // Expr
        //
        //
        // Functions
        //
        // n, x$, y$
        // x$, y$
        // x$, n, m
        // x$, n
        //
        // Stmt
        //
        // struct
        // array
        // scalar data type
        // struct
        // scalar data type
        // scalar
        //PuffinBasicAtomTypeId.lookup(compCtx.var2.getText());
        // array
        // list
        // struct
        // scalar data type
        // set
        // dict
        // struct
        // scalar data type
        // struct
        // throw
        // GOTO postFuncDecl
        // LABEL FuncStart
        // Push child scope
        // Copy expr to result
        // Pop declaration scope
        // GOTO Caller
        // LABEL postFuncDecl
        // Patch GOTO postFuncDecl
        // GOTO postFuncDecl
        // LABEL FuncStart
        // Push child scope
        // scalar
        //PuffinBasicAtomTypeId.lookup(compCtx.var2.getText());
        // list
        // struct
        // array
        // scalar data type
        // set
        // dict
        // struct
        // scalar data type
        // struct
        // array
        // throw
        // Copy expr to result
        // GOTO LABEL gotoCaller
        // Pop declaration scope
        // LABEL gotoCaller
        // GOTO Caller
        // LABEL postFuncDecl
        // Patch GOTO LABEL gotoCaller
        // Patch GOTO postFuncDecl
        // Unset current UDF state
        // LABEL beforeWhile
        // expr()
        // NOT expr()
        // If expr is false, GOTO afterWend
        // GOTO LABEL beforeWhile
        // LABEL afterWend
        // Patch GOTO afterWend
        // stepCopy = step or 1 (default)
        // var=init
        // endCopy=end
        // GOTO LABEL CHECK
        // APPLY STEP
        // JUMP here from NEXT
        // Add step
        // CHECK
        // If (step >= 0 and var > end) or (step < 0 and var < end) GOTO after "next"
        // step >= 0
        // Patch GOTO LABEL Check
        // var > end
        // (step >= 0 and var > end)
        // step < 0
        // var < end
        // (step < 0 and var < end)
        // if (true) GOTO after NEXT
        // set linenumber on exitNext().
        // GOTO APPLY STEP
        // LABEL afterNext
        /*
         * condition
         * GOTOIF condition labelBeforeThen
         * GOTO labelAfterThen|labelBeforeElse
         * labelBeforeThen
         * ThenStmts
         * GOTO labelAfterThen|labelAfterElse
         * labelAfterThen
         * ElseStmts
         * labelAfterElse
         */
        // Patch IF true: condition
        // Patch IF true: GOTO labelBeforeThen
        // Patch IF false: GOTO labelAfterThen|labelBeforeElse
        // Patch THEN: GOTO labelAfterThen|labelAfterElse
        // IF condition is true, GOTO labelBeforeThen
        // IF condition is false, GOTO labelAfterThen|labelBeforeElse
        // Add instruction for:
        // THEN GOTO linenum | THEN linenum
        // GOTO labelAfterThen|labelAfterElse
        // Add instruction for:
        // ELSE linenum
        //
        // IF expr THEN BEGIN
        // ...
        // ELSE BEGIN
        // ...
        // END IF
        //
        //
        // expr.result
        // GOTO labelBeforeThen IF expr.result is true
        // GOTO labelAfterThen|labelBeforeElse
        // labelBeforeThen (patch GOTOIF)
        // GOTO labelAfterThen|labelAfterElse (else begin)
        // labelAfterThen
        // labelBeforeElse
        //
        // IF condition is true, GOTO labelBeforeThen
        // IF condition is false, GOTO labelAfterThen|labelBeforeElse
        // Add labelBeforeThen
        // Patch IF true: GOTO labelBeforeThen
        // GOTO labelAfterThen|labelAfterElse
        // GOTO labelAfterThen|labelAfterElse
        // Add labelAfterElse
        // Patch IF true: GOTO labelBeforeThen
        // Patch IF false: GOTO labelAfterThen|labelBeforeElse
        // Patch THEN: GOTO labelAfterThen|labelAfterElse
        // fileName, fileNumber
        // openMode, accessMode
        // lockMode, recordLen
        // fileName, fileNumber
        // openMode, accessMode
        // lockMode, recordLen
        // FileNumber, #fields
        // if fileNumber != null, skip first instruction
        // GraphicsRuntime
        private static FileAccessMode GetFileAccessMode(PuffinBasicParser.AccessContext access)
        {
            if (access == null || (access.READ() != null && access.WRITE() != null))
            {
                return FileAccessMode.READ_WRITE;
            }
            else if (access.READ() != null)
            {
                return FileAccessMode.READ_ONLY;
            }
            else
            {
                return FileAccessMode.WRITE_ONLY;
            }
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        //
        // Expr
        //
        //
        // Functions
        //
        // n, x$, y$
        // x$, y$
        // x$, n, m
        // x$, n
        //
        // Stmt
        //
        // struct
        // array
        // scalar data type
        // struct
        // scalar data type
        // scalar
        //PuffinBasicAtomTypeId.lookup(compCtx.var2.getText());
        // array
        // list
        // struct
        // scalar data type
        // set
        // dict
        // struct
        // scalar data type
        // struct
        // throw
        // GOTO postFuncDecl
        // LABEL FuncStart
        // Push child scope
        // Copy expr to result
        // Pop declaration scope
        // GOTO Caller
        // LABEL postFuncDecl
        // Patch GOTO postFuncDecl
        // GOTO postFuncDecl
        // LABEL FuncStart
        // Push child scope
        // scalar
        //PuffinBasicAtomTypeId.lookup(compCtx.var2.getText());
        // list
        // struct
        // array
        // scalar data type
        // set
        // dict
        // struct
        // scalar data type
        // struct
        // array
        // throw
        // Copy expr to result
        // GOTO LABEL gotoCaller
        // Pop declaration scope
        // LABEL gotoCaller
        // GOTO Caller
        // LABEL postFuncDecl
        // Patch GOTO LABEL gotoCaller
        // Patch GOTO postFuncDecl
        // Unset current UDF state
        // LABEL beforeWhile
        // expr()
        // NOT expr()
        // If expr is false, GOTO afterWend
        // GOTO LABEL beforeWhile
        // LABEL afterWend
        // Patch GOTO afterWend
        // stepCopy = step or 1 (default)
        // var=init
        // endCopy=end
        // GOTO LABEL CHECK
        // APPLY STEP
        // JUMP here from NEXT
        // Add step
        // CHECK
        // If (step >= 0 and var > end) or (step < 0 and var < end) GOTO after "next"
        // step >= 0
        // Patch GOTO LABEL Check
        // var > end
        // (step >= 0 and var > end)
        // step < 0
        // var < end
        // (step < 0 and var < end)
        // if (true) GOTO after NEXT
        // set linenumber on exitNext().
        // GOTO APPLY STEP
        // LABEL afterNext
        /*
         * condition
         * GOTOIF condition labelBeforeThen
         * GOTO labelAfterThen|labelBeforeElse
         * labelBeforeThen
         * ThenStmts
         * GOTO labelAfterThen|labelAfterElse
         * labelAfterThen
         * ElseStmts
         * labelAfterElse
         */
        // Patch IF true: condition
        // Patch IF true: GOTO labelBeforeThen
        // Patch IF false: GOTO labelAfterThen|labelBeforeElse
        // Patch THEN: GOTO labelAfterThen|labelAfterElse
        // IF condition is true, GOTO labelBeforeThen
        // IF condition is false, GOTO labelAfterThen|labelBeforeElse
        // Add instruction for:
        // THEN GOTO linenum | THEN linenum
        // GOTO labelAfterThen|labelAfterElse
        // Add instruction for:
        // ELSE linenum
        //
        // IF expr THEN BEGIN
        // ...
        // ELSE BEGIN
        // ...
        // END IF
        //
        //
        // expr.result
        // GOTO labelBeforeThen IF expr.result is true
        // GOTO labelAfterThen|labelBeforeElse
        // labelBeforeThen (patch GOTOIF)
        // GOTO labelAfterThen|labelAfterElse (else begin)
        // labelAfterThen
        // labelBeforeElse
        //
        // IF condition is true, GOTO labelBeforeThen
        // IF condition is false, GOTO labelAfterThen|labelBeforeElse
        // Add labelBeforeThen
        // Patch IF true: GOTO labelBeforeThen
        // GOTO labelAfterThen|labelAfterElse
        // GOTO labelAfterThen|labelAfterElse
        // Add labelAfterElse
        // Patch IF true: GOTO labelBeforeThen
        // Patch IF false: GOTO labelAfterThen|labelBeforeElse
        // Patch THEN: GOTO labelAfterThen|labelAfterElse
        // fileName, fileNumber
        // openMode, accessMode
        // lockMode, recordLen
        // fileName, fileNumber
        // openMode, accessMode
        // lockMode, recordLen
        // FileNumber, #fields
        // if fileNumber != null, skip first instruction
        // GraphicsRuntime
        private static LockMode GetLockMode(PuffinBasicParser.LockContext @lock)
        {
            if (@lock == null)
            {
                return LockMode.DEFAULT;
            }
            else if (@lock.SHARED() != null)
            {
                return LockMode.SHARED;
            }
            else if (@lock.READ() != null && @lock.WRITE() != null)
            {
                return LockMode.READ_WRITE;
            }
            else if (@lock.READ() != null)
            {
                return LockMode.READ;
            }
            else
            {
                return LockMode.WRITE;
            }
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        //
        // Expr
        //
        //
        // Functions
        //
        // n, x$, y$
        // x$, y$
        // x$, n, m
        // x$, n
        //
        // Stmt
        //
        // struct
        // array
        // scalar data type
        // struct
        // scalar data type
        // scalar
        //PuffinBasicAtomTypeId.lookup(compCtx.var2.getText());
        // array
        // list
        // struct
        // scalar data type
        // set
        // dict
        // struct
        // scalar data type
        // struct
        // throw
        // GOTO postFuncDecl
        // LABEL FuncStart
        // Push child scope
        // Copy expr to result
        // Pop declaration scope
        // GOTO Caller
        // LABEL postFuncDecl
        // Patch GOTO postFuncDecl
        // GOTO postFuncDecl
        // LABEL FuncStart
        // Push child scope
        // scalar
        //PuffinBasicAtomTypeId.lookup(compCtx.var2.getText());
        // list
        // struct
        // array
        // scalar data type
        // set
        // dict
        // struct
        // scalar data type
        // struct
        // array
        // throw
        // Copy expr to result
        // GOTO LABEL gotoCaller
        // Pop declaration scope
        // LABEL gotoCaller
        // GOTO Caller
        // LABEL postFuncDecl
        // Patch GOTO LABEL gotoCaller
        // Patch GOTO postFuncDecl
        // Unset current UDF state
        // LABEL beforeWhile
        // expr()
        // NOT expr()
        // If expr is false, GOTO afterWend
        // GOTO LABEL beforeWhile
        // LABEL afterWend
        // Patch GOTO afterWend
        // stepCopy = step or 1 (default)
        // var=init
        // endCopy=end
        // GOTO LABEL CHECK
        // APPLY STEP
        // JUMP here from NEXT
        // Add step
        // CHECK
        // If (step >= 0 and var > end) or (step < 0 and var < end) GOTO after "next"
        // step >= 0
        // Patch GOTO LABEL Check
        // var > end
        // (step >= 0 and var > end)
        // step < 0
        // var < end
        // (step < 0 and var < end)
        // if (true) GOTO after NEXT
        // set linenumber on exitNext().
        // GOTO APPLY STEP
        // LABEL afterNext
        /*
         * condition
         * GOTOIF condition labelBeforeThen
         * GOTO labelAfterThen|labelBeforeElse
         * labelBeforeThen
         * ThenStmts
         * GOTO labelAfterThen|labelAfterElse
         * labelAfterThen
         * ElseStmts
         * labelAfterElse
         */
        // Patch IF true: condition
        // Patch IF true: GOTO labelBeforeThen
        // Patch IF false: GOTO labelAfterThen|labelBeforeElse
        // Patch THEN: GOTO labelAfterThen|labelAfterElse
        // IF condition is true, GOTO labelBeforeThen
        // IF condition is false, GOTO labelAfterThen|labelBeforeElse
        // Add instruction for:
        // THEN GOTO linenum | THEN linenum
        // GOTO labelAfterThen|labelAfterElse
        // Add instruction for:
        // ELSE linenum
        //
        // IF expr THEN BEGIN
        // ...
        // ELSE BEGIN
        // ...
        // END IF
        //
        //
        // expr.result
        // GOTO labelBeforeThen IF expr.result is true
        // GOTO labelAfterThen|labelBeforeElse
        // labelBeforeThen (patch GOTOIF)
        // GOTO labelAfterThen|labelAfterElse (else begin)
        // labelAfterThen
        // labelBeforeElse
        //
        // IF condition is true, GOTO labelBeforeThen
        // IF condition is false, GOTO labelAfterThen|labelBeforeElse
        // Add labelBeforeThen
        // Patch IF true: GOTO labelBeforeThen
        // GOTO labelAfterThen|labelAfterElse
        // GOTO labelAfterThen|labelAfterElse
        // Add labelAfterElse
        // Patch IF true: GOTO labelBeforeThen
        // Patch IF false: GOTO labelAfterThen|labelBeforeElse
        // Patch THEN: GOTO labelAfterThen|labelAfterElse
        // fileName, fileNumber
        // openMode, accessMode
        // lockMode, recordLen
        // fileName, fileNumber
        // openMode, accessMode
        // lockMode, recordLen
        // FileNumber, #fields
        // if fileNumber != null, skip first instruction
        // GraphicsRuntime
        private int GetGotoLineNumberOp1(int lineNumber)
        {
            return ir.GetSymbolTable().AddTmp(INT32, (e) => e.GetValue().SetInt32(lineNumber));
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        //
        // Expr
        //
        //
        // Functions
        //
        // n, x$, y$
        // x$, y$
        // x$, n, m
        // x$, n
        //
        // Stmt
        //
        // struct
        // array
        // scalar data type
        // struct
        // scalar data type
        // scalar
        //PuffinBasicAtomTypeId.lookup(compCtx.var2.getText());
        // array
        // list
        // struct
        // scalar data type
        // set
        // dict
        // struct
        // scalar data type
        // struct
        // throw
        // GOTO postFuncDecl
        // LABEL FuncStart
        // Push child scope
        // Copy expr to result
        // Pop declaration scope
        // GOTO Caller
        // LABEL postFuncDecl
        // Patch GOTO postFuncDecl
        // GOTO postFuncDecl
        // LABEL FuncStart
        // Push child scope
        // scalar
        //PuffinBasicAtomTypeId.lookup(compCtx.var2.getText());
        // list
        // struct
        // array
        // scalar data type
        // set
        // dict
        // struct
        // scalar data type
        // struct
        // array
        // throw
        // Copy expr to result
        // GOTO LABEL gotoCaller
        // Pop declaration scope
        // LABEL gotoCaller
        // GOTO Caller
        // LABEL postFuncDecl
        // Patch GOTO LABEL gotoCaller
        // Patch GOTO postFuncDecl
        // Unset current UDF state
        // LABEL beforeWhile
        // expr()
        // NOT expr()
        // If expr is false, GOTO afterWend
        // GOTO LABEL beforeWhile
        // LABEL afterWend
        // Patch GOTO afterWend
        // stepCopy = step or 1 (default)
        // var=init
        // endCopy=end
        // GOTO LABEL CHECK
        // APPLY STEP
        // JUMP here from NEXT
        // Add step
        // CHECK
        // If (step >= 0 and var > end) or (step < 0 and var < end) GOTO after "next"
        // step >= 0
        // Patch GOTO LABEL Check
        // var > end
        // (step >= 0 and var > end)
        // step < 0
        // var < end
        // (step < 0 and var < end)
        // if (true) GOTO after NEXT
        // set linenumber on exitNext().
        // GOTO APPLY STEP
        // LABEL afterNext
        /*
         * condition
         * GOTOIF condition labelBeforeThen
         * GOTO labelAfterThen|labelBeforeElse
         * labelBeforeThen
         * ThenStmts
         * GOTO labelAfterThen|labelAfterElse
         * labelAfterThen
         * ElseStmts
         * labelAfterElse
         */
        // Patch IF true: condition
        // Patch IF true: GOTO labelBeforeThen
        // Patch IF false: GOTO labelAfterThen|labelBeforeElse
        // Patch THEN: GOTO labelAfterThen|labelAfterElse
        // IF condition is true, GOTO labelBeforeThen
        // IF condition is false, GOTO labelAfterThen|labelBeforeElse
        // Add instruction for:
        // THEN GOTO linenum | THEN linenum
        // GOTO labelAfterThen|labelAfterElse
        // Add instruction for:
        // ELSE linenum
        //
        // IF expr THEN BEGIN
        // ...
        // ELSE BEGIN
        // ...
        // END IF
        //
        //
        // expr.result
        // GOTO labelBeforeThen IF expr.result is true
        // GOTO labelAfterThen|labelBeforeElse
        // labelBeforeThen (patch GOTOIF)
        // GOTO labelAfterThen|labelAfterElse (else begin)
        // labelAfterThen
        // labelBeforeElse
        //
        // IF condition is true, GOTO labelBeforeThen
        // IF condition is false, GOTO labelAfterThen|labelBeforeElse
        // Add labelBeforeThen
        // Patch IF true: GOTO labelBeforeThen
        // GOTO labelAfterThen|labelAfterElse
        // GOTO labelAfterThen|labelAfterElse
        // Add labelAfterElse
        // Patch IF true: GOTO labelBeforeThen
        // Patch IF false: GOTO labelAfterThen|labelBeforeElse
        // Patch THEN: GOTO labelAfterThen|labelAfterElse
        // fileName, fileNumber
        // openMode, accessMode
        // lockMode, recordLen
        // fileName, fileNumber
        // openMode, accessMode
        // lockMode, recordLen
        // FileNumber, #fields
        // if fileNumber != null, skip first instruction
        // GraphicsRuntime
        private void CheckDataTypeMatch(int id1, int id2, string lineSupplier)
        {
            CheckDataTypeMatch(ir.GetSymbolTable()[id1], id2, lineSupplier);
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        //
        // Expr
        //
        //
        // Functions
        //
        // n, x$, y$
        // x$, y$
        // x$, n, m
        // x$, n
        //
        // Stmt
        //
        // struct
        // array
        // scalar data type
        // struct
        // scalar data type
        // scalar
        //PuffinBasicAtomTypeId.lookup(compCtx.var2.getText());
        // array
        // list
        // struct
        // scalar data type
        // set
        // dict
        // struct
        // scalar data type
        // struct
        // throw
        // GOTO postFuncDecl
        // LABEL FuncStart
        // Push child scope
        // Copy expr to result
        // Pop declaration scope
        // GOTO Caller
        // LABEL postFuncDecl
        // Patch GOTO postFuncDecl
        // GOTO postFuncDecl
        // LABEL FuncStart
        // Push child scope
        // scalar
        //PuffinBasicAtomTypeId.lookup(compCtx.var2.getText());
        // list
        // struct
        // array
        // scalar data type
        // set
        // dict
        // struct
        // scalar data type
        // struct
        // array
        // throw
        // Copy expr to result
        // GOTO LABEL gotoCaller
        // Pop declaration scope
        // LABEL gotoCaller
        // GOTO Caller
        // LABEL postFuncDecl
        // Patch GOTO LABEL gotoCaller
        // Patch GOTO postFuncDecl
        // Unset current UDF state
        // LABEL beforeWhile
        // expr()
        // NOT expr()
        // If expr is false, GOTO afterWend
        // GOTO LABEL beforeWhile
        // LABEL afterWend
        // Patch GOTO afterWend
        // stepCopy = step or 1 (default)
        // var=init
        // endCopy=end
        // GOTO LABEL CHECK
        // APPLY STEP
        // JUMP here from NEXT
        // Add step
        // CHECK
        // If (step >= 0 and var > end) or (step < 0 and var < end) GOTO after "next"
        // step >= 0
        // Patch GOTO LABEL Check
        // var > end
        // (step >= 0 and var > end)
        // step < 0
        // var < end
        // (step < 0 and var < end)
        // if (true) GOTO after NEXT
        // set linenumber on exitNext().
        // GOTO APPLY STEP
        // LABEL afterNext
        /*
         * condition
         * GOTOIF condition labelBeforeThen
         * GOTO labelAfterThen|labelBeforeElse
         * labelBeforeThen
         * ThenStmts
         * GOTO labelAfterThen|labelAfterElse
         * labelAfterThen
         * ElseStmts
         * labelAfterElse
         */
        // Patch IF true: condition
        // Patch IF true: GOTO labelBeforeThen
        // Patch IF false: GOTO labelAfterThen|labelBeforeElse
        // Patch THEN: GOTO labelAfterThen|labelAfterElse
        // IF condition is true, GOTO labelBeforeThen
        // IF condition is false, GOTO labelAfterThen|labelBeforeElse
        // Add instruction for:
        // THEN GOTO linenum | THEN linenum
        // GOTO labelAfterThen|labelAfterElse
        // Add instruction for:
        // ELSE linenum
        //
        // IF expr THEN BEGIN
        // ...
        // ELSE BEGIN
        // ...
        // END IF
        //
        //
        // expr.result
        // GOTO labelBeforeThen IF expr.result is true
        // GOTO labelAfterThen|labelBeforeElse
        // labelBeforeThen (patch GOTOIF)
        // GOTO labelAfterThen|labelAfterElse (else begin)
        // labelAfterThen
        // labelBeforeElse
        //
        // IF condition is true, GOTO labelBeforeThen
        // IF condition is false, GOTO labelAfterThen|labelBeforeElse
        // Add labelBeforeThen
        // Patch IF true: GOTO labelBeforeThen
        // GOTO labelAfterThen|labelAfterElse
        // GOTO labelAfterThen|labelAfterElse
        // Add labelAfterElse
        // Patch IF true: GOTO labelBeforeThen
        // Patch IF false: GOTO labelAfterThen|labelBeforeElse
        // Patch THEN: GOTO labelAfterThen|labelAfterElse
        // fileName, fileNumber
        // openMode, accessMode
        // lockMode, recordLen
        // fileName, fileNumber
        // openMode, accessMode
        // lockMode, recordLen
        // FileNumber, #fields
        // if fileNumber != null, skip first instruction
        // GraphicsRuntime
        private void CheckDataTypeMatch(ISTEntry entry1, int id2, string line)
        {
            var entry2 = ir.GetSymbolTable()[id2];
            if ((entry1.GetType().GetAtomTypeId() == PuffinBasicAtomTypeId.STRING && entry2.GetType().GetAtomTypeId() != PuffinBasicAtomTypeId.STRING) 
                || (entry1.GetType().GetAtomTypeId() != PuffinBasicAtomTypeId.STRING && entry2.GetType().GetAtomTypeId() == PuffinBasicAtomTypeId.STRING))
            {
                throw new PuffinBasicSemanticError(DATA_TYPE_MISMATCH, line, "Data type " + entry1.GetType().GetAtomTypeId() + " mismatches with " + entry2.GetType().GetAtomTypeId());
            }
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        //
        // Expr
        //
        //
        // Functions
        //
        // n, x$, y$
        // x$, y$
        // x$, n, m
        // x$, n
        //
        // Stmt
        //
        // struct
        // array
        // scalar data type
        // struct
        // scalar data type
        // scalar
        //PuffinBasicAtomTypeId.lookup(compCtx.var2.getText());
        // array
        // list
        // struct
        // scalar data type
        // set
        // dict
        // struct
        // scalar data type
        // struct
        // throw
        // GOTO postFuncDecl
        // LABEL FuncStart
        // Push child scope
        // Copy expr to result
        // Pop declaration scope
        // GOTO Caller
        // LABEL postFuncDecl
        // Patch GOTO postFuncDecl
        // GOTO postFuncDecl
        // LABEL FuncStart
        // Push child scope
        // scalar
        //PuffinBasicAtomTypeId.lookup(compCtx.var2.getText());
        // list
        // struct
        // array
        // scalar data type
        // set
        // dict
        // struct
        // scalar data type
        // struct
        // array
        // throw
        // Copy expr to result
        // GOTO LABEL gotoCaller
        // Pop declaration scope
        // LABEL gotoCaller
        // GOTO Caller
        // LABEL postFuncDecl
        // Patch GOTO LABEL gotoCaller
        // Patch GOTO postFuncDecl
        // Unset current UDF state
        // LABEL beforeWhile
        // expr()
        // NOT expr()
        // If expr is false, GOTO afterWend
        // GOTO LABEL beforeWhile
        // LABEL afterWend
        // Patch GOTO afterWend
        // stepCopy = step or 1 (default)
        // var=init
        // endCopy=end
        // GOTO LABEL CHECK
        // APPLY STEP
        // JUMP here from NEXT
        // Add step
        // CHECK
        // If (step >= 0 and var > end) or (step < 0 and var < end) GOTO after "next"
        // step >= 0
        // Patch GOTO LABEL Check
        // var > end
        // (step >= 0 and var > end)
        // step < 0
        // var < end
        // (step < 0 and var < end)
        // if (true) GOTO after NEXT
        // set linenumber on exitNext().
        // GOTO APPLY STEP
        // LABEL afterNext
        /*
         * condition
         * GOTOIF condition labelBeforeThen
         * GOTO labelAfterThen|labelBeforeElse
         * labelBeforeThen
         * ThenStmts
         * GOTO labelAfterThen|labelAfterElse
         * labelAfterThen
         * ElseStmts
         * labelAfterElse
         */
        // Patch IF true: condition
        // Patch IF true: GOTO labelBeforeThen
        // Patch IF false: GOTO labelAfterThen|labelBeforeElse
        // Patch THEN: GOTO labelAfterThen|labelAfterElse
        // IF condition is true, GOTO labelBeforeThen
        // IF condition is false, GOTO labelAfterThen|labelBeforeElse
        // Add instruction for:
        // THEN GOTO linenum | THEN linenum
        // GOTO labelAfterThen|labelAfterElse
        // Add instruction for:
        // ELSE linenum
        //
        // IF expr THEN BEGIN
        // ...
        // ELSE BEGIN
        // ...
        // END IF
        //
        //
        // expr.result
        // GOTO labelBeforeThen IF expr.result is true
        // GOTO labelAfterThen|labelBeforeElse
        // labelBeforeThen (patch GOTOIF)
        // GOTO labelAfterThen|labelAfterElse (else begin)
        // labelAfterThen
        // labelBeforeElse
        //
        // IF condition is true, GOTO labelBeforeThen
        // IF condition is false, GOTO labelAfterThen|labelBeforeElse
        // Add labelBeforeThen
        // Patch IF true: GOTO labelBeforeThen
        // GOTO labelAfterThen|labelAfterElse
        // GOTO labelAfterThen|labelAfterElse
        // Add labelAfterElse
        // Patch IF true: GOTO labelBeforeThen
        // Patch IF false: GOTO labelAfterThen|labelBeforeElse
        // Patch THEN: GOTO labelAfterThen|labelAfterElse
        // fileName, fileNumber
        // openMode, accessMode
        // lockMode, recordLen
        // fileName, fileNumber
        // openMode, accessMode
        // lockMode, recordLen
        // FileNumber, #fields
        // if fileNumber != null, skip first instruction
        // GraphicsRuntime
        private void CheckDataTypeMatch(PuffinBasicAtomTypeId dt1, PuffinBasicAtomTypeId dt2, string line)
        {
            if ((dt1 == PuffinBasicAtomTypeId.STRING && dt2 != PuffinBasicAtomTypeId.STRING) || (dt1 != PuffinBasicAtomTypeId.STRING && dt2 == PuffinBasicAtomTypeId.STRING))
            {
                throw new PuffinBasicSemanticError(DATA_TYPE_MISMATCH, line, "Data type " + dt1 + " mismatches with " + dt2);
            }
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        //
        // Expr
        //
        //
        // Functions
        //
        // n, x$, y$
        // x$, y$
        // x$, n, m
        // x$, n
        //
        // Stmt
        //
        // struct
        // array
        // scalar data type
        // struct
        // scalar data type
        // scalar
        //PuffinBasicAtomTypeId.lookup(compCtx.var2.getText());
        // array
        // list
        // struct
        // scalar data type
        // set
        // dict
        // struct
        // scalar data type
        // struct
        // throw
        // GOTO postFuncDecl
        // LABEL FuncStart
        // Push child scope
        // Copy expr to result
        // Pop declaration scope
        // GOTO Caller
        // LABEL postFuncDecl
        // Patch GOTO postFuncDecl
        // GOTO postFuncDecl
        // LABEL FuncStart
        // Push child scope
        // scalar
        //PuffinBasicAtomTypeId.lookup(compCtx.var2.getText());
        // list
        // struct
        // array
        // scalar data type
        // set
        // dict
        // struct
        // scalar data type
        // struct
        // array
        // throw
        // Copy expr to result
        // GOTO LABEL gotoCaller
        // Pop declaration scope
        // LABEL gotoCaller
        // GOTO Caller
        // LABEL postFuncDecl
        // Patch GOTO LABEL gotoCaller
        // Patch GOTO postFuncDecl
        // Unset current UDF state
        // LABEL beforeWhile
        // expr()
        // NOT expr()
        // If expr is false, GOTO afterWend
        // GOTO LABEL beforeWhile
        // LABEL afterWend
        // Patch GOTO afterWend
        // stepCopy = step or 1 (default)
        // var=init
        // endCopy=end
        // GOTO LABEL CHECK
        // APPLY STEP
        // JUMP here from NEXT
        // Add step
        // CHECK
        // If (step >= 0 and var > end) or (step < 0 and var < end) GOTO after "next"
        // step >= 0
        // Patch GOTO LABEL Check
        // var > end
        // (step >= 0 and var > end)
        // step < 0
        // var < end
        // (step < 0 and var < end)
        // if (true) GOTO after NEXT
        // set linenumber on exitNext().
        // GOTO APPLY STEP
        // LABEL afterNext
        /*
         * condition
         * GOTOIF condition labelBeforeThen
         * GOTO labelAfterThen|labelBeforeElse
         * labelBeforeThen
         * ThenStmts
         * GOTO labelAfterThen|labelAfterElse
         * labelAfterThen
         * ElseStmts
         * labelAfterElse
         */
        // Patch IF true: condition
        // Patch IF true: GOTO labelBeforeThen
        // Patch IF false: GOTO labelAfterThen|labelBeforeElse
        // Patch THEN: GOTO labelAfterThen|labelAfterElse
        // IF condition is true, GOTO labelBeforeThen
        // IF condition is false, GOTO labelAfterThen|labelBeforeElse
        // Add instruction for:
        // THEN GOTO linenum | THEN linenum
        // GOTO labelAfterThen|labelAfterElse
        // Add instruction for:
        // ELSE linenum
        //
        // IF expr THEN BEGIN
        // ...
        // ELSE BEGIN
        // ...
        // END IF
        //
        //
        // expr.result
        // GOTO labelBeforeThen IF expr.result is true
        // GOTO labelAfterThen|labelBeforeElse
        // labelBeforeThen (patch GOTOIF)
        // GOTO labelAfterThen|labelAfterElse (else begin)
        // labelAfterThen
        // labelBeforeElse
        //
        // IF condition is true, GOTO labelBeforeThen
        // IF condition is false, GOTO labelAfterThen|labelBeforeElse
        // Add labelBeforeThen
        // Patch IF true: GOTO labelBeforeThen
        // GOTO labelAfterThen|labelAfterElse
        // GOTO labelAfterThen|labelAfterElse
        // Add labelAfterElse
        // Patch IF true: GOTO labelBeforeThen
        // Patch IF false: GOTO labelAfterThen|labelBeforeElse
        // Patch THEN: GOTO labelAfterThen|labelAfterElse
        // fileName, fileNumber
        // openMode, accessMode
        // lockMode, recordLen
        // fileName, fileNumber
        // openMode, accessMode
        // lockMode, recordLen
        // FileNumber, #fields
        // if fileNumber != null, skip first instruction
        // GraphicsRuntime
        internal sealed class UDFState
        {
            internal readonly VariableName variableName;
            internal readonly STUDF udfEntry;
            public Instruction gotoPostFuncDecl;
            public Instruction labelFuncStart;
            public int udfId;
            public readonly IList<Instruction> gotoLabelGotoCaller;
            public UDFState(VariableName variableName, STUDF udfEntry)
            {
                this.variableName = variableName;
                this.udfEntry = udfEntry;
                this.gotoLabelGotoCaller = new List<Instruction>(2);
            }
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        //
        // Expr
        //
        //
        // Functions
        //
        // n, x$, y$
        // x$, y$
        // x$, n, m
        // x$, n
        //
        // Stmt
        //
        // struct
        // array
        // scalar data type
        // struct
        // scalar data type
        // scalar
        //PuffinBasicAtomTypeId.lookup(compCtx.var2.getText());
        // array
        // list
        // struct
        // scalar data type
        // set
        // dict
        // struct
        // scalar data type
        // struct
        // throw
        // GOTO postFuncDecl
        // LABEL FuncStart
        // Push child scope
        // Copy expr to result
        // Pop declaration scope
        // GOTO Caller
        // LABEL postFuncDecl
        // Patch GOTO postFuncDecl
        // GOTO postFuncDecl
        // LABEL FuncStart
        // Push child scope
        // scalar
        //PuffinBasicAtomTypeId.lookup(compCtx.var2.getText());
        // list
        // struct
        // array
        // scalar data type
        // set
        // dict
        // struct
        // scalar data type
        // struct
        // array
        // throw
        // Copy expr to result
        // GOTO LABEL gotoCaller
        // Pop declaration scope
        // LABEL gotoCaller
        // GOTO Caller
        // LABEL postFuncDecl
        // Patch GOTO LABEL gotoCaller
        // Patch GOTO postFuncDecl
        // Unset current UDF state
        // LABEL beforeWhile
        // expr()
        // NOT expr()
        // If expr is false, GOTO afterWend
        // GOTO LABEL beforeWhile
        // LABEL afterWend
        // Patch GOTO afterWend
        // stepCopy = step or 1 (default)
        // var=init
        // endCopy=end
        // GOTO LABEL CHECK
        // APPLY STEP
        // JUMP here from NEXT
        // Add step
        // CHECK
        // If (step >= 0 and var > end) or (step < 0 and var < end) GOTO after "next"
        // step >= 0
        // Patch GOTO LABEL Check
        // var > end
        // (step >= 0 and var > end)
        // step < 0
        // var < end
        // (step < 0 and var < end)
        // if (true) GOTO after NEXT
        // set linenumber on exitNext().
        // GOTO APPLY STEP
        // LABEL afterNext
        /*
         * condition
         * GOTOIF condition labelBeforeThen
         * GOTO labelAfterThen|labelBeforeElse
         * labelBeforeThen
         * ThenStmts
         * GOTO labelAfterThen|labelAfterElse
         * labelAfterThen
         * ElseStmts
         * labelAfterElse
         */
        // Patch IF true: condition
        // Patch IF true: GOTO labelBeforeThen
        // Patch IF false: GOTO labelAfterThen|labelBeforeElse
        // Patch THEN: GOTO labelAfterThen|labelAfterElse
        // IF condition is true, GOTO labelBeforeThen
        // IF condition is false, GOTO labelAfterThen|labelBeforeElse
        // Add instruction for:
        // THEN GOTO linenum | THEN linenum
        // GOTO labelAfterThen|labelAfterElse
        // Add instruction for:
        // ELSE linenum
        //
        // IF expr THEN BEGIN
        // ...
        // ELSE BEGIN
        // ...
        // END IF
        //
        //
        // expr.result
        // GOTO labelBeforeThen IF expr.result is true
        // GOTO labelAfterThen|labelBeforeElse
        // labelBeforeThen (patch GOTOIF)
        // GOTO labelAfterThen|labelAfterElse (else begin)
        // labelAfterThen
        // labelBeforeElse
        //
        // IF condition is true, GOTO labelBeforeThen
        // IF condition is false, GOTO labelAfterThen|labelBeforeElse
        // Add labelBeforeThen
        // Patch IF true: GOTO labelBeforeThen
        // GOTO labelAfterThen|labelAfterElse
        // GOTO labelAfterThen|labelAfterElse
        // Add labelAfterElse
        // Patch IF true: GOTO labelBeforeThen
        // Patch IF false: GOTO labelAfterThen|labelBeforeElse
        // Patch THEN: GOTO labelAfterThen|labelAfterElse
        // fileName, fileNumber
        // openMode, accessMode
        // lockMode, recordLen
        // fileName, fileNumber
        // openMode, accessMode
        // lockMode, recordLen
        // FileNumber, #fields
        // if fileNumber != null, skip first instruction
        // GraphicsRuntime
        internal sealed class WhileLoopState
        {
            public Instruction labelBeforeWhile;
            public Instruction gotoAfterWend;
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        //
        // Expr
        //
        //
        // Functions
        //
        // n, x$, y$
        // x$, y$
        // x$, n, m
        // x$, n
        //
        // Stmt
        //
        // struct
        // array
        // scalar data type
        // struct
        // scalar data type
        // scalar
        //PuffinBasicAtomTypeId.lookup(compCtx.var2.getText());
        // array
        // list
        // struct
        // scalar data type
        // set
        // dict
        // struct
        // scalar data type
        // struct
        // throw
        // GOTO postFuncDecl
        // LABEL FuncStart
        // Push child scope
        // Copy expr to result
        // Pop declaration scope
        // GOTO Caller
        // LABEL postFuncDecl
        // Patch GOTO postFuncDecl
        // GOTO postFuncDecl
        // LABEL FuncStart
        // Push child scope
        // scalar
        //PuffinBasicAtomTypeId.lookup(compCtx.var2.getText());
        // list
        // struct
        // array
        // scalar data type
        // set
        // dict
        // struct
        // scalar data type
        // struct
        // array
        // throw
        // Copy expr to result
        // GOTO LABEL gotoCaller
        // Pop declaration scope
        // LABEL gotoCaller
        // GOTO Caller
        // LABEL postFuncDecl
        // Patch GOTO LABEL gotoCaller
        // Patch GOTO postFuncDecl
        // Unset current UDF state
        // LABEL beforeWhile
        // expr()
        // NOT expr()
        // If expr is false, GOTO afterWend
        // GOTO LABEL beforeWhile
        // LABEL afterWend
        // Patch GOTO afterWend
        // stepCopy = step or 1 (default)
        // var=init
        // endCopy=end
        // GOTO LABEL CHECK
        // APPLY STEP
        // JUMP here from NEXT
        // Add step
        // CHECK
        // If (step >= 0 and var > end) or (step < 0 and var < end) GOTO after "next"
        // step >= 0
        // Patch GOTO LABEL Check
        // var > end
        // (step >= 0 and var > end)
        // step < 0
        // var < end
        // (step < 0 and var < end)
        // if (true) GOTO after NEXT
        // set linenumber on exitNext().
        // GOTO APPLY STEP
        // LABEL afterNext
        /*
         * condition
         * GOTOIF condition labelBeforeThen
         * GOTO labelAfterThen|labelBeforeElse
         * labelBeforeThen
         * ThenStmts
         * GOTO labelAfterThen|labelAfterElse
         * labelAfterThen
         * ElseStmts
         * labelAfterElse
         */
        // Patch IF true: condition
        // Patch IF true: GOTO labelBeforeThen
        // Patch IF false: GOTO labelAfterThen|labelBeforeElse
        // Patch THEN: GOTO labelAfterThen|labelAfterElse
        // IF condition is true, GOTO labelBeforeThen
        // IF condition is false, GOTO labelAfterThen|labelBeforeElse
        // Add instruction for:
        // THEN GOTO linenum | THEN linenum
        // GOTO labelAfterThen|labelAfterElse
        // Add instruction for:
        // ELSE linenum
        //
        // IF expr THEN BEGIN
        // ...
        // ELSE BEGIN
        // ...
        // END IF
        //
        //
        // expr.result
        // GOTO labelBeforeThen IF expr.result is true
        // GOTO labelAfterThen|labelBeforeElse
        // labelBeforeThen (patch GOTOIF)
        // GOTO labelAfterThen|labelAfterElse (else begin)
        // labelAfterThen
        // labelBeforeElse
        //
        // IF condition is true, GOTO labelBeforeThen
        // IF condition is false, GOTO labelAfterThen|labelBeforeElse
        // Add labelBeforeThen
        // Patch IF true: GOTO labelBeforeThen
        // GOTO labelAfterThen|labelAfterElse
        // GOTO labelAfterThen|labelAfterElse
        // Add labelAfterElse
        // Patch IF true: GOTO labelBeforeThen
        // Patch IF false: GOTO labelAfterThen|labelBeforeElse
        // Patch THEN: GOTO labelAfterThen|labelAfterElse
        // fileName, fileNumber
        // openMode, accessMode
        // lockMode, recordLen
        // fileName, fileNumber
        // openMode, accessMode
        // lockMode, recordLen
        // FileNumber, #fields
        // if fileNumber != null, skip first instruction
        // GraphicsRuntime
        internal sealed class ForLoopState
        {
            public Variable variable;
            public Instruction labelApplyStep;
            public Instruction gotoAfterNext;
        }

        //
        // Variable, Number, etc.
        //
        // Scalar
        // Array
        // UDF
        // Create & Push Runtime scope
        // Copy caller params to Runtime scope
        // GOTO labelFuncStart
        // LABEL caller return address
        // Patch address of the caller
        // Pop Runtime scope
        //
        // Expr
        //
        //
        // Functions
        //
        // n, x$, y$
        // x$, y$
        // x$, n, m
        // x$, n
        //
        // Stmt
        //
        // struct
        // array
        // scalar data type
        // struct
        // scalar data type
        // scalar
        //PuffinBasicAtomTypeId.lookup(compCtx.var2.getText());
        // array
        // list
        // struct
        // scalar data type
        // set
        // dict
        // struct
        // scalar data type
        // struct
        // throw
        // GOTO postFuncDecl
        // LABEL FuncStart
        // Push child scope
        // Copy expr to result
        // Pop declaration scope
        // GOTO Caller
        // LABEL postFuncDecl
        // Patch GOTO postFuncDecl
        // GOTO postFuncDecl
        // LABEL FuncStart
        // Push child scope
        // scalar
        //PuffinBasicAtomTypeId.lookup(compCtx.var2.getText());
        // list
        // struct
        // array
        // scalar data type
        // set
        // dict
        // struct
        // scalar data type
        // struct
        // array
        // throw
        // Copy expr to result
        // GOTO LABEL gotoCaller
        // Pop declaration scope
        // LABEL gotoCaller
        // GOTO Caller
        // LABEL postFuncDecl
        // Patch GOTO LABEL gotoCaller
        // Patch GOTO postFuncDecl
        // Unset current UDF state
        // LABEL beforeWhile
        // expr()
        // NOT expr()
        // If expr is false, GOTO afterWend
        // GOTO LABEL beforeWhile
        // LABEL afterWend
        // Patch GOTO afterWend
        // stepCopy = step or 1 (default)
        // var=init
        // endCopy=end
        // GOTO LABEL CHECK
        // APPLY STEP
        // JUMP here from NEXT
        // Add step
        // CHECK
        // If (step >= 0 and var > end) or (step < 0 and var < end) GOTO after "next"
        // step >= 0
        // Patch GOTO LABEL Check
        // var > end
        // (step >= 0 and var > end)
        // step < 0
        // var < end
        // (step < 0 and var < end)
        // if (true) GOTO after NEXT
        // set linenumber on exitNext().
        // GOTO APPLY STEP
        // LABEL afterNext
        /*
         * condition
         * GOTOIF condition labelBeforeThen
         * GOTO labelAfterThen|labelBeforeElse
         * labelBeforeThen
         * ThenStmts
         * GOTO labelAfterThen|labelAfterElse
         * labelAfterThen
         * ElseStmts
         * labelAfterElse
         */
        // Patch IF true: condition
        // Patch IF true: GOTO labelBeforeThen
        // Patch IF false: GOTO labelAfterThen|labelBeforeElse
        // Patch THEN: GOTO labelAfterThen|labelAfterElse
        // IF condition is true, GOTO labelBeforeThen
        // IF condition is false, GOTO labelAfterThen|labelBeforeElse
        // Add instruction for:
        // THEN GOTO linenum | THEN linenum
        // GOTO labelAfterThen|labelAfterElse
        // Add instruction for:
        // ELSE linenum
        //
        // IF expr THEN BEGIN
        // ...
        // ELSE BEGIN
        // ...
        // END IF
        //
        //
        // expr.result
        // GOTO labelBeforeThen IF expr.result is true
        // GOTO labelAfterThen|labelBeforeElse
        // labelBeforeThen (patch GOTOIF)
        // GOTO labelAfterThen|labelAfterElse (else begin)
        // labelAfterThen
        // labelBeforeElse
        //
        // IF condition is true, GOTO labelBeforeThen
        // IF condition is false, GOTO labelAfterThen|labelBeforeElse
        // Add labelBeforeThen
        // Patch IF true: GOTO labelBeforeThen
        // GOTO labelAfterThen|labelAfterElse
        // GOTO labelAfterThen|labelAfterElse
        // Add labelAfterElse
        // Patch IF true: GOTO labelBeforeThen
        // Patch IF false: GOTO labelAfterThen|labelBeforeElse
        // Patch THEN: GOTO labelAfterThen|labelAfterElse
        // fileName, fileNumber
        // openMode, accessMode
        // lockMode, recordLen
        // fileName, fileNumber
        // openMode, accessMode
        // lockMode, recordLen
        // FileNumber, #fields
        // if fileNumber != null, skip first instruction
        // GraphicsRuntime
        internal sealed class IfState
        {
            public Instruction gotoIfConditionTrue;
            public Instruction gotoIfConditionFalse;
            public Instruction gotoFromThenAfterIf;
            public Instruction labelBeforeThen;
            public Instruction labelAfterThen;
            public Instruction labelBeforeElse;
            public Instruction labelAfterElse;
        }
    }
}

