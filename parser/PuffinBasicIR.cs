//using Org.Antlr.V4.Runtime.Misc;
//using Org.Jetbrains.Annotations;
using Antlr4.Runtime.Misc;

using Org.Puffinbasic.Domain;
//using Java.Util;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;

namespace Org.Puffinbasic.Parser
{
    public static class OpCodeEnumExtensions
    {
        public static string? Repr(this PuffinBasicIR.OpCode opCode)
        {
            switch (opCode)
            {
                case PuffinBasicIR.OpCode.COMMENT:
                    return "comment";
                case PuffinBasicIR.OpCode.VARIABLE:
                    return "var";
                case PuffinBasicIR.OpCode.VALUE:
                    return "val";
                case PuffinBasicIR.OpCode.VARREF:
                    return "varref";
                case PuffinBasicIR.OpCode.STRUCT_LVALUE:
                    return "structLValue";
                case PuffinBasicIR.OpCode.MEMBER_FUNC_CALL:
                    return "memberFuncCall";
                case PuffinBasicIR.OpCode.DIM:
                    return "dim";
                case PuffinBasicIR.OpCode.ALLOCARRAY:
                    return "allocArray";
                case PuffinBasicIR.OpCode.REALLOCARRAY:
                    return "reAllocArray";
                case PuffinBasicIR.OpCode.CREATE_INSTANCE:
                    return "createAndSetInstance";
                case PuffinBasicIR.OpCode.STRUCT_MEMBER_REF:
                    return "structMemberRef";
                case PuffinBasicIR.OpCode.ASSIGN:
                    return "a=";
                case PuffinBasicIR.OpCode.COPY:
                    return "c=";
                case PuffinBasicIR.OpCode.PARAM_COPY:
                    return "p=";
                case PuffinBasicIR.OpCode.UNARY_MINUS:
                    return "u-";
                case PuffinBasicIR.OpCode.LEFTSHIFT:
                    return "<<";
                case PuffinBasicIR.OpCode.RIGHTSHIFT:
                    return ">>";
                case PuffinBasicIR.OpCode.PRINT:
                    return "?";
                case PuffinBasicIR.OpCode.PRINTUSING:
                    return "?f";
                case PuffinBasicIR.OpCode.FLUSH:
                    return "flush";
                case PuffinBasicIR.OpCode.RESET_ARRAY_IDX:
                    return "resetArrIdx";
                case PuffinBasicIR.OpCode.SET_ARRAY_IDX:
                    return "setArrIdx";
                case PuffinBasicIR.OpCode.GOTO_LINENUM:
                    return "goto";
                case PuffinBasicIR.OpCode.GOTO_LABEL:
                    return "gotoLabel";
                case PuffinBasicIR.OpCode.GOTO_LABEL_IF:
                    return "gotoLabelIf";
                case PuffinBasicIR.OpCode.GOTO_CALLER:
                    return "gotoCaller";
                case PuffinBasicIR.OpCode.LABEL:
                    return "label";
                case PuffinBasicIR.OpCode.PUSH_RT_SCOPE:
                    return "pushRtScope";
                case PuffinBasicIR.OpCode.POP_RT_SCOPE:
                    return "popRtScope";
                case PuffinBasicIR.OpCode.END:
                    return "end";
                case PuffinBasicIR.OpCode.RETURN:
                    return "ret";
                case PuffinBasicIR.OpCode.PUSH_RETLABEL:
                    return "pushRetLabel";
                case PuffinBasicIR.OpCode.SWAP:
                    return "swap";
                case PuffinBasicIR.OpCode.EXPI32:
                    return "i32^";
                case PuffinBasicIR.OpCode.EXPI64:
                    return "i64^";
                case PuffinBasicIR.OpCode.EXPF32:
                    return "f32^";
                case PuffinBasicIR.OpCode.EXPF64:
                    return "f64^";
                case PuffinBasicIR.OpCode.MULI32:
                    return "i32*";
                case PuffinBasicIR.OpCode.MULI64:
                    return "i64*";
                case PuffinBasicIR.OpCode.MULF32:
                    return "f32*";
                case PuffinBasicIR.OpCode.MULF64:
                    return "f64*";
                case PuffinBasicIR.OpCode.IDIV:
                    return "\\";
                case PuffinBasicIR.OpCode.FDIV:
                    return "/";
                case PuffinBasicIR.OpCode.ADDI32:
                    return "i32+";
                case PuffinBasicIR.OpCode.ADDI64:
                    return "i64+";
                case PuffinBasicIR.OpCode.ADDF32:
                    return "f32+";
                case PuffinBasicIR.OpCode.ADDF64:
                    return "f64+";
                case PuffinBasicIR.OpCode.CONCAT:
                    return "concat";
                case PuffinBasicIR.OpCode.SUBI32:
                    return "i32-";
                case PuffinBasicIR.OpCode.SUBI64:
                    return "i64-";
                case PuffinBasicIR.OpCode.SUBF32:
                    return "f32-";
                case PuffinBasicIR.OpCode.SUBF64:
                    return "f64-";
                case PuffinBasicIR.OpCode.MOD:
                    return "mod";
                case PuffinBasicIR.OpCode.EQI32:
                    return "i32=";
                case PuffinBasicIR.OpCode.EQI64:
                    return "i64=";
                case PuffinBasicIR.OpCode.EQF32:
                    return "f32=";
                case PuffinBasicIR.OpCode.EQF64:
                    return "f64=";
                case PuffinBasicIR.OpCode.EQSTR:
                    return "str=";
                case PuffinBasicIR.OpCode.NEI32:
                    return "i32<>";
                case PuffinBasicIR.OpCode.NEI64:
                    return "i64<>";
                case PuffinBasicIR.OpCode.NEF32:
                    return "f32<>";
                case PuffinBasicIR.OpCode.NEF64:
                    return "f64<>";
                case PuffinBasicIR.OpCode.NESTR:
                    return "str<>";
                case PuffinBasicIR.OpCode.LTI32:
                    return "i32<";
                case PuffinBasicIR.OpCode.LTI64:
                    return "i64<";
                case PuffinBasicIR.OpCode.LTF32:
                    return "f32<";
                case PuffinBasicIR.OpCode.LTF64:
                    return "f64<";
                case PuffinBasicIR.OpCode.LTSTR:
                    return "str<";
                case PuffinBasicIR.OpCode.LEI32:
                    return "i32<=";
                case PuffinBasicIR.OpCode.LEI64:
                    return "i64<=";
                case PuffinBasicIR.OpCode.LEF32:
                    return "f32<=";
                case PuffinBasicIR.OpCode.LEF64:
                    return "f64<=";
                case PuffinBasicIR.OpCode.LESTR:
                    return "str<=";
                case PuffinBasicIR.OpCode.GTI32:
                    return "i32>";
                case PuffinBasicIR.OpCode.GTI64:
                    return "i64>";
                case PuffinBasicIR.OpCode.GTF32:
                    return "f32>";
                case PuffinBasicIR.OpCode.GTF64:
                    return "f64>";
                case PuffinBasicIR.OpCode.GTSTR:
                    return "str>";
                case PuffinBasicIR.OpCode.GEI32:
                    return "i32>=";
                case PuffinBasicIR.OpCode.GEI64:
                    return "i64>=";
                case PuffinBasicIR.OpCode.GEF32:
                    return "f32>=";
                case PuffinBasicIR.OpCode.GEF64:
                    return "f64>=";
                case PuffinBasicIR.OpCode.GESTR:
                    return "str>=";
                case PuffinBasicIR.OpCode.NOT:
                    return "not";
                case PuffinBasicIR.OpCode.AND:
                    return "and";
                case PuffinBasicIR.OpCode.OR:
                    return "or";
                case PuffinBasicIR.OpCode.XOR:
                    return "xor";
                case PuffinBasicIR.OpCode.EQV:
                    return "eqv";
                case PuffinBasicIR.OpCode.IMP:
                    return "imp";
                case PuffinBasicIR.OpCode.ABS:
                    return "abs";
                case PuffinBasicIR.OpCode.ASC:
                    return "asc";
                case PuffinBasicIR.OpCode.SIN:
                    return "sin";
                case PuffinBasicIR.OpCode.COS:
                    return "cos";
                case PuffinBasicIR.OpCode.TAN:
                    return "tan";
                case PuffinBasicIR.OpCode.ASIN:
                    return "asin";
                case PuffinBasicIR.OpCode.ACOS:
                    return "acos";
                case PuffinBasicIR.OpCode.ATN:
                    return "atn";
                case PuffinBasicIR.OpCode.SINH:
                    return "sinh";
                case PuffinBasicIR.OpCode.COSH:
                    return "cosh";
                case PuffinBasicIR.OpCode.TANH:
                    return "tanh";
                case PuffinBasicIR.OpCode.SQR:
                    return "sqr";
                case PuffinBasicIR.OpCode.EEXP:
                    return "exp";
                case PuffinBasicIR.OpCode.CINT:
                    return "cint";
                case PuffinBasicIR.OpCode.CLNG:
                    return "clng";
                case PuffinBasicIR.OpCode.CSNG:
                    return "csng";
                case PuffinBasicIR.OpCode.CDBL:
                    return "cdbl";
                case PuffinBasicIR.OpCode.CHRDLR:
                    return "chr$";
                case PuffinBasicIR.OpCode.CVI:
                    return "cvi";
                case PuffinBasicIR.OpCode.CVL:
                    return "cvl";
                case PuffinBasicIR.OpCode.CVS:
                    return "cvs";
                case PuffinBasicIR.OpCode.CVD:
                    return "cvd";
                case PuffinBasicIR.OpCode.MKIDLR:
                    return "mki$";
                case PuffinBasicIR.OpCode.MKLDLR:
                    return "mkl$";
                case PuffinBasicIR.OpCode.MKSDLR:
                    return "mks$";
                case PuffinBasicIR.OpCode.MKDDLR:
                    return "mkd$";
                case PuffinBasicIR.OpCode.SPACEDLR:
                    return "space$";
                case PuffinBasicIR.OpCode.STRDLR:
                    return "str$";
                case PuffinBasicIR.OpCode.VAL:
                    return "val";
                case PuffinBasicIR.OpCode.INT:
                    return "int";
                case PuffinBasicIR.OpCode.FIX:
                    return "fix";
                case PuffinBasicIR.OpCode.LOG:
                    return "log";
                case PuffinBasicIR.OpCode.LOG10:
                    return "log10";
                case PuffinBasicIR.OpCode.LOG2:
                    return "log2";
                case PuffinBasicIR.OpCode.TORAD:
                    return "torad";
                case PuffinBasicIR.OpCode.TODEG:
                    return "todeg";
                case PuffinBasicIR.OpCode.FLOOR:
                    return "floor";
                case PuffinBasicIR.OpCode.CEIL:
                    return "ceil";
                case PuffinBasicIR.OpCode.ROUND:
                    return "round";
                case PuffinBasicIR.OpCode.E:
                    return "e";
                case PuffinBasicIR.OpCode.PI:
                    return "pi";
                case PuffinBasicIR.OpCode.MIN:
                    return "min";
                case PuffinBasicIR.OpCode.MAX:
                    return "max";
                case PuffinBasicIR.OpCode.ARRAYFILL:
                    return "arrayfill";
                case PuffinBasicIR.OpCode.ARRAY1DMIN:
                    return "array1dmin";
                case PuffinBasicIR.OpCode.ARRAY1DMAX:
                    return "array1dmax";
                case PuffinBasicIR.OpCode.ARRAY1DMEAN:
                    return "array1dmean";
                case PuffinBasicIR.OpCode.ARRAY1DSUM:
                    return "array1dsum";
                case PuffinBasicIR.OpCode.ARRAY1DSTD:
                    return "array1dstd";
                case PuffinBasicIR.OpCode.ARRAY1DMEDIAN:
                    return "array1dmedian";
                case PuffinBasicIR.OpCode.ARRAY1DPCT:
                    return "array1dpct";
                case PuffinBasicIR.OpCode.ARRAY1DSORT:
                    return "array1dsort";
                case PuffinBasicIR.OpCode.ARRAY1DBINSEARCH:
                    return "array1dbinsearch";
                case PuffinBasicIR.OpCode.ARRAY1DCOPY:
                    return "array1dcopy";
                case PuffinBasicIR.OpCode.ARRAYCOPY:
                    return "arraycopy";
                case PuffinBasicIR.OpCode.ARRAY2DSHIFTHOR:
                    return "array2dshifthor";
                case PuffinBasicIR.OpCode.ARRAY2DSHIFTVER:
                    return "array2dshiftver";
                case PuffinBasicIR.OpCode.ARRAY2DFINDROW:
                    return "array2dFindRow";
                case PuffinBasicIR.OpCode.ARRAY2DFINDCOLUMN:
                    return "array2sFindColumn";
                case PuffinBasicIR.OpCode.LEN:
                    return "len";
                case PuffinBasicIR.OpCode.HEXDLR:
                    return "hex$";
                case PuffinBasicIR.OpCode.OCTDLR:
                    return "oct$";
                case PuffinBasicIR.OpCode.LEFTDLR:
                    return "left$";
                case PuffinBasicIR.OpCode.RIGHTDLR:
                    return "right$";
                case PuffinBasicIR.OpCode.INSTR:
                    return "instr";
                case PuffinBasicIR.OpCode.MIDDLR:
                    return "mid$";
                case PuffinBasicIR.OpCode.MIDDLR_STMT:
                    return "mid";
                case PuffinBasicIR.OpCode.SPLITDLR:
                    return "split$";
                case PuffinBasicIR.OpCode.RND:
                    return "rnd";
                case PuffinBasicIR.OpCode.SGN:
                    return "sgn";
                case PuffinBasicIR.OpCode.TIMER:
                    return "timer";
                case PuffinBasicIR.OpCode.TIMERMILLIS:
                    return "timerMillis";
                case PuffinBasicIR.OpCode.STRINGDLR:
                    return "string$";
                case PuffinBasicIR.OpCode.OPEN:
                    return "open";
                case PuffinBasicIR.OpCode.CLOSE_ALL:
                    return "close_all";
                case PuffinBasicIR.OpCode.CLOSE:
                    return "close";
                case PuffinBasicIR.OpCode.FIELD:
                    return "field";
                case PuffinBasicIR.OpCode.PUTF:
                    return "putf";
                case PuffinBasicIR.OpCode.GETF:
                    return "getf";
                case PuffinBasicIR.OpCode.LOC:
                    return "loc";
                case PuffinBasicIR.OpCode.LOF:
                    return "lof";
                case PuffinBasicIR.OpCode.EOF:
                    return "eof";
                case PuffinBasicIR.OpCode.RANDOMIZE:
                    return "randomize";
                case PuffinBasicIR.OpCode.RANDOMIZE_TIMER:
                    return "randomizeTimer";
                case PuffinBasicIR.OpCode.LSET:
                    return "lset";
                case PuffinBasicIR.OpCode.RSET:
                    return "rset";
                case PuffinBasicIR.OpCode.INPUTDLR:
                    return "input$";
                case PuffinBasicIR.OpCode.INPUT:
                    return "input";
                case PuffinBasicIR.OpCode.LINE_INPUT:
                    return "lineInput";
                case PuffinBasicIR.OpCode.WRITE:
                    return "write";
                case PuffinBasicIR.OpCode.RESTORE:
                    return "restore";
                case PuffinBasicIR.OpCode.DATA:
                    return "data";
                case PuffinBasicIR.OpCode.READ:
                    return "read";
                case PuffinBasicIR.OpCode.ENVIRONDLR:
                    return "environ$";
                case PuffinBasicIR.OpCode.SCREEN:
                    return "screen";
                case PuffinBasicIR.OpCode.REPAINT:
                    return "repaint";
                case PuffinBasicIR.OpCode.CIRCLE:
                    return "circle";
                case PuffinBasicIR.OpCode.SLEEP:
                    return "sleep";
                case PuffinBasicIR.OpCode.LINE:
                    return "line";
                case PuffinBasicIR.OpCode.COLOR:
                    return "color";
                case PuffinBasicIR.OpCode.INKEYDLR:
                    return "inkey$";
                case PuffinBasicIR.OpCode.PAINT:
                    return "paint";
                case PuffinBasicIR.OpCode.PSET:
                    return "pset";
                case PuffinBasicIR.OpCode.GPUT:
                    return "gput";
                case PuffinBasicIR.OpCode.GGET:
                    return "gget";
                case PuffinBasicIR.OpCode.BUFFERCOPYHOR:
                    return "buffercopyhor";
                case PuffinBasicIR.OpCode.LOADIMG:
                    return "loadimg";
                case PuffinBasicIR.OpCode.SAVEIMG:
                    return "saveimg";
                case PuffinBasicIR.OpCode.DRAWSTR:
                    return "drawstr";
                case PuffinBasicIR.OpCode.DRAW:
                    return "draw";
                case PuffinBasicIR.OpCode.FONT:
                    return "font";
                case PuffinBasicIR.OpCode.CLS:
                    return "cls";
                case PuffinBasicIR.OpCode.BEEP:
                    return "beep";
                case PuffinBasicIR.OpCode.ARRAYREF:
                    return "arrayref";
                case PuffinBasicIR.OpCode.HSB2RGB:
                    return "hsb2rgb";
                case PuffinBasicIR.OpCode.LOADWAV:
                    return "loadwav";
                case PuffinBasicIR.OpCode.PLAYWAV:
                    return "playwav";
                case PuffinBasicIR.OpCode.STOPWAV:
                    return "stopwav";
                case PuffinBasicIR.OpCode.LOOPWAV:
                    return "loopwav";
                case PuffinBasicIR.OpCode.PARAM2:
                    return "param2";
                case PuffinBasicIR.OpCode.PARAM1:
                    return "param1";
                case PuffinBasicIR.OpCode.MOUSEMOVEDX:
                    return "mousemovedx";
                case PuffinBasicIR.OpCode.MOUSEMOVEDY:
                    return "mousemovedy";
                case PuffinBasicIR.OpCode.MOUSEDRAGGEDX:
                    return "mousedraggedx";
                case PuffinBasicIR.OpCode.MOUSEDRAGGEDY:
                    return "mousedraggedy";
                case PuffinBasicIR.OpCode.MOUSEBUTTONCLICKED:
                    return "mousebuttonclicked";
                case PuffinBasicIR.OpCode.MOUSEBUTTONPRESSED:
                    return "mousebuttonpressed";
                case PuffinBasicIR.OpCode.MOUSEBUTTONRELEASED:
                    return "mousebuttonreleased";
                case PuffinBasicIR.OpCode.ISKEYPRESSED:
                    return "iskeypressed";
                default:
                    throw new NotImplementedException();
            }
        }
    }

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

            public new string ToString()
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

            public new string ToString()
            {
                return String.Format("[%s:%4d]\t%4s\t%4s %4s %4s", inputRef.sourceFile.GetRelativePath(), inputRef.lineNumber, opCode.Repr(), op1, op2, result);
            }
        }
    }
}

