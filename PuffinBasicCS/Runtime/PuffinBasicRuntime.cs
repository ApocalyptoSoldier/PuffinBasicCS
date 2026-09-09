//using It.Unimi.Dsi.Fastutil.Ints;
namespace PuffinBasicCS.Runtime
{
    using PuffinBasicCS.Parser;
    using static PuffinBasicCS.Parser.PuffinBasicIR;
    using static PuffinBasicCS.Runtime.ArraysUtil;
    using static PuffinBasicCS.Runtime.Formatter;
    using static PuffinBasicCS.Runtime.Statements;
    //using Java.Io;
    //using Java.Util;
    //using Java.Util.Stream;
    using static PuffinBasicCS.Domain.PuffinBasicSymbolTable;
    using static PuffinBasicCS.Parser.PuffinBasicIR.OpCode;

    using System;
    using System.Collections.Generic;
    using System.IO;

    using PuffinBasicCS.File;
    using PuffinBasicCS.Error;

    public class PuffinBasicRuntime
    {
        private readonly PuffinBasicIR ir;
        private PrintBuffer printBuffer = new PrintBuffer();
        private ArrayState arrayState = new ArrayState();
        private Stack<int> gosubReturnLabelStack = new Stack<int>();
        private int programCounter;
        private Random random = new Random();
        private Dictionary<int, int> labelToInstrNum;
        private Dictionary<int, int> lineNumToInstrNum;
        private IList<Instruction> @params = new List<Instruction>();
        private FormatterCache formatterCache = new FormatterCache();
        private PuffinBasicFiles files;
        private ReadData readData;
        private readonly TextWriter @out;
        private readonly IEnvironment env;
        private readonly bool graphicsEnabled;
        //private GraphicsState graphicsState;
        //private SoundState soundState;
        public PuffinBasicRuntime(PuffinBasicIR ir, TextWriter @out, IEnvironment env, bool graphicsEnabled = false)
        {
            this.ir = ir;
            this.@out = @out;
            this.env = env;
        }

        private Dictionary<int,int> ComputeLabelToInstructionNumber(IList<Instruction> instructions)
        {
            Dictionary<int, int> labelToInstrNum = new Dictionary<int, int>();
            for (int i = 0; i < instructions.Count; i++)
            {
                var instr = instructions[i];
                if (instr.opCode == LABEL)
                {
                    labelToInstrNum.Add(instr.op1, i);
                }
            }

            return labelToInstrNum;
        }

        private int GetInstrNumForLabel(int id)
        {
            if (!labelToInstrNum.TryGetValue(id, out int instrNum))
                throw new PuffinBasicInternalError("Failed to find instruction# for label: " + id);

            return instrNum;
        }

        private Dictionary<int, int> ComputeLineNumberToInstructionNumber(IList<Instruction> instructions)
        {
            var linenumToInstrNum = new Dictionary<int, int>();
            int instrNum = 0;
            foreach (var instruction in instructions)
            {
                int lineNumber = instruction.GetInputRef().lineNumber;
                if (lineNumber >= 0)
                {
                    if (!linenumToInstrNum.ContainsKey(lineNumber))
                        linenumToInstrNum.Add(lineNumber, instrNum);
                }

                ++instrNum;
            }

            return linenumToInstrNum;
        }

        private int GetInstrNumForLineNumber(int lineNumber)
        {
            if (!lineNumToInstrNum.TryGetValue(lineNumber, out int instrNum))
                throw new PuffinBasicInternalError("Failed to find instruction# for line#: " + lineNumber);

            return instrNum;
        }

        public virtual void Run()
        {
            var instructions = ir.GetInstructions();
            this.labelToInstrNum = ComputeLabelToInstructionNumber(instructions);
            this.lineNumToInstrNum = ComputeLineNumberToInstructionNumber(instructions);
            this.files = new PuffinBasicFiles(new SystemInputOutputFile(Console.In, @out));
            this.readData = ProcessDataInstructions(instructions);
            //this.graphicsState = new GraphicsState();
            //this.soundState = new SoundState();
            try
            {
                var numInstructions = instructions.Count;
                bool end = false;
                while (!end && programCounter < numInstructions)
                {
                    var instruction = instructions[programCounter];
                    try
                    {
                        end = RunInstruction(instruction);
                    }
                    catch (PuffinBasicRuntimeError e)
                    {
                        throw new PuffinBasicRuntimeError(e, instruction, ir.GetCodeStreamFor(instruction));
                    }
                    catch (Exception e)
                    {
                        throw new PuffinBasicRuntimeError(e, instruction, ir.GetCodeStreamFor(instruction));
                    }
                }
            }
            catch (Exception e)
            { 
                Console.Error.WriteLine(e.ToString());
                //e.PrintStackTrace(Console.Error);
            }
            finally
            {
                if (graphicsEnabled)
                    GraphicsRuntime.End();
                //soundState.Dispose();
            }
        }

