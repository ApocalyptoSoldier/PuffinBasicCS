//using It.Unimi.Dsi.Fastutil.Ints;
using Org.Puffinbasic.Error;
using Org.Puffinbasic.File;
using Org.Puffinbasic.Parser;
using static Org.Puffinbasic.Parser.PuffinBasicIR;
using static Org.Puffinbasic.Runtime.ArraysUtil;
using static Org.Puffinbasic.Runtime.Formatter;
using static Org.Puffinbasic.Runtime.GraphicsRuntime;
using static Org.Puffinbasic.Runtime.Statements;
//using Java.Io;
//using Java.Util;
//using Java.Util.Stream;
using static Org.Puffinbasic.Domain.PuffinBasicSymbolTable;
using static Org.Puffinbasic.Parser.PuffinBasicIR.OpCode;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.IO;
using System.Collections;

namespace Org.Puffinbasic.Runtime
{
    public class PuffinBasicRuntime
    {
        private readonly PuffinBasicIR ir;
        private PrintBuffer printBuffer;
        private ArrayState arrayState;
        private IntStack gosubReturnLabelStack;
        private int programCounter;
        private Random random;
        private Dictionary<int, int> labelToInstrNum;
        private Dictionary<int, int> lineNumToInstrNum;
        private IList<Instruction> @params;
        private FormatterCache formatterCache;
        private PuffinBasicFiles files;
        private ReadData readData;
        private readonly PrintStream @out;
        private readonly IEnvironment env;
        private GraphicsState graphicsState;
        private SoundState soundState;
        public PuffinBasicRuntime(PuffinBasicIR ir, TextWriter @out, IEnvironment env)
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
                    labelToInstrNum.Put(instr.op1, i);
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
            this.printBuffer = new PrintBuffer();
            this.arrayState = new ArrayState();
            this.gosubReturnLabelStack = new ArrayList<int>();
            this.random = new Random();
            this.formatterCache = new FormatterCache();
            this.@params = new List<object>(4);
            this.files = new PuffinBasicFiles(new SystemInputOutputFile(System.@in, @out));
            this.readData = ProcessDataInstructions(instructions);
            this.graphicsState = new GraphicsState();
            this.soundState = new SoundState();
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
                e.PrintStackTrace(Console.Error);
            }
            finally
            {
                GraphicsRuntime.End(graphicsState);
                soundState.Dispose();
            }
        }

        private ReadData ProcessDataInstructions(IList<Instruction> instructions)
        {
            return new ReadData(instructions.Stream().Filter((i) => i.opCode == DATA).Map((instruction) => ir.GetSymbolTable()[instruction.op1]).Collect(Collectors.ToList()));
        }

        private bool RunInstruction(Instruction instruction)
        {
            int nextProgramCounter = programCounter + 1;
            switch (instruction.opCode)
            {
                case VARREF:
                    Types.Varref(ir.GetSymbolTable(), instruction);
                    break;
                case DIM:
                {
                    if (@params.Count == 0)
                    {
                        throw new PuffinBasicInternalError("Expected >0 params, but found none!");
                    }

                    ArraysUtil.Dim(ir.GetSymbolTable(), @params, instruction);
                    @params.Clear();
                }

                    break;
                case ALLOCARRAY:
                {
                    if (@params.Count == 0)
                    {
                        throw new PuffinBasicInternalError("Expected >0 params, but found none!");
                    }

                    ArraysUtil.AllocArray(ir.GetSymbolTable(), @params, instruction);
                    @params.Clear();
                }

                    break;
                case REALLOCARRAY:
                {
                    if (@params.Count == 0)
                    {
                        throw new PuffinBasicInternalError("Expected >0 params, but found none!");
                    }

                    ArraysUtil.ReallocArray(ir.GetSymbolTable(), @params, instruction);
                    @params.Clear();
                }

                    break;
                case CREATE_INSTANCE:
                    Statements.CreateInstance(ir.GetSymbolTable(), instruction);
                    break;
                case STRUCT_LVALUE:
                {
                    if (@params.Count == 0)
                    {
                        throw new PuffinBasicInternalError("Expected >0 params, but found none!");
                    }

                    Statements.StructLValue(ir.GetSymbolTable(), @params, instruction);
                    @params.Clear();
                }

                    break;
                case MEMBER_FUNC_CALL:
                {
                    Statements.MemberFuncCall(ir.GetSymbolTable(), @params, instruction);
                    @params.Clear();
                }

                    break;
                case STRUCT_MEMBER_REF:
                {
                    if (@params.Count == 0)
                    {
                        throw new PuffinBasicInternalError("Expected >0 params, but found none!");
                    }

                    Statements.StructMemberRef(ir.GetSymbolTable(), @params, instruction);
                    @params.Clear();
                }

                    break;
                case ASSIGN:
                case COPY:
                    Types.Copy(ir.GetSymbolTable(), instruction);
                    break;
                case PARAM_COPY:
                    Types.ParamCopy(ir.GetSymbolTable(), instruction);
                    break;
                case UNARY_MINUS:
                    Operators.UnaryMinus(ir.GetSymbolTable(), instruction);
                    break;
                case PRINT:
                    Statements.Print(printBuffer, ir.GetSymbolTable(), instruction);
                    break;
                case PRINTUSING:
                    Statements.Printusing(formatterCache, printBuffer, ir.GetSymbolTable(), instruction);
                    break;
                case FLUSH:
                    Statements.Flush(files, printBuffer, ir.GetSymbolTable(), instruction);
                    break;
                case RESET_ARRAY_IDX:
                    ArraysUtil.ResetIndex(arrayState, ir.GetSymbolTable(), instruction);
                    break;
                case SET_ARRAY_IDX:
                    ArraysUtil.SetIndex(arrayState, ir.GetSymbolTable(), instruction);
                    break;
                case ARRAYREF:
                    ArraysUtil.Arrayref(ir.GetSymbolTable(), instruction);
                    break;
                case LABEL:
                    break;
                case GOTO_LINENUM:
                {
                    var lineNumber = ir.GetSymbolTable()[instruction.op1].GetValue().GetInt32();
                    nextProgramCounter = GetInstrNumForLineNumber(lineNumber);
                }

                    break;
                case GOTO_LABEL_IF:
                {
                    if (ir.GetSymbolTable()[instruction.op1].GetValue().GetInt64() != 0)
                    {
                        nextProgramCounter = GetInstrNumForLabel(instruction.op2);
                    }
                }

                    break;
                case GOTO_LABEL:
                    nextProgramCounter = GetInstrNumForLabel(instruction.op1);
                    break;
                case GOTO_CALLER:
                    nextProgramCounter = ir.GetSymbolTable().GetCurrentScope().GetCallerInstrId();
                    break;
                case PUSH_RT_SCOPE:
                    ir.GetSymbolTable().PushRuntimeScope(instruction.op1, GetInstrNumForLabel(instruction.op2));
                    break;
                case POP_RT_SCOPE:
                    ir.GetSymbolTable().PopScope();
                    break;
                case PUSH_RETLABEL:
                    gosubReturnLabelStack.Push(instruction.op1);
                    break;
                case RETURN:
                {
                    if (instruction.op1 == NULL_ID)
                    {
                        nextProgramCounter = GetInstrNumForLabel(gosubReturnLabelStack.PopInt());
                    }
                    else
                    {

                        // Ignore label because we need to return to the lineNumber
                        gosubReturnLabelStack.PopInt();
                        var lineNumber = ir.GetSymbolTable()[instruction.op1].GetValue().GetInt32();
                        nextProgramCounter = GetInstrNumForLineNumber(lineNumber);
                    }
                }

                    break;
                case EXPI32:
                    Operators.ExpInt32(ir.GetSymbolTable(), instruction);
                    break;
                case EXPI64:
                    Operators.ExpInt64(ir.GetSymbolTable(), instruction);
                    break;
                case EXPF32:
                    Operators.ExpFloat32(ir.GetSymbolTable(), instruction);
                    break;
                case EXPF64:
                    Operators.ExpFloat64(ir.GetSymbolTable(), instruction);
                    break;
                case MULI32:
                    Operators.MulInt32(ir.GetSymbolTable(), instruction);
                    break;
                case MULI64:
                    Operators.MulInt64(ir.GetSymbolTable(), instruction);
                    break;
                case MULF32:
                    Operators.MulFloat32(ir.GetSymbolTable(), instruction);
                    break;
                case MULF64:
                    Operators.MulFloat64(ir.GetSymbolTable(), instruction);
                    break;
                case IDIV:
                    Operators.Idiv(ir.GetSymbolTable(), instruction);
                    break;
                case FDIV:
                    Operators.Fdiv(ir.GetSymbolTable(), instruction);
                    break;
                case ADDI32:
                    Operators.AddInt32(ir.GetSymbolTable(), instruction);
                    break;
                case ADDI64:
                    Operators.AddInt64(ir.GetSymbolTable(), instruction);
                    break;
                case ADDF32:
                    Operators.AddFloat32(ir.GetSymbolTable(), instruction);
                    break;
                case ADDF64:
                    Operators.AddFloat64(ir.GetSymbolTable(), instruction);
                    break;
                case SUBI32:
                    Operators.SubInt32(ir.GetSymbolTable(), instruction);
                    break;
                case SUBI64:
                    Operators.SubInt64(ir.GetSymbolTable(), instruction);
                    break;
                case SUBF32:
                    Operators.SubFloat32(ir.GetSymbolTable(), instruction);
                    break;
                case SUBF64:
                    Operators.SubFloat64(ir.GetSymbolTable(), instruction);
                    break;
                case MOD:
                    Operators.Mod(ir.GetSymbolTable(), instruction);
                    break;
                case EQI32:
                    Operators.EqInt32(ir.GetSymbolTable(), instruction);
                    break;
                case EQI64:
                    Operators.EqInt64(ir.GetSymbolTable(), instruction);
                    break;
                case EQF32:
                    Operators.EqFloat32(ir.GetSymbolTable(), instruction);
                    break;
                case EQF64:
                    Operators.EqFloat64(ir.GetSymbolTable(), instruction);
                    break;
                case EQSTR:
                    Operators.EqStr(ir.GetSymbolTable(), instruction);
                    break;
                case NEI32:
                    Operators.NeInt32(ir.GetSymbolTable(), instruction);
                    break;
                case NEI64:
                    Operators.NeInt64(ir.GetSymbolTable(), instruction);
                    break;
                case NEF32:
                    Operators.NeFloat32(ir.GetSymbolTable(), instruction);
                    break;
                case NEF64:
                    Operators.NeFloat64(ir.GetSymbolTable(), instruction);
                    break;
                case NESTR:
                    Operators.NeStr(ir.GetSymbolTable(), instruction);
                    break;
                case LTI32:
                    Operators.LtInt32(ir.GetSymbolTable(), instruction);
                    break;
                case LTI64:
                    Operators.LtInt64(ir.GetSymbolTable(), instruction);
                    break;
                case LTF32:
                    Operators.LtFloat32(ir.GetSymbolTable(), instruction);
                    break;
                case LTF64:
                    Operators.LtFloat64(ir.GetSymbolTable(), instruction);
                    break;
                case LTSTR:
                    Operators.LtStr(ir.GetSymbolTable(), instruction);
                    break;
                case LEI32:
                    Operators.LeInt32(ir.GetSymbolTable(), instruction);
                    break;
                case LEI64:
                    Operators.LeInt64(ir.GetSymbolTable(), instruction);
                    break;
                case LEF32:
                    Operators.LeFloat32(ir.GetSymbolTable(), instruction);
                    break;
                case LEF64:
                    Operators.LeFloat64(ir.GetSymbolTable(), instruction);
                    break;
                case LESTR:
                    Operators.LeStr(ir.GetSymbolTable(), instruction);
                    break;
                case GTI32:
                    Operators.GtInt32(ir.GetSymbolTable(), instruction);
                    break;
                case GTI64:
                    Operators.GtInt64(ir.GetSymbolTable(), instruction);
                    break;
                case GTF32:
                    Operators.GtFloat32(ir.GetSymbolTable(), instruction);
                    break;
                case GTF64:
                    Operators.GtFloat64(ir.GetSymbolTable(), instruction);
                    break;
                case GTSTR:
                    Operators.GtStr(ir.GetSymbolTable(), instruction);
                    break;
                case GEI32:
                    Operators.GeInt32(ir.GetSymbolTable(), instruction);
                    break;
                case GEI64:
                    Operators.GeInt64(ir.GetSymbolTable(), instruction);
                    break;
                case GEF32:
                    Operators.GeFloat32(ir.GetSymbolTable(), instruction);
                    break;
                case GEF64:
                    Operators.GeFloat64(ir.GetSymbolTable(), instruction);
                    break;
                case GESTR:
                    Operators.GeStr(ir.GetSymbolTable(), instruction);
                    break;
                case NOT:
                    Operators.UnaryNot(ir.GetSymbolTable(), instruction);
                    break;
                case AND:
                    Operators.And(ir.GetSymbolTable(), instruction);
                    break;
                case OR:
                    Operators.Or(ir.GetSymbolTable(), instruction);
                    break;
                case XOR:
                    Operators.Xor(ir.GetSymbolTable(), instruction);
                    break;
                case EQV:
                    Operators.Eqv(ir.GetSymbolTable(), instruction);
                    break;
                case IMP:
                    Operators.Imp(ir.GetSymbolTable(), instruction);
                    break;
                case LEFTSHIFT:
                    Operators.LeftShift(ir.GetSymbolTable(), instruction);
                    break;
                case RIGHTSHIFT:
                    Operators.RightShift(ir.GetSymbolTable(), instruction);
                    break;
                case END:
                    return true;
                case ABS:
                    Functions.Abs(ir.GetSymbolTable(), instruction);
                    break;
                case ASC:
                    Functions.Asc(ir.GetSymbolTable(), instruction);
                    break;
                case SIN:
                    Functions.Sin(ir.GetSymbolTable(), instruction);
                    break;
                case COS:
                    Functions.Cos(ir.GetSymbolTable(), instruction);
                    break;
                case TAN:
                    Functions.Tan(ir.GetSymbolTable(), instruction);
                    break;
                case ASIN:
                    Functions.Asin(ir.GetSymbolTable(), instruction);
                    break;
                case ACOS:
                    Functions.Acos(ir.GetSymbolTable(), instruction);
                    break;
                case ATN:
                    Functions.Atn(ir.GetSymbolTable(), instruction);
                    break;
                case SINH:
                    Functions.Sinh(ir.GetSymbolTable(), instruction);
                    break;
                case COSH:
                    Functions.Cosh(ir.GetSymbolTable(), instruction);
                    break;
                case TANH:
                    Functions.Tanh(ir.GetSymbolTable(), instruction);
                    break;
                case SQR:
                    Functions.Sqr(ir.GetSymbolTable(), instruction);
                    break;
                case LOG:
                    Functions.Log(ir.GetSymbolTable(), instruction);
                    break;
                case LOG10:
                    Functions.Log10(ir.GetSymbolTable(), instruction);
                    break;
                case LOG2:
                    Functions.Log2(ir.GetSymbolTable(), instruction);
                    break;
                case EEXP:
                    Functions.Exp(ir.GetSymbolTable(), instruction);
                    break;
                case TORAD:
                    Functions.ToRad(ir.GetSymbolTable(), instruction);
                    break;
                case TODEG:
                    Functions.ToDeg(ir.GetSymbolTable(), instruction);
                    break;
                case FLOOR:
                    Functions.Floor(ir.GetSymbolTable(), instruction);
                    break;
                case CEIL:
                    Functions.Ceil(ir.GetSymbolTable(), instruction);
                    break;
                case ROUND:
                    Functions.Round(ir.GetSymbolTable(), instruction);
                    break;
                case E:
                    Functions.E(ir.GetSymbolTable(), instruction);
                    break;
                case PI:
                    Functions.Pi(ir.GetSymbolTable(), instruction);
                    break;
                case MIN:
                    Functions.Min(ir.GetSymbolTable(), instruction);
                    break;
                case MAX:
                    Functions.Max(ir.GetSymbolTable(), instruction);
                    break;
                case ARRAYFILL:
                    ArraysUtil.Arrayfill(ir.GetSymbolTable(), instruction);
                    break;
                case ARRAYCOPY:
                    ArraysUtil.ArrayCopy(ir.GetSymbolTable(), instruction);
                    break;
                case ARRAY1DMIN:
                    ArraysUtil.Array1dMin(ir.GetSymbolTable(), instruction);
                    break;
                case ARRAY1DMAX:
                    ArraysUtil.Array1dMax(ir.GetSymbolTable(), instruction);
                    break;
                case ARRAY1DMEAN:
                    ArraysUtil.Array1dMean(ir.GetSymbolTable(), instruction);
                    break;
                case ARRAY1DSUM:
                    ArraysUtil.Array1dSum(ir.GetSymbolTable(), instruction);
                    break;
                case ARRAY1DSTD:
                    ArraysUtil.Array1dStddev(ir.GetSymbolTable(), instruction);
                    break;
                case ARRAY1DMEDIAN:
                    ArraysUtil.Array1dMedian(ir.GetSymbolTable(), instruction);
                    break;
                case ARRAY1DPCT:
                    ArraysUtil.Array1dPercentile(ir.GetSymbolTable(), instruction);
                    break;
                case ARRAY1DSORT:
                    ArraysUtil.Array1dSort(ir.GetSymbolTable(), instruction);
                    break;
                case ARRAY1DBINSEARCH:
                    ArraysUtil.Array1dBinSearch(ir.GetSymbolTable(), instruction);
                    break;
                case ARRAY2DSHIFTVER:
                    ArraysUtil.Array2dShiftVertical(ir.GetSymbolTable(), instruction);
                    break;
                case ARRAY2DSHIFTHOR:
                    ArraysUtil.Array2dShiftHorizontal(ir.GetSymbolTable(), instruction);
                    break;
                case ARRAY1DCOPY:
                {
                    if (@params.Count != 2)
                    {
                        throw new PuffinBasicInternalError("Expected 2 params, but found: " + @params);
                    }

                    ArraysUtil.Array1DCopy(ir.GetSymbolTable(), @params[0], @params[1], instruction);
                    @params.Clear();
                }

                    break;
                case ARRAY2DFINDROW:
                {
                    if (@params.Count != 2)
                    {
                        throw new PuffinBasicInternalError("Expected 2 params, but found: " + @params);
                    }

                    ArraysUtil.Array2dFindRow(ir.GetSymbolTable(), @params, instruction);
                    @params.Clear();
                }

                    break;
                case ARRAY2DFINDCOLUMN:
                {
                    if (@params.Count != 2)
                    {
                        throw new PuffinBasicInternalError("Expected 2 params, but found: " + @params);
                    }

                    ArraysUtil.Array2dFindColumn(ir.GetSymbolTable(), @params, instruction);
                    @params.Clear();
                }

                    break;
                case CINT:
                    Functions.Cint(ir.GetSymbolTable(), instruction);
                    break;
                case CLNG:
                    Functions.Clng(ir.GetSymbolTable(), instruction);
                    break;
                case CSNG:
                    Functions.Csng(ir.GetSymbolTable(), instruction);
                    break;
                case CDBL:
                    Functions.Cdbl(ir.GetSymbolTable(), instruction);
                    break;
                case CHRDLR:
                    Functions.Chrdlr(ir.GetSymbolTable(), instruction);
                    break;
                case CVI:
                    Functions.Cvi(ir.GetSymbolTable(), instruction);
                    break;
                case CVL:
                    Functions.Cvl(ir.GetSymbolTable(), instruction);
                    break;
                case CVS:
                    Functions.Cvs(ir.GetSymbolTable(), instruction);
                    break;
                case CVD:
                    Functions.Cvd(ir.GetSymbolTable(), instruction);
                    break;
                case MKIDLR:
                    Functions.Mkidlr(ir.GetSymbolTable(), instruction);
                    break;
                case MKLDLR:
                    Functions.Mkldlr(ir.GetSymbolTable(), instruction);
                    break;
                case MKSDLR:
                    Functions.Mksdlr(ir.GetSymbolTable(), instruction);
                    break;
                case MKDDLR:
                    Functions.Mkddlr(ir.GetSymbolTable(), instruction);
                    break;
                case SPACEDLR:
                    Functions.Spacedlr(ir.GetSymbolTable(), instruction);
                    break;
                case STRDLR:
                    Functions.Strdlr(ir.GetSymbolTable(), instruction);
                    break;
                case VAL:
                    Functions.Val(ir.GetSymbolTable(), instruction);
                    break;
                case INT:
                    Functions.Fnint(ir.GetSymbolTable(), instruction);
                    break;
                case FIX:
                    Functions.Fix(ir.GetSymbolTable(), instruction);
                    break;
                case LEN:
                    Functions.Len(ir.GetSymbolTable(), instruction);
                    break;
                case HEXDLR:
                    Functions.Hexdlr(ir.GetSymbolTable(), instruction);
                    break;
                case OCTDLR:
                    Functions.Octdlr(ir.GetSymbolTable(), instruction);
                    break;
                case LEFTDLR:
                    Functions.Leftdlr(ir.GetSymbolTable(), instruction);
                    break;
                case RIGHTDLR:
                    Functions.Rightdlr(ir.GetSymbolTable(), instruction);
                    break;
                case SPLITDLR:
                    Functions.Splitdlr(ir.GetSymbolTable(), instruction);
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

                    Functions.Instr(ir.GetSymbolTable(), @params[0], instruction);
                    @params.Clear();
                }

                    break;
                case MIDDLR:
                {
                    if (@params.Count != 1)
                    {
                        throw new PuffinBasicInternalError("Expected 1 param, but found: " + @params);
                    }

                    Functions.Middlr(ir.GetSymbolTable(), @params[0], instruction);
                    @params.Clear();
                }

                    break;
                case MIDDLR_STMT:
                {
                    if (@params.Count != 1)
                    {
                        throw new PuffinBasicInternalError("Expected 1 param, but found: " + @params);
                    }

                    Statements.Middlr(ir.GetSymbolTable(), @params[0], instruction);
                    @params.Clear();
                }

                    break;
                case OPEN:
                {
                    if (@params.Count != 2)
                    {
                        throw new PuffinBasicInternalError("Expected 2 params, but found: " + @params);
                    }

                    Statements.Open(files, ir.GetSymbolTable(), @params[0], @params[1], instruction);
                    @params.Clear();
                }

                    break;
                case CLOSE_ALL:
                    Statements.CloseAll(files);
                    break;
                case CLOSE:
                    Statements.Dispose(files, ir.GetSymbolTable(), instruction);
                    break;
                case FIELD:
                {
                    Statements.Field(files, ir.GetSymbolTable(), @params, instruction);
                    @params.Clear();
                }

                    break;
                case HSB2RGB:
                {
                    if (@params.Count != 1)
                    {
                        throw new PuffinBasicInternalError("Expected 1 param, but found: " + @params);
                    }

                    GraphicsRuntime.Hsb2rgb(ir.GetSymbolTable(), @params[0], instruction);
                    @params.Clear();
                }

                    break;
                case PUTF:
                    Statements.Putf(files, ir.GetSymbolTable(), instruction);
                    break;
                case GETF:
                    Statements.Getf(files, ir.GetSymbolTable(), instruction);
                    break;
                case LOC:
                    Functions.Loc(files, ir.GetSymbolTable(), instruction);
                    break;
                case LOF:
                    Functions.Lof(files, ir.GetSymbolTable(), instruction);
                    break;
                case EOF:
                    Functions.Eof(files, ir.GetSymbolTable(), instruction);
                    break;
                case RND:
                    Functions.Rnd(random, ir.GetSymbolTable(), instruction);
                    break;
                case RANDOMIZE:
                    Statements.Randomize(random, ir.GetSymbolTable(), instruction);
                    break;
                case RANDOMIZE_TIMER:
                    Statements.RandomizeTimer(random);
                    break;
                case SGN:
                    Functions.Sgn(ir.GetSymbolTable(), instruction);
                    break;
                case LSET:
                    Statements.Lset(ir.GetSymbolTable(), instruction);
                    break;
                case RSET:
                    Statements.Rset(ir.GetSymbolTable(), instruction);
                    break;
                case TIMER:
                    Functions.Timer(ir.GetSymbolTable(), instruction);
                    break;
                case TIMERMILLIS:
                    Functions.TimerMillis(ir.GetSymbolTable(), instruction);
                    break;
                case STRINGDLR:
                    Functions.Stringdlr(ir.GetSymbolTable(), instruction);
                    break;
                case SWAP:
                    Statements.Swap(ir.GetSymbolTable(), instruction);
                    break;
                case CONCAT:
                    Operators.Concat(ir.GetSymbolTable(), instruction);
                    break;
                case INPUTDLR:
                    Functions.Inputdlr(files, ir.GetSymbolTable(), instruction);
                    break;
                case INPUT:
                {
                    Statements.Input(files, ir.GetSymbolTable(), @params, instruction);
                    @params.Clear();
                }

                    break;
                case LINE_INPUT:
                {
                    if (@params.Count != 1)
                    {
                        throw new PuffinBasicInternalError("Expected 1 param, but found: " + @params);
                    }

                    Statements.Lineinput(files, ir.GetSymbolTable(), @params[0], instruction);
                    @params.Clear();
                }

                    break;
                case WRITE:
                    Statements.Write(printBuffer, ir.GetSymbolTable(), instruction);
                    break;
                case DATA:
                    break;
                case RESTORE:
                    readData.Restore();
                    break;
                case READ:
                    Statements.Read(readData, ir.GetSymbolTable(), instruction);
                    break;
                case ENVIRONDLR:
                    Functions.Environdlr(env, ir.GetSymbolTable(), instruction);
                    break;
                case SLEEP:
                    Statements.Sleep(ir.GetSymbolTable(), instruction);
                    break;
                case SCREEN:
                {
                    if (@params.Count != 3)
                    {
                        throw new PuffinBasicInternalError("Expected 1 param, but found: " + @params);
                    }

                    GraphicsRuntime.Screen(graphicsState, ir.GetSymbolTable(), @params, instruction);
                    @params.Clear();
                }

                    break;
                case REPAINT:
                    GraphicsRuntime.Repaint(graphicsState);
                    break;
                case CIRCLE:
                {
                    if (@params.Count != 3)
                    {
                        throw new PuffinBasicInternalError("Expected 3 params, but found: " + @params);
                    }

                    GraphicsRuntime.Circle(graphicsState, ir.GetSymbolTable(), @params, instruction);
                    @params.Clear();
                }

                    break;
                case LINE:
                {
                    if (@params.Count != 2)
                    {
                        throw new PuffinBasicInternalError("Expected 2 params, but found: " + @params);
                    }

                    GraphicsRuntime.Line(graphicsState, ir.GetSymbolTable(), @params, instruction);
                    @params.Clear();
                }

                    break;
                case COLOR:
                {
                    if (@params.Count != 1)
                    {
                        throw new PuffinBasicInternalError("Expected 1 params, but found: " + @params);
                    }

                    GraphicsRuntime.Color(graphicsState, ir.GetSymbolTable(), @params[0], instruction);
                    @params.Clear();
                }

                    break;
                case PAINT:
                {
                    if (@params.Count != 2)
                    {
                        throw new PuffinBasicInternalError("Expected 2 params, but found: " + @params);
                    }

                    GraphicsRuntime.Paint(graphicsState, ir.GetSymbolTable(), @params, instruction);
                    @params.Clear();
                }

                    break;
                case PSET:
                {
                    if (@params.Count != 2)
                    {
                        throw new PuffinBasicInternalError("Expected 2 params, but found: " + @params);
                    }

                    GraphicsRuntime.Pset(graphicsState, ir.GetSymbolTable(), @params, instruction);
                    @params.Clear();
                }

                    break;
                case GGET:
                {
                    if (@params.Count != 2)
                    {
                        throw new PuffinBasicInternalError("Expected 2 params, but found: " + @params);
                    }

                    GraphicsRuntime.Get(graphicsState, ir.GetSymbolTable(), @params, instruction);
                    @params.Clear();
                }

                    break;
                case GPUT:
                {
                    if (@params.Count != 2)
                    {
                        throw new PuffinBasicInternalError("Expected 2 params, but found: " + @params);
                    }

                    GraphicsRuntime.Put(graphicsState, ir.GetSymbolTable(), @params[0], @params[1], instruction);
                    @params.Clear();
                }

                    break;
                case BUFFERCOPYHOR:
                {
                    if (@params.Count != 1)
                    {
                        throw new PuffinBasicInternalError("Expected 1 param, but found: " + @params);
                    }

                    GraphicsRuntime.BufferCopyHor(graphicsState, ir.GetSymbolTable(), @params[0], instruction);
                    @params.Clear();
                }

                    break;
                case FONT:
                {
                    if (@params.Count != 1)
                    {
                        throw new PuffinBasicInternalError("Expected 1 param, but found: " + @params);
                    }

                    GraphicsRuntime.Font(graphicsState, ir.GetSymbolTable(), @params[0], instruction);
                    @params.Clear();
                }

                    break;
                case DRAWSTR:
                {
                    if (@params.Count != 1)
                    {
                        throw new PuffinBasicInternalError("Expected 1 param, but found: " + @params);
                    }

                    GraphicsRuntime.Drawstr(graphicsState, ir.GetSymbolTable(), @params[0], instruction);
                    @params.Clear();
                }

                    break;
                case LOADIMG:
                    GraphicsRuntime.Loadimg(ir.GetSymbolTable(), instruction);
                    break;
                case SAVEIMG:
                    GraphicsRuntime.Saveimg(ir.GetSymbolTable(), instruction);
                    break;
                case DRAW:
                    GraphicsRuntime.Draw(graphicsState, ir.GetSymbolTable(), instruction);
                    break;
                case INKEYDLR:
                    GraphicsRuntime.Inkeydlr(graphicsState, ir.GetSymbolTable(), instruction);
                    break;
                case CLS:
                    GraphicsRuntime.Cls(graphicsState);
                    break;
                case BEEP:
                    GraphicsRuntime.Beep();
                    break;
                case LOADWAV:
                    GraphicsRuntime.Loadwav(soundState, ir.GetSymbolTable(), instruction);
                    break;
                case PLAYWAV:
                    GraphicsRuntime.Playwav(soundState, ir.GetSymbolTable(), instruction);
                    break;
                case STOPWAV:
                    GraphicsRuntime.Stopwav(soundState, ir.GetSymbolTable(), instruction);
                    break;
                case LOOPWAV:
                    GraphicsRuntime.Loopwav(soundState, ir.GetSymbolTable(), instruction);
                    break;
                case MOUSEMOVEDX:
                    GraphicsRuntime.MouseMovedX(graphicsState, ir.GetSymbolTable(), instruction);
                    break;
                case MOUSEMOVEDY:
                    GraphicsRuntime.MouseMovedY(graphicsState, ir.GetSymbolTable(), instruction);
                    break;
                case MOUSEDRAGGEDX:
                    GraphicsRuntime.MouseDraggedX(graphicsState, ir.GetSymbolTable(), instruction);
                    break;
                case MOUSEDRAGGEDY:
                    GraphicsRuntime.MouseDraggedY(graphicsState, ir.GetSymbolTable(), instruction);
                    break;
                case MOUSEBUTTONCLICKED:
                    GraphicsRuntime.MouseButtonClicked(graphicsState, ir.GetSymbolTable(), instruction);
                    break;
                case MOUSEBUTTONPRESSED:
                    GraphicsRuntime.MouseButtonPressed(graphicsState, ir.GetSymbolTable(), instruction);
                    break;
                case MOUSEBUTTONRELEASED:
                    GraphicsRuntime.MouseButtonReleased(graphicsState, ir.GetSymbolTable(), instruction);
                    break;
                case ISKEYPRESSED:
                    GraphicsRuntime.IsKeyPressed(graphicsState, ir.GetSymbolTable(), instruction);
                    break;
            }

            this.programCounter = nextProgramCounter;
            return false;
        }
    }
}

