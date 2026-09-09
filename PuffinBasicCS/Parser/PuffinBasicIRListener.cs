//using It.Unimi.Dsi.Fastutil.Ints;
//using It.Unimi.Dsi.Fastutil.Objects;
//using Org.Antlr.V4.Runtime;
//using Org.Antlr.V4.Runtime.Misc;
//using Org.Antlr.V4.Runtime.Tree;
//using Org.Jetbrains.Annotations;
//using Org.Puffinbasic.Antlr4;
//using Org.Puffinbasic.Antlr4.PuffinBasicParser;
namespace PuffinBasicCS.Parser
{
    using PuffinBasicCS.Domain;
    using PuffinBasicCS.Error;
    using static PuffinBasicCS.Domain.STObjects;
    using static PuffinBasicCS.Domain.Variable;
    using static PuffinBasicCS.File.IPuffinBasicFile;
    using static PuffinBasicCS.Parser.PuffinBasicIR;
    //using Java.Util;
    //using Java.Util.Concurrent.Atomic;
    //using Java.Util.Function;
    //using Java.Util.Stream;
    using static PuffinBasicCS.Domain.PuffinBasicSymbolTable;
    using static PuffinBasicCS.Domain.STObjects.PuffinBasicAtomTypeId;
    using static PuffinBasicCS.Domain.STObjects.PuffinBasicTypeId;
    using static PuffinBasicCS.Error.PuffinBasicSemanticError.ErrorCode;
    using static PuffinBasicCS.Parser.LinenumberListener;
    using static PuffinBasicCS.Runtime.Types;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Antlr4.Runtime;
    using Antlr4.Runtime.Tree;
    using Antlr4.Runtime.Misc;
    using Org.Puffinbasic.Antlr;
    using static Org.Puffinbasic.Antlr.PuffinBasicParser;
    using System.Threading;
    using PuffinBasicCS.Runtime;

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
        private readonly LinkedList<WhileLoopState> whileLoopStateList = new LinkedList<WhileLoopState>();
        private readonly LinkedList<ForLoopState> forLoopStateList = new LinkedList<ForLoopState>();
        private readonly LinkedList<IfState> ifStateList = new LinkedList<IfState>();
        private UDFState? currentUdfState;
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
            if (whileLoopStateList.Count != 0)
            {
                throw new PuffinBasicSemanticError(WHILE_WITHOUT_WEND, "<UNKNOWN LINE>", "WHILE without WEND");
            }

            if (forLoopStateList.Count != 0)
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
                throw new PuffinBasicInternalError($"Failed to find instruction for node: {ctx.GetText()}");
            }

            return exprInstruction;
        }

        public override void EnterLine(LineContext ctx)
        {
            this.currentLineNumber = ctx.linenum() != null ? ParseLinenum(ctx.linenum().DECIMAL().GetText()) : Interlocked.Increment(ref this.currentLineNumber);
        }

        //
        // Variable, Number, etc.
        //
        public override void ExitNumber(NumberContext ctx)
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
                    id = ir.SymbolTable.AddTmp(isLong ? PuffinBasicAtomTypeId.INT64 : PuffinBasicAtomTypeId.DOUBLE,
                        (entry) => entry.Value.SetInt64(parsed));
                }
                else
                {
                    id = ir.SymbolTable.AddTmp(isFloat ? PuffinBasicAtomTypeId.FLOAT : PuffinBasicAtomTypeId.INT32,
                        (entry) => entry.Value.SetInt32(Numbers.ParseInt32(strValue, @base, GetCtxString(ctx))));
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
                id = ir.SymbolTable.AddTmp(PuffinBasicAtomTypeId.FLOAT,
                    (entry) => entry.Value.SetFloat32(floatValue));
            }
            else
            {
                var doubleStr = ctx.DOUBLE().GetText();
                if (doubleStr.EndsWith("#"))
                {
                    doubleStr = doubleStr.Substring(0, doubleStr.Length - 1);
                }

                var doubleValue = Numbers.ParseFloat64(doubleStr, GetCtxString(ctx));
                id = ir.SymbolTable.AddTmp(PuffinBasicAtomTypeId.DOUBLE,
                    (entry) => entry.Value.SetFloat64(doubleValue));
            }

            var instr = ir.AddInstruction(sourceFile,
                currentLineNumber, ctx,
                OpCode.VALUE, id, NULL_ID, id);
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
        private Instruction ExitLeafVariable(LeafvariableContext ctx)
        {
            PuffinBasicSymbolTable symbolTable = ir.SymbolTable;
            symbolTable.CheckUnused(ctx.varname().VARNAME().GetText()); // Check that the variable name doesn't match an existing user defined type

            IScope currentScope = symbolTable.GetCurrentScope();

            var variableName = GetVariableNameFromCtx(ctx.varname(), ctx.varsuffix());

            int refId = 0;
            ir.SymbolTable.AddVariableOrUDF(variableName,
                (variableName1) => Variable.Of(variableName1, VariableKindHint.DERIVE_FROM_NAME, GetCtxString(ctx)),
                (varId, varEntry, variable) =>
                {
                    refId = varId;
                    if (variable.IsScalar())
                    {

                        // Scalar
                        if (ctx.expr().Length != 0)
                        {
                            var expr = ctx.expr().ToList();
                            throw new PuffinBasicSemanticError(PuffinBasicSemanticError.ErrorCode.SCALAR_VARIABLE_CANNOT_BE_INDEXED,
                                GetCtxString(ctx),
                                $"Scalar variable cannot be indexed: {variable}");
                        }
                    }
                    else if (variable.IsArray())
                    {
                        if (ctx.expr().Length != 0)
                        {

                            // Array
                            ir.AddInstruction(sourceFile,
                                currentLineNumber, ctx,
                                OpCode.RESET_ARRAY_IDX, varId, NULL_ID, NULL_ID);
                            foreach (var exprCtx in ctx.expr())
                            {
                                var exprInstr = LookupInstruction(exprCtx);
                                ir.AddInstruction(sourceFile,
                                    currentLineNumber, ctx,
                                    OpCode.SET_ARRAY_IDX, varId, exprInstr.result, NULL_ID);
                            }

                            refId = ir.SymbolTable.AddArrayReference((STLValue)varEntry);
                            ir.AddInstruction(sourceFile,
                                currentLineNumber, ctx,
                                OpCode.ARRAYREF, varId, refId, refId);
                        }
                    }
                    else if (variable.IsUDF())
                    {

                        // UDF
                        var udfEntry = (STUDF)varEntry;
                        var udfState = udfStateMap[variable];

                        // Create & Push Runtime scope
                        var pushScopeInstr = ir.AddInstruction(sourceFile,
                            currentLineNumber, ctx,
                            OpCode.PUSH_RT_SCOPE, varId, NULL_ID, NULL_ID);

                        // Copy caller params to Runtime scope
                        if (ctx.expr().Length != udfEntry.GetNumDeclaredParams())
                        {
                            throw new PuffinBasicSemanticError(INSUFFICIENT_UDF_ARGS,
                                GetCtxString(ctx),
                                $"{variable} expects {udfEntry.GetNumDeclaredParams()}, #args passed: {ctx.expr().Length}");
                        }

                        int i = 0;
                        foreach (var exprCtx in ctx.expr())
                        {
                            var exprInstr = LookupInstruction(exprCtx);
                            var declParamId = udfEntry.GetDeclaredParam(i++);
                            ir.AddInstruction(sourceFile,
                                currentLineNumber, ctx,
                                OpCode.PARAM_COPY, exprInstr.result, declParamId, declParamId);
                        }


                        // GOTO labelFuncStart
                        ir.AddInstruction(sourceFile,
                            currentLineNumber, ctx,
                            OpCode.GOTO_LABEL, udfState.labelFuncStart.op1, NULL_ID, NULL_ID);

                        // LABEL caller return address
                        var labelCallerReturn = ir.AddInstruction(sourceFile,
                            currentLineNumber, ctx,
                            OpCode.LABEL, ir.SymbolTable.AddLabel(), NULL_ID, NULL_ID);

                        // Patch address of the caller
                        pushScopeInstr.PatchOp2(labelCallerReturn.op1);

                        // Pop Runtime scope
                        ir.AddInstruction(sourceFile,
                            currentLineNumber, ctx,
                            OpCode.POP_RT_SCOPE, varId, NULL_ID, NULL_ID);
                    }
                });

            return ir.AddInstruction(sourceFile,
                currentLineNumber, ctx,
                OpCode.VARIABLE, refId, NULL_ID, refId);
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
        private Instruction ExitStructVariable(StructvariableContext ctx)
        {
            var root = ctx.varname(0).VARNAME().GetText();
            var rootId = ir.SymbolTable.GetCompositeVariableIdForVariable(new VariableName(root, null, COMPOSITE));
            var structType = ir.SymbolTable[rootId].Type.AsStruct();
            var parentTypeName = structType.GetTypeName();
            for (int i = 1; i < ctx.varname().Length; i++)
            {
                var localStruct = ir.SymbolTable.GetStructType(parentTypeName);
                var childVarname = ctx.varname(i).VARNAME().GetText();
                var childName = new VariableName(childVarname, null, COMPOSITE);
                var childRefId = localStruct.GetMemberRefId(childName);
                var childTypeName = localStruct.GetMemberType(childName).AsStruct().GetTypeName();
                ir.AddInstruction(sourceFile,
                    currentLineNumber, ctx,
                    OpCode.PARAM1,
                    ir.SymbolTable.AddTmp(INT32, (e) => e.Value.SetInt32(childRefId)), NULL_ID, NULL_ID);
                parentTypeName = childTypeName;
            }

            var @struct = ir.SymbolTable.GetStructType(parentTypeName);
            var leafCtx = ctx.leafvariable();
            var leafVarname = leafCtx.varname().VARNAME().GetText();
            var leafDataType = @struct.ContainsMember(new VariableName(leafVarname, null, COMPOSITE))
                ? @struct.GetMemberType(new VariableName(leafVarname, null, COMPOSITE)).AtomTypeId
                : ir.SymbolTable.GetDataTypeFor(leafVarname, leafCtx.varsuffix()?.GetText());
            var leafName = new VariableName(leafVarname, leafDataType.GetRepr(), leafDataType);
            var leafRefId = @struct.GetMemberRefId(leafName);
            var leafType = @struct.GetMemberType(leafName);
            ir.AddInstruction(sourceFile,
                currentLineNumber, ctx,
                OpCode.PARAM1,
                ir.SymbolTable.AddTmp(INT32, (e) => e.Value.SetInt32(leafRefId)), NULL_ID, NULL_ID);
            var result = ir.AddInstruction(sourceFile,
                currentLineNumber, ctx,
                OpCode.STRUCT_LVALUE, rootId, NULL_ID, ir.SymbolTable.AddRef(leafType));
            if (ctx.expr().Any())
            {
                ir.AddInstruction(sourceFile,
                    currentLineNumber, ctx,
                    OpCode.RESET_ARRAY_IDX, result.result, NULL_ID, NULL_ID);
                foreach (var exprCtx in ctx.expr())
                {
                    var exprInstr = LookupInstruction(exprCtx);
                    ir.AddInstruction(sourceFile,
                        currentLineNumber, ctx,
                        OpCode.SET_ARRAY_IDX, result.result, exprInstr.result, NULL_ID);
                }

                var refId = ir.SymbolTable.AddArrayReference((STLValue)ir.SymbolTable[result.result]);
                result = ir.AddInstruction(sourceFile,
                    currentLineNumber, ctx,
                    OpCode.ARRAYREF, result.result, refId, refId);
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
                var copy = ir.SymbolTable.AddTmpCompatibleWith(instruction.result);
                instruction = ir.AddInstruction(sourceFile,
                    currentLineNumber, ctx,
                    OpCode.COPY, instruction.result, copy, copy);
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
        public override void ExitExprVariable(ExprVariableContext ctx)
        {
            var instruction = nodeToInstruction.Get(ctx.variable());
            var varEntry = ir.SymbolTable[instruction.result];
            bool copy = (varEntry is STVariable) && ((STVariable)varEntry).GetVariable().IsUDF();
            if (ctx.MINUS() != null)
            {
                if (ir.SymbolTable[instruction.result].Type.AtomTypeId == PuffinBasicAtomTypeId.STRING)
                {
                    throw new PuffinBasicSemanticError(DATA_TYPE_MISMATCH, GetCtxString(ctx), "Unary minus cannot be used with a String!");
                }

                instruction = ir.AddInstruction(sourceFile,
                    currentLineNumber, ctx,
                    OpCode.UNARY_MINUS, instruction.result, NULL_ID, ir.SymbolTable.AddTmpCompatibleWith(instruction.result));
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
        public override void ExitExprParen(ExprParenContext ctx)
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
        public override void ExitExprNumber(ExprNumberContext ctx)
        {
            var instruction = nodeToInstruction.Get(ctx.number());
            if (ctx.MINUS() != null)
            {
                instruction = ir.AddInstruction(sourceFile,
                    currentLineNumber, ctx,
                    OpCode.UNARY_MINUS, instruction.result, NULL_ID, ir.SymbolTable.AddTmpCompatibleWith(instruction.result));
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
        public override void ExitExprFunc(ExprFuncContext ctx)
        {
            var instruction = nodeToInstruction.Get(ctx.func());
            if (ctx.MINUS() != null)
            {
                instruction = ir.AddInstruction(sourceFile,
                    currentLineNumber, ctx,
                    OpCode.UNARY_MINUS, instruction.result, NULL_ID, ir.SymbolTable.AddTmpCompatibleWith(instruction.result));
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
        public override void ExitExprString(ExprStringContext ctx)
        {
            var v = ctx.@string().STRING();
            var w = v.GetText();

            var text = Unquote(ctx.@string().STRING().GetText());
            var id = ir.SymbolTable.AddTmp(PuffinBasicAtomTypeId.STRING, (entry) => entry.Value.SetString(text));
            CopyAndRegisterExprResult(ctx, ir.AddInstruction(sourceFile,
                currentLineNumber, ctx,
                OpCode.VALUE, id, NULL_ID, id), false);
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
        public override void ExitExprExp(ExprExpContext ctx)
        {
            var expr1 = ctx.expr(0);
            var expr2 = ctx.expr(1);
            int instr1res = LookupInstruction(expr1).result;
            int instr2res = LookupInstruction(expr2).result;
            var dt1 = ir.SymbolTable[instr1res].Type.AtomTypeId;
            var dt2 = ir.SymbolTable[instr2res].Type.AtomTypeId;
            Types.AssertNumeric(dt1, dt2, GetCtxString(ctx));
            var upcast = Types.Upcast(dt1, dt2, GetCtxString(ctx));
            var result = ir.SymbolTable.AddTmp(upcast);
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

            nodeToInstruction.Put(ctx, ir.AddInstruction(sourceFile,
                currentLineNumber, ctx,
                opCode, instr1res, instr2res, result));
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
        public override void ExitExprMulDiv(ExprMulDivContext ctx)
        {
            var expr1 = ctx.expr(0);
            var expr2 = ctx.expr(1);
            int instr1res = LookupInstruction(expr1).result;
            int instr2res = LookupInstruction(expr2).result;
            var dt1 = ir.SymbolTable[instr1res].Type.AtomTypeId;
            var dt2 = ir.SymbolTable[instr2res].Type.AtomTypeId;
            Types.AssertNumeric(dt1, dt2, GetCtxString(ctx));
            var upcast = Types.Upcast(dt1, dt2, GetCtxString(ctx));
            int result;
            OpCode opCode;
            if (ctx.MUL() != null)
            {
                result = ir.SymbolTable.AddTmp(upcast);
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
                result = ir.SymbolTable.AddTmp(upcast);
                opCode = OpCode.IDIV;
            }
            else
            {
                result = ir.SymbolTable.AddTmp(PuffinBasicAtomTypeId.DOUBLE);
                opCode = OpCode.FDIV;
            }

            nodeToInstruction.Put(ctx, ir.AddInstruction(sourceFile,
                currentLineNumber, ctx,
                opCode, instr1res, instr2res, result));
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
        public override void ExitExprMod(ExprModContext ctx)
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
        public override void ExitExprPlusMinus(ExprPlusMinusContext ctx)
        {
            var expr1 = ctx.expr(0);
            var expr2 = ctx.expr(1);
            int instr1res = LookupInstruction(expr1).result;
            int instr2res = LookupInstruction(expr2).result;
            var dt1 = ir.SymbolTable[instr1res].Type.AtomTypeId;
            var dt2 = ir.SymbolTable[instr2res].Type.AtomTypeId;
            bool plus = ctx.PLUS() != null;
            if (dt1 == PuffinBasicAtomTypeId.STRING && dt2 == PuffinBasicAtomTypeId.STRING)
            {
                if (plus)
                {
                    nodeToInstruction.Put(ctx, ir.AddInstruction(sourceFile,
                        currentLineNumber, ctx,
                        OpCode.CONCAT, instr1res, instr2res,
                        ir.SymbolTable.AddTmp(PuffinBasicAtomTypeId.STRING)));
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
                var result = ir.SymbolTable.AddTmp(upcast);
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
                        //break;
                }

                nodeToInstruction.Put(ctx, ir.AddInstruction(sourceFile,
                    currentLineNumber, ctx,
                    opCode, instr1res, instr2res, result));
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
        private void AddArithmeticOpExpr(ParserRuleContext parent, OpCode opCode, ExprContext exprLeft, ExprContext exprRight)
        {
            var exprL = LookupInstruction(exprLeft);
            var exprR = LookupInstruction(exprRight);
            var dt1 = ir.SymbolTable[exprL.result].Type.AtomTypeId;
            var dt2 = ir.SymbolTable[exprR.result].Type.AtomTypeId;
            Types.AssertNumeric(dt1, dt2, GetCtxString(parent));
            var result = ir.SymbolTable.AddTmp(Types.Upcast(dt1, ir.SymbolTable[exprR.result].Type.AtomTypeId,
                GetCtxString(parent)));
            nodeToInstruction.Put(parent, ir.AddInstruction(sourceFile,
                currentLineNumber, parent.Start.StartIndex, parent.Stop.StopIndex,
                opCode, exprL.result, exprR.result, result));
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
        public override void ExitExprRelational(ExprRelationalContext ctx)
        {
            var exprL = LookupInstruction(ctx.expr(0));
            var exprR = LookupInstruction(ctx.expr(1));
            var dt1 = ir.SymbolTable[exprL.result].Type.AtomTypeId;
            var dt2 = ir.SymbolTable[exprR.result].Type.AtomTypeId;
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

            var result = ir.SymbolTable.AddTmp(INT64);
            nodeToInstruction.Put(ctx, ir.AddInstruction(sourceFile,
                currentLineNumber, ctx,
                opCode.Value, exprL.result, exprR.result, result));
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
        public override void ExitExprLogNot(ExprLogNotContext ctx)
        {
            var expr = LookupInstruction(ctx.expr());
            Types.AssertNumeric(ir.SymbolTable[expr.result].Type.AtomTypeId, GetCtxString(ctx));
            var result = ir.SymbolTable.AddTmp(INT64);
            nodeToInstruction.Put(ctx, ir.AddInstruction(sourceFile,
                currentLineNumber, ctx,
                OpCode.NOT, expr.result, NULL_ID, result));
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
        public override void ExitExprLogical(ExprLogicalContext ctx)
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
        public override void ExitExprBitwise(ExprBitwiseContext ctx)
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
        private void AddLogicalOpExpr(ParserRuleContext parent, OpCode opCode, ExprContext exprLeft, ExprContext exprRight)
        {
            var exprL = LookupInstruction(exprLeft);
            var exprR = LookupInstruction(exprRight);
            Types.AssertNumeric(ir.SymbolTable[exprL.result].Type.AtomTypeId, ir.SymbolTable[exprR.result].Type.AtomTypeId, GetCtxString(parent));
            var result = ir.SymbolTable.AddTmp(INT64);
            nodeToInstruction.Put(parent, ir.AddInstruction(sourceFile,
                currentLineNumber, parent.Start.StartIndex, parent.Stop.StopIndex,
                opCode, exprL.result, exprR.result, result));
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
        private void AddBitwiseOpExpr(ParserRuleContext parent, OpCode opCode, ExprContext exprLeft, ExprContext exprRight)
        {
            var exprL = LookupInstruction(exprLeft);
            var exprR = LookupInstruction(exprRight);
            Types.AssertNumeric(ir.SymbolTable[exprL.result].Type.AtomTypeId, ir.SymbolTable[exprR.result].Type.AtomTypeId, GetCtxString(parent));
            var result = ir.SymbolTable.AddTmp(INT64);
            nodeToInstruction.Put(parent, ir.AddInstruction(sourceFile,
                currentLineNumber, parent.Start.StartIndex, parent.Stop.StopIndex,
                opCode, exprL.result, exprR.result, result));
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
        public override void ExitFuncAbs(FuncAbsContext ctx)
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
        public override void ExitFuncAsc(FuncAscContext ctx)
        {
            nodeToInstruction.Put(ctx, AddFuncWithExprInstruction(OpCode.ASC, ctx, ctx.expr(), NumericOrString.STRING, ir.SymbolTable.AddTmp(INT32)));
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
        public override void ExitFuncSin(FuncSinContext ctx)
        {
            nodeToInstruction.Put(ctx, AddFuncWithExprInstruction(OpCode.SIN, ctx, ctx.expr(),
                NumericOrString.NUMERIC, ir.SymbolTable.AddTmp(PuffinBasicAtomTypeId.DOUBLE)));
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
        public override void ExitFuncCos(FuncCosContext ctx)
        {
            nodeToInstruction.Put(ctx, AddFuncWithExprInstruction(OpCode.COS, ctx, ctx.expr(), NumericOrString.NUMERIC,
                ir.SymbolTable.AddTmp(PuffinBasicAtomTypeId.DOUBLE)));
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
        public override void ExitFuncTan(FuncTanContext ctx)
        {
            nodeToInstruction.Put(ctx, AddFuncWithExprInstruction(OpCode.TAN, ctx, ctx.expr(), NumericOrString.NUMERIC,
                ir.SymbolTable.AddTmp(PuffinBasicAtomTypeId.DOUBLE)));
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
        public override void ExitFuncASin(FuncASinContext ctx)
        {
            nodeToInstruction.Put(ctx, AddFuncWithExprInstruction(OpCode.ASIN, ctx, ctx.expr(), NumericOrString.NUMERIC,
                ir.SymbolTable.AddTmp(PuffinBasicAtomTypeId.DOUBLE)));
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
        public override void ExitFuncACos(FuncACosContext ctx)
        {
            nodeToInstruction.Put(ctx, AddFuncWithExprInstruction(OpCode.ACOS, ctx, ctx.expr(), NumericOrString.NUMERIC,
                ir.SymbolTable.AddTmp(PuffinBasicAtomTypeId.DOUBLE)));
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
        public override void ExitFuncAtn(FuncAtnContext ctx)
        {
            nodeToInstruction.Put(ctx, AddFuncWithExprInstruction(OpCode.ATN, ctx, ctx.expr(), NumericOrString.NUMERIC,
                ir.SymbolTable.AddTmp(PuffinBasicAtomTypeId.DOUBLE)));
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
        public override void ExitFuncSinh(FuncSinhContext ctx)
        {
            nodeToInstruction.Put(ctx, AddFuncWithExprInstruction(OpCode.SINH, ctx, ctx.expr(), NumericOrString.NUMERIC,
                ir.SymbolTable.AddTmp(PuffinBasicAtomTypeId.DOUBLE)));
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
        public override void ExitFuncCosh(FuncCoshContext ctx)
        {
            nodeToInstruction.Put(ctx, AddFuncWithExprInstruction(OpCode.COSH, ctx, ctx.expr(), NumericOrString.NUMERIC,
                ir.SymbolTable.AddTmp(PuffinBasicAtomTypeId.DOUBLE)));
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
        public override void ExitFuncTanh(FuncTanhContext ctx)
        {
            nodeToInstruction.Put(ctx, AddFuncWithExprInstruction(OpCode.TANH, ctx, ctx.expr(), NumericOrString.NUMERIC,
                ir.SymbolTable.AddTmp(PuffinBasicAtomTypeId.DOUBLE)));
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
        public override void ExitFuncExp(FuncExpContext ctx)
        {
            nodeToInstruction.Put(ctx, AddFuncWithExprInstruction(OpCode.EEXP, ctx, ctx.expr(), NumericOrString.NUMERIC,
                ir.SymbolTable.AddTmp(PuffinBasicAtomTypeId.DOUBLE)));
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
        public override void ExitFuncLog10(FuncLog10Context ctx)
        {
            nodeToInstruction.Put(ctx, AddFuncWithExprInstruction(OpCode.LOG10, ctx, ctx.expr(), NumericOrString.NUMERIC,
                ir.SymbolTable.AddTmp(PuffinBasicAtomTypeId.DOUBLE)));
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
        public override void ExitFuncLog2(FuncLog2Context ctx)
        {
            nodeToInstruction.Put(ctx, AddFuncWithExprInstruction(OpCode.LOG2, ctx, ctx.expr(), NumericOrString.NUMERIC,
                ir.SymbolTable.AddTmp(PuffinBasicAtomTypeId.DOUBLE)));
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
        public override void ExitFuncToRad(FuncToRadContext ctx)
        {
            nodeToInstruction.Put(ctx, AddFuncWithExprInstruction(OpCode.TORAD, ctx, ctx.expr(), NumericOrString.NUMERIC,
                ir.SymbolTable.AddTmp(PuffinBasicAtomTypeId.DOUBLE)));
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
        public override void ExitFuncToDeg(FuncToDegContext ctx)
        {
            nodeToInstruction.Put(ctx, AddFuncWithExprInstruction(OpCode.TODEG, ctx, ctx.expr(), NumericOrString.NUMERIC,
                ir.SymbolTable.AddTmp(PuffinBasicAtomTypeId.DOUBLE)));
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
        public override void ExitFuncFloor(FuncFloorContext ctx)
        {
            nodeToInstruction.Put(ctx, AddFuncWithExprInstruction(OpCode.FLOOR, ctx, ctx.expr(), NumericOrString.NUMERIC,
                ir.SymbolTable.AddTmp(PuffinBasicAtomTypeId.DOUBLE)));
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
        public override void ExitFuncCeil(FuncCeilContext ctx)
        {
            nodeToInstruction.Put(ctx, AddFuncWithExprInstruction(OpCode.CEIL, ctx, ctx.expr(), NumericOrString.NUMERIC,
                ir.SymbolTable.AddTmp(PuffinBasicAtomTypeId.DOUBLE)));
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
        public override void ExitFuncRound(FuncRoundContext ctx)
        {
            nodeToInstruction.Put(ctx, AddFuncWithExprInstruction(OpCode.ROUND, ctx, ctx.expr(), NumericOrString.NUMERIC,
                ir.SymbolTable.AddTmp(PuffinBasicAtomTypeId.DOUBLE)));
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
        public override void ExitFuncSqr(FuncSqrContext ctx)
        {
            nodeToInstruction.Put(ctx, AddFuncWithExprInstruction(OpCode.SQR, ctx, ctx.expr(), NumericOrString.NUMERIC,
                ir.SymbolTable.AddTmp(PuffinBasicAtomTypeId.DOUBLE)));
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
        public override void ExitFuncCint(FuncCintContext ctx)
        {
            nodeToInstruction.Put(ctx, AddFuncWithExprInstruction(OpCode.CINT, ctx, ctx.expr(), NumericOrString.NUMERIC,
                ir.SymbolTable.AddTmp(INT32)));
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
        public override void ExitFuncClng(FuncClngContext ctx)
        {
            nodeToInstruction.Put(ctx, AddFuncWithExprInstruction(OpCode.CLNG, ctx, ctx.expr(), NumericOrString.NUMERIC,
                ir.SymbolTable.AddTmp(INT64)));
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
        public override void ExitFuncCsng(FuncCsngContext ctx)
        {
            nodeToInstruction.Put(ctx, AddFuncWithExprInstruction(OpCode.CSNG, ctx, ctx.expr(), NumericOrString.NUMERIC,
                ir.SymbolTable.AddTmp(PuffinBasicAtomTypeId.FLOAT)));
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
        public override void ExitFuncCdbl(FuncCdblContext ctx)
        {
            nodeToInstruction.Put(ctx, AddFuncWithExprInstruction(OpCode.CDBL, ctx, ctx.expr(), NumericOrString.NUMERIC,
                ir.SymbolTable.AddTmp(PuffinBasicAtomTypeId.DOUBLE)));
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
        public override void ExitFuncCvi(FuncCviContext ctx)
        {
            nodeToInstruction.Put(ctx, AddFuncWithExprInstruction(OpCode.CVI, ctx, ctx.expr(), NumericOrString.STRING,
                ir.SymbolTable.AddTmp(INT32)));
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
        public override void ExitFuncCvl(FuncCvlContext ctx)
        {
            nodeToInstruction.Put(ctx, AddFuncWithExprInstruction(OpCode.CVL, ctx, ctx.expr(), NumericOrString.STRING,
                ir.SymbolTable.AddTmp(INT64)));
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
        public override void ExitFuncCvs(FuncCvsContext ctx)
        {
            nodeToInstruction.Put(ctx, AddFuncWithExprInstruction(OpCode.CVS, ctx, ctx.expr(), NumericOrString.STRING,
                ir.SymbolTable.AddTmp(PuffinBasicAtomTypeId.FLOAT)));
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
        public override void ExitFuncCvd(FuncCvdContext ctx)
        {
            nodeToInstruction.Put(ctx, AddFuncWithExprInstruction(OpCode.CVD, ctx, ctx.expr(), NumericOrString.STRING,
                ir.SymbolTable.AddTmp(PuffinBasicAtomTypeId.DOUBLE)));
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
        public override void ExitFuncMkiDlr(FuncMkiDlrContext ctx)
        {
            nodeToInstruction.Put(ctx, AddFuncWithExprInstruction(OpCode.MKIDLR, ctx, ctx.expr(), NumericOrString.NUMERIC,
                ir.SymbolTable.AddTmp(PuffinBasicAtomTypeId.STRING)));
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
        public override void ExitFuncMklDlr(FuncMklDlrContext ctx)
        {
            nodeToInstruction.Put(ctx, AddFuncWithExprInstruction(OpCode.MKLDLR, ctx, ctx.expr(), NumericOrString.NUMERIC,
                ir.SymbolTable.AddTmp(PuffinBasicAtomTypeId.STRING)));
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
        public override void ExitFuncMksDlr(FuncMksDlrContext ctx)
        {
            nodeToInstruction.Put(ctx, AddFuncWithExprInstruction(OpCode.MKSDLR, ctx, ctx.expr(), NumericOrString.NUMERIC,
                ir.SymbolTable.AddTmp(PuffinBasicAtomTypeId.STRING)));
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
        public override void ExitFuncMkdDlr(FuncMkdDlrContext ctx)
        {
            nodeToInstruction.Put(ctx, AddFuncWithExprInstruction(OpCode.MKDDLR, ctx, ctx.expr(), NumericOrString.NUMERIC,
                ir.SymbolTable.AddTmp(PuffinBasicAtomTypeId.STRING)));
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
        public override void ExitFuncSpaceDlr(FuncSpaceDlrContext ctx)
        {
            nodeToInstruction.Put(ctx, AddFuncWithExprInstruction(OpCode.SPACEDLR, ctx, ctx.expr(), NumericOrString.NUMERIC,
                ir.SymbolTable.AddTmp(PuffinBasicAtomTypeId.STRING)));
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
        public override void ExitFuncStrDlr(FuncStrDlrContext ctx)
        {
            nodeToInstruction.Put(ctx, AddFuncWithExprInstruction(OpCode.STRDLR, ctx, ctx.expr(), NumericOrString.NUMERIC,
                ir.SymbolTable.AddTmp(PuffinBasicAtomTypeId.STRING)));
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
        public override void ExitFuncVal(FuncValContext ctx)
        {
            nodeToInstruction.Put(ctx, AddFuncWithExprInstruction(OpCode.VAL, ctx, ctx.expr(), NumericOrString.STRING,
                ir.SymbolTable.AddTmp(PuffinBasicAtomTypeId.DOUBLE)));
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
        public override void ExitFuncInt(FuncIntContext ctx)
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
        public override void ExitFuncFix(FuncFixContext ctx)
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
        public override void ExitFuncLog(FuncLogContext ctx)
        {
            nodeToInstruction.Put(ctx, AddFuncWithExprInstruction(OpCode.LOG, ctx, ctx.expr(), NumericOrString.NUMERIC,
                ir.SymbolTable.AddTmp(PuffinBasicAtomTypeId.DOUBLE)));
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
        public override void ExitFuncLen(FuncLenContext ctx)
        {
            var exprInstruction = LookupInstruction(ctx.expr(0));
            var axisId = ctx.axis != null ? LookupInstruction(ctx.axis).result : NULL_ID;
            nodeToInstruction.Put(ctx, ir.AddInstruction(sourceFile,
                currentLineNumber, ctx,
                OpCode.LEN, exprInstruction.result, axisId, ir.SymbolTable.AddTmp(INT32)));
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
        public override void ExitFuncChrDlr(FuncChrDlrContext ctx)
        {
            nodeToInstruction.Put(ctx, AddFuncWithExprInstruction(OpCode.CHRDLR, ctx, ctx.expr(), NumericOrString.NUMERIC,
                ir.SymbolTable.AddTmp(PuffinBasicAtomTypeId.STRING)));
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
        public override void ExitFuncHexDlr(FuncHexDlrContext ctx)
        {
            nodeToInstruction.Put(ctx, AddFuncWithExprInstruction(OpCode.HEXDLR, ctx, ctx.expr(), NumericOrString.NUMERIC,
                ir.SymbolTable.AddTmp(PuffinBasicAtomTypeId.STRING)));
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
        public override void ExitFuncOctDlr(FuncOctDlrContext ctx)
        {
            nodeToInstruction.Put(ctx, AddFuncWithExprInstruction(OpCode.OCTDLR, ctx, ctx.expr(), NumericOrString.NUMERIC,
                ir.SymbolTable.AddTmp(PuffinBasicAtomTypeId.STRING)));
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
        public override void ExitFuncLeftDlr(FuncLeftDlrContext ctx)
        {
            var xdlr = LookupInstruction(ctx.expr(0));
            var n = LookupInstruction(ctx.expr(1));
            Types.AssertString(ir.SymbolTable[xdlr.result].Type.AtomTypeId, GetCtxString(ctx));
            Types.AssertNumeric(ir.SymbolTable[n.result].Type.AtomTypeId, GetCtxString(ctx));
            nodeToInstruction.Put(ctx, ir.AddInstruction(sourceFile,
                currentLineNumber, ctx,
                OpCode.LEFTDLR, xdlr.result, n.result,
                ir.SymbolTable.AddTmp(PuffinBasicAtomTypeId.STRING)));
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
        public override void ExitFuncRightDlr(FuncRightDlrContext ctx)
        {
            var xdlr = LookupInstruction(ctx.expr(0));
            var n = LookupInstruction(ctx.expr(1));
            Types.AssertString(ir.SymbolTable[xdlr.result].Type.AtomTypeId, GetCtxString(ctx));
            Types.AssertNumeric(ir.SymbolTable[n.result].Type.AtomTypeId, GetCtxString(ctx));
            nodeToInstruction.Put(ctx, ir.AddInstruction(sourceFile,
                currentLineNumber, ctx,
                OpCode.RIGHTDLR, xdlr.result, n.result,
                ir.SymbolTable.AddTmp(PuffinBasicAtomTypeId.STRING)));
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
        public override void ExitFuncInstr(FuncInstrContext ctx)
        {
            int xdlr, ydlr, n;
            if (ctx.expr().Length == 3)
            {

                // n, x$, y$
                n = LookupInstruction(ctx.expr(0)).result;
                xdlr = LookupInstruction(ctx.expr(1)).result;
                ydlr = LookupInstruction(ctx.expr(2)).result;
                Types.AssertNumeric(ir.SymbolTable[n].Type.AtomTypeId, GetCtxString(ctx));
            }
            else
            {

                // x$, y$
                n = ir.SymbolTable.AddTmp(INT32, (e) => e.Value.SetInt32(1));
                xdlr = LookupInstruction(ctx.expr(0)).result;
                ydlr = LookupInstruction(ctx.expr(1)).result;
            }

            Types.AssertString(ir.SymbolTable[xdlr].Type.AtomTypeId, GetCtxString(ctx));
            Types.AssertString(ir.SymbolTable[ydlr].Type.AtomTypeId, GetCtxString(ctx));
            ir.AddInstruction(sourceFile,
                currentLineNumber, ctx,
                OpCode.PARAM2, xdlr, ydlr, NULL_ID);
            nodeToInstruction.Put(ctx, ir.AddInstruction(sourceFile,
                currentLineNumber, ctx,
                OpCode.INSTR, n, NULL_ID,
                ir.SymbolTable.AddTmp(INT32)));
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
        public override void ExitFuncMidDlr(FuncMidDlrContext ctx)
        {
            int xdlr, n, m;
            if (ctx.expr().Length == 3)
            {

                // x$, n, m
                xdlr = LookupInstruction(ctx.expr(0)).result;
                n = LookupInstruction(ctx.expr(1)).result;
                m = LookupInstruction(ctx.expr(2)).result;
                Types.AssertNumeric(ir.SymbolTable[m].Type.AtomTypeId, GetCtxString(ctx));
            }
            else
            {

                // x$, n
                xdlr = LookupInstruction(ctx.expr(0)).result;
                n = LookupInstruction(ctx.expr(1)).result;
                m = ir.SymbolTable.AddTmp(INT32, (e) => e.Value.SetInt32(int.MaxValue));
            }

            Types.AssertString(ir.SymbolTable[xdlr].Type.AtomTypeId, GetCtxString(ctx));
            Types.AssertNumeric(ir.SymbolTable[n].Type.AtomTypeId, GetCtxString(ctx));
            ir.AddInstruction(sourceFile,
                currentLineNumber, ctx,
                OpCode.PARAM2, xdlr, n, NULL_ID);
            nodeToInstruction.Put(ctx,
                ir.AddInstruction(sourceFile,
                currentLineNumber, ctx,
                OpCode.MIDDLR, m, NULL_ID,
                ir.SymbolTable.AddTmp(PuffinBasicAtomTypeId.STRING)));
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
        public override void ExitFuncRnd(FuncRndContext ctx)
        {
            nodeToInstruction.Put(ctx, ir.AddInstruction(sourceFile,
                currentLineNumber, ctx,
                OpCode.RND, NULL_ID, NULL_ID,
                ir.SymbolTable.AddTmp(PuffinBasicAtomTypeId.DOUBLE)));
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
        public override void ExitFuncSgn(FuncSgnContext ctx)
        {
            nodeToInstruction.Put(ctx, AddFuncWithExprInstruction(OpCode.SGN, ctx, ctx.expr(), NumericOrString.NUMERIC,
                ir.SymbolTable.AddTmp(INT32)));
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
        public override void ExitFuncTimer(FuncTimerContext ctx)
        {
            nodeToInstruction.Put(ctx, ir.AddInstruction(sourceFile,
                currentLineNumber, ctx,
                OpCode.TIMER, NULL_ID, NULL_ID,
                ir.SymbolTable.AddTmp(PuffinBasicAtomTypeId.DOUBLE)));
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
        public override void ExitFuncTimerMillis(FuncTimerMillisContext ctx)
        {
            nodeToInstruction.Put(ctx, ir.AddInstruction(sourceFile,
                currentLineNumber, ctx,
                OpCode.TIMERMILLIS, NULL_ID, NULL_ID,
                ir.SymbolTable.AddTmp(INT64)));
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
        public override void ExitFuncStringDlr(FuncStringDlrContext ctx)
        {
            int n = LookupInstruction(ctx.expr(0)).result;
            int jOrxdlr = LookupInstruction(ctx.expr(1)).result;
            Types.AssertNumeric(ir.SymbolTable[n].Type.AtomTypeId, GetCtxString(ctx));
            nodeToInstruction.Put(ctx, ir.AddInstruction(sourceFile,
                currentLineNumber, ctx,
                OpCode.STRINGDLR, n, jOrxdlr,
                ir.SymbolTable.AddTmp(PuffinBasicAtomTypeId.STRING)));
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
        public override void ExitFuncLoc(FuncLocContext ctx)
        {
            var fileNumber = LookupInstruction(ctx.expr());
            Types.AssertNumeric(ir.SymbolTable[fileNumber.result].Type.AtomTypeId, GetCtxString(ctx));
            nodeToInstruction.Put(ctx, ir.AddInstruction(sourceFile,
                currentLineNumber, ctx,
                OpCode.LOC, fileNumber.result, NULL_ID,
                ir.SymbolTable.AddTmp(INT32)));
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
        public override void ExitFuncLof(FuncLofContext ctx)
        {
            var fileNumber = LookupInstruction(ctx.expr());
            Types.AssertNumeric(ir.SymbolTable[fileNumber.result].Type.AtomTypeId, GetCtxString(ctx));
            nodeToInstruction.Put(ctx, ir.AddInstruction(sourceFile,
                currentLineNumber, ctx,
                OpCode.LOF, fileNumber.result, NULL_ID,
                ir.SymbolTable.AddTmp(INT64)));
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
        public override void ExitFuncEof(FuncEofContext ctx)
        {
            var fileNumber = LookupInstruction(ctx.expr());
            Types.AssertNumeric(ir.SymbolTable[fileNumber.result].Type.AtomTypeId, GetCtxString(ctx));
            nodeToInstruction.Put(ctx, ir.AddInstruction(sourceFile,
                currentLineNumber, ctx,
                OpCode.EOF, fileNumber.result, NULL_ID,
                ir.SymbolTable.AddTmp(INT32)));
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
        public override void ExitFuncEnvironDlr(FuncEnvironDlrContext ctx)
        {
            var expr = LookupInstruction(ctx.expr());
            Types.AssertString(ir.SymbolTable[expr.result].Type.AtomTypeId, GetCtxString(ctx));
            nodeToInstruction.Put(ctx, ir.AddInstruction(sourceFile,
                currentLineNumber, ctx,
                OpCode.ENVIRONDLR, expr.result, NULL_ID,
                ir.SymbolTable.AddTmp(PuffinBasicAtomTypeId.STRING)));
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
        public override void ExitFuncInputDlr(FuncInputDlrContext ctx)
        {
            var x = LookupInstruction(ctx.expr(0));
            Types.AssertNumeric(ir.SymbolTable[x.result].Type.AtomTypeId, GetCtxString(ctx));
            int fileNumberId;
            if (ctx.expr().Length == 2)
            {
                var fileNumber = LookupInstruction(ctx.expr(1));
                Types.AssertNumeric(ir.SymbolTable[fileNumber.result].Type.AtomTypeId, GetCtxString(ctx));
                fileNumberId = fileNumber.result;
            }
            else
            {
                fileNumberId = ir.SymbolTable.AddTmp(INT32, (e) => e.Value.SetInt32(-1));
            }

            nodeToInstruction.Put(ctx, ir.AddInstruction(sourceFile,
                currentLineNumber, ctx,
                OpCode.INPUTDLR, x.result, fileNumberId, ir.SymbolTable.AddTmp(PuffinBasicAtomTypeId.STRING)));
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
        public override void ExitFuncInkeyDlr(FuncInkeyDlrContext ctx)
        {
            AssertGraphics();
            nodeToInstruction.Put(ctx, ir.AddInstruction(sourceFile,
                currentLineNumber, ctx,
                OpCode.INKEYDLR, NULL_ID, NULL_ID, ir.SymbolTable.AddTmp(PuffinBasicAtomTypeId.STRING)));
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
        public override void ExitFuncE(FuncEContext ctx)
        {
            nodeToInstruction.Put(ctx, ir.AddInstruction(sourceFile,
                currentLineNumber, ctx,
                OpCode.E, NULL_ID, NULL_ID, ir.SymbolTable.AddTmp(PuffinBasicAtomTypeId.DOUBLE)));
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
        public override void ExitFuncPI(FuncPIContext ctx)
        {
            nodeToInstruction.Put(ctx, ir.AddInstruction(sourceFile,
                currentLineNumber, ctx,
                OpCode.PI, NULL_ID, NULL_ID, ir.SymbolTable.AddTmp(PuffinBasicAtomTypeId.DOUBLE)));
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
        public override void ExitFuncMin(FuncMinContext ctx)
        {
            var expr1 = LookupInstruction(ctx.expr(0));
            var expr2 = LookupInstruction(ctx.expr(1));
            var dt1 = ir.SymbolTable[expr1.result].Type.AtomTypeId;
            var dt2 = ir.SymbolTable[expr2.result].Type.AtomTypeId;
            Types.AssertNumeric(dt1, GetCtxString(ctx));
            Types.AssertNumeric(dt2, GetCtxString(ctx));
            var resdt = Types.Upcast(dt1, dt2, GetCtxString(ctx));
            nodeToInstruction.Put(ctx, ir.AddInstruction(sourceFile,
                currentLineNumber, ctx,
                OpCode.MIN, expr1.result, expr2.result, ir.SymbolTable.AddTmp(resdt)));
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
        public override void ExitFuncMax(FuncMaxContext ctx)
        {
            var expr1 = LookupInstruction(ctx.expr(0));
            var expr2 = LookupInstruction(ctx.expr(1));
            var dt1 = ir.SymbolTable[expr1.result].Type.AtomTypeId;
            var dt2 = ir.SymbolTable[expr2.result].Type.AtomTypeId;
            Types.AssertNumeric(dt1, GetCtxString(ctx));
            Types.AssertNumeric(dt2, GetCtxString(ctx));
            var resdt = Types.Upcast(dt1, dt2, GetCtxString(ctx));
            nodeToInstruction.Put(ctx, ir.AddInstruction(sourceFile,
                currentLineNumber, ctx,
                OpCode.MAX, expr1.result, expr2.result, ir.SymbolTable.AddTmp(resdt)));
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
            AssertVariable(ir.SymbolTable[varInstr.result], GetCtxString(ctx));
            var varEntry = (STVariable)ir.SymbolTable[varInstr.result];
            Assert1DArray(varEntry, GetCtxString(ctx));
            if (numeric)
            {
                AssertNumeric(varEntry.Type.AtomTypeId, GetCtxString(ctx));
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
            AssertVariable(ir.SymbolTable[varInstr.result], GetCtxString(ctx));
            var varEntry = (STVariable)ir.SymbolTable[varInstr.result];
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
            AssertVariable(ir.SymbolTable[varInstr.result], GetCtxString(ctx));
            var varEntry = (STVariable)ir.SymbolTable[varInstr.result];
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
        public override void ExitFuncArray1DMin(FuncArray1DMinContext ctx)
        {
            var var1Instr = GetArray1dVariableInstruction(ctx, ctx.variable(), true);
            nodeToInstruction.Put(ctx, ir.AddInstruction(sourceFile,
                currentLineNumber, ctx,
                OpCode.ARRAY1DMIN, var1Instr.result, NULL_ID, ir.SymbolTable.AddTmpCompatibleWith(var1Instr.result)));
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
        public override void ExitFuncArray1DMax(FuncArray1DMaxContext ctx)
        {
            var var1Instr = GetArray1dVariableInstruction(ctx, ctx.variable(), true);
            nodeToInstruction.Put(ctx, ir.AddInstruction(sourceFile,
                currentLineNumber, ctx,
                OpCode.ARRAY1DMAX, var1Instr.result, NULL_ID, ir.SymbolTable.AddTmpCompatibleWith(var1Instr.result)));
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
        public override void ExitFuncArray1DMean(FuncArray1DMeanContext ctx)
        {
            var var1Instr = GetArray1dVariableInstruction(ctx, ctx.variable(), true);
            nodeToInstruction.Put(ctx, ir.AddInstruction(sourceFile,
                currentLineNumber, ctx,
                OpCode.ARRAY1DMEAN, var1Instr.result, NULL_ID, ir.SymbolTable.AddTmp(PuffinBasicAtomTypeId.DOUBLE)));
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
        public override void ExitFuncArray1DSum(FuncArray1DSumContext ctx)
        {
            var var1Instr = GetArray1dVariableInstruction(ctx, ctx.variable(), true);
            nodeToInstruction.Put(ctx, ir.AddInstruction(sourceFile,
                currentLineNumber, ctx,
                OpCode.ARRAY1DSUM, var1Instr.result, NULL_ID, ir.SymbolTable.AddTmp(PuffinBasicAtomTypeId.DOUBLE)));
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
        public override void ExitFuncArray1DStd(FuncArray1DStdContext ctx)
        {
            var var1Instr = GetArray1dVariableInstruction(ctx, ctx.variable(), true);
            nodeToInstruction.Put(ctx, ir.AddInstruction(sourceFile,
                currentLineNumber, ctx,
                OpCode.ARRAY1DSTD, var1Instr.result, NULL_ID, ir.SymbolTable.AddTmp(PuffinBasicAtomTypeId.DOUBLE)));
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
        public override void ExitFuncArray1DMedian(FuncArray1DMedianContext ctx)
        {
            var var1Instr = GetArray1dVariableInstruction(ctx, ctx.variable(), true);
            nodeToInstruction.Put(ctx, ir.AddInstruction(sourceFile,
                currentLineNumber, ctx,
                OpCode.ARRAY1DMEDIAN, var1Instr.result, NULL_ID, ir.SymbolTable.AddTmp(PuffinBasicAtomTypeId.DOUBLE)));
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
        public override void ExitFuncArray1DBinSearch(FuncArray1DBinSearchContext ctx)
        {
            var var1Instr = GetArray1dVariableInstruction(ctx, ctx.variable(), false);
            var expr = LookupInstruction(ctx.expr());
            Types.AssertNumeric(ir.SymbolTable[expr.result].Type.AtomTypeId, GetCtxString(ctx));
            nodeToInstruction.Put(ctx, ir.AddInstruction(sourceFile,
                currentLineNumber, ctx,
                OpCode.ARRAY1DBINSEARCH, var1Instr.result, expr.result, ir.SymbolTable.AddTmp(INT32)));
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
        public override void ExitFuncArray1DPct(FuncArray1DPctContext ctx)
        {
            var var1Instr = GetArray1dVariableInstruction(ctx, ctx.variable(), true);
            var expr = LookupInstruction(ctx.expr());
            Types.AssertNumeric(ir.SymbolTable[expr.result].Type.AtomTypeId, GetCtxString(ctx));
            nodeToInstruction.Put(ctx, ir.AddInstruction(sourceFile,
                currentLineNumber, ctx,
                OpCode.ARRAY1DPCT, var1Instr.result, expr.result, ir.SymbolTable.AddTmp(PuffinBasicAtomTypeId.DOUBLE)));
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
        public override void ExitFuncArray2DFindRow(FuncArray2DFindRowContext ctx)
        {
            var varInstr = GetArray2dVariableInstruction(ctx, ctx.variable());
            var x1 = LookupInstruction(ctx.x1);
            var y1 = LookupInstruction(ctx.y1);
            var x2 = LookupInstruction(ctx.x2);
            var y2 = LookupInstruction(ctx.y2);
            var search = LookupInstruction(ctx.search);
            Types.AssertIntType(ir.SymbolTable[x1.result].Type.AtomTypeId, GetCtxString(ctx));
            Types.AssertIntType(ir.SymbolTable[y1.result].Type.AtomTypeId, GetCtxString(ctx));
            Types.AssertIntType(ir.SymbolTable[x2.result].Type.AtomTypeId, GetCtxString(ctx));
            Types.AssertIntType(ir.SymbolTable[y2.result].Type.AtomTypeId, GetCtxString(ctx));
            Types.AssertIntType(ir.SymbolTable[search.result].Type.AtomTypeId, GetCtxString(ctx));
            ir.AddInstruction(sourceFile,
                currentLineNumber, ctx,
                OpCode.PARAM2, x1.result, y1.result, NULL_ID);
            ir.AddInstruction(sourceFile,
                currentLineNumber, ctx,
                OpCode.PARAM2, x2.result, y2.result, NULL_ID);
            nodeToInstruction.Put(ctx, ir.AddInstruction(sourceFile,
                currentLineNumber, ctx,
                OpCode.ARRAY2DFINDROW, varInstr.result, search.result, ir.SymbolTable.AddTmp(PuffinBasicAtomTypeId.DOUBLE)));
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
        public override void ExitFuncArray2DFindColumn(FuncArray2DFindColumnContext ctx)
        {
            var varInstr = GetArray2dVariableInstruction(ctx, ctx.variable());
            var x1 = LookupInstruction(ctx.x1);
            var y1 = LookupInstruction(ctx.y1);
            var x2 = LookupInstruction(ctx.x2);
            var y2 = LookupInstruction(ctx.y2);
            var search = LookupInstruction(ctx.search);
            Types.AssertIntType(ir.SymbolTable[x1.result].Type.AtomTypeId, GetCtxString(ctx));
            Types.AssertIntType(ir.SymbolTable[y1.result].Type.AtomTypeId, GetCtxString(ctx));
            Types.AssertIntType(ir.SymbolTable[x2.result].Type.AtomTypeId, GetCtxString(ctx));
            Types.AssertIntType(ir.SymbolTable[y2.result].Type.AtomTypeId, GetCtxString(ctx));
            Types.AssertIntType(ir.SymbolTable[search.result].Type.AtomTypeId, GetCtxString(ctx));
            ir.AddInstruction(sourceFile,
                currentLineNumber, ctx,
                OpCode.PARAM2, x1.result, y1.result, NULL_ID);
            ir.AddInstruction(sourceFile,
                currentLineNumber, ctx,
                OpCode.PARAM2, x2.result, y2.result, NULL_ID);
            nodeToInstruction.Put(ctx, ir.AddInstruction(sourceFile,
                currentLineNumber, ctx,
                OpCode.ARRAY2DFINDCOLUMN, varInstr.result, search.result, ir.SymbolTable.AddTmp(PuffinBasicAtomTypeId.DOUBLE)));
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
        public override void ExitFuncHsb2Rgb(FuncHsb2RgbContext ctx)
        {
            var h = LookupInstruction(ctx.expr(0));
            var s = LookupInstruction(ctx.expr(1));
            var b = LookupInstruction(ctx.expr(2));
            Types.AssertNumeric(ir.SymbolTable[h.result].Type.AtomTypeId, GetCtxString(ctx));
            Types.AssertNumeric(ir.SymbolTable[s.result].Type.AtomTypeId, GetCtxString(ctx));
            Types.AssertNumeric(ir.SymbolTable[b.result].Type.AtomTypeId, GetCtxString(ctx));
            ir.AddInstruction(sourceFile,
                currentLineNumber, ctx,
                OpCode.PARAM2, h.result, s.result, NULL_ID);
             nodeToInstruction.Put(ctx, ir.AddInstruction(sourceFile,
                currentLineNumber, ctx,
                OpCode.HSB2RGB, b.result, NULL_ID, ir.SymbolTable.AddTmp(INT32)));
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
        public override void ExitFuncMouseMovedX(FuncMouseMovedXContext ctx)
        {
            AssertGraphics();
            nodeToInstruction.Put(ctx, ir.AddInstruction(sourceFile,
                currentLineNumber, ctx,
                OpCode.MOUSEMOVEDX, NULL_ID, NULL_ID, ir.SymbolTable.AddTmp(INT32)));
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
        public override void ExitFuncMouseMovedY(FuncMouseMovedYContext ctx)
        {
            AssertGraphics();
            nodeToInstruction.Put(ctx, ir.AddInstruction(sourceFile,
                currentLineNumber, ctx,
                OpCode.MOUSEMOVEDY, NULL_ID, NULL_ID, ir.SymbolTable.AddTmp(INT32)));
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
        public override void ExitFuncMouseDraggedX(FuncMouseDraggedXContext ctx)
        {
            AssertGraphics();
            nodeToInstruction.Put(ctx, ir.AddInstruction(sourceFile,
                currentLineNumber, ctx,
                OpCode.MOUSEDRAGGEDX, NULL_ID, NULL_ID, ir.SymbolTable.AddTmp(INT32)));
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
        public override void ExitFuncMouseDraggedY(FuncMouseDraggedYContext ctx)
        {
            AssertGraphics();
            nodeToInstruction.Put(ctx, ir.AddInstruction(sourceFile,
                currentLineNumber, ctx,
                OpCode.MOUSEDRAGGEDY, NULL_ID, NULL_ID, ir.SymbolTable.AddTmp(INT32)));
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
        public override void ExitFuncMouseButtonClicked(FuncMouseButtonClickedContext ctx)
        {
            AssertGraphics();
            nodeToInstruction.Put(ctx, ir.AddInstruction(sourceFile,
                currentLineNumber, ctx,
                OpCode.MOUSEBUTTONCLICKED, NULL_ID, NULL_ID, ir.SymbolTable.AddTmp(INT32)));
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
        public override void ExitFuncMouseButtonPressed(FuncMouseButtonPressedContext ctx)
        {
            AssertGraphics();
            nodeToInstruction.Put(ctx, ir.AddInstruction(sourceFile,
                currentLineNumber, ctx,
                OpCode.MOUSEBUTTONPRESSED, NULL_ID, NULL_ID, ir.SymbolTable.AddTmp(INT32)));
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
        public override void ExitFuncMouseButtonReleased(FuncMouseButtonReleasedContext ctx)
        {
            AssertGraphics();
            nodeToInstruction.Put(ctx, ir.AddInstruction(sourceFile,
                currentLineNumber, ctx,
                OpCode.MOUSEBUTTONRELEASED, NULL_ID, NULL_ID, ir.SymbolTable.AddTmp(INT32)));
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
        public override void ExitFuncIsKeyPressed(FuncIsKeyPressedContext ctx)
        {
            AssertGraphics();
            var expr = LookupInstruction(ctx.expr());
            Types.AssertString(ir.SymbolTable[expr.result].Type.AtomTypeId, GetCtxString(ctx));
            nodeToInstruction.Put(ctx, ir.AddInstruction(sourceFile,
                currentLineNumber, ctx,
                OpCode.ISKEYPRESSED, expr.result, NULL_ID, ir.SymbolTable.AddTmp(INT32)));
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
        public override void ExitFuncMemberMethodCall(FuncMemberMethodCallContext ctx)
        {
            var varInstruction = LookupInstruction(ctx.variable());
            var objectType = ir.SymbolTable[varInstruction.result].Type;
            var funcName = ctx.funcname().GetText();
            var returnType = objectType.GetFuncCallReturnType(funcName);
            IList<PuffinBasicType> paramTypes = new List<PuffinBasicType>(ctx.expr().Length);
            foreach (var exprCtx in ctx.expr())
            {
                var exprInstruction = LookupInstruction(exprCtx);
                ir.AddInstruction(sourceFile,
                    currentLineNumber, ctx,
                    OpCode.PARAM1, exprInstruction.result, NULL_ID, NULL_ID);
                paramTypes.Add(ir.SymbolTable[exprInstruction.result].Type);
            }

            objectType.CheckFuncCallArguments(funcName, paramTypes);
            nodeToInstruction.Put(ctx, ir.AddInstruction(sourceFile,
                currentLineNumber, ctx,
                OpCode.MEMBER_FUNC_CALL, varInstruction.result, 
                ir.SymbolTable.AddTmp(PuffinBasicAtomTypeId.STRING, (e) => e.Value.SetString(funcName)), 
                ir.SymbolTable.AddTmp(returnType)));
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
        public override void ExitFuncSplitDlr(FuncSplitDlrContext ctx)
        {
            var str = LookupInstruction(ctx.expr(0));
            var regex = LookupInstruction(ctx.expr(1));
            Types.AssertString(ir.SymbolTable[str.result].Type.AtomTypeId, GetCtxString(ctx));
            Types.AssertString(ir.SymbolTable[regex.result].Type.AtomTypeId, GetCtxString(ctx));
            nodeToInstruction.Put(ctx, ir.AddInstruction(sourceFile,
                currentLineNumber, ctx,
                OpCode.SPLITDLR, str.result, regex.result, ir.SymbolTable.AddTmp(new ArrayType(PuffinBasicAtomTypeId.STRING))));
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
        public override void ExitFuncAllocArray(FuncAllocArrayContext ctx)
        {
            var elementType = PuffinBasicAtomTypeIdExtensions.Lookup(ctx.varsuffix().GetText());
            foreach (var exprCtx in ctx.expr())
            {
                var exprInstr = LookupInstruction(exprCtx);
                ir.AddInstruction(sourceFile,
                    currentLineNumber, ctx,
                    OpCode.PARAM1, exprInstr.result, NULL_ID, NULL_ID);
            }

            nodeToInstruction.Put(ctx, ir.AddInstruction(sourceFile,
                currentLineNumber, ctx,
                OpCode.ALLOCARRAY, NULL_ID, NULL_ID, ir.SymbolTable.AddTmp(new ArrayType(elementType))));
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
        private Instruction AddFuncWithExprInstruction(OpCode opCode, ParserRuleContext parent, ExprContext expr, NumericOrString numericOrString)
        {
            var exprInstruction = LookupInstruction(expr);
            AssertNumericOrString(exprInstruction.result, parent, numericOrString);
            return ir.AddInstruction(sourceFile,
                currentLineNumber, parent, opCode, exprInstruction.result, NULL_ID, ir.SymbolTable.AddTmpCompatibleWith(exprInstruction.result));
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
        private Instruction AddFuncWithExprInstruction(OpCode opCode, ParserRuleContext parent, ExprContext expr, NumericOrString numericOrString, int result)
        {
            var exprInstruction = LookupInstruction(expr);
            AssertNumericOrString(exprInstruction.result, parent, numericOrString);
            return ir.AddInstruction(sourceFile,
                currentLineNumber, parent, opCode, exprInstruction.result, NULL_ID, result);
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
            var dt = ir.SymbolTable[id].Type.AtomTypeId;
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
        public override void ExitListstmt(ListstmtContext ctx)
        {
            PuffinBasicType itemType;
            if (ctx.typename != null)
            {

                // struct
                var typeName = ctx.typename.VARNAME().GetText();
                itemType = ir.SymbolTable.GetStructType(typeName);
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
            var id = ir.SymbolTable.AddCompositeVariable(variableName, new STVariable(null, new Variable(variableName, listType)));
            ir.AddInstruction(sourceFile,
                currentLineNumber, ctx,
                OpCode.CREATE_INSTANCE, id, NULL_ID, id);
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
        public override void ExitSetstmt(SetstmtContext ctx)
        {
            var atomType = PuffinBasicAtomTypeIdExtensions.Lookup(ctx.typesuffix.GetText());
            PuffinBasicType itemType = new ScalarType(atomType);
            var instanceName = ctx.setname.VARNAME().GetText();
            var variableName = new VariableName(instanceName, null, COMPOSITE);
            var setType = new SetType(itemType);
            var id = ir.SymbolTable.AddCompositeVariable(variableName, new STVariable(null, new Variable(variableName, setType)));
            ir.AddInstruction(sourceFile,
                currentLineNumber, ctx,
                OpCode.CREATE_INSTANCE, id, NULL_ID, id);
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
        public override void ExitDictstmt(DictstmtContext ctx)
        {
            var keyAtomType = PuffinBasicAtomTypeIdExtensions.Lookup(ctx.dictk1.GetText());
            PuffinBasicType keyType = new ScalarType(keyAtomType);
            PuffinBasicType valueType;
            if (ctx.dictv1 != null)
            {

                // struct
                var typeName = ctx.dictv1.VARNAME().GetText();
                valueType = ir.SymbolTable.GetStructType(typeName);
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
            var id = ir.SymbolTable.AddCompositeVariable(variableName, new STVariable(null, new Variable(variableName, dictType)));
            ir.AddInstruction(sourceFile,
                currentLineNumber, ctx,
                OpCode.CREATE_INSTANCE, id, NULL_ID, id);
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
        public override void ExitStructinstancestmt(StructinstancestmtContext ctx)
        {
            var typeName = ctx.varname(0).VARNAME().GetText();
            var instanceName = ctx.varname(1).VARNAME().GetText();
            var variableName = new VariableName(instanceName, null, COMPOSITE);
            var type = ir.SymbolTable.GetStructType(typeName);
            var id = ir.SymbolTable.AddCompositeVariable(variableName, new STVariable(null, new Variable(variableName, type)));
            ir.AddInstruction(sourceFile,
                currentLineNumber, ctx,
                OpCode.CREATE_INSTANCE, id, NULL_ID, id);
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
        public override void ExitStructstmt(StructstmtContext ctx)
        {
            var typeName = ctx.varname().VARNAME().GetText();
            var @struct = new StructType(typeName);
            foreach (var compCtx in ctx.compositetype())
            {
                if (compCtx.var1 != null)
                {

                    // scalar
                    var scalarVarName = compCtx.var1.VARNAME().GetText();
                    var scalarAtomTypeId = ir.SymbolTable.GetDataTypeFor(scalarVarName, compCtx.var2?.GetText());

                    //PuffinBasicAtomTypeId.lookup(compCtx.var2.getText());
                    var name = new VariableName(scalarVarName, scalarAtomTypeId.GetRepr(), scalarAtomTypeId);
                    @struct.DeclareField(name, new ScalarType(name.DataType));
                }
                else if (compCtx.DIM() != null)
                {

                    // array
                    var arrayName = compCtx.elem.VARNAME().GetText();
                    var arrayAtomType = ir.SymbolTable.GetDataTypeFor(arrayName, compCtx.elemsuffix?.GetText());
                    List<int> dims = new List<int>(compCtx.DECIMAL().Length);
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
                        itemType = ir.SymbolTable.GetStructType(compCtx.list1.VARNAME().GetText());
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
                        valueType = ir.SymbolTable.GetStructType(compCtx.dictv1.VARNAME().GetText());
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
                    @struct.DeclareField(name, ir.SymbolTable.GetStructType(memberType));
                }
                else
                {

                    // throw
                    throw new PuffinBasicSemanticError(DATA_TYPE_MISMATCH, GetCtxString(ctx), "Bad struct field: " + compCtx.GetText());
                }
            }

            ir.SymbolTable.AddStructType(typeName, @struct);
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
        public override void ExitComment(CommentContext ctx)
        {
            ir.AddInstruction(sourceFile,
                currentLineNumber, ctx,
                OpCode.COMMENT, NULL_ID, NULL_ID, NULL_ID);
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
        public override void ExitLetstmt(LetstmtContext ctx)
        {
            var varInstruction = LookupInstruction(ctx.variable());
            var exprInstruction = LookupInstruction(ctx.expr());
            var varType = ir.SymbolTable[varInstruction.result].Type;
            if (varType.TypeId == UDF)
            {
                throw new PuffinBasicSemanticError(BAD_ASSIGNMENT, GetCtxString(ctx), "Can't assign to UDF: " + varType);
            }

            if (!varType.IsCompatibleWith(ir.SymbolTable[exprInstruction.result].Type))
            {
                throw new PuffinBasicSemanticError(DATA_TYPE_MISMATCH, GetCtxString(ctx), $"Data type {varType} mismatches with {ir.SymbolTable[exprInstruction.result].Type}");
            }

            var assignInstruction = ir.AddInstruction(sourceFile,
                currentLineNumber, ctx,
                OpCode.ASSIGN, exprInstruction.result, varInstruction.result, varInstruction.result);
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
        public override void ExitAutoletstmt(AutoletstmtContext ctx)
        {
            var varname = ctx.varname().GetText();
            var exprInstruction = LookupInstruction(ctx.expr());
            var resultType = ir.SymbolTable[exprInstruction.result].Type;
            int varId = ir.SymbolTable.AddVariableOrUDF(new VariableName(varname, null, resultType.AtomTypeId), 
                (variableName1) => new Variable(variableName1, resultType), 
                (id, entry, v1) =>
            {
            });
            ir.AddInstruction(sourceFile,
                currentLineNumber, ctx,
                OpCode.VARREF, exprInstruction.result, varId, NULL_ID);
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
        public override void ExitPrintstmt(PrintstmtContext ctx)
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
        public override void ExitPrinthashstmt(PrinthashstmtContext ctx)
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
        private void HandlePrintstmt(ParserRuleContext ctx, IList<IParseTree> children, Instruction? fileNumber)
        {
            bool endsWithNewline = true;
            foreach (IParseTree child in children)
            {
                if (child is ExprContext)
                {
                    var exprInstruction = LookupInstruction((ExprContext)child);
                    ir.AddInstruction(sourceFile,
                        currentLineNumber, ctx,
                        OpCode.PRINT, exprInstruction.result, NULL_ID, NULL_ID);
                    endsWithNewline = true;
                }
                else
                {
                    endsWithNewline = false;
                }
            }

            if (endsWithNewline || fileNumber != null)
            {
                var newlineId = ir.SymbolTable.AddTmp(PuffinBasicAtomTypeId.STRING, (entry) => entry.Value.SetString(Environment.NewLine));
                ir.AddInstruction(sourceFile,
                    currentLineNumber, ctx,
                    OpCode.PRINT, newlineId, NULL_ID, NULL_ID);
            }

            int fileNumberId;
            if (fileNumber != null)
            {
                Types.AssertNumeric(ir.SymbolTable[fileNumber.result].Type.AtomTypeId, GetCtxString(ctx));
                fileNumberId = fileNumber.result;
            }
            else
            {
                fileNumberId = NULL_ID;
            }

            ir.AddInstruction(sourceFile,
                currentLineNumber, ctx,
                OpCode.FLUSH, fileNumberId, NULL_ID, NULL_ID);
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
        public override void ExitPrintusingstmt(PrintusingstmtContext ctx)
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
        public override void ExitPrinthashusingstmt(PrinthashusingstmtContext ctx)
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
        private void HandlePrintusing(ParserRuleContext ctx, ExprContext formatCtx, IList<IParseTree> children, Instruction? fileNumber)
        {
            var format = LookupInstruction(formatCtx);
            bool endsWithNewline = true;
            foreach (IParseTree child in children)
            {
                if (child is ExprContext)
                {
                    var exprInstruction = LookupInstruction((ExprContext)child);
                    ir.AddInstruction(sourceFile,
                        currentLineNumber, ctx,
                        OpCode.PRINTUSING, format.result, exprInstruction.result, NULL_ID);
                    endsWithNewline = true;
                }
                else
                {
                    endsWithNewline = false;
                }
            }

            if (endsWithNewline || fileNumber != null)
            {
                var newlineId = ir.SymbolTable.AddTmp(PuffinBasicAtomTypeId.STRING, (entry) => entry.Value.SetString(Environment.NewLine));
                ir.AddInstruction(sourceFile,
                    currentLineNumber, ctx,
                    OpCode.PRINT, newlineId, NULL_ID, NULL_ID);
            }

            int fileNumberId;
            if (fileNumber != null)
            {
                Types.AssertNumeric(ir.SymbolTable[fileNumber.result].Type.AtomTypeId, GetCtxString(ctx));
                fileNumberId = fileNumber.result;
            }
            else
            {
                fileNumberId = NULL_ID;
            }

            ir.AddInstruction(sourceFile,
                currentLineNumber, ctx,
                OpCode.FLUSH, fileNumberId, NULL_ID, NULL_ID);
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
        public override void ExitDimstmt(DimstmtContext ctx)
        {
            List<int> dims = new List<int>(ctx.expr().Length);
            for (int i = 0; i < ctx.expr().Length; i++)
            {
                dims.Add(0);
            }

            var variableName = GetVariableNameFromCtx(ctx.varname(), ctx.varsuffix());
            var varId = ir.SymbolTable.AddVariableOrUDF(variableName, 
                (variableName1) => new Variable(variableName1, new ArrayType(variableName1.DataType, dims, true)), 
                (id, entry, v1) => entry.Value.SetArrayDimensions(dims));
            foreach (var expr in ctx.expr())
            {
                var dimi = LookupInstruction(expr);
                Types.AssertNumeric(ir.SymbolTable[dimi.result].Type.AtomTypeId, GetCtxString(ctx));
                ir.AddInstruction(sourceFile,
                    currentLineNumber, ctx,
                    OpCode.PARAM1, dimi.result, NULL_ID, NULL_ID);
            }

            ir.AddInstruction(sourceFile,
                currentLineNumber, ctx,
                OpCode.DIM, varId, NULL_ID, NULL_ID);
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
        public override void ExitReallocstmt(ReallocstmtContext ctx)
        {
            List<int> dims = new List<int>(ctx.expr().Length);
            for (int i = 0; i < ctx.expr().Length; i++)
            {
                dims.Add(0);
            }

            var variableName = GetVariableNameFromCtx(ctx.varname(), ctx.varsuffix());
            var varId = ir.SymbolTable.AddVariableOrUDF(variableName, 
                (variableName1) => new Variable(variableName1, new ArrayType(variableName1.DataType, dims, true)), 
                (id, entry, v1) => entry.Value.SetArrayDimensions(dims));
            foreach (var expr in ctx.expr())
            {
                var dimi = LookupInstruction(expr);
                Types.AssertNumeric(ir.SymbolTable[dimi.result].Type.AtomTypeId, GetCtxString(ctx));
                ir.AddInstruction(sourceFile,
                    currentLineNumber, ctx,
                    OpCode.PARAM1, dimi.result, NULL_ID, NULL_ID);
            }

            ir.AddInstruction(sourceFile,
                currentLineNumber, ctx,
                OpCode.REALLOCARRAY, varId, NULL_ID, NULL_ID);
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
        public override void EnterDeffnstmt(DeffnstmtContext ctx)
        {
            var variableName = GetVariableNameFromCtx(ctx.varname(), ctx.varsuffix());

            ir.SymbolTable.AddVariableOrUDF(variableName,
                (variableName1) => Variable.Of(variableName1, VariableKindHint.DERIVE_FROM_NAME, GetCtxString(ctx)),
                (varId, varEntry, variable) =>
                {
                    var udfState = new UDFState(variableName, (STUDF)varEntry);
                    udfStateMap[variable] = udfState;

                    // GOTO postFuncDecl
                    udfState.gotoPostFuncDecl = ir.AddInstruction(sourceFile,
                        currentLineNumber, ctx,
                        OpCode.GOTO_LABEL,
                        ir.SymbolTable.AddGotoTarget(), NULL_ID, NULL_ID);

                    // LABEL FuncStart
                    udfState.labelFuncStart = ir.AddInstruction(sourceFile,
                        currentLineNumber, ctx,
                        OpCode.LABEL,
                        ir.SymbolTable.AddLabel(), NULL_ID, NULL_ID);

                    // Push child scope
                    ir.SymbolTable.PushDeclarationScope(varId, false);
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
        public override void ExitDeffnstmt(DeffnstmtContext ctx)
        {
            var variableName = GetVariableNameFromCtx(ctx.varname(), ctx.varsuffix());

            ir.SymbolTable.AddVariableOrUDF(variableName,
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
                    ir.AddInstruction(sourceFile,
                        currentLineNumber, ctx,
                        OpCode.COPY, exprInstr.result, varId, varId);

                    // Pop declaration scope
                    ir.SymbolTable.PopScope();

                    // GOTO Caller
                    ir.AddInstruction(sourceFile,
                        currentLineNumber, ctx,
                        OpCode.GOTO_CALLER, NULL_ID, NULL_ID, NULL_ID);

                    // LABEL postFuncDecl
                    var labelPostFuncDecl = ir.AddInstruction(sourceFile,
                        currentLineNumber, ctx,
                        OpCode.LABEL,
                        ir.SymbolTable.AddLabel(), NULL_ID, NULL_ID);

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
        private VariableName GetVariableNameFromCtx(VarnameContext varnameCtx, VarsuffixContext varsuffixCtx)
        {
            var varname = varnameCtx.GetText();
            var varsuffix = varsuffixCtx != null ? varsuffixCtx.GetText() : null;
            var dataType = ir.SymbolTable.GetDataTypeFor(varname, varsuffix);
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
        public override void EnterFunctionbeginstmt(FunctionbeginstmtContext ctx)
        {
            var variableName = GetVariableNameFromCtx(ctx.varname(), ctx.varsuffix());
            var udfId = ir.SymbolTable.AddVariableOrUDF(variableName, 
                (variableName1) => Variable.Of(variableName1, VariableKindHint.UDF, GetCtxString(ctx)), 
                (varId, varEntry, variable) =>
            {
                if (currentUdfState != null)
                {
                    throw new PuffinBasicSemanticError(BAD_FUNCTION_DEF, GetCtxString(ctx), "Function " + variableName + " defined in another function: " + currentUdfState.variableName);
                }

                currentUdfState = new UDFState(variableName, (STUDF)varEntry);
                udfStateMap[variable] = currentUdfState;

                // GOTO postFuncDecl
                currentUdfState.gotoPostFuncDecl = ir.AddInstruction(sourceFile,
                    currentLineNumber, ctx,
                    OpCode.GOTO_LABEL, ir.SymbolTable.AddGotoTarget(), NULL_ID, NULL_ID);

                // LABEL FuncStart
                currentUdfState.labelFuncStart = ir.AddInstruction(sourceFile,
                    currentLineNumber, ctx,
                    OpCode.LABEL, ir.SymbolTable.AddLabel(), NULL_ID, NULL_ID);

                // Push child scope
                ir.SymbolTable.PushDeclarationScope(varId, true);
            });
            #pragma warning disable CS8602 // Dereference of a possibly null reference.
            currentUdfState.udfId = udfId;
            #pragma warning restore CS8602 // Dereference of a possibly null reference.
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
        public override void ExitFunctionbeginstmt(FunctionbeginstmtContext ctx)
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
                    var scalarAtomTypeId = ir.SymbolTable.GetDataTypeFor(scalarVarName, compCtx.var2?.GetText());

                    //PuffinBasicAtomTypeId.lookup(compCtx.var2.getText());
                    paramName = new VariableName(scalarVarName, scalarAtomTypeId.GetRepr(), scalarAtomTypeId);
                    paramType = new ScalarType(paramName.DataType);
                }
                else if (compCtx.LIST() != null)
                {

                    // list
                    paramName = new VariableName(compCtx.elem.VARNAME().GetText(), null, COMPOSITE);
                    PuffinBasicType itemType;
                    if (compCtx.list1 != null)
                    {

                        // struct
                        itemType = ir.SymbolTable.GetStructType(compCtx.list1.VARNAME().GetText());
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
                        valueType = ir.SymbolTable.GetStructType(compCtx.dictv1.VARNAME().GetText());
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
                    paramType = ir.SymbolTable.GetStructType(memberType);
                }
                else if (compCtx.DIM() != null)
                {

                    // array
                    var arrayName = compCtx.elem.VARNAME().GetText();
                    var arrayAtomType = ir.SymbolTable.GetDataTypeFor(arrayName, compCtx.elemsuffix?.GetText());
                    List<int> dims = new List<int>(compCtx.DECIMAL().Length);
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

                var paramId = ir.SymbolTable.AddVariableOrUDF(paramName, 
                    (variableName1) => new Variable(variableName1, paramType), 
                    (varId, varEntry, variable) =>
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
        public override void ExitFunctionreturnstmt(FunctionreturnstmtContext ctx)
        {
            if (currentUdfState == null)
            {
                throw new PuffinBasicSemanticError(BAD_FUNCTION_DEF, GetCtxString(ctx), "Function return called without function begin!");
            }

            var udfId = currentUdfState.udfId;
            var returnInstr = LookupInstruction(ctx.expr());
            CheckDataTypeMatch(udfId, returnInstr.result, GetCtxString(ctx));

            // Copy expr to result
            ir.AddInstruction(sourceFile,
                currentLineNumber, ctx,
                OpCode.COPY, returnInstr.result, udfId, udfId);

            // GOTO LABEL gotoCaller
            currentUdfState.gotoLabelGotoCaller.Add(ir.AddInstruction(sourceFile,
                currentLineNumber, ctx,
                OpCode.GOTO_LABEL, ir.SymbolTable.AddGotoTarget(), NULL_ID, NULL_ID));
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
        public override void ExitFunctionendstmt(FunctionendstmtContext ctx)
        {
            if (currentUdfState == null)
            {
                throw new PuffinBasicSemanticError(BAD_FUNCTION_DEF, GetCtxString(ctx), "Function return called without function begin!");
            }


            // Pop declaration scope
            ir.SymbolTable.PopScope();

            // LABEL gotoCaller
            var labelGotocaller = ir.AddInstruction(sourceFile,
                currentLineNumber, ctx,
                OpCode.LABEL, ir.SymbolTable.AddLabel(), NULL_ID, NULL_ID);

            // GOTO Caller
            ir.AddInstruction(sourceFile,
                currentLineNumber, ctx,
                OpCode.GOTO_CALLER, NULL_ID, NULL_ID, NULL_ID);

            // LABEL postFuncDecl
            var labelPostFuncDecl = ir.AddInstruction(sourceFile,
                currentLineNumber, ctx,
                OpCode.LABEL, ir.SymbolTable.AddLabel(), NULL_ID, NULL_ID);

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
        public override void ExitImportstmt(ImportstmtContext ctx)
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
        public override void ExitEndstmt(EndstmtContext ctx)
        {
            ir.AddInstruction(sourceFile,
                currentLineNumber, ctx,
                OpCode.END, NULL_ID, NULL_ID, NULL_ID);
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
        public override void EnterWhilestmt(WhilestmtContext ctx)
        {
            var whileLoopState = new WhileLoopState();

            // LABEL beforeWhile
            whileLoopState.labelBeforeWhile = ir.AddInstruction(sourceFile,
                currentLineNumber, ctx,
                OpCode.LABEL, ir.SymbolTable.AddLabel(), NULL_ID, NULL_ID);
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
        public override void ExitWhilestmt(WhilestmtContext ctx)
        {
            var whileLoopState = whileLoopStateList.Last();

            // expr()
            var expr = LookupInstruction(ctx.expr());

            // NOT expr()
            var notExpr = ir.AddInstruction(sourceFile,
                currentLineNumber, ctx,
                OpCode.NOT, expr.result, NULL_ID, ir.SymbolTable.AddTmp(INT64));

            // If expr is false, GOTO afterWend
            whileLoopState.gotoAfterWend = ir.AddInstruction(sourceFile,
                currentLineNumber, ctx,
                OpCode.GOTO_LABEL_IF, notExpr.result, ir.SymbolTable.AddLabel(), NULL_ID);
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
        public override void ExitWendstmt(WendstmtContext ctx)
        {
            //if (whileLoopStateList.IsEmpty())
            if (whileLoopStateList.Count == 0)
            {
                throw new PuffinBasicSemanticError(WEND_WITHOUT_WHILE, GetCtxString(ctx), "Wend without while");
            }

            var whileLoopState = whileLoopStateList.Last();
            whileLoopStateList.RemoveLast();

            // GOTO LABEL beforeWhile
            ir.AddInstruction(sourceFile,
                currentLineNumber, ctx,
                OpCode.GOTO_LABEL, whileLoopState.labelBeforeWhile.op1, NULL_ID, NULL_ID);

            // LABEL afterWend
            var labelAfterWend = ir.AddInstruction(sourceFile,
                currentLineNumber, ctx,
                OpCode.LABEL, ir.SymbolTable.AddLabel(), NULL_ID, NULL_ID);

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
        private OpCode GetLTOpCode(PuffinBasicAtomTypeId dt1, PuffinBasicAtomTypeId dt2)
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
        private OpCode GetGTOpCode(PuffinBasicAtomTypeId dt1, PuffinBasicAtomTypeId dt2)
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
        private OpCode GetGEOpCode(PuffinBasicAtomTypeId dt1, PuffinBasicAtomTypeId dt2)
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
        public override void ExitForstmt(ForstmtContext ctx)
        {
            var varInstr = LookupInstruction(ctx.variable());
            var init = LookupInstruction(ctx.expr(0));
            var end = LookupInstruction(ctx.expr(1));
            Types.AssertNumeric(ir.SymbolTable[init.result].Type.AtomTypeId, GetCtxString(ctx));
            Types.AssertNumeric(ir.SymbolTable[end.result].Type.AtomTypeId, GetCtxString(ctx));
            var forLoopState = new ForLoopState();
            var stVariable = (STVariable)ir.SymbolTable[varInstr.result];
            forLoopState.variable = stVariable.GetVariable();

            // stepCopy = step or 1 (default)
            Instruction stepCopy;
            if (ctx.expr(2) != null)
            {
                var step = LookupInstruction(ctx.expr(2));
                Types.AssertNumeric(ir.SymbolTable[step.result].Type.AtomTypeId, GetCtxString(ctx));
                var tmpStep = ir.SymbolTable.AddTmpCompatibleWith(step.result);
                stepCopy = ir.AddInstruction(sourceFile,
                    currentLineNumber, ctx,
                    OpCode.COPY, step.result, tmpStep, tmpStep);
            }
            else
            {
                var tmpStep = ir.SymbolTable.AddTmp(INT32, (e) => e.Value.SetInt32(1));
                stepCopy = ir.AddInstruction(sourceFile,
                    currentLineNumber, ctx,
                    OpCode.VALUE, tmpStep, NULL_ID, tmpStep);
            }


            // var=init
            ir.AddInstruction(sourceFile,
                currentLineNumber, ctx,
                OpCode.ASSIGN, init.result, varInstr.result, varInstr.result);

            // endCopy=end
            var tmpEnd = ir.SymbolTable.AddTmpCompatibleWith(end.result);
            var endCopy = ir.AddInstruction(sourceFile,
                currentLineNumber, ctx,
                OpCode.ASSIGN, end.result, tmpEnd, tmpEnd);

            // GOTO LABEL CHECK
            var gotoLabelCheck = ir.AddInstruction(sourceFile,
                currentLineNumber, ctx,
                OpCode.GOTO_LABEL, ir.SymbolTable.AddGotoTarget(), NULL_ID, NULL_ID);

            // APPLY STEP
            // JUMP here from NEXT
            forLoopState.labelApplyStep = ir.AddInstruction(sourceFile,
                currentLineNumber, ctx,
                OpCode.LABEL, ir.SymbolTable.AddLabel(), NULL_ID, NULL_ID);

            // Add step
            var tmpAdd = ir.SymbolTable.AddTmpCompatibleWith(varInstr.result);
            OpCode addOpCode;
            switch (stVariable.Type.AtomTypeId)
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
                    throw new PuffinBasicInternalError("Bad type: " + stVariable.Type.AtomTypeId);
                    //break;
            }

            ir.AddInstruction(sourceFile,
                currentLineNumber, ctx, addOpCode, varInstr.result, stepCopy.result, tmpAdd);
            ir.AddInstruction(sourceFile,
                currentLineNumber, ctx,
                OpCode.ASSIGN, tmpAdd, varInstr.result, varInstr.result);

            // CHECK
            // If (step >= 0 and var > end) or (step < 0 and var < end) GOTO after "next"
            // step >= 0
            var labelCheck = ir.AddInstruction(sourceFile,
                currentLineNumber, ctx,
                OpCode.LABEL, ir.SymbolTable.AddLabel(), NULL_ID, NULL_ID);
            var zero = ir.SymbolTable.AddTmp(INT32, (e) => e.Value.SetInt32(0));
            var t1 = ir.SymbolTable.AddTmp(INT32);
            ir.AddInstruction(sourceFile,
                currentLineNumber, ctx, 
                GetGEOpCode(ir.SymbolTable[stepCopy.result].Type.AtomTypeId, INT32), stepCopy.result, zero, t1);

            // Patch GOTO LABEL Check
            gotoLabelCheck.PatchOp1(labelCheck.op1);

            // var > end
            var t2 = ir.SymbolTable.AddTmp(INT32);
            ir.AddInstruction(sourceFile,
                currentLineNumber, ctx, 
                GetGTOpCode(
                    ir.SymbolTable[varInstr.result].Type.AtomTypeId, 
                    ir.SymbolTable[endCopy.result].Type.AtomTypeId), 
                varInstr.result, endCopy.result, t2);

            // (step >= 0 and var > end)
            var t3 = ir.SymbolTable.AddTmp(INT32);
            ir.AddInstruction(sourceFile,
                currentLineNumber, ctx,
                OpCode.AND, t1, t2, t3);

            // step < 0
            var t4 = ir.SymbolTable.AddTmp(INT32);
            ir.AddInstruction(sourceFile,
                currentLineNumber, ctx, 
                GetLTOpCode(
                    ir.SymbolTable[stepCopy.result].Type.AtomTypeId, INT32), 
                stepCopy.result, zero, t4);

            // var < end
            var t5 = ir.SymbolTable.AddTmp(INT32);
            ir.AddInstruction(sourceFile,
                currentLineNumber, ctx, 
                GetLTOpCode(
                    ir.SymbolTable[varInstr.result].Type.AtomTypeId, 
                    ir.SymbolTable[endCopy.result].Type.AtomTypeId), 
                varInstr.result, endCopy.result, t5);

            // (step < 0 and var < end)
            var t6 = ir.SymbolTable.AddTmp(INT32);
            ir.AddInstruction(sourceFile,
                currentLineNumber, ctx,
                OpCode.AND, t4, t5, t6);
            var t7 = ir.SymbolTable.AddTmp(INT32);
            ir.AddInstruction(sourceFile,
                currentLineNumber, ctx,
                OpCode.OR, t3, t6, t7);

            // if (true) GOTO after NEXT
            // set linenumber on exitNext().
            forLoopState.gotoAfterNext = ir.AddInstruction(sourceFile,
                currentLineNumber, ctx,
                OpCode.GOTO_LABEL_IF, t7, ir.SymbolTable.AddLabel(), NULL_ID);
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
        public override void ExitNextstmt(NextstmtContext ctx)
        {
            IList<ForLoopState> states = new List<ForLoopState>(1);
            if (!ctx.variable().Any())
            {
                if (forLoopStateList.Count != 0)
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
                    var dataType = ir.SymbolTable.GetDataTypeFor(varname, varsuffix);
                    var variableName = new VariableName(varname, dataType.GetRepr(), dataType);
                    int id = ir.SymbolTable.AddVariableOrUDF(variableName, 
                        (variableName1) => Variable.Of(variableName1, VariableKindHint.DERIVE_FROM_NAME, GetCtxString(ctx)), 
                        (id1, e1, v1) =>
                    {
                    });
                    var variable = ((STVariable)ir.SymbolTable[id]).GetVariable();
                    if (forLoopStateList.Count == 0)
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
                ir.AddInstruction(sourceFile,
                    currentLineNumber, ctx,
                    OpCode.GOTO_LABEL, state.labelApplyStep.op1, NULL_ID, NULL_ID);

                // LABEL afterNext
                var labelAfterNext = ir.AddInstruction(sourceFile,
                    currentLineNumber, ctx,
                    OpCode.LABEL, ir.SymbolTable.AddLabel(), NULL_ID, NULL_ID);
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
        public override void EnterIfThenElse(IfThenElseContext ctx)
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
        public override void ExitIfThenElse(IfThenElseContext ctx)
        {
            var ifState = nodeToIfState.Get(ctx);
            bool noElseStmt = ifState.labelBeforeElse == null;
            var condition = LookupInstruction(ctx.expr());

            // Patch IF true: condition
            ifState.gotoIfConditionTrue.PatchOp1(condition.result);

            // Patch IF true: GOTO labelBeforeThen
            ifState.gotoIfConditionTrue.PatchOp2(ifState.labelBeforeThen.op1);

            // Patch IF false: GOTO labelAfterThen|labelBeforeElse
             #pragma warning disable CS8602 // Dereference of a possibly null reference.
            ifState.gotoIfConditionFalse.PatchOp1(noElseStmt ? ifState.labelAfterThen.op1 : ifState.labelBeforeElse.op1);
            #pragma warning restore CS8602 // Dereference of a possibly null reference.

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
        public override void EnterThen(ThenContext ctx)
        {
            var ifState = nodeToIfState.Get(ctx.Parent);

            // IF condition is true, GOTO labelBeforeThen
            ifState.gotoIfConditionTrue = ir.AddInstruction(sourceFile,
                currentLineNumber, ctx,
                OpCode.GOTO_LABEL_IF, ir.SymbolTable.AddGotoTarget(), NULL_ID, NULL_ID);

            // IF condition is false, GOTO labelAfterThen|labelBeforeElse
            ifState.gotoIfConditionFalse = ir.AddInstruction(sourceFile,
                currentLineNumber, ctx,
                OpCode.GOTO_LABEL, ir.SymbolTable.AddGotoTarget(), NULL_ID, NULL_ID);
            ifState.labelBeforeThen = ir.AddInstruction(sourceFile,
                currentLineNumber, ctx,
                OpCode.LABEL, ir.SymbolTable.AddLabel(), NULL_ID, NULL_ID);
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
        public override void ExitThen(ThenContext ctx)
        {

            // Add instruction for:
            // THEN GOTO linenum | THEN linenum
            if (ctx.linenum() != null)
            {
                var gotoLinenum = ParseLinenum(ctx.linenum().GetText());
                ir.AddInstruction(sourceFile,
                    currentLineNumber, ctx,
                    OpCode.GOTO_LINENUM, GetGotoLineNumberOp1(gotoLinenum), NULL_ID, NULL_ID);
            }

            var ifState = nodeToIfState.Get(ctx.Parent);

            // GOTO labelAfterThen|labelAfterElse
            ifState.gotoFromThenAfterIf = ir.AddInstruction(sourceFile,
                currentLineNumber, ctx,
                OpCode.GOTO_LABEL, ir.SymbolTable.AddLabel(), NULL_ID, NULL_ID);
            ifState.labelAfterThen = ir.AddInstruction(sourceFile,
                currentLineNumber, ctx,
                OpCode.LABEL, ir.SymbolTable.AddLabel(), NULL_ID, NULL_ID);
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
        public override void EnterElsestmt(ElsestmtContext ctx)
        {
            var ifState = nodeToIfState.Get(ctx.Parent);
            ifState.labelBeforeElse = ir.AddInstruction(sourceFile,
                currentLineNumber, ctx,
                OpCode.LABEL, ir.SymbolTable.AddLabel(), NULL_ID, NULL_ID);
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
        public override void ExitElsestmt(ElsestmtContext ctx)
        {

            // Add instruction for:
            // ELSE linenum
            if (ctx.linenum() != null)
            {
                var gotoLinenum = ParseLinenum(ctx.linenum().GetText());
                ir.AddInstruction(sourceFile,
                    currentLineNumber, ctx,
                    OpCode.GOTO_LINENUM, GetGotoLineNumberOp1(gotoLinenum), NULL_ID, NULL_ID);
            }

            var ifState = nodeToIfState.Get(ctx.Parent);
            ifState.labelAfterElse = ir.AddInstruction(sourceFile,
                currentLineNumber, ctx,
                OpCode.LABEL, ir.SymbolTable.AddLabel(), NULL_ID, NULL_ID);
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
        public override void EnterIfthenbeginstmt(IfthenbeginstmtContext ctx)
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
        public override void ExitIfthenbeginstmt(IfthenbeginstmtContext ctx)
        {
            var ifState = nodeToIfState.Get(ctx);
            var condition = LookupInstruction(ctx.expr());

            // IF condition is true, GOTO labelBeforeThen
            ifState.gotoIfConditionTrue = ir.AddInstruction(sourceFile,
                currentLineNumber, ctx,
                OpCode.GOTO_LABEL_IF, condition.result, ir.SymbolTable.AddLabel(), NULL_ID);

            // IF condition is false, GOTO labelAfterThen|labelBeforeElse
            ifState.gotoIfConditionFalse = ir.AddInstruction(sourceFile,
                currentLineNumber, ctx,
                OpCode.GOTO_LABEL, ir.SymbolTable.AddGotoTarget(), NULL_ID, NULL_ID);

            // Add labelBeforeThen
            ifState.labelBeforeThen = ir.AddInstruction(sourceFile,
                currentLineNumber, ctx,
                OpCode.LABEL, ir.SymbolTable.AddLabel(), NULL_ID, NULL_ID);

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
        public override void EnterElsebeginstmt(ElsebeginstmtContext ctx)
        {
            if (ifStateList.Count == 0)
            {
                throw new PuffinBasicSemanticError(MISMATCHED_ELSEBEGIN, GetCtxString(ctx), "ELSE BEGIN without IF THEN BEGIN");
            }

            var ifState = ifStateList.Last();

            // GOTO labelAfterThen|labelAfterElse
            ifState.gotoFromThenAfterIf = ir.AddInstruction(sourceFile,
                currentLineNumber, ctx,
                OpCode.GOTO_LABEL, ir.SymbolTable.AddLabel(), NULL_ID, NULL_ID);
            ifState.labelAfterThen = ir.AddInstruction(sourceFile,
                currentLineNumber, ctx,
                OpCode.LABEL, ir.SymbolTable.AddLabel(), NULL_ID, NULL_ID);
            ifState.labelBeforeElse = ir.AddInstruction(sourceFile,
                currentLineNumber, ctx,
                OpCode.LABEL, ir.SymbolTable.AddLabel(), NULL_ID, NULL_ID);
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
        public override void ExitEndifstmt(EndifstmtContext ctx)
        {
            if (ifStateList.Count == 0)
            {
                throw new PuffinBasicSemanticError(MISMATCHED_ENDIF, GetCtxString(ctx), "ENDIF without IF THEN BEGIN");
            }

            var ifState = ifStateList.Last();
            ifStateList.RemoveLast();
            bool noElseStmt = ifState.labelBeforeElse == null;
            if (noElseStmt)
            {

                // GOTO labelAfterThen|labelAfterElse
                ifState.gotoFromThenAfterIf = ir.AddInstruction(sourceFile,
                    currentLineNumber, ctx,
                    OpCode.GOTO_LABEL, ir.SymbolTable.AddLabel(), NULL_ID, NULL_ID);
                ifState.labelAfterThen = ir.AddInstruction(sourceFile,
                    currentLineNumber, ctx,
                    OpCode.LABEL, ir.SymbolTable.AddLabel(), NULL_ID, NULL_ID);
            }


            // Add labelAfterElse
            ifState.labelAfterElse = ir.AddInstruction(sourceFile,
                currentLineNumber, ctx,
                OpCode.LABEL, ir.SymbolTable.AddLabel(), NULL_ID, NULL_ID);

            // Patch IF true: GOTO labelBeforeThen
            ifState.gotoIfConditionTrue.PatchOp2(ifState.labelBeforeThen.op1);

            // Patch IF false: GOTO labelAfterThen|labelBeforeElse
            #pragma warning disable CS8602 // Dereference of a possibly null reference.
            ifState.gotoIfConditionFalse.PatchOp1(noElseStmt ? ifState.labelAfterThen.op1 : ifState.labelBeforeElse.op1);
            #pragma warning restore CS8602 // Dereference of a possibly null reference.

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
        public override void ExitGosubstmt(GosubstmtContext ctx)
        {
            var gotoLinenum = ParseLinenum(ctx.linenum().GetText());
            var pushReturnLabel = ir.AddInstruction(sourceFile,
                currentLineNumber, ctx,
                OpCode.PUSH_RETLABEL, ir.SymbolTable.AddGotoTarget(), NULL_ID, NULL_ID);
            ir.AddInstruction(sourceFile,
                currentLineNumber, ctx,
                OpCode.GOTO_LINENUM, GetGotoLineNumberOp1(gotoLinenum), NULL_ID, NULL_ID);
            var labelReturn = ir.AddInstruction(sourceFile,
                currentLineNumber, ctx,
                OpCode.LABEL, ir.SymbolTable.AddLabel(), NULL_ID, NULL_ID);
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
        public override void ExitGosublabelstmt(GosublabelstmtContext ctx)
        {
            var gotoLabel = ir.SymbolTable.AddLabel(ctx.@string().STRING().GetText());
            var pushReturnLabel = ir.AddInstruction(sourceFile,
                currentLineNumber, ctx,
                OpCode.PUSH_RETLABEL, ir.SymbolTable.AddGotoTarget(), NULL_ID, NULL_ID);
            ir.AddInstruction(sourceFile,
                currentLineNumber, ctx,
                OpCode.GOTO_LABEL, gotoLabel, NULL_ID, NULL_ID);
            var labelReturn = ir.AddInstruction(sourceFile,
                currentLineNumber, ctx,
                OpCode.LABEL, ir.SymbolTable.AddLabel(), NULL_ID, NULL_ID);
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
        public override void ExitReturnstmt(ReturnstmtContext ctx)
        {
            ir.AddInstruction(sourceFile,
                currentLineNumber, ctx,
                OpCode.RETURN, NULL_ID, NULL_ID, NULL_ID);
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
        public override void ExitGotostmt(GotostmtContext ctx)
        {
            var gotoLinenum = ParseLinenum(ctx.linenum().GetText());
            ir.AddInstruction(sourceFile,
                currentLineNumber, ctx,
                OpCode.GOTO_LINENUM, GetGotoLineNumberOp1(gotoLinenum), NULL_ID, NULL_ID);
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
        public override void ExitGotolabelstmt(GotolabelstmtContext ctx)
        {
            var gotoLabel = ir.SymbolTable.AddLabel(ctx.@string().STRING().GetText());
            ir.AddInstruction(sourceFile,
                currentLineNumber, ctx,
                OpCode.GOTO_LABEL, gotoLabel, NULL_ID, NULL_ID);
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
        public override void ExitSwapstmt(SwapstmtContext ctx)
        {
            var var1 = LookupInstruction(ctx.variable(0));
            var var2 = LookupInstruction(ctx.variable(1));
            var dt1 = ir.SymbolTable[var1.result].Type.AtomTypeId;
            var dt2 = ir.SymbolTable[var2.result].Type.AtomTypeId;
            if (dt1 != dt2)
            {
                throw new PuffinBasicSemanticError(DATA_TYPE_MISMATCH, GetCtxString(ctx), dt1 + " doesn't match " + dt2);
            }

            ir.AddInstruction(sourceFile,
                currentLineNumber, ctx,
                OpCode.SWAP, var1.result, var2.result, NULL_ID);
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
        public override void ExitOpen1stmt(Open1stmtContext ctx)
        {
            var filenameInstr = LookupInstruction(ctx.filename);
            var fileOpenMode = GetFileOpenMode(ctx.filemode1());
            var accessMode = GetFileAccessMode(null);
            var lockMode = GetLockMode(null);
            var fileNumber = Numbers.ParseInt32(ctx.filenum.Text, GetCtxString(ctx));
            var recordLenInstrId = ctx.reclen != null ?
                LookupInstruction(ctx.reclen).result :
                ir.SymbolTable.AddTmp(INT32, (e) => e.Value.SetInt32(File.PuffinBasicFile.DEFAULT_RECORD_LEN));

            Types.AssertString(ir.SymbolTable[filenameInstr.result].Type.AtomTypeId, GetCtxString(ctx));
            Types.AssertNumeric(ir.SymbolTable[recordLenInstrId].Type.AtomTypeId, GetCtxString(ctx));

            // fileName, fileNumber
            ir.AddInstruction(sourceFile,
                currentLineNumber, ctx,
                OpCode.PARAM2,
                filenameInstr.result,
                ir.SymbolTable.AddTmp(INT32, (e) => e.Value.SetInt32(fileNumber)),
                NULL_ID);

            // openMode, accessMode
            ir.AddInstruction(sourceFile,
                currentLineNumber, ctx,
                OpCode.PARAM2,
                ir.SymbolTable.AddTmp(PuffinBasicAtomTypeId.STRING, (e) => e.Value.SetString(fileOpenMode.ToString())),
                ir.SymbolTable.AddTmp(PuffinBasicAtomTypeId.STRING, (e) => e.Value.SetString(accessMode.ToString())), NULL_ID);

            // lockMode, recordLen
            ir.AddInstruction(sourceFile,
                currentLineNumber, ctx,
                OpCode.OPEN,
                ir.SymbolTable.AddTmp(PuffinBasicAtomTypeId.STRING, (e) => e.Value.SetString(lockMode.ToString())),
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
        public override void ExitOpen2stmt(Open2stmtContext ctx)
        {
            var filenameInstr = LookupInstruction(ctx.filename);
            var fileOpenMode = GetFileOpenMode(ctx.filemode2());
            var accessMode = GetFileAccessMode(ctx.access());
            var lockMode = GetLockMode(ctx.@lock());
            var fileNumber = Numbers.ParseInt32(ctx.filenum.Text, GetCtxString(ctx));
            var recordLenInstrId = ctx.reclen != null ? 
                LookupInstruction(ctx.reclen).result 
                : ir.SymbolTable.AddTmp(INT32, (e) => e.Value.SetInt32(File.PuffinBasicFile.DEFAULT_RECORD_LEN));
            Types.AssertString(ir.SymbolTable[filenameInstr.result].Type.AtomTypeId, GetCtxString(ctx));
            Types.AssertNumeric(ir.SymbolTable[recordLenInstrId].Type.AtomTypeId, GetCtxString(ctx));

            // fileName, fileNumber
            ir.AddInstruction(sourceFile,
                currentLineNumber, ctx,
                OpCode.PARAM2, filenameInstr.result, ir.SymbolTable.AddTmp(INT32, (e) => e.Value.SetInt32(fileNumber)), NULL_ID);

            // openMode, accessMode
            ir.AddInstruction(sourceFile,
                currentLineNumber, ctx,
                OpCode.PARAM2, ir.SymbolTable.AddTmp(PuffinBasicAtomTypeId.STRING, (e) => e.Value.SetString(fileOpenMode.ToString())), 
                ir.SymbolTable.AddTmp(PuffinBasicAtomTypeId.STRING, (e) => e.Value.SetString(accessMode.ToString())), NULL_ID);

            // lockMode, recordLen
            ir.AddInstruction(sourceFile,
                currentLineNumber, ctx,
                OpCode.OPEN, ir.SymbolTable.AddTmp(PuffinBasicAtomTypeId.STRING, (e) => e.Value.SetString(lockMode.ToString())), recordLenInstrId, NULL_ID);
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
        public override void ExitClosestmt(ClosestmtContext ctx)
        {
            //var fileNumbers = ctx.DECIMAL().Stream().Map((fileNumberCtx) => Numbers.ParseInt32(fileNumberCtx.GetText(), GetCtxString(ctx))).Collect(Collectors.ToList());
            var fileNumbers = ctx.DECIMAL().Select((fileNumberCtx) => Numbers.ParseInt32(fileNumberCtx.GetText(), GetCtxString(ctx)));
            if (!fileNumbers.Any())
            {
                ir.AddInstruction(sourceFile,
                    currentLineNumber, ctx,
                    OpCode.CLOSE_ALL, NULL_ID, NULL_ID, NULL_ID);
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
                    ir.AddInstruction(sourceFile,
                    currentLineNumber, ctx,
                    OpCode.CLOSE,
                    ir.SymbolTable.AddTmp(PuffinBasicAtomTypeId.INT32, (e) => e.Value.SetInt32(fileNumber)),
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
        public override void ExitFieldstmt(FieldstmtContext ctx)
        {
            var fileNumberInstr = LookupInstruction(ctx.filenum);
            Types.AssertNumeric(ir.SymbolTable[fileNumberInstr.result].Type.AtomTypeId, GetCtxString(ctx));
            var numEntries = ctx.variable().Length;
            for (int i = 0; i < numEntries; i++)
            {
                var recordPartLen = Numbers.ParseInt32(ctx.DECIMAL(i).GetText(), GetCtxString(ctx));
                var varInstr = LookupInstruction(ctx.variable(i));
                AssertVariable(ir.SymbolTable[varInstr.result], GetCtxString(ctx));
                ir.AddInstruction(sourceFile,
                    currentLineNumber, ctx,
                    OpCode.PARAM2, varInstr.result, ir.SymbolTable.AddTmp(INT32, (e) => e.Value.SetInt32(recordPartLen)), NULL_ID);
            }


            // FileNumber, #fields
            ir.AddInstruction(sourceFile,
                currentLineNumber, ctx,
                OpCode.FIELD, fileNumberInstr.result, ir.SymbolTable.AddTmp(INT32, (e) => e.Value.SetInt32(numEntries)), NULL_ID);
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
            if (!variable.IsArray() || !((ArrayType)variable.Type).IsNDArray(1))
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
            if (!variable.IsArray() || !((ArrayType)variable.Type).IsNDArray(2))
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
        public override void ExitPutstmt(PutstmtContext ctx)
        {
            var fileNumberInstr = Numbers.ParseInt32(ctx.filenum.Text, GetCtxString(ctx));
            int exprId;
            if (ctx.expr() != null)
            {
                exprId = LookupInstruction(ctx.expr()).result;
                Types.AssertNumeric(ir.SymbolTable[exprId].Type.AtomTypeId, GetCtxString(ctx));
            }
            else
            {
                exprId = NULL_ID;
            }

            ir.AddInstruction(sourceFile,
                currentLineNumber, ctx,
                OpCode.PUTF, ir.SymbolTable.AddTmp(INT32, (e) => e.Value.SetInt32(fileNumberInstr)), exprId, NULL_ID);
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
        public override void ExitMiddlrstmt(MiddlrstmtContext ctx)
        {
            var varInstr = LookupInstruction(ctx.variable());
            var nInstr = LookupInstruction(ctx.expr(0));
            var mInstrId = ctx.expr().Length == 3 
                ? LookupInstruction(ctx.expr(1)).result 
                : ir.SymbolTable.AddTmp(INT32, (e) => e.Value.SetInt32(-1));
            var replacement = ctx.expr().Length == 3 ? LookupInstruction(ctx.expr(2)) : LookupInstruction(ctx.expr(1));
            Types.AssertString(ir.SymbolTable[varInstr.result].Type.AtomTypeId, GetCtxString(ctx));
            Types.AssertString(ir.SymbolTable[replacement.result].Type.AtomTypeId, GetCtxString(ctx));
            AssertVariable(ir.SymbolTable[varInstr.result], GetCtxString(ctx));
            Types.AssertNumeric(ir.SymbolTable[nInstr.result].Type.AtomTypeId, GetCtxString(ctx));
            Types.AssertNumeric(ir.SymbolTable[mInstrId].Type.AtomTypeId, GetCtxString(ctx));
            ir.AddInstruction(sourceFile,
                currentLineNumber, ctx,
                OpCode.PARAM2, varInstr.result, nInstr.result, NULL_ID);
            ir.AddInstruction(sourceFile,
                currentLineNumber, ctx,
                OpCode.MIDDLR_STMT, mInstrId, replacement.result, NULL_ID);
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
        public override void ExitGetstmt(GetstmtContext ctx)
        {
            var fileNumberInstr = Numbers.ParseInt32(ctx.filenum.Text, GetCtxString(ctx));
            int exprId;
            if (ctx.expr() != null)
            {
                exprId = LookupInstruction(ctx.expr()).result;
                Types.AssertNumeric(ir.SymbolTable[exprId].Type.AtomTypeId, GetCtxString(ctx));
            }
            else
            {
                exprId = NULL_ID;
            }

            ir.AddInstruction(sourceFile,
                currentLineNumber, ctx,
                OpCode.GETF, ir.SymbolTable.AddTmp(INT32, (e) => e.Value.SetInt32(fileNumberInstr)), exprId, NULL_ID);
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
        public override void ExitRandomizestmt(RandomizestmtContext ctx)
        {
            var exprId = LookupInstruction(ctx.expr()).result;
            Types.AssertNumeric(ir.SymbolTable[exprId].Type.AtomTypeId, GetCtxString(ctx));
            ir.AddInstruction(sourceFile,
                currentLineNumber, ctx,
                OpCode.RANDOMIZE, exprId, NULL_ID, NULL_ID);
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
        public override void ExitRandomizetimerstmt(RandomizetimerstmtContext ctx)
        {
            ir.AddInstruction(sourceFile,
                currentLineNumber, ctx,
                OpCode.RANDOMIZE_TIMER, NULL_ID, NULL_ID, NULL_ID);
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
        public override void ExitDefintstmt(DefintstmtContext ctx)
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
        public override void ExitDeflngstmt(DeflngstmtContext ctx)
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
        public override void ExitDefsngstmt(DefsngstmtContext ctx)
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
        public override void ExitDefdblstmt(DefdblstmtContext ctx)
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
        public override void ExitDefstrstmt(DefstrstmtContext ctx)
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
        public override void ExitLsetstmt(LsetstmtContext ctx)
        {
            var varInstr = LookupInstruction(ctx.variable());
            var exprInstr = LookupInstruction(ctx.expr());
            var varEntry = ir.SymbolTable[varInstr.result];
            AssertVariable(varEntry, GetCtxString(ctx));
            Types.AssertString(varEntry.Type.AtomTypeId, GetCtxString(ctx));
            Types.AssertString(ir.SymbolTable[exprInstr.result].Type.AtomTypeId, GetCtxString(ctx));
            ir.AddInstruction(sourceFile,
                currentLineNumber, ctx,
                OpCode.LSET, varInstr.result, exprInstr.result, NULL_ID);
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
        public override void ExitRsetstmt(RsetstmtContext ctx)
        {
            var varInstr = LookupInstruction(ctx.variable());
            var exprInstr = LookupInstruction(ctx.expr());
            var varEntry = ir.SymbolTable[varInstr.result];
            AssertVariable(varEntry, GetCtxString(ctx));
            Types.AssertString(varEntry.Type.AtomTypeId, GetCtxString(ctx));
            Types.AssertString(ir.SymbolTable[exprInstr.result].Type.AtomTypeId, GetCtxString(ctx));
            ir.AddInstruction(sourceFile,
                currentLineNumber, ctx,
                OpCode.RSET, varInstr.result, exprInstr.result, NULL_ID);
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
        public override void ExitInputstmt(InputstmtContext ctx)
        {
            foreach (var varCtx in ctx.variable())
            {
                var varInstr = LookupInstruction(varCtx);
                AssertVariable(ir.SymbolTable[varInstr.result], GetCtxString(ctx));
                ir.AddInstruction(sourceFile,
                    currentLineNumber, ctx,
                    OpCode.PARAM1, varInstr.result, NULL_ID, NULL_ID);
            }

            int promptId;
            if (ctx.expr() != null)
            {
                promptId = LookupInstruction(ctx.expr()).result;
                Types.AssertString(ir.SymbolTable[promptId].Type.AtomTypeId, GetCtxString(ctx));
            }
            else
            {
                promptId = ir.SymbolTable.AddTmp(PuffinBasicAtomTypeId.STRING, (e) => e.Value.SetString("?"));
            }

            ir.AddInstruction(sourceFile,
                currentLineNumber, ctx,
                OpCode.INPUT, promptId, NULL_ID, NULL_ID);
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
        public override void ExitInputhashstmt(InputhashstmtContext ctx)
        {
            foreach (var varCtx in ctx.variable())
            {
                var varInstr = LookupInstruction(varCtx);
                AssertVariable(ir.SymbolTable[varInstr.result], GetCtxString(ctx));
                ir.AddInstruction(sourceFile,
                    currentLineNumber, ctx,
                    OpCode.PARAM1, varInstr.result, NULL_ID, NULL_ID);
            }

            var fileNumInstr = LookupInstruction(ctx.filenum);
            Types.AssertNumeric(ir.SymbolTable[fileNumInstr.result].Type.AtomTypeId, GetCtxString(ctx));
            ir.AddInstruction(sourceFile,
                currentLineNumber, ctx,
                OpCode.INPUT, NULL_ID, fileNumInstr.result, NULL_ID);
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
        public override void ExitLineinputstmt(LineinputstmtContext ctx)
        {
            var varInstr = LookupInstruction(ctx.variable());
            AssertVariable(ir.SymbolTable[varInstr.result], GetCtxString(ctx));
            ir.AddInstruction(sourceFile,
                currentLineNumber, ctx,
                OpCode.PARAM1, varInstr.result, NULL_ID, NULL_ID);
            int promptId;
            if (ctx.expr() != null)
            {
                promptId = LookupInstruction(ctx.expr()).result;
                Types.AssertString(ir.SymbolTable[promptId].Type.AtomTypeId, GetCtxString(ctx));
            }
            else
            {
                promptId = ir.SymbolTable.AddTmp(PuffinBasicAtomTypeId.STRING, (e) => e.Value.SetString(""));
            }

            ir.AddInstruction(sourceFile,
                currentLineNumber, ctx,
                OpCode.LINE_INPUT, promptId, NULL_ID, NULL_ID);
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
        public override void ExitLineinputhashstmt(LineinputhashstmtContext ctx)
        {
            var varInstr = LookupInstruction(ctx.variable());
            AssertVariable(ir.SymbolTable[varInstr.result], GetCtxString(ctx));
            ir.AddInstruction(sourceFile,
                currentLineNumber, ctx,
                OpCode.PARAM1, varInstr.result, NULL_ID, NULL_ID);
            var fileNumInstr = LookupInstruction(ctx.filenum);
            Types.AssertNumeric(ir.SymbolTable[fileNumInstr.result].Type.AtomTypeId, GetCtxString(ctx));
            ir.AddInstruction(sourceFile,
                currentLineNumber, ctx,
                OpCode.LINE_INPUT, NULL_ID, fileNumInstr.result, NULL_ID);
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
        public override void ExitWritestmt(WritestmtContext ctx)
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
        public override void ExitWritehashstmt(WritehashstmtContext ctx)
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
        public virtual void HandleWritestmt(ParserRuleContext ctx, IList<ExprContext> exprs, Instruction? fileNumber)
        {

            // if fileNumber != null, skip first instruction
            for (int i = fileNumber == null ? 0 : 1; i < exprs.Count; i++)
            {
                var exprCtx = exprs[i];
                var exprInstr = LookupInstruction(exprCtx);
                ir.AddInstruction(sourceFile,
                    currentLineNumber, ctx,
                    OpCode.WRITE, exprInstr.result, NULL_ID, NULL_ID);
                if (i + 1 < exprs.Count)
                {
                    var commaId = ir.SymbolTable.AddTmp(PuffinBasicAtomTypeId.STRING, (entry) => entry.Value.SetString(","));
                    ir.AddInstruction(sourceFile,
                        currentLineNumber, ctx,
                        OpCode.PRINT, commaId, NULL_ID, NULL_ID);
                }
            }

            var newlineId = ir.SymbolTable.AddTmp(PuffinBasicAtomTypeId.STRING, (entry) => entry.Value.SetString(Environment.NewLine));
            ir.AddInstruction(sourceFile,
                currentLineNumber, ctx,
                OpCode.PRINT, newlineId, NULL_ID, NULL_ID);
            int fileNumberId;
            if (fileNumber != null)
            {
                Types.AssertNumeric(ir.SymbolTable[fileNumber.result].Type.AtomTypeId, GetCtxString(ctx));
                fileNumberId = fileNumber.result;
            }
            else
            {
                fileNumberId = NULL_ID;
            }

            ir.AddInstruction(sourceFile,
                currentLineNumber, ctx,
                OpCode.FLUSH, fileNumberId, NULL_ID, NULL_ID);
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
        public override void ExitReadstmt(ReadstmtContext ctx)
        {
            foreach (var varCtx in ctx.variable())
            {
                var varInstr = LookupInstruction(varCtx);
                AssertVariable(ir.SymbolTable[varInstr.result], GetCtxString(ctx));
                ir.AddInstruction(sourceFile,
                    currentLineNumber, ctx,
                    OpCode.READ, varInstr.result, NULL_ID, NULL_ID);
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
        public override void ExitRestorestmt(RestorestmtContext ctx)
        {
            ir.AddInstruction(sourceFile,
                currentLineNumber, ctx,
                OpCode.RESTORE, NULL_ID, NULL_ID, NULL_ID);
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
        public override void ExitDatastmt(DatastmtContext ctx)
        {
            var children = ctx.children;
            for (int i = 1; i < children.Count; i += 2)
            {
                var child = children[i];
                int valueId;
                if (child is NumberContext)
                {
                    valueId = LookupInstruction((NumberContext)child).result;
                }
                else
                {
                    var text = Unquote(child.GetText());
                    valueId = ir.SymbolTable.AddTmp(PuffinBasicAtomTypeId.STRING, (e) => e.Value.SetString(text));
                }

                ir.AddInstruction(sourceFile,
                    currentLineNumber, ctx,
                    OpCode.DATA, valueId, NULL_ID, NULL_ID);
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
        public override void ExitLabelstmt(LabelstmtContext ctx)
        {
            var label = ctx.@string().STRING().GetText();
            ir.AddInstruction(sourceFile,
                currentLineNumber, ctx,
                OpCode.LABEL, ir.SymbolTable.AddLabel(label), NULL_ID, NULL_ID);
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
        public override void ExitScreenstmt(ScreenstmtContext ctx)
        {
            AssertGraphics();
            var title = LookupInstruction(ctx.expr(0));
            var w = LookupInstruction(ctx.expr(1));
            var h = LookupInstruction(ctx.expr(2));
            var iw = ctx.expr().Length == 5 ? LookupInstruction(ctx.expr(3)) : w;
            var ih = ctx.expr().Length == 5 ? LookupInstruction(ctx.expr(4)) : h;
            var manualRepaintFlag = ctx.mr != null;
            var doubleBufferFlag = ctx.db != null;
            Types.AssertString(ir.SymbolTable[title.result].Type.AtomTypeId, GetCtxString(ctx));
            Types.AssertNumeric(ir.SymbolTable[w.result].Type.AtomTypeId, GetCtxString(ctx));
            Types.AssertNumeric(ir.SymbolTable[h.result].Type.AtomTypeId, GetCtxString(ctx));
            Types.AssertNumeric(ir.SymbolTable[iw.result].Type.AtomTypeId, GetCtxString(ctx));
            Types.AssertNumeric(ir.SymbolTable[ih.result].Type.AtomTypeId, GetCtxString(ctx));
            ir.AddInstruction(sourceFile,
                currentLineNumber, ctx,
                OpCode.PARAM2, w.result, h.result, NULL_ID);
            ir.AddInstruction(sourceFile,
                currentLineNumber, ctx,
                OpCode.PARAM2, iw.result, ih.result, NULL_ID);
            var repaint = ir.SymbolTable.AddTmp(INT32, (e) => e.Value.SetInt32(manualRepaintFlag ? 0 : -1));
            var doubleBuffer = ir.SymbolTable.AddTmp(INT32, (e) => e.Value.SetInt32(doubleBufferFlag ? -1 : 0));
            ir.AddInstruction(sourceFile,
                currentLineNumber, ctx,
                OpCode.PARAM2, repaint, doubleBuffer, NULL_ID);
            ir.AddInstruction(sourceFile,
                currentLineNumber, ctx,
                OpCode.SCREEN, title.result, NULL_ID, NULL_ID);
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
        public override void ExitRepaintstmt(RepaintstmtContext ctx)
        {
            AssertGraphics();
            ir.AddInstruction(sourceFile,
                currentLineNumber, ctx,
                OpCode.REPAINT, NULL_ID, NULL_ID, NULL_ID);
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
        public override void ExitCirclestmt(CirclestmtContext ctx)
        {
            AssertGraphics();
            var x = LookupInstruction(ctx.x);
            var y = LookupInstruction(ctx.y);
            var r1 = LookupInstruction(ctx.r1);
            var r2 = LookupInstruction(ctx.r2);
            var s = ctx.s != null ? LookupInstruction(ctx.s) : null;
            var e = ctx.e != null ? LookupInstruction(ctx.e) : null;
            var fill = ctx.fill != null ? LookupInstruction(ctx.fill) : null;
            Types.AssertNumeric(ir.SymbolTable[x.result].Type.AtomTypeId, GetCtxString(ctx));
            Types.AssertNumeric(ir.SymbolTable[y.result].Type.AtomTypeId, GetCtxString(ctx));
            Types.AssertNumeric(ir.SymbolTable[r1.result].Type.AtomTypeId, GetCtxString(ctx));
            Types.AssertNumeric(ir.SymbolTable[r2.result].Type.AtomTypeId, GetCtxString(ctx));
            if (s != null)
            {
                Types.AssertNumeric(ir.SymbolTable[s.result].Type.AtomTypeId, GetCtxString(ctx));
            }

            if (e != null)
            {
                Types.AssertNumeric(ir.SymbolTable[e.result].Type.AtomTypeId, GetCtxString(ctx));
            }

            if (fill != null)
            {
                Types.AssertString(ir.SymbolTable[fill.result].Type.AtomTypeId, GetCtxString(ctx));
            }

            ir.AddInstruction(sourceFile,
                currentLineNumber, ctx,
                OpCode.PARAM2, x.result, y.result, NULL_ID);
            ir.AddInstruction(sourceFile,
                currentLineNumber, ctx,
                OpCode.PARAM2, s != null ? s.result : NULL_ID, e != null ? e.result : NULL_ID, NULL_ID);
            ir.AddInstruction(sourceFile,
                currentLineNumber, ctx,
                OpCode.PARAM1, fill != null ? fill.result : NULL_ID, NULL_ID, NULL_ID);
            ir.AddInstruction(sourceFile,
                currentLineNumber, ctx,
                OpCode.CIRCLE, r1.result, r2.result, NULL_ID);
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
        public override void ExitLinestmt(LinestmtContext ctx)
        {
            AssertGraphics();
            var x1 = LookupInstruction(ctx.x1);
            var y1 = LookupInstruction(ctx.y1);
            var x2 = LookupInstruction(ctx.x2);
            var y2 = LookupInstruction(ctx.y2);
            Types.AssertNumeric(ir.SymbolTable[x1.result].Type.AtomTypeId, GetCtxString(ctx));
            Types.AssertNumeric(ir.SymbolTable[y1.result].Type.AtomTypeId, GetCtxString(ctx));
            Types.AssertNumeric(ir.SymbolTable[x2.result].Type.AtomTypeId, GetCtxString(ctx));
            Types.AssertNumeric(ir.SymbolTable[y2.result].Type.AtomTypeId, GetCtxString(ctx));
            Instruction? bf = null;
            if (ctx.bf != null)
            {
                bf = LookupInstruction(ctx.bf);
                Types.AssertString(ir.SymbolTable[bf.result].Type.AtomTypeId, GetCtxString(ctx));
            }

            ir.AddInstruction(sourceFile,
                currentLineNumber, ctx,
                OpCode.PARAM2, x1.result, y1.result, NULL_ID);
            ir.AddInstruction(sourceFile,
                currentLineNumber, ctx,
                OpCode.PARAM2, x2.result, y2.result, NULL_ID);
            ir.AddInstruction(sourceFile,
                currentLineNumber, ctx,
                OpCode.LINE, bf != null ? bf.result : NULL_ID, NULL_ID, NULL_ID);
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
        public override void ExitColorstmt(ColorstmtContext ctx)
        {
            var r = LookupInstruction(ctx.r);
            var g = LookupInstruction(ctx.g);
            var b = LookupInstruction(ctx.b);
            Types.AssertNumeric(ir.SymbolTable[r.result].Type.AtomTypeId, GetCtxString(ctx));
            Types.AssertNumeric(ir.SymbolTable[g.result].Type.AtomTypeId, GetCtxString(ctx));
            Types.AssertNumeric(ir.SymbolTable[b.result].Type.AtomTypeId, GetCtxString(ctx));
            ir.AddInstruction(sourceFile,
                currentLineNumber, ctx,
                OpCode.PARAM2, r.result, g.result, NULL_ID);
            ir.AddInstruction(sourceFile,
                currentLineNumber, ctx,
                OpCode.COLOR, b.result, NULL_ID, NULL_ID);
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
        public override void ExitPaintstmt(PaintstmtContext ctx)
        {
            AssertGraphics();
            var x = LookupInstruction(ctx.x);
            var y = LookupInstruction(ctx.y);
            var r = LookupInstruction(ctx.r);
            var g = LookupInstruction(ctx.g);
            var b = LookupInstruction(ctx.b);
            Types.AssertNumeric(ir.SymbolTable[x.result].Type.AtomTypeId, GetCtxString(ctx));
            Types.AssertNumeric(ir.SymbolTable[y.result].Type.AtomTypeId, GetCtxString(ctx));
            Types.AssertNumeric(ir.SymbolTable[r.result].Type.AtomTypeId, GetCtxString(ctx));
            Types.AssertNumeric(ir.SymbolTable[g.result].Type.AtomTypeId, GetCtxString(ctx));
            Types.AssertNumeric(ir.SymbolTable[b.result].Type.AtomTypeId, GetCtxString(ctx));
            ir.AddInstruction(sourceFile,
                currentLineNumber, ctx,
                OpCode.PARAM2, r.result, g.result, NULL_ID);
            ir.AddInstruction(sourceFile,
                currentLineNumber, ctx,
                OpCode.PARAM1, b.result, NULL_ID, NULL_ID);
            ir.AddInstruction(sourceFile,
                currentLineNumber, ctx,
                OpCode.PAINT, x.result, y.result, NULL_ID);
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
        public override void ExitPsetstmt(PsetstmtContext ctx)
        {
            AssertGraphics();
            var x = LookupInstruction(ctx.x);
            var y = LookupInstruction(ctx.y);
            int rId = NULL_ID, gId = NULL_ID, bId = NULL_ID;
            if (ctx.r != null)
            {
                rId = LookupInstruction(ctx.r).result;
                Types.AssertNumeric(ir.SymbolTable[rId].Type.AtomTypeId, GetCtxString(ctx));
            }

            if (ctx.g != null)
            {
                gId = LookupInstruction(ctx.g).result;
                Types.AssertNumeric(ir.SymbolTable[gId].Type.AtomTypeId, GetCtxString(ctx));
            }

            if (ctx.b != null)
            {
                bId = LookupInstruction(ctx.b).result;
                Types.AssertNumeric(ir.SymbolTable[bId].Type.AtomTypeId, GetCtxString(ctx));
            }

            Types.AssertNumeric(ir.SymbolTable[x.result].Type.AtomTypeId, GetCtxString(ctx));
            Types.AssertNumeric(ir.SymbolTable[y.result].Type.AtomTypeId, GetCtxString(ctx));
            ir.AddInstruction(sourceFile,
                currentLineNumber, ctx,
                OpCode.PARAM2, rId, gId, NULL_ID);
            ir.AddInstruction(sourceFile,
                currentLineNumber, ctx,
                OpCode.PARAM1, bId, NULL_ID, NULL_ID);
            ir.AddInstruction(sourceFile,
                currentLineNumber, ctx,
                OpCode.PSET, x.result, y.result, NULL_ID);
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
        public override void ExitGraphicsgetstmt(GraphicsgetstmtContext ctx)
        {
            AssertGraphics();
            var x1 = LookupInstruction(ctx.x1);
            var y1 = LookupInstruction(ctx.y1);
            var x2 = LookupInstruction(ctx.x2);
            var y2 = LookupInstruction(ctx.y2);
            var varInstr = LookupInstruction(ctx.variable());
            //int bufferNumber = ctx.BACK1() != null ? GraphicsUtil.BUFFER_NUM_BACK1 : GraphicsUtil.BUFFER_NUM_FRONT;
            int bufferNumber = ctx.BACK1() != null ? 0 : 1;
            Types.AssertNumeric(ir.SymbolTable[x1.result].Type.AtomTypeId, GetCtxString(ctx));
            Types.AssertNumeric(ir.SymbolTable[y1.result].Type.AtomTypeId, GetCtxString(ctx));
            Types.AssertNumeric(ir.SymbolTable[x2.result].Type.AtomTypeId, GetCtxString(ctx));
            Types.AssertNumeric(ir.SymbolTable[y2.result].Type.AtomTypeId, GetCtxString(ctx));
            AssertVariable(ir.SymbolTable[varInstr.result], GetCtxString(ctx));
            ir.AddInstruction(sourceFile,
                currentLineNumber, ctx,
                OpCode.PARAM2, x1.result, y1.result, NULL_ID);
            ir.AddInstruction(sourceFile,
                currentLineNumber, ctx,
                OpCode.PARAM2, x2.result, y2.result, NULL_ID);
            ir.AddInstruction(sourceFile,
                currentLineNumber, ctx,
                OpCode.GGET, varInstr.result, ir.SymbolTable.AddTmp(INT32, (e) => e.Value.SetInt32(bufferNumber)), NULL_ID);
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
        public override void ExitGraphicsputstmt(GraphicsputstmtContext ctx)
        {
            AssertGraphics();
            var x = LookupInstruction(ctx.x);
            var y = LookupInstruction(ctx.y);
            var varInstr = LookupInstruction(ctx.variable());
            var action = ctx.action != null ? LookupInstruction(ctx.action) : null;
            //int bufferNumber = ctx.FRONT() == null ? GraphicsUtil.BUFFER_NUM_BACK1 : GraphicsUtil.BUFFER_NUM_FRONT;
            int bufferNumber = ctx.FRONT() == null ? 0 : 1;
            Types.AssertNumeric(ir.SymbolTable[x.result].Type.AtomTypeId, GetCtxString(ctx));
            Types.AssertNumeric(ir.SymbolTable[y.result].Type.AtomTypeId, GetCtxString(ctx));
            AssertVariable(ir.SymbolTable[varInstr.result], GetCtxString(ctx));
            if (action != null)
            {
                Types.AssertString(ir.SymbolTable[action.result].Type.AtomTypeId, GetCtxString(ctx));
            }

            ir.AddInstruction(sourceFile,
                currentLineNumber, ctx,
                OpCode.PARAM2, x.result, y.result, NULL_ID);
            ir.AddInstruction(sourceFile,
                currentLineNumber, ctx,
                OpCode.PARAM1, ir.SymbolTable.AddTmp(INT32, (e) => e.Value.SetInt32(bufferNumber)), NULL_ID, NULL_ID);
            ir.AddInstruction(sourceFile,
                currentLineNumber, ctx,
                OpCode.GPUT, action != null ? action.result : NULL_ID, varInstr.result, NULL_ID);
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
        public override void ExitGraphicsbuffercopyhorstmt(GraphicsbuffercopyhorstmtContext ctx)
        {
            AssertGraphics();
            var srcx = LookupInstruction(ctx.srcx);
            var dstx = LookupInstruction(ctx.dstx);
            var w = LookupInstruction(ctx.w);
            Types.AssertNumeric(ir.SymbolTable[srcx.result].Type.AtomTypeId, GetCtxString(ctx));
            Types.AssertNumeric(ir.SymbolTable[dstx.result].Type.AtomTypeId, GetCtxString(ctx));
            Types.AssertNumeric(ir.SymbolTable[w.result].Type.AtomTypeId, GetCtxString(ctx));
            ir.AddInstruction(sourceFile,
                currentLineNumber, ctx,
                OpCode.PARAM2, srcx.result, dstx.result, NULL_ID);
            ir.AddInstruction(sourceFile,
                currentLineNumber, ctx,
                OpCode.BUFFERCOPYHOR, w.result, NULL_ID, NULL_ID);
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
        public override void ExitDrawstmt(DrawstmtContext ctx)
        {
            AssertGraphics();
            var str = LookupInstruction(ctx.expr());
            Types.AssertString(ir.SymbolTable[str.result].Type.AtomTypeId, GetCtxString(ctx));
            ir.AddInstruction(sourceFile,
                currentLineNumber, ctx,
                OpCode.DRAW, str.result, NULL_ID, NULL_ID);
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
        public override void ExitFontstmt(FontstmtContext ctx)
        {
            AssertGraphics();
            var name = LookupInstruction(ctx.name);
            var style = LookupInstruction(ctx.style);
            var size = LookupInstruction(ctx.size);
            Types.AssertString(ir.SymbolTable[style.result].Type.AtomTypeId, GetCtxString(ctx));
            Types.AssertNumeric(ir.SymbolTable[size.result].Type.AtomTypeId, GetCtxString(ctx));
            Types.AssertString(ir.SymbolTable[name.result].Type.AtomTypeId, GetCtxString(ctx));
            ir.AddInstruction(sourceFile,
                currentLineNumber, ctx,
                OpCode.PARAM2, style.result, size.result, NULL_ID);
            ir.AddInstruction(sourceFile,
                currentLineNumber, ctx,
                OpCode.FONT, name.result, NULL_ID, NULL_ID);
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
        public override void ExitDrawstrstmt(DrawstrstmtContext ctx)
        {
            var str = LookupInstruction(ctx.str);
            var x = LookupInstruction(ctx.x);
            var y = LookupInstruction(ctx.y);
            Types.AssertNumeric(ir.SymbolTable[x.result].Type.AtomTypeId, GetCtxString(ctx));
            Types.AssertNumeric(ir.SymbolTable[y.result].Type.AtomTypeId, GetCtxString(ctx));
            Types.AssertString(ir.SymbolTable[str.result].Type.AtomTypeId, GetCtxString(ctx));
            ir.AddInstruction(sourceFile,
                currentLineNumber, ctx,
                OpCode.PARAM2, x.result, y.result, NULL_ID);
            ir.AddInstruction(sourceFile,
                currentLineNumber, ctx,
                OpCode.DRAWSTR, str.result, NULL_ID, NULL_ID);
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
        public override void ExitLoadimgstmt(LoadimgstmtContext ctx)
        {
            AssertGraphics();
            var path = LookupInstruction(ctx.path);
            var varInstr = LookupInstruction(ctx.variable());
            Types.AssertString(ir.SymbolTable[path.result].Type.AtomTypeId, GetCtxString(ctx));
            AssertVariable(ir.SymbolTable[varInstr.result], GetCtxString(ctx));
            ir.AddInstruction(sourceFile,
                currentLineNumber, ctx,
                OpCode.LOADIMG, path.result, varInstr.result, NULL_ID);
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
        public override void ExitSaveimgstmt(SaveimgstmtContext ctx)
        {
            AssertGraphics();
            var path = LookupInstruction(ctx.path);
            var varInstr = LookupInstruction(ctx.variable());
            Types.AssertString(ir.SymbolTable[path.result].Type.AtomTypeId, GetCtxString(ctx));
            AssertVariable(ir.SymbolTable[varInstr.result], GetCtxString(ctx));
            ir.AddInstruction(sourceFile,
                currentLineNumber, ctx,
                OpCode.SAVEIMG, path.result, varInstr.result, NULL_ID);
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
        public override void ExitClsstmt(ClsstmtContext ctx)
        {
            AssertGraphics();
            ir.AddInstruction(sourceFile,
                currentLineNumber, ctx,
                OpCode.CLS, NULL_ID, NULL_ID, NULL_ID);
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
        public override void ExitLoadwavstmt(LoadwavstmtContext ctx)
        {
            AssertGraphics();
            var path = LookupInstruction(ctx.path);
            var varInstr = LookupInstruction(ctx.variable());
            Types.AssertString(ir.SymbolTable[path.result].Type.AtomTypeId, GetCtxString(ctx));
            AssertVariable(ir.SymbolTable[varInstr.result], GetCtxString(ctx));
            ir.AddInstruction(sourceFile,
                currentLineNumber, ctx,
                OpCode.LOADWAV, path.result, varInstr.result, NULL_ID);
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
        public override void ExitPlaywavstmt(PlaywavstmtContext ctx)
        {
            AssertGraphics();
            var varInstr = LookupInstruction(ctx.variable());
            AssertVariable(ir.SymbolTable[varInstr.result], GetCtxString(ctx));
            ir.AddInstruction(sourceFile,
                currentLineNumber, ctx,
                OpCode.PLAYWAV, varInstr.result, NULL_ID, NULL_ID);
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
        public override void ExitStopwavstmt(StopwavstmtContext ctx)
        {
            AssertGraphics();
            var varInstr = LookupInstruction(ctx.variable());
            AssertVariable(ir.SymbolTable[varInstr.result], GetCtxString(ctx));
            ir.AddInstruction(sourceFile,
                currentLineNumber, ctx,
                OpCode.STOPWAV, varInstr.result, NULL_ID, NULL_ID);
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
        public override void ExitLoopwavstmt(LoopwavstmtContext ctx)
        {
            AssertGraphics();
            var varInstr = LookupInstruction(ctx.variable());
            AssertVariable(ir.SymbolTable[varInstr.result], GetCtxString(ctx));
            ir.AddInstruction(sourceFile,
                currentLineNumber, ctx,
                OpCode.LOOPWAV, varInstr.result, NULL_ID, NULL_ID);
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
        public override void ExitSleepstmt(SleepstmtContext ctx)
        {
            var millis = LookupInstruction(ctx.expr());
            Types.AssertNumeric(ir.SymbolTable[millis.result].Type.AtomTypeId, GetCtxString(ctx));
            ir.AddInstruction(sourceFile,
                currentLineNumber, ctx,
                OpCode.SLEEP, millis.result, NULL_ID, NULL_ID);
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
        public override void ExitBeepstmt(BeepstmtContext ctx)
        {
            AssertGraphics();
            ir.AddInstruction(sourceFile,
                currentLineNumber, ctx,
                OpCode.BEEP, NULL_ID, NULL_ID, NULL_ID);
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
        public override void ExitArray1dsortstmt(Array1dsortstmtContext ctx)
        {
            var var1Instr = GetArray1dVariableInstruction(ctx, ctx.variable(), false);
            ir.AddInstruction(sourceFile,
                currentLineNumber, ctx,
                OpCode.ARRAY1DSORT, var1Instr.result, NULL_ID, NULL_ID);
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
        public override void ExitArraycopystmt(ArraycopystmtContext ctx)
        {
            var var1Instr = GetArrayNdVariableInstruction(ctx, ctx.variable(0));
            var var2Instr = GetArrayNdVariableInstruction(ctx, ctx.variable(1));
            ir.AddInstruction(sourceFile,
                currentLineNumber, ctx,
                OpCode.ARRAYCOPY, var1Instr.result, var2Instr.result, NULL_ID);
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
        public override void ExitArray1dcopystmt(Array1dcopystmtContext ctx)
        {
            var var1Instr = GetArray1dVariableInstruction(ctx, ctx.variable(0), false);
            var var2Instr = GetArray1dVariableInstruction(ctx, ctx.variable(1), false);
            var src0 = LookupInstruction(ctx.src0);
            Types.AssertNumeric(ir.SymbolTable[src0.result].Type.AtomTypeId, GetCtxString(ctx));
            var dst0 = LookupInstruction(ctx.dst0);
            Types.AssertNumeric(ir.SymbolTable[dst0.result].Type.AtomTypeId, GetCtxString(ctx));
            var len = LookupInstruction(ctx.len);
            Types.AssertNumeric(ir.SymbolTable[len.result].Type.AtomTypeId, GetCtxString(ctx));
            ir.AddInstruction(sourceFile,
                currentLineNumber, ctx,
                OpCode.PARAM2, var1Instr.result, src0.result, NULL_ID);
            ir.AddInstruction(sourceFile,
                currentLineNumber, ctx,
                OpCode.PARAM2, var2Instr.result, dst0.result, NULL_ID);
            ir.AddInstruction(sourceFile,
                currentLineNumber, ctx,
                OpCode.ARRAY1DCOPY, len.result, NULL_ID, NULL_ID);
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
        public override void ExitArray2dshifthorstmt(Array2dshifthorstmtContext ctx)
        {
            var varInstr = GetArray2dVariableInstruction(ctx, ctx.variable());
            var expr = LookupInstruction(ctx.expr());
            Types.AssertNumeric(ir.SymbolTable[expr.result].Type.AtomTypeId, GetCtxString(ctx));
            ir.AddInstruction(sourceFile,
                currentLineNumber, ctx,
                OpCode.ARRAY2DSHIFTHOR, varInstr.result, expr.result, NULL_ID);
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
        public override void ExitArray2dshiftverstmt(Array2dshiftverstmtContext ctx)
        {
            var varInstr = GetArray2dVariableInstruction(ctx, ctx.variable());
            var expr = LookupInstruction(ctx.expr());
            Types.AssertNumeric(ir.SymbolTable[expr.result].Type.AtomTypeId, GetCtxString(ctx));
            ir.AddInstruction(sourceFile,
                currentLineNumber, ctx,
                OpCode.ARRAY2DSHIFTVER, varInstr.result, expr.result, NULL_ID);
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
        public override void ExitArrayfillstmt(ArrayfillstmtContext ctx)
        {
            var varInstr = GetArrayNdVariableInstruction(ctx, ctx.variable());
            var expr = LookupInstruction(ctx.expr());
            Types.AssertNumeric(ir.SymbolTable[expr.result].Type.AtomTypeId, GetCtxString(ctx));
            ir.AddInstruction(sourceFile,
                currentLineNumber, ctx,
                OpCode.ARRAYFILL, varInstr.result, expr.result, NULL_ID);
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
            foreach (var lr in letterRanges.Select(x => x.GetText()))
            {
                for (char i = lr[0]; i < lr[2]; i++)
                    defs.Add(i);
            }

            foreach (var def in defs)
                ir.SymbolTable.SetDefaultDataType(def, dataType);
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
        private static FileOpenMode GetFileOpenMode(Filemode1Context filemode1)
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
        private static FileOpenMode GetFileOpenMode(Filemode2Context filemode2)
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
        private static FileAccessMode GetFileAccessMode(AccessContext? access)
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
        private static LockMode GetLockMode(LockContext? @lock)
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
            return ir.SymbolTable.AddTmp(INT32, (e) => e.Value.SetInt32(lineNumber));
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
            CheckDataTypeMatch(ir.SymbolTable[id1], id2, lineSupplier);
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
            var entry2 = ir.SymbolTable[id2];
            if ((entry1.Type.AtomTypeId == PuffinBasicAtomTypeId.STRING && entry2.Type.AtomTypeId != PuffinBasicAtomTypeId.STRING)
                || (entry1.Type.AtomTypeId != PuffinBasicAtomTypeId.STRING && entry2.Type.AtomTypeId == PuffinBasicAtomTypeId.STRING))
            {
                throw new PuffinBasicSemanticError(DATA_TYPE_MISMATCH, line, $"Data type {entry1.Type.AtomTypeId} mismatches with {entry2.Type.AtomTypeId}");
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