        private ReadData ProcessDataInstructions(IList<Instruction> instructions)
        {
            List<Domain.STObjects.ISTEntry> entries = new List<Domain.STObjects.ISTEntry>();
            
            foreach (Instruction i  in instructions)
                if (i.opCode == OpCode.DATA)
                    entries.Add(ir.SymbolTable[i.op1]);

            return new ReadData(entries);
        }

        private bool RunInstruction(Instruction instruction)
        {
            int nextProgramCounter = programCounter + 1;
            switch (instruction.opCode)
            {
                case VARREF:
                    Types.Varref(ir.SymbolTable, instruction);
                    break;
                case DIM:
                {
                    if (@params.Count == 0)
                    {
                        throw new PuffinBasicInternalError("Expected >0 params, but found none!");
                    }

                    ArraysUtil.Dim(ir.SymbolTable, @params, instruction);
                    @params.Clear();
                }

                    break;
                case ALLOCARRAY:
                {
                    if (@params.Count == 0)
                    {
                        throw new PuffinBasicInternalError("Expected >0 params, but found none!");
                    }

                    ArraysUtil.AllocArray(ir.SymbolTable, @params, instruction);
                    @params.Clear();
                }

                    break;
                case REALLOCARRAY:
                {
                    if (@params.Count == 0)
                    {
                        throw new PuffinBasicInternalError("Expected >0 params, but found none!");
                    }

                    ArraysUtil.ReallocArray(ir.SymbolTable, @params, instruction);
                    @params.Clear();
                }

                    break;
                case CREATE_INSTANCE:
                    Statements.CreateInstance(ir.SymbolTable, instruction);
                    break;
                case STRUCT_LVALUE:
                {
                    if (@params.Count == 0)
                    {
                        throw new PuffinBasicInternalError("Expected >0 params, but found none!");
                    }

                    Statements.StructLValue(ir.SymbolTable, @params, instruction);
                    @params.Clear();
                }

                    break;
                case MEMBER_FUNC_CALL:
                {
                    Statements.MemberFuncCall(ir.SymbolTable, @params, instruction);
                    @params.Clear();
                }

                    break;
                case STRUCT_MEMBER_REF:
                {
                    if (@params.Count == 0)
                    {
                        throw new PuffinBasicInternalError("Expected >0 params, but found none!");
                    }

                    Statements.StructMemberRef(ir.SymbolTable, @params, instruction);
                    @params.Clear();
                }

                    break;
                case ASSIGN:
                case COPY:
                    Types.Copy(ir.SymbolTable, instruction);
                    break;
                case PARAM_COPY:
                    Types.ParamCopy(ir.SymbolTable, instruction);
                    break;
                case UNARY_MINUS:
                    Operators.UnaryMinus(ir.SymbolTable, instruction);
                    break;
                case PRINT:
                    Statements.Print(printBuffer, ir.SymbolTable, instruction);
                    break;
                case PRINTUSING:
                    Statements.Printusing(formatterCache, printBuffer, ir.SymbolTable, instruction);
                    break;
                case FLUSH:
                    Statements.Flush(files, printBuffer, ir.SymbolTable, instruction);
                    break;
                case RESET_ARRAY_IDX:
                    ArraysUtil.ResetIndex(arrayState, ir.SymbolTable, instruction);
                    break;
                case SET_ARRAY_IDX:
                    ArraysUtil.SetIndex(arrayState, ir.SymbolTable, instruction);
                    break;
                case ARRAYREF:
                    ArraysUtil.Arrayref(ir.SymbolTable, instruction);
                    break;
                case LABEL:
                    break;
                case GOTO_LINENUM:
                {
                    var lineNumber = ir.SymbolTable[instruction.op1].Value.GetInt32();
                    nextProgramCounter = GetInstrNumForLineNumber(lineNumber);
                }

                    break;
                case GOTO_LABEL_IF:
                {
                    if (ir.SymbolTable[instruction.op1].Value.GetInt64() != 0)
                    {
                        nextProgramCounter = GetInstrNumForLabel(instruction.op2);
                    }
                }

                    break;
                case GOTO_LABEL:
                    nextProgramCounter = GetInstrNumForLabel(instruction.op1);
                    break;
                case GOTO_CALLER:
                    nextProgramCounter = ir.SymbolTable.GetCurrentScope().GetCallerInstrId();
                    break;
                case PUSH_RT_SCOPE:
                    ir.SymbolTable.PushRuntimeScope(instruction.op1, GetInstrNumForLabel(instruction.op2));
                    break;
                case POP_RT_SCOPE:
                    ir.SymbolTable.PopScope();
                    break;
                case PUSH_RETLABEL:
                    gosubReturnLabelStack.Push(instruction.op1);
                    break;
                case RETURN:
                {
                    if (instruction.op1 == NULL_ID)
                    {
                        nextProgramCounter = GetInstrNumForLabel(gosubReturnLabelStack.Pop());
                    }
                    else
                    {

                        // Ignore label because we need to return to the lineNumber
                        gosubReturnLabelStack.Pop();
                        var lineNumber = ir.SymbolTable[instruction.op1].Value.GetInt32();
                        nextProgramCounter = GetInstrNumForLineNumber(lineNumber);
                    }
                }

                    break;
                case EXPI32:
                    Operators.ExpInt32(ir.SymbolTable, instruction);
                    break;
                case EXPI64:
                    Operators.ExpInt64(ir.SymbolTable, instruction);
                    break;
                case EXPF32:
                    Operators.ExpFloat32(ir.SymbolTable, instruction);
                    break;
                case EXPF64:
                    Operators.ExpFloat64(ir.SymbolTable, instruction);
                    break;
                case MULI32:
                    Operators.MulInt32(ir.SymbolTable, instruction);
                    break;
                case MULI64:
                    Operators.MulInt64(ir.SymbolTable, instruction);
                    break;
                case MULF32:
                    Operators.MulFloat32(ir.SymbolTable, instruction);
                    break;
                case MULF64:
                    Operators.MulFloat64(ir.SymbolTable, instruction);
                    break;
                case IDIV:
                    Operators.Idiv(ir.SymbolTable, instruction);
                    break;
                case FDIV:
                    Operators.Fdiv(ir.SymbolTable, instruction);
                    break;
                case ADDI32:
                    Operators.AddInt32(ir.SymbolTable, instruction);
                    break;
                case ADDI64:
                    Operators.AddInt64(ir.SymbolTable, instruction);
                    break;
                case ADDF32:
                    Operators.AddFloat32(ir.SymbolTable, instruction);
                    break;
                case ADDF64:
                    Operators.AddFloat64(ir.SymbolTable, instruction);
                    break;
                case SUBI32:
                    Operators.SubInt32(ir.SymbolTable, instruction);
                    break;
                case SUBI64:
                    Operators.SubInt64(ir.SymbolTable, instruction);
                    break;
                case SUBF32:
                    Operators.SubFloat32(ir.SymbolTable, instruction);
                    break;
                case SUBF64:
                    Operators.SubFloat64(ir.SymbolTable, instruction);
                    break;
                case MOD:
                    Operators.Mod(ir.SymbolTable, instruction);
                    break;
                case EQI32:
                    Operators.EqInt32(ir.SymbolTable, instruction);
                    break;
                case EQI64:
                    Operators.EqInt64(ir.SymbolTable, instruction);
                    break;
                case EQF32:
                    Operators.EqFloat32(ir.SymbolTable, instruction);
                    break;
                case EQF64:
                    Operators.EqFloat64(ir.SymbolTable, instruction);
                    break;
                case EQSTR:
                    Operators.EqStr(ir.SymbolTable, instruction);
                    break;
                case NEI32:
                    Operators.NeInt32(ir.SymbolTable, instruction);
                    break;
                case NEI64:
                    Operators.NeInt64(ir.SymbolTable, instruction);
                    break;
                case NEF32:
                    Operators.NeFloat32(ir.SymbolTable, instruction);
                    break;
                case NEF64:
                    Operators.NeFloat64(ir.SymbolTable, instruction);
                    break;
                case NESTR:
                    Operators.NeStr(ir.SymbolTable, instruction);
                    break;
                case LTI32:
                    Operators.LtInt32(ir.SymbolTable, instruction);
                    break;
                case LTI64:
                    Operators.LtInt64(ir.SymbolTable, instruction);
                    break;
                case LTF32:
                    Operators.LtFloat32(ir.SymbolTable, instruction);
                    break;
                case LTF64:
                    Operators.LtFloat64(ir.SymbolTable, instruction);
                    break;
                case LTSTR:
                    Operators.LtStr(ir.SymbolTable, instruction);
                    break;
                case LEI32:
                    Operators.LeInt32(ir.SymbolTable, instruction);
                    break;
                case LEI64:
                    Operators.LeInt64(ir.SymbolTable, instruction);
                    break;
                case LEF32:
                    Operators.LeFloat32(ir.SymbolTable, instruction);
                    break;
                case LEF64:
                    Operators.LeFloat64(ir.SymbolTable, instruction);
                    break;
                case LESTR:
                    Operators.LeStr(ir.SymbolTable, instruction);
                    break;
                case GTI32:
                    Operators.GtInt32(ir.SymbolTable, instruction);
                    break;
                case GTI64:
                    Operators.GtInt64(ir.SymbolTable, instruction);
                    break;
                case GTF32:
                    Operators.GtFloat32(ir.SymbolTable, instruction);
                    break;
                case GTF64:
                    Operators.GtFloat64(ir.SymbolTable, instruction);
                    break;
                case GTSTR:
                    Operators.GtStr(ir.SymbolTable, instruction);
                    break;
                case GEI32:
                    Operators.GeInt32(ir.SymbolTable, instruction);
                    break;
                case GEI64:
                    Operators.GeInt64(ir.SymbolTable, instruction);
                    break;
                case GEF32:
                    Operators.GeFloat32(ir.SymbolTable, instruction);
                    break;
                case GEF64:
                    Operators.GeFloat64(ir.SymbolTable, instruction);
                    break;
                case GESTR:
                    Operators.GeStr(ir.SymbolTable, instruction);
                    break;
                case NOT:
                    Operators.UnaryNot(ir.SymbolTable, instruction);
                    break;
                case AND:
                    Operators.And(ir.SymbolTable, instruction);
                    break;
                case OR:
                    Operators.Or(ir.SymbolTable, instruction);
                    break;
                case XOR:
                    Operators.Xor(ir.SymbolTable, instruction);
                    break;
                case EQV:
                    Operators.Eqv(ir.SymbolTable, instruction);
                    break;
                case IMP:
                    Operators.Imp(ir.SymbolTable, instruction);
                    break;
                case LEFTSHIFT:
                    Operators.LeftShift(ir.SymbolTable, instruction);
                    break;
                case RIGHTSHIFT:
                    Operators.RightShift(ir.SymbolTable, instruction);
                    break;
                case END:
                    return true;
                case ABS:
                    Functions.Abs(ir.SymbolTable, instruction);
                    break;
                case ASC:
                    Functions.Asc(ir.SymbolTable, instruction);
                    break;
                case SIN:
                    Functions.Sin(ir.SymbolTable, instruction);
                    break;
                case COS:
                    Functions.Cos(ir.SymbolTable, instruction);
                    break;
                case TAN:
                    Functions.Tan(ir.SymbolTable, instruction);
                    break;
                case ASIN:
                    Functions.Asin(ir.SymbolTable, instruction);
                    break;
                case ACOS:
                    Functions.Acos(ir.SymbolTable, instruction);
                    break;
                case ATN:
                    Functions.Atn(ir.SymbolTable, instruction);
                    break;
                case SINH:
                    Functions.Sinh(ir.SymbolTable, instruction);
                    break;
                case COSH:
                    Functions.Cosh(ir.SymbolTable, instruction);
                    break;
                case TANH:
                    Functions.Tanh(ir.SymbolTable, instruction);
                    break;
                case SQR:
                    Functions.Sqr(ir.SymbolTable, instruction);
                    break;
                case LOG:
                    Functions.Log(ir.SymbolTable, instruction);
                    break;
                case LOG10:
                    Functions.Log10(ir.SymbolTable, instruction);
                    break;
                case LOG2:
                    Functions.Log2(ir.SymbolTable, instruction);
                    break;
                case EEXP:
                    Functions.Exp(ir.SymbolTable, instruction);
                    break;
                case TORAD:
                    Functions.ToRad(ir.SymbolTable, instruction);
                    break;
                case TODEG:
                    Functions.ToDeg(ir.SymbolTable, instruction);
                    break;
                case FLOOR:
                    Functions.Floor(ir.SymbolTable, instruction);
                    break;
                case CEIL:
                    Functions.Ceil(ir.SymbolTable, instruction);
                    break;
                case ROUND:
                    Functions.Round(ir.SymbolTable, instruction);
                    break;
                case E:
                    Functions.E(ir.SymbolTable, instruction);
                    break;
                case PI:
                    Functions.Pi(ir.SymbolTable, instruction);
                    break;
                case MIN:
                    Functions.Min(ir.SymbolTable, instruction);
                    break;
                case MAX:
                    Functions.Max(ir.SymbolTable, instruction);
                    break;
                case ARRAYFILL:
                    ArraysUtil.Arrayfill(ir.SymbolTable, instruction);
                    break;
                case ARRAYCOPY:
                    ArraysUtil.ArrayCopy(ir.SymbolTable, instruction);
                    break;
                case ARRAY1DMIN:
                    ArraysUtil.Array1dMin(ir.SymbolTable, instruction);
                    break;
                case ARRAY1DMAX:
                    ArraysUtil.Array1dMax(ir.SymbolTable, instruction);
                    break;
                case ARRAY1DMEAN:
                    ArraysUtil.Array1dMean(ir.SymbolTable, instruction);
                    break;
                case ARRAY1DSUM:
                    ArraysUtil.Array1dSum(ir.SymbolTable, instruction);
                    break;
                case ARRAY1DSTD:
                    ArraysUtil.Array1dStddev(ir.SymbolTable, instruction);
                    break;
                case ARRAY1DMEDIAN:
                    ArraysUtil.Array1dMedian(ir.SymbolTable, instruction);
                    break;
                case ARRAY1DPCT:
                    ArraysUtil.Array1dPercentile(ir.SymbolTable, instruction);
                    break;
                case ARRAY1DSORT:
                    ArraysUtil.Array1dSort(ir.SymbolTable, instruction);
                    break;
                case ARRAY1DBINSEARCH:
                    ArraysUtil.Array1dBinSearch(ir.SymbolTable, instruction);
                    break;
                case ARRAY2DSHIFTVER:
                    ArraysUtil.Array2dShiftVertical(ir.SymbolTable, instruction);
                    break;
                case ARRAY2DSHIFTHOR:
                    ArraysUtil.Array2dShiftHorizontal(ir.SymbolTable, instruction);
                    break;
                case ARRAY1DCOPY:
                {
                    if (@params.Count != 2)
                    {
                        throw new PuffinBasicInternalError("Expected 2 params, but found: " + @params);
                    }

                    ArraysUtil.Array1DCopy(ir.SymbolTable, @params[0], @params[1], instruction);
                    @params.Clear();
                }

                    break;
                case ARRAY2DFINDROW:
                {
                    if (@params.Count != 2)
                    {
                        throw new PuffinBasicInternalError("Expected 2 params, but found: " + @params);
                    }

                    ArraysUtil.Array2dFindRow(ir.SymbolTable, @params, instruction);
                    @params.Clear();
                }

                    break;
                case ARRAY2DFINDCOLUMN:
                {
                    if (@params.Count != 2)
                    {
                        throw new PuffinBasicInternalError("Expected 2 params, but found: " + @params);
                    }

                    ArraysUtil.Array2dFindColumn(ir.SymbolTable, @params, instruction);
                    @params.Clear();
                }

                    break;
                case CINT:
                    Functions.Cint(ir.SymbolTable, instruction);
                    break;
                case CLNG:
                    Functions.Clng(ir.SymbolTable, instruction);
                    break;
                case CSNG:
                    Functions.Csng(ir.SymbolTable, instruction);
                    break;
                case CDBL:
                    Functions.Cdbl(ir.SymbolTable, instruction);
                    break;
                case CHRDLR:
                    Functions.Chrdlr(ir.SymbolTable, instruction);
                    break;
                case CVI:
                    Functions.Cvi(ir.SymbolTable, instruction);
                    break;
                case CVL:
                    Functions.Cvl(ir.SymbolTable, instruction);
                    break;
                case CVS:
                    Functions.Cvs(ir.SymbolTable, instruction);
                    break;
                case CVD:
                    Functions.Cvd(ir.SymbolTable, instruction);
                    break;
                case MKIDLR:
                    Functions.Mkidlr(ir.SymbolTable, instruction);
                    break;
                case MKLDLR:
                    Functions.Mkldlr(ir.SymbolTable, instruction);
                    break;
                case MKSDLR:
                    Functions.Mksdlr(ir.SymbolTable, instruction);
                    break;
                case MKDDLR:
                    Functions.Mkddlr(ir.SymbolTable, instruction);
                    break;
                case SPACEDLR:
                    Functions.Spacedlr(ir.SymbolTable, instruction);
                    break;
                case STRDLR:
                    Functions.Strdlr(ir.SymbolTable, instruction);
                    break;
                case VAL:
                    Functions.Val(ir.SymbolTable, instruction);
                    break;
                case INT:
                    Functions.Fnint(ir.SymbolTable, instruction);
                    break;
                case FIX:
                    Functions.Fix(ir.SymbolTable, instruction);
                    break;
                case LEN:
                    Functions.Len(ir.SymbolTable, instruction);
                    break;
                case HEXDLR:
                    Functions.Hexdlr(ir.SymbolTable, instruction);
                    break;
                case OCTDLR:
                    Functions.Octdlr(ir.SymbolTable, instruction);
                    break;
                case LEFTDLR:
                    Functions.Leftdlr(ir.SymbolTable, instruction);
                    break;
                case RIGHTDLR:
                    Functions.Rightdlr(ir.SymbolTable, instruction);
                    break;
                case SPLITDLR:
                    Functions.Splitdlr(ir.SymbolTable, instruction);
                    break;
                case PARAM1:
                case PARAM2:
                    @params.Add(instruction);
                    break;
                case INSTR:
                {
                    if (@params.Count != 1)
                    {
                        throw new PuffinBasicInternalError("Expected 1 param, but found: " + @params);
                    }

                    Functions.Instr(ir.SymbolTable, @params[0], instruction);
                    @params.Clear();
                }

                    break;
                case MIDDLR:
                {
                    if (@params.Count != 1)
                    {
                        throw new PuffinBasicInternalError("Expected 1 param, but found: " + @params);
                    }

                    Functions.Middlr(ir.SymbolTable, @params[0], instruction);
                    @params.Clear();
                }

                    break;
                case MIDDLR_STMT:
                {
                    if (@params.Count != 1)
                    {
                        throw new PuffinBasicInternalError("Expected 1 param, but found: " + @params);
                    }

                    Statements.Middlr(ir.SymbolTable, @params[0], instruction);
                    @params.Clear();
                }

                    break;
                case OPEN:
                {
                    if (@params.Count != 2)
                    {
                        throw new PuffinBasicInternalError("Expected 2 params, but found: " + @params);
                    }

                    Statements.Open(files, ir.SymbolTable, @params[0], @params[1], instruction);
                    @params.Clear();
                }

                    break;
                case CLOSE_ALL:
                    Statements.CloseAll(files);
                    break;
                case CLOSE:
                    Statements.Dispose(files, ir.SymbolTable, instruction);
                    break;
                case FIELD:
                {
                    Statements.Field(files, ir.SymbolTable, @params, instruction);
                    @params.Clear();
                }

                    break;
                case HSB2RGB:
                {
                    if (@params.Count != 1)
                    {
                        throw new PuffinBasicInternalError("Expected 1 param, but found: " + @params);
                    }

                    GraphicsRuntime.Hsb2rgb(ir.SymbolTable, @params[0], instruction);
                    @params.Clear();
                }

                    break;
                case PUTF:
                    Statements.Putf(files, ir.SymbolTable, instruction);
                    break;
                case GETF:
                    Statements.Getf(files, ir.SymbolTable, instruction);
                    break;
                case LOC:
                    Functions.Loc(files, ir.SymbolTable, instruction);
                    break;
                case LOF:
                    Functions.Lof(files, ir.SymbolTable, instruction);
                    break;
                case EOF:
                    Functions.Eof(files, ir.SymbolTable, instruction);
                    break;
                case RND:
                    Functions.Rnd(random, ir.SymbolTable, instruction);
                    break;
                case RANDOMIZE:
                    Statements.Randomize(random, ir.SymbolTable, instruction);
                    break;
                case RANDOMIZE_TIMER:
                    Statements.RandomizeTimer(random);
                    break;
                case SGN:
                    Functions.Sgn(ir.SymbolTable, instruction);
                    break;
                case LSET:
                    Statements.Lset(ir.SymbolTable, instruction);
                    break;
                case RSET:
                    Statements.Rset(ir.SymbolTable, instruction);
                    break;
                case TIMER:
                    Functions.Timer(ir.SymbolTable, instruction);
                    break;
                case TIMERMILLIS:
                    Functions.TimerMillis(ir.SymbolTable, instruction);
                    break;
                case STRINGDLR:
                    Functions.Stringdlr(ir.SymbolTable, instruction);
                    break;
                case SWAP:
                    Statements.Swap(ir.SymbolTable, instruction);
                    break;
                case CONCAT:
                    Operators.Concat(ir.SymbolTable, instruction);
                    break;
                case INPUTDLR:
                    Functions.Inputdlr(files, ir.SymbolTable, instruction);
                    break;
                case INPUT:
                {
                    Statements.Input(files, ir.SymbolTable, @params, instruction);
                    @params.Clear();
                }

                    break;
                case LINE_INPUT:
                {
                    if (@params.Count != 1)
                    {
                        throw new PuffinBasicInternalError("Expected 1 param, but found: " + @params);
                    }

                    Statements.Lineinput(files, ir.SymbolTable, @params[0], instruction);
                    @params.Clear();
                }

                    break;
                case WRITE:
                    Statements.Write(printBuffer, ir.SymbolTable, instruction);
                    break;
                case DATA:
                    break;
                case RESTORE:
                    readData.Restore();
                    break;
                case READ:
                    Statements.Read(readData, ir.SymbolTable, instruction);
                    break;
                case ENVIRONDLR:
                    Functions.Environdlr(env, ir.SymbolTable, instruction);
                    break;
                case SLEEP:
                    Statements.Sleep(ir.SymbolTable, instruction);
                    break;
                case SCREEN:
                {
                    if (@params.Count != 3)
                    {
                        throw new PuffinBasicInternalError("Expected 1 param, but found: " + @params);
                    }

                    GraphicsRuntime.Screen(ir.SymbolTable, @params, instruction);
                    @params.Clear();
                }

                    break;
                case REPAINT:
                    GraphicsRuntime.Repaint();
                    break;
                case CIRCLE:
                {
                    if (@params.Count != 3)
                    {
                        throw new PuffinBasicInternalError("Expected 3 params, but found: " + @params);
                    }

                    GraphicsRuntime.Circle(ir.SymbolTable, @params, instruction);
                    @params.Clear();
                }

                    break;
                case LINE:
                {
                    if (@params.Count != 2)
                    {
                        throw new PuffinBasicInternalError("Expected 2 params, but found: " + @params);
                    }

                    GraphicsRuntime.Line(ir.SymbolTable, @params, instruction);
                    @params.Clear();
                }

                    break;
                case COLOR:
                {
                    if (@params.Count != 1)
                    {
                        throw new PuffinBasicInternalError("Expected 1 params, but found: " + @params);
                    }

                    GraphicsRuntime.Color(ir.SymbolTable, @params[0], instruction);
                    @params.Clear();
                }

                    break;
                case PAINT:
                {
                    if (@params.Count != 2)
                    {
                        throw new PuffinBasicInternalError("Expected 2 params, but found: " + @params);
                    }

                    GraphicsRuntime.Paint(ir.SymbolTable, @params, instruction);
                    @params.Clear();
                }

                    break;
                case PSET:
                {
                    if (@params.Count != 2)
                    {
                        throw new PuffinBasicInternalError("Expected 2 params, but found: " + @params);
                    }

                    GraphicsRuntime.Pset(ir.SymbolTable, @params, instruction);
                    @params.Clear();
                }

                    break;
                case GGET:
                {
                    if (@params.Count != 2)
                    {
                        throw new PuffinBasicInternalError("Expected 2 params, but found: " + @params);
                    }

                    GraphicsRuntime.Get(ir.SymbolTable, @params, instruction);
                    @params.Clear();
                }

                    break;
                case GPUT:
                {
                    if (@params.Count != 2)
                    {
                        throw new PuffinBasicInternalError("Expected 2 params, but found: " + @params);
                    }

                    GraphicsRuntime.Put(ir.SymbolTable, @params[0], @params[1], instruction);
                    @params.Clear();
                }

                    break;
                case BUFFERCOPYHOR:
                {
                    if (@params.Count != 1)
                    {
                        throw new PuffinBasicInternalError("Expected 1 param, but found: " + @params);
                    }

                    GraphicsRuntime.BufferCopyHor(ir.SymbolTable, @params[0], instruction);
                    @params.Clear();
                }

                    break;
                case FONT:
                {
                    if (@params.Count != 1)
                    {
                        throw new PuffinBasicInternalError("Expected 1 param, but found: " + @params);
                    }

                    GraphicsRuntime.Font(ir.SymbolTable, @params[0], instruction);
                    @params.Clear();
                }

                    break;
                case DRAWSTR:
                {
                    if (@params.Count != 1)
                    {
                        throw new PuffinBasicInternalError("Expected 1 param, but found: " + @params);
                    }

                    GraphicsRuntime.Drawstr(ir.SymbolTable, @params[0], instruction);
                    @params.Clear();
                }

                    break;
                case LOADIMG:
                    GraphicsRuntime.Loadimg(ir.SymbolTable, instruction);
                    break;
                case SAVEIMG:
                    GraphicsRuntime.Saveimg(ir.SymbolTable, instruction);
                    break;
                case DRAW:
                    GraphicsRuntime.Draw(ir.SymbolTable, instruction);
                    break;
                case INKEYDLR:
                    GraphicsRuntime.Inkeydlr(ir.SymbolTable, instruction);
                    break;
                case CLS:
                    GraphicsRuntime.Cls();
                    break;
                case BEEP:
                    GraphicsRuntime.Beep();
                    break;
                case LOADWAV:
                    GraphicsRuntime.Loadwav(ir.SymbolTable, instruction);
                    break;
                case PLAYWAV:
                    GraphicsRuntime.Playwav(ir.SymbolTable, instruction);
                    break;
                case STOPWAV:
                    GraphicsRuntime.Stopwav(ir.SymbolTable, instruction);
                    break;
                case LOOPWAV:
                    GraphicsRuntime.Loopwav(ir.SymbolTable, instruction);
                    break;
                case MOUSEMOVEDX:
                    GraphicsRuntime.MouseMovedX(ir.SymbolTable, instruction);
                    break;
                case MOUSEMOVEDY:
                    GraphicsRuntime.MouseMovedY(ir.SymbolTable, instruction);
                    break;
                case MOUSEDRAGGEDX:
                    GraphicsRuntime.MouseDraggedX(ir.SymbolTable, instruction);
                    break;
                case MOUSEDRAGGEDY:
                    GraphicsRuntime.MouseDraggedY(ir.SymbolTable, instruction);
                    break;
                case MOUSEBUTTONCLICKED:
                    GraphicsRuntime.MouseButtonClicked(ir.SymbolTable, instruction);
                    break;
                case MOUSEBUTTONPRESSED:
                    GraphicsRuntime.MouseButtonPressed(ir.SymbolTable, instruction);
                    break;
                case MOUSEBUTTONRELEASED:
                    GraphicsRuntime.MouseButtonReleased(ir.SymbolTable, instruction);
                    break;
                case ISKEYPRESSED:
                    GraphicsRuntime.IsKeyPressed(ir.SymbolTable, instruction);
                    break;
            }

            this.programCounter = nextProgramCounter;
            return false;
        }
    }
}

