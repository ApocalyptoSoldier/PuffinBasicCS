//using Org.Antlr.V4.Runtime.Misc;
//using Org.Jetbrains.Annotations;
namespace Org.Puffinbasic.Parser
{
    using Antlr4.Runtime.Misc;

    using Org.Puffinbasic.Domain;
    //using Java.Util;
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Reflection;

    public static class OpCodeRepr
    {
        private static readonly Dictionary<PuffinBasicIR.OpCode, string> opCodeToRepr = new Dictionary<PuffinBasicIR.OpCode, string>();

        static OpCodeRepr()
        {
            var enumMembers = typeof(PuffinBasicIR.OpCode).GetMembers(BindingFlags.Public | BindingFlags.Static);

            foreach (var m in enumMembers)
            {
                if (m is FieldInfo field)
                {
                    PuffinBasicIR.OpCode opCode = (PuffinBasicIR.OpCode)(field.GetValue(null));
                    opCodeToRepr[opCode] = field.GetCustomAttribute<DescriptionAttribute>().Description;
                }
            }
        }

        public static string Repr(this PuffinBasicIR.OpCode opCode) => opCodeToRepr[opCode];
    }

    public class PuffinBasicIR
    {
		// Find 	// [\w\_]+([^\)]+\))
		// Replace 	[Description(\1)]
        public enum OpCode
        {
            [Description("comment")]
            COMMENT,
            [Description("var")]
            VARIABLE,
            [Description("val")]
            VALUE,
            [Description("varref")]
            VARREF,
            [Description("structLValue")]
            STRUCT_LVALUE,
            [Description("memberFuncCall")]
            MEMBER_FUNC_CALL,
            [Description("dim")]
            DIM,
            [Description("allocArray")]
            ALLOCARRAY,
            [Description("reAllocArray")]
            REALLOCARRAY,
            [Description("createAndSetInstance")]
            CREATE_INSTANCE,
            [Description("structMemberRef")]
            STRUCT_MEMBER_REF,
            [Description("a=")]
            ASSIGN,
            [Description("c=")]
            COPY,
            [Description("p=")]
            PARAM_COPY,
            [Description("u-")]
            UNARY_MINUS,
            [Description("<<")]
            LEFTSHIFT,
            [Description(">>")]
            RIGHTSHIFT,
            [Description("?")]
            PRINT,
            [Description("?f")]
            PRINTUSING,
            [Description("flush")]
            FLUSH,
            [Description("resetArrIdx")]
            RESET_ARRAY_IDX,
            [Description("setArrIdx")]
            SET_ARRAY_IDX,
            [Description("goto")]
            GOTO_LINENUM,
            [Description("gotoLabel")]
            GOTO_LABEL,
            [Description("gotoLabelIf")]
            GOTO_LABEL_IF,
            [Description("gotoCaller")]
            GOTO_CALLER,
            [Description("label")]
            LABEL,
            [Description("pushRtScope")]
            PUSH_RT_SCOPE,
            [Description("popRtScope")]
            POP_RT_SCOPE,
            [Description("end")]
            END,
            [Description("ret")]
            RETURN,
            [Description("pushRetLabel")]
            PUSH_RETLABEL,
            [Description("swap")]
            SWAP,
            [Description("i32^")]
            EXPI32,
            [Description("i64^")]
            EXPI64,
            [Description("f32^")]
            EXPF32,
            [Description("f64^")]
            EXPF64,
            [Description("i32*")]
            MULI32,
            [Description("i64*")]
            MULI64,
            [Description("f32*")]
            MULF32,
            [Description("f64*")]
            MULF64,
            [Description("\\")]
            IDIV,
            [Description("/")]
            FDIV,
            [Description("i32+")]
            ADDI32,
            [Description("i64+")]
            ADDI64,
            [Description("f32+")]
            ADDF32,
            [Description("f64+")]
            ADDF64,
            [Description("concat")]
            CONCAT,
            [Description("i32-")]
            SUBI32,
            [Description("i64-")]
            SUBI64,
            [Description("f32-")]
            SUBF32,
            [Description("f64-")]
            SUBF64,
            [Description("mod")]
            MOD,
            [Description("i32=")]
            EQI32,
            [Description("i64=")]
            EQI64,
            [Description("f32=")]
            EQF32,
            [Description("f64=")]
            EQF64,
            [Description("str=")]
            EQSTR,
            [Description("i32<>")]
            NEI32,
            [Description("i64<>")]
            NEI64,
            [Description("f32<>")]
            NEF32,
            [Description("f64<>")]
            NEF64,
            [Description("str<>")]
            NESTR,
            [Description("i32<")]
            LTI32,
            [Description("i64<")]
            LTI64,
            [Description("f32<")]
            LTF32,
            [Description("f64<")]
            LTF64,
            [Description("str<")]
            LTSTR,
            [Description("i32<=")]
            LEI32,
            [Description("i64<=")]
            LEI64,
            [Description("f32<=")]
            LEF32,
            [Description("f64<=")]
            LEF64,
            [Description("str<=")]
            LESTR,
            [Description("i32>")]
            GTI32,
            [Description("i64>")]
            GTI64,
            [Description("f32>")]
            GTF32,
            [Description("f64>")]
            GTF64,
            [Description("str>")]
            GTSTR,
            [Description("i32>=")]
            GEI32,
            [Description("i64>=")]
            GEI64,
            [Description("f32>=")]
            GEF32,
            [Description("f64>=")]
            GEF64,
            [Description("str>=")]
            GESTR,
            [Description("not")]
            NOT,
            [Description("and")]
            AND,
            [Description("or")]
            OR,
            [Description("xor")]
            XOR,
            [Description("eqv")]
            EQV,
            [Description("imp")]
            IMP,
            [Description("abs")]
            ABS,
            [Description("asc")]
            ASC,
            [Description("sin")]
            SIN,
            [Description("cos")]
            COS,
            [Description("tan")]
            TAN,
            [Description("asin")]
            ASIN,
            [Description("acos")]
            ACOS,
            [Description("atn")]
            ATN,
            [Description("sinh")]
            SINH,
            [Description("cosh")]
            COSH,
            [Description("tanh")]
            TANH,
            [Description("sqr")]
            SQR,
            [Description("exp")]
            EEXP,
            [Description("cint")]
            CINT,
            [Description("clng")]
            CLNG,
            [Description("csng")]
            CSNG,
            [Description("cdbl")]
            CDBL,
            [Description("chr$")]
            CHRDLR,
            [Description("cvi")]
            CVI,
            [Description("cvl")]
            CVL,
            [Description("cvs")]
            CVS,
            [Description("cvd")]
            CVD,
            [Description("mki$")]
            MKIDLR,
            [Description("mkl$")]
            MKLDLR,
            [Description("mks$")]
            MKSDLR,
            [Description("mkd$")]
            MKDDLR,
            [Description("space$")]
            SPACEDLR,
            [Description("str$")]
            STRDLR,
            [Description("val")]
            VAL,
            [Description("int")]
            INT,
            [Description("fix")]
            FIX,
            [Description("log")]
            LOG,
            [Description("log10")]
            LOG10,
            [Description("log2")]
            LOG2,
            [Description("torad")]
            TORAD,
            [Description("todeg")]
            TODEG,
            [Description("floor")]
            FLOOR,
            [Description("ceil")]
            CEIL,
            [Description("round")]
            ROUND,
            [Description("e")]
            E,
            [Description("pi")]
            PI,
            [Description("min")]
            MIN,
            [Description("max")]
            MAX,
            [Description("arrayfill")]
            ARRAYFILL,
            [Description("array1dmin")]
            ARRAY1DMIN,
            [Description("array1dmax")]
            ARRAY1DMAX,
            [Description("array1dmean")]
            ARRAY1DMEAN,
            [Description("array1dsum")]
            ARRAY1DSUM,
            [Description("array1dstd")]
            ARRAY1DSTD,
            [Description("array1dmedian")]
            ARRAY1DMEDIAN,
            [Description("array1dpct")]
            ARRAY1DPCT,
            [Description("array1dsort")]
            ARRAY1DSORT,
            [Description("array1dbinsearch")]
            ARRAY1DBINSEARCH,
            [Description("array1dcopy")]
            ARRAY1DCOPY,
            [Description("arraycopy")]
            ARRAYCOPY,
            [Description("array2dshifthor")]
            ARRAY2DSHIFTHOR,
            [Description("array2dshiftver")]
            ARRAY2DSHIFTVER,
            [Description("array2dFindRow")]
            ARRAY2DFINDROW,
            [Description("array2sFindColumn")]
            ARRAY2DFINDCOLUMN,
            [Description("len")]
            LEN,
            [Description("hex$")]
            HEXDLR,
            [Description("oct$")]
            OCTDLR,
            [Description("left$")]
            LEFTDLR,
            [Description("right$")]
            RIGHTDLR,
            [Description("instr")]
            INSTR,
            [Description("mid$")]
            MIDDLR,
            [Description("mid$_stmt")]
            MIDDLR_STMT,
            [Description("split$")]
            SPLITDLR,
            [Description("rnd")]
            RND,
            [Description("sgn")]
            SGN,
            [Description("timer")]
            TIMER,
            [Description("timerMillis")]
            TIMERMILLIS,
            [Description("string$")]
            STRINGDLR,
            [Description("open")]
            OPEN,
            [Description("close_all")]
            CLOSE_ALL,
            [Description("close")]
            CLOSE,
            [Description("field")]
            FIELD,
            [Description("putf")]
            PUTF,
            [Description("getf")]
            GETF,
            [Description("loc")]
            LOC,
            [Description("lof")]
            LOF,
            [Description("eof")]
            EOF,
            [Description("randomize")]
            RANDOMIZE,
            [Description("randomizeTimer")]
            RANDOMIZE_TIMER,
            [Description("lset")]
            LSET,
            [Description("rset")]
            RSET,
            [Description("input$")]
            INPUTDLR,
            [Description("input")]
            INPUT,
            [Description("lineInput")]
            LINE_INPUT,
            [Description("write")]
            WRITE,
            [Description("restore")]
            RESTORE,
            [Description("data")]
            DATA,
            [Description("read")]
            READ,
            [Description("environ$")]
            ENVIRONDLR,
            [Description("screen")]
            SCREEN,
            [Description("repaint")]
            REPAINT,
            [Description("circle")]
            CIRCLE,
            [Description("sleep")]
            SLEEP,
            [Description("line")]
            LINE,
            [Description("color")]
            COLOR,
            [Description("inkey$")]
            INKEYDLR,
            [Description("paint")]
            PAINT,
            [Description("pset")]
            PSET,
            [Description("gput")]
            GPUT,
            [Description("gget")]
            GGET,
            [Description("buffercopyhor")]
            BUFFERCOPYHOR,
            [Description("loadimg")]
            LOADIMG,
            [Description("saveimg")]
            SAVEIMG,
            [Description("drawstr")]
            DRAWSTR,
            [Description("draw")]
            DRAW,
            [Description("font")]
            FONT,
            [Description("cls")]
            CLS,
            [Description("beep")]
            BEEP,
            [Description("arrayref")]
            ARRAYREF,
            [Description("hsb2rgb")]
            HSB2RGB,
            [Description("loadwav")]
            LOADWAV,
            [Description("playwav")]
            PLAYWAV,
            [Description("stopwav")]
            STOPWAV,
            [Description("loopwav")]
            LOOPWAV,
            [Description("param2")]
            PARAM2,
            [Description("param1")]
            PARAM1,
            [Description("mousemovedx")]
            MOUSEMOVEDX,
            [Description("mousemovedy")]
            MOUSEMOVEDY,
            [Description("mousedraggedx")]
            MOUSEDRAGGEDX,
            [Description("mousedraggedy")]
            MOUSEDRAGGEDY,
            [Description("mousebuttonclicked")]
            MOUSEBUTTONCLICKED,
            [Description("mousebuttonpressed")]
            MOUSEBUTTONPRESSED,
            [Description("mousebuttonreleased")]
            MOUSEBUTTONRELEASED,
            [Description("iskeypressed")]
            ISKEYPRESSED 

