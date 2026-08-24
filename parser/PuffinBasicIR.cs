using Org.Antlr.V4.Runtime.Misc;
using Org.Jetbrains.Annotations;
using Org.Puffinbasic.Domain;
using Java.Util;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;

namespace Org.Puffinbasic.Parser
{
    public class PuffinBasicIR
    {
        public enum OpCode
        {
            // COMMENT("comment")
            COMMENT,
            // VARIABLE("var")
            VARIABLE,
            // VALUE("val")
            VALUE,
            // VARREF("varref")
            VARREF,
            // STRUCT_LVALUE("structLValue")
            STRUCT_LVALUE,
            // MEMBER_FUNC_CALL("memberFuncCall")
            MEMBER_FUNC_CALL,
            // DIM("dim")
            DIM,
            // ALLOCARRAY("allocArray")
            ALLOCARRAY,
            // REALLOCARRAY("reAllocArray")
            REALLOCARRAY,
            // CREATE_INSTANCE("createAndSetInstance")
            CREATE_INSTANCE,
            // STRUCT_MEMBER_REF("structMemberRef")
            STRUCT_MEMBER_REF,
            // ASSIGN("a=")
            ASSIGN,
            // COPY("c=")
            COPY,
            // PARAM_COPY("p=")
            PARAM_COPY,
            // UNARY_MINUS("u-")
            UNARY_MINUS,
            // LEFTSHIFT("<<")
            LEFTSHIFT,
            // RIGHTSHIFT(">>")
            RIGHTSHIFT,
            // PRINT("?")
            PRINT,
            // PRINTUSING("?f")
            PRINTUSING,
            // FLUSH("flush")
            FLUSH,
            // RESET_ARRAY_IDX("resetArrIdx")
            RESET_ARRAY_IDX,
            // SET_ARRAY_IDX("setArrIdx")
            SET_ARRAY_IDX,
            // GOTO_LINENUM("goto")
            GOTO_LINENUM,
            // GOTO_LABEL("gotoLabel")
            GOTO_LABEL,
            // GOTO_LABEL_IF("gotoLabelIf")
            GOTO_LABEL_IF,
            // GOTO_CALLER("gotoCaller")
            GOTO_CALLER,
            // LABEL("label")
            LABEL,
            // PUSH_RT_SCOPE("pushRtScope")
            PUSH_RT_SCOPE,
            // POP_RT_SCOPE("popRtScope")
            POP_RT_SCOPE,
            // END("end")
            END,
            // RETURN("ret")
            RETURN,
            // PUSH_RETLABEL("pushRetLabel")
            PUSH_RETLABEL,
            // SWAP("swap")
            SWAP,
            // EXPI32("i32^")
            EXPI32,
            // EXPI64("i64^")
            EXPI64,
            // EXPF32("f32^")
            EXPF32,
            // EXPF64("f64^")
            EXPF64,
            // MULI32("i32*")
            MULI32,
            // MULI64("i64*")
            MULI64,
            // MULF32("f32*")
            MULF32,
            // MULF64("f64*")
            MULF64,
            // IDIV("\\")
            IDIV,
            // FDIV("/")
            FDIV,
            // ADDI32("i32+")
            ADDI32,
            // ADDI64("i64+")
            ADDI64,
            // ADDF32("f32+")
            ADDF32,
            // ADDF64("f64+")
            ADDF64,
            // CONCAT("concat")
            CONCAT,
            // SUBI32("i32-")
            SUBI32,
            // SUBI64("i64-")
            SUBI64,
            // SUBF32("f32-")
            SUBF32,
            // SUBF64("f64-")
            SUBF64,
            // MOD("mod")
            MOD,
            // EQI32("i32=")
            EQI32,
            // EQI64("i64=")
            EQI64,
            // EQF32("f32=")
            EQF32,
            // EQF64("f64=")
            EQF64,
            // EQSTR("str=")
            EQSTR,
            // NEI32("i32<>")
            NEI32,
            // NEI64("i64<>")
            NEI64,
            // NEF32("f32<>")
            NEF32,
            // NEF64("f64<>")
            NEF64,
            // NESTR("str<>")
            NESTR,
            // LTI32("i32<")
            LTI32,
            // LTI64("i64<")
            LTI64,
            // LTF32("f32<")
            LTF32,
            // LTF64("f64<")
            LTF64,
            // LTSTR("str<")
            LTSTR,
            // LEI32("i32<=")
            LEI32,
            // LEI64("i64<=")
            LEI64,
            // LEF32("f32<=")
            LEF32,
            // LEF64("f64<=")
            LEF64,
            // LESTR("str<=")
            LESTR,
            // GTI32("i32>")
            GTI32,
            // GTI64("i64>")
            GTI64,
            // GTF32("f32>")
            GTF32,
            // GTF64("f64>")
            GTF64,
            // GTSTR("str>")
            GTSTR,
            // GEI32("i32>=")
            GEI32,
            // GEI64("i64>=")
            GEI64,
            // GEF32("f32>=")
            GEF32,
            // GEF64("f64>=")
            GEF64,
            // GESTR("str>=")
            GESTR,
            // NOT("not")
            NOT,
            // AND("and")
            AND,
            // OR("or")
            OR,
            // XOR("xor")
            XOR,
            // EQV("eqv")
            EQV,
            // IMP("imp")
            IMP,
            // ABS("abs")
            ABS,
            // ASC("asc")
            ASC,
            // SIN("sin")
            SIN,
            // COS("cos")
            COS,
            // TAN("tan")
            TAN,
            // ASIN("asin")
            ASIN,
            // ACOS("acos")
            ACOS,
            // ATN("atn")
            ATN,
            // SINH("sinh")
            SINH,
            // COSH("cosh")
            COSH,
            // TANH("tanh")
            TANH,
            // SQR("sqr")
            SQR,
            // EEXP("exp")
            EEXP,
            // CINT("cint")
            CINT,
            // CLNG("clng")
            CLNG,
            // CSNG("csng")
            CSNG,
            // CDBL("cdbl")
            CDBL,
            // CHRDLR("chr$")
            CHRDLR,
            // CVI("cvi")
            CVI,
            // CVL("cvl")
            CVL,
            // CVS("cvs")
            CVS,
            // CVD("cvd")
            CVD,
            // MKIDLR("mki$")
            MKIDLR,
            // MKLDLR("mkl$")
            MKLDLR,
            // MKSDLR("mks$")
            MKSDLR,
            // MKDDLR("mkd$")
            MKDDLR,
            // SPACEDLR("space$")
            SPACEDLR,
            // STRDLR("str$")
            STRDLR,
            // VAL("val")
            VAL,
            // INT("int")
            INT,
            // FIX("fix")
            FIX,
            // LOG("log")
            LOG,
            // LOG10("log10")
            LOG10,
            // LOG2("log2")
            LOG2,
            // TORAD("torad")
            TORAD,
            // TODEG("todeg")
            TODEG,
            // FLOOR("floor")
            FLOOR,
            // CEIL("ceil")
            CEIL,
            // ROUND("round")
            ROUND,
            // E("e")
            E,
            // PI("pi")
            PI,
            // MIN("min")
            MIN,
            // MAX("max")
            MAX,
            // ARRAYFILL("arrayfill")
            ARRAYFILL,
            // ARRAY1DMIN("array1dmin")
            ARRAY1DMIN,
            // ARRAY1DMAX("array1dmax")
            ARRAY1DMAX,
            // ARRAY1DMEAN("array1dmean")
            ARRAY1DMEAN,
            // ARRAY1DSUM("array1dsum")
            ARRAY1DSUM,
            // ARRAY1DSTD("array1dstd")
            ARRAY1DSTD,
            // ARRAY1DMEDIAN("array1dmedian")
            ARRAY1DMEDIAN,
            // ARRAY1DPCT("array1dpct")
            ARRAY1DPCT,
            // ARRAY1DSORT("array1dsort")
            ARRAY1DSORT,
            // ARRAY1DBINSEARCH("array1dbinsearch")
            ARRAY1DBINSEARCH,
            // ARRAY1DCOPY("array1dcopy")
            ARRAY1DCOPY,
            // ARRAYCOPY("arraycopy")
            ARRAYCOPY,
            // ARRAY2DSHIFTHOR("array2dshifthor")
            ARRAY2DSHIFTHOR,
            // ARRAY2DSHIFTVER("array2dshiftver")
            ARRAY2DSHIFTVER,
            // ARRAY2DFINDROW("array2dFindRow")
            ARRAY2DFINDROW,
            // ARRAY2DFINDCOLUMN("array2sFindColumn")
            ARRAY2DFINDCOLUMN,
            // LEN("len")
            LEN,
            // HEXDLR("hex$")
            HEXDLR,
            // OCTDLR("oct$")
            OCTDLR,
            // LEFTDLR("left$")
            LEFTDLR,
            // RIGHTDLR("right$")
            RIGHTDLR,
            // INSTR("instr")
            INSTR,
            // MIDDLR("mid$")
            MIDDLR,
            // MIDDLR_STMT("mid$_stmt")
            MIDDLR_STMT,
            // SPLITDLR("split$")
            SPLITDLR,
            // RND("rnd")
            RND,
            // SGN("sgn")
            SGN,
            // TIMER("timer")
            TIMER,
            // TIMERMILLIS("timerMillis")
            TIMERMILLIS,
            // STRINGDLR("string$")
            STRINGDLR,
            // OPEN("open")
            OPEN,
            // CLOSE_ALL("close_all")
            CLOSE_ALL,
            // CLOSE("close")
            CLOSE,
            // FIELD("field")
            FIELD,
            // PUTF("putf")
            PUTF,
            // GETF("getf")
            GETF,
            // LOC("loc")
            LOC,
            // LOF("lof")
            LOF,
            // EOF("eof")
            EOF,
            // RANDOMIZE("randomize")
            RANDOMIZE,
            // RANDOMIZE_TIMER("randomizeTimer")
            RANDOMIZE_TIMER,
            // LSET("lset")
            LSET,
            // RSET("rset")
            RSET,
            // INPUTDLR("input$")
            INPUTDLR,
            // INPUT("input")
            INPUT,
            // LINE_INPUT("lineInput")
            LINE_INPUT,
            // WRITE("write")
            WRITE,
            // RESTORE("restore")
            RESTORE,
            // DATA("data")
            DATA,
            // READ("read")
            READ,
            // ENVIRONDLR("environ$")
            ENVIRONDLR,
            // SCREEN("screen")
            SCREEN,
            // REPAINT("repaint")
            REPAINT,
            // CIRCLE("circle")
            CIRCLE,
            // SLEEP("sleep")
            SLEEP,
            // LINE("line")
            LINE,
            // COLOR("color")
            COLOR,
            // INKEYDLR("inkey$")
            INKEYDLR,
            // PAINT("paint")
            PAINT,
            // PSET("pset")
            PSET,
            // GPUT("gput")
            GPUT,
            // GGET("gget")
            GGET,
            // BUFFERCOPYHOR("buffercopyhor")
            BUFFERCOPYHOR,
            // LOADIMG("loadimg")
            LOADIMG,
            // SAVEIMG("saveimg")
            SAVEIMG,
            // DRAWSTR("drawstr")
            DRAWSTR,
            // DRAW("draw")
            DRAW,
            // FONT("font")
            FONT,
            // CLS("cls")
            CLS,
            // BEEP("beep")
            BEEP,
            // ARRAYREF("arrayref")
            ARRAYREF,
            // HSB2RGB("hsb2rgb")
            HSB2RGB,
            // LOADWAV("loadwav")
            LOADWAV,
            // PLAYWAV("playwav")
            PLAYWAV,
            // STOPWAV("stopwav")
            STOPWAV,
            // LOOPWAV("loopwav")
            LOOPWAV,
            // PARAM2("param2")
            PARAM2,
            // PARAM1("param1")
            PARAM1,
            // MOUSEMOVEDX("mousemovedx")
            MOUSEMOVEDX,
            // MOUSEMOVEDY("mousemovedy")
            MOUSEMOVEDY,
            // MOUSEDRAGGEDX("mousedraggedx")
            MOUSEDRAGGEDX,
            // MOUSEDRAGGEDY("mousedraggedy")
            MOUSEDRAGGEDY,
            // MOUSEBUTTONCLICKED("mousebuttonclicked")
            MOUSEBUTTONCLICKED,
            // MOUSEBUTTONPRESSED("mousebuttonpressed")
            MOUSEBUTTONPRESSED,
            // MOUSEBUTTONRELEASED("mousebuttonreleased")
            MOUSEBUTTONRELEASED,
            // ISKEYPRESSED("iskeypressed")
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
            this.instructions = new List();
        }

        public virtual string GetCodeStreamFor(Instruction instruction)
        {
            try
            {
                return instruction.GetInputRef().sourceFile.GetSourceCodeStream().GetText(new Interval(instruction.inputRef.inputStartIndex, instruction.inputRef.inputStopIndex));
            }
            catch (Exception e)
            {
                return "Internal error: " + e.GetMessage();
            }
        }

        public virtual IList<Instruction> GetInstructions()
        {
            return new List(instructions);
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

            public bool Equals(object o)
            {
                if (this == o)
                    return true;
                if (o == null || GetType() != o.GetType())
                    return false;
                InputRef other = (InputRef)o;
                return sourceFile.Equals(other.sourceFile) && lineNumber == other.lineNumber && inputStartIndex == other.inputStartIndex && inputStopIndex == other.inputStopIndex;
            }

            public int GetHashCode()
            {
                return Objects.Hash(sourceFile, lineNumber, inputStartIndex, inputStopIndex);
            }

            public string ToString()
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

            public string ToString()
            {
                return String.Format("[%s:%4d]\t%4s\t%4s %4s %4s", inputRef.sourceFile.GetRelativePath(), inputRef.lineNumber, opCode.repr, op1, op2, result);
            }
        }
    }
}

