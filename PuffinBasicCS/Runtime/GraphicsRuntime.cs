//using Org.Apache.Commons.Io;
namespace PuffinBasicCS.Runtime
{
    using PuffinBasicCS.Domain;

    using static PuffinBasicCS.Parser.PuffinBasicIR;

    //using Javax.Imageio;
    //using Javax.Swing;
    //using Java.Awt;
    //using Java.Awt.Event;
    //using Java.Awt.Geom;
    //using Java.Awt.Image;
    //using Java.Io;
    //using Java.Util;
    //using Java.Util.Regex;
    public abstract class GraphicsRuntime
    {
        public interface IGraphicsRuntimeImplementation
        {
            abstract void Beep();
            abstract void BufferCopyHor(PuffinBasicSymbolTable symbolTable, Instruction instr0, Instruction instruction);
            abstract void Circle(PuffinBasicSymbolTable symbolTable, IList<Instruction> instr0, Instruction instruction);
            abstract void Cls();
            abstract void Color(PuffinBasicSymbolTable symbolTable, Instruction instr0, Instruction instruction);
            abstract void Draw(PuffinBasicSymbolTable symbolTable, Instruction instruction);
            abstract void Drawstr(PuffinBasicSymbolTable symbolTable, Instruction instr0, Instruction instruction);
            abstract void End();
            abstract void Font(PuffinBasicSymbolTable symbolTable, Instruction instr0, Instruction instruction);
            abstract void Get(PuffinBasicSymbolTable symbolTable, IList<Instruction> instr0, Instruction instruction);
            abstract void Hsb2rgb(PuffinBasicSymbolTable symbolTable, Instruction instr0, Instruction instruction);
            abstract void Inkeydlr(PuffinBasicSymbolTable symbolTable, Instruction instruction);
            abstract void IsKeyPressed(PuffinBasicSymbolTable symbolTable, Instruction instruction);
            abstract void Line(PuffinBasicSymbolTable symbolTable, IList<Instruction> instr0, Instruction instruction);
            abstract void Loadimg(PuffinBasicSymbolTable symbolTable, Instruction instruction);
            abstract void Loadwav(PuffinBasicSymbolTable symbolTable, Instruction instruction);
            abstract void Loopwav(PuffinBasicSymbolTable symbolTable, Instruction instruction);
            abstract void MouseButtonClicked(PuffinBasicSymbolTable symbolTable, Instruction instruction);
            abstract void MouseButtonPressed(PuffinBasicSymbolTable symbolTable, Instruction instruction);
            abstract void MouseButtonReleased(PuffinBasicSymbolTable symbolTable, Instruction instruction);
            abstract void MouseDraggedX(PuffinBasicSymbolTable symbolTable, Instruction instruction);
            abstract void MouseDraggedY(PuffinBasicSymbolTable symbolTable, Instruction instruction);
            abstract void MouseMovedX(PuffinBasicSymbolTable symbolTable, Instruction instruction);
            abstract void MouseMovedY(PuffinBasicSymbolTable symbolTable, Instruction instruction);
            abstract void Paint(PuffinBasicSymbolTable symbolTable, IList<Instruction> instr0, Instruction instruction);
            abstract void Playwav(PuffinBasicSymbolTable symbolTable, Instruction instruction);
            abstract void Pset(PuffinBasicSymbolTable symbolTable, IList<Instruction> instr0, Instruction instruction);
            abstract void Put(PuffinBasicSymbolTable symbolTable, Instruction instr0, Instruction instr1, Instruction instruction);
            abstract void Repaint();
            abstract void Saveimg(PuffinBasicSymbolTable symbolTable, Instruction instruction);
            abstract void Screen(PuffinBasicSymbolTable symbolTable, IList<Instruction> instr0, Instruction instruction);
            abstract void Stopwav(PuffinBasicSymbolTable symbolTable, Instruction instruction);
        }

        public static IGraphicsRuntimeImplementation Implementation { get; set; }