            // --------------------
            // TODO enum body members
            // public final String repr;
            // OpCode(String repr) {
            //     this.repr = repr;
            // }
            // --------------------
        }

        private readonly PuffinBasicSymbolTable symbolTable;
        private readonly IList<Instruction> instructions;
        public PuffinBasicIR(PuffinBasicSymbolTable symbolTable)
        {
            this.symbolTable = symbolTable;
            this.instructions = new List<Instruction>();
        }

        public virtual string GetCodeStreamFor(Instruction instruction)
        {
            try
            {
                return instruction.GetInputRef().sourceFile.GetSourceCodeStream().GetText(new Interval(instruction.inputRef.inputStartIndex, instruction.inputRef.inputStopIndex));
            }
            catch (Exception e)
            {
                return "Internal error: " + e.Message;
            }
        }

        public virtual IList<Instruction> GetInstructions()
        {
            return new List<Instruction>(instructions);
        }

        public virtual Instruction AddInstruction(PuffinBasicSourceFile sourceFile, int linenum, int startIndex, int stopIndex, OpCode opCode, int op1, int op2, int result)
        {
            var instruction = new Instruction(new InputRef(sourceFile, linenum, startIndex, stopIndex), opCode, op1, op2, result);
            instructions.Add(instruction);
            return instruction;
        }

        public virtual PuffinBasicSymbolTable GetSymbolTable()
        {
            return symbolTable;
        }

        public sealed class InputRef
        {
            public readonly PuffinBasicSourceFile sourceFile;
            public readonly int lineNumber;
            public readonly int inputStartIndex;
            public readonly int inputStopIndex;
            public InputRef(PuffinBasicSourceFile sourceFile, int lineNumber, int inputStartIndex, int inputStopIndex)
            {
                this.sourceFile = sourceFile;
                this.lineNumber = lineNumber;
                this.inputStartIndex = inputStartIndex;
                this.inputStopIndex = inputStopIndex;
            }

            public new bool Equals(object o)
            {
                if (this == o)
                    return true;
                if (o == null || GetType() != o.GetType())
                    return false;
                InputRef other = (InputRef)o;
                return sourceFile.Equals(other.sourceFile) && lineNumber == other.lineNumber && inputStartIndex == other.inputStartIndex && inputStopIndex == other.inputStopIndex;
            }

            public new int GetHashCode()
            {
                int hash = 17;
                hash = hash * 23 + sourceFile.GetHashCode();
                hash = hash * 23 + lineNumber.GetHashCode();
                hash = hash * 23 + inputStartIndex.GetHashCode();
                hash = hash * 23 + inputStopIndex.GetHashCode();
                return hash;
            }

            public override string ToString()
            {
                return "[" + sourceFile.GetRelativePath() + ":" + lineNumber + "(" + inputStartIndex + "-" + inputStopIndex + ")]";
            }
        }

        public sealed class Instruction
        {
            public readonly InputRef inputRef;
            public readonly OpCode opCode;
            public int op1;
            public int op2;
            public readonly int result;
            public Instruction(InputRef inputRef, OpCode opCode, int op1, int op2, int result)
            {
                this.inputRef = inputRef;
                this.opCode = opCode;
                this.op1 = op1;
                this.op2 = op2;
                this.result = result;
            }

            public InputRef GetInputRef()
            {
                return inputRef;
            }

            public void PatchOp1(int op1)
            {
                this.op1 = op1;
            }

            public void PatchOp2(int op2)
            {
                this.op2 = op2;
            }

            public override string ToString()
            {
                return $"[{inputRef.sourceFile.GetRelativePath()}:{inputRef.lineNumber}]\t{opCode.Repr()}\t{op1} {op2} {result}";
                //return String.Format("[%s:%4d]\t%4s\t%4s %4s %4s", inputRef.sourceFile.GetRelativePath(), inputRef.lineNumber, opCode.Repr(), op1, op2, result);
            }
        }
    }
}