        public static void Beep() { Console.Beep(); }
        public static void BufferCopyHor(PuffinBasicSymbolTable symbolTable, Instruction instr0, Instruction instruction)
        {
            Implementation.BufferCopyHor(symbolTable, instr0, instruction);
        }
        public static void Circle(PuffinBasicSymbolTable symbolTable, IList<Instruction> instr0, Instruction instruction)
        {
            Implementation.Circle(symbolTable, instr0, instruction);
        }
        public static void Cls()
        {
            Implementation.Cls();
        }
        public static void Color(PuffinBasicSymbolTable symbolTable, Instruction instr0, Instruction instruction)
        {
            Implementation.Color(symbolTable, instr0, instruction);
        }
        public static void Draw(PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            Implementation.Draw(symbolTable, instruction);
        }
        public static void Drawstr(PuffinBasicSymbolTable symbolTable, Instruction instr0, Instruction instruction)
        {
            Implementation.Drawstr(symbolTable, instr0, instruction);
        }
        public static void End()
        {
            Implementation.End();
        }
        public static void Font(PuffinBasicSymbolTable symbolTable, Instruction instr0, Instruction instruction)
        {
            Implementation.Font(symbolTable, instr0, instruction);
        }
        public static void Get(PuffinBasicSymbolTable symbolTable, IList<Instruction> instr0, Instruction instruction)
        {
            Implementation.Get(symbolTable, instr0, instruction);
        }
        public static void Hsb2rgb(PuffinBasicSymbolTable symbolTable, Instruction instr0, Instruction instruction)
        {
            Implementation.Hsb2rgb(symbolTable, instr0, instruction);
        }
        public static void Inkeydlr(PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            Implementation.Inkeydlr(symbolTable, instruction);
        }
        public static void IsKeyPressed(PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            Implementation.IsKeyPressed(symbolTable, instruction);
        }
        public static void Line(PuffinBasicSymbolTable symbolTable, IList<Instruction> instr0, Instruction instruction)
        {
            Implementation.Line(symbolTable, instr0, instruction);
        }
        public static void Loadimg(PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            Implementation.Loadimg(symbolTable, instruction);
        }
        public static void Loadwav(PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            Implementation.Loadwav(symbolTable, instruction);
        }
        public static void Loopwav(PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            Implementation.Loopwav(symbolTable, instruction);
        }
        public static void MouseButtonClicked(PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            Implementation.MouseButtonClicked(symbolTable, instruction);
        }
        public static void MouseButtonPressed(PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            Implementation.MouseButtonPressed(symbolTable, instruction);
        }
        public static void MouseButtonReleased(PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            Implementation.MouseButtonReleased(symbolTable, instruction);
        }
        public static void MouseDraggedX(PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            Implementation.MouseDraggedX(symbolTable, instruction);
        }
        public static void MouseDraggedY(PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            Implementation.MouseDraggedY(symbolTable, instruction);
        }
        public static void MouseMovedX(PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            Implementation.MouseMovedX(symbolTable, instruction);
        }
        public static void MouseMovedY(PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            Implementation.MouseMovedY(symbolTable, instruction);
        }
        public static void Paint(PuffinBasicSymbolTable symbolTable, IList<Instruction> instr0, Instruction instruction)
        {
            Implementation.Paint(symbolTable, instr0, instruction);
        }
        public static void Playwav(PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            Implementation.Playwav(symbolTable, instruction);
        }
        public static void Pset(PuffinBasicSymbolTable symbolTable, IList<Instruction> instr0, Instruction instruction)
        {
            Implementation.Pset(symbolTable, instr0, instruction);
        }
        public static void Put(PuffinBasicSymbolTable symbolTable, Instruction instr0, Instruction instr1, Instruction instruction)
        {
            Implementation.Put(symbolTable, instr0, instr1, instruction);
        }
        public static void Repaint()
        {
            Implementation.Repaint();
        }
        public static void Saveimg(PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            Implementation.Saveimg(symbolTable, instruction);
        }
        public static void Screen(PuffinBasicSymbolTable symbolTable, IList<Instruction> instr0, Instruction instruction)
        {
            Implementation.Screen(symbolTable, instr0, instruction);
        }
        public static void Stopwav(PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            Implementation.Stopwav(symbolTable, instruction);
        }
    }
}