//using Org.Apache.Commons.Io;
using Org.Puffinbasic.Domain;
using Org.Puffinbasic.Error;
using static Org.Puffinbasic.Parser.PuffinBasicIR;
using static Org.Puffinbasic.Runtime.GraphicsUtil;
//using Javax.Imageio;
//using Javax.Swing;
//using Java.Awt;
//using Java.Awt.Event;
//using Java.Awt.Geom;
//using Java.Awt.Image;
//using Java.Io;
//using Java.Util;
//using Java.Util.Regex;
using static Org.Puffinbasic.Domain.PuffinBasicSymbolTable;
using static Org.Puffinbasic.Domain.STObjects.PuffinBasicAtomTypeId;
using static Org.Puffinbasic.Domain.STObjects.PuffinBasicTypeId;
using static Org.Puffinbasic.Error.PuffinBasicRuntimeError.ErrorCode;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Org.Puffinbasic.Runtime
{
    public class GraphicsRuntime
    {
        /*
        private static readonly Regex DRAW_ARG1 = new Regex("([UDLREFGHA])([BN]+)?([0-9]+)", RegexOptions.Compiled);
        private static readonly Regex DRAW_ARG2 = new Regex("M([+\\-]?[0-9]+),([+\\-]?[0-9]+)", RegexOptions.Compiled);
        public class GraphicsState
        {
            private BasicFrame frame;
            public virtual bool IsInitialized()
            {
                return frame != null;
            }

            public virtual BasicFrame GetFrame()
            {
                AssertScreenInitialized();
                return frame;
            }

            public virtual Graphics2D GetGraphics2D()
            {
                return GetFrame().GetDrawingCanvas().GetGraphics2D();
            }

            public virtual int GetImageWidth()
            {
                return GetFrame().GetDrawingCanvas().GetImageWidth();
            }

            public virtual int GetImageHeight()
            {
                return GetFrame().GetDrawingCanvas().GetImageHeight();
            }

            public virtual void SetFrame(BasicFrame frame)
            {
                AssertNewScreen();
                this.frame = frame;
            }

            private void AssertNewScreen()
            {
                if (frame != null)
                {
                    throw new PuffinBasicRuntimeError(GRAPHICS_ERROR, "Screen cannot be called again!");
                }
            }

            private void AssertScreenInitialized()
            {
                if (frame == null)
                {
                    throw new PuffinBasicRuntimeError(GRAPHICS_ERROR, "Screen has already been created!");
                }
            }
        }

        public static void Cls(GraphicsState graphicsState)
        {
            graphicsState.GetFrame().GetDrawingCanvas().Clear();
        }

        public static void Beep()
        {
            Toolkit.GetDefaultToolkit().Beep();
        }

        public static void Saveimg(PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            var path = symbolTable[instruction.op1].GetValue().GetString();
            var entry = symbolTable.GetVariable(instruction.op2);
            var variableValue = entry.GetValue();
            if (variableValue.GetNumArrayDimensions() != 2 || entry.GetType().GetAtomTypeId() != INT32)
            {
                throw new PuffinBasicRuntimeError(GRAPHICS_ERROR, "Bad Array Variable, expected Int32 2D-Array Variable: " + entry);
            }

            var dims = variableValue.GetArrayDimensions();
            BufferedImage image = new BufferedImage(dims.GetInt(0), dims.GetInt(1), BufferedImage.TYPE_3BYTE_BGR);
            image.SetRGB(0, 0, image.GetWidth(), image.GetHeight(), variableValue.GetInt32Array1D(), 0, image.GetWidth());
            var ext = FilenameUtils.GetExtension(path);
            try
            {
                ImageIO.Write(image, ext, new File(path));
            }
            catch (IOException e)
            {
                throw new PuffinBasicRuntimeError(IO_ERROR, "Failed to save image: " + path + ", error: " + e.GetMessage());
            }
        }

        public static void Loadimg(PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            var path = symbolTable[instruction.op1].GetValue().GetString();
            var entry = symbolTable.GetVariable(instruction.op2);
            var variableValue = entry.GetValue();
            if (variableValue.GetNumArrayDimensions() != 2 || entry.GetType().GetAtomTypeId() != INT32)
            {
                throw new PuffinBasicRuntimeError(GRAPHICS_ERROR, "Bad Array Variable, expected Int32 2D-Array Variable: " + entry);
            }

            BufferedImage image;
            try
            {
                image = ImageIO.Read(new File(path));
            }
            catch (IOException e)
            {
                throw new PuffinBasicRuntimeError(IO_ERROR, "Failed to load image: " + path + ", error: " + e.GetMessage());
            }

            var dims = variableValue.GetArrayDimensions();
            if (image.GetWidth() != dims.GetInt(0) || image.GetHeight() != dims.GetInt(1))
            {
                throw new PuffinBasicRuntimeError(GRAPHICS_ERROR, "Image dimensions: " + image.GetWidth() + ", " + image.GetHeight() + " doesn't match with variable dimensions: " + dims.GetInt(0) + ", " + dims.GetInt(1));
            }

            image.GetRGB(0, 0, image.GetWidth(), image.GetHeight(), variableValue.GetInt32Array1D(), 0, image.GetWidth());
        }

        public static void Screen(GraphicsState graphicsState, PuffinBasicSymbolTable symbolTable, IList<Instruction> instr0, Instruction instruction)
        {
            var i0 = instr0[0];
            var i1 = instr0[1];
            var i2 = instr0[2];
            var w = symbolTable[i0.op1].GetValue().GetInt32();
            var h = symbolTable[i0.op2].GetValue().GetInt32();
            var iw = symbolTable[i1.op1].GetValue().GetInt32();
            var ih = symbolTable[i1.op2].GetValue().GetInt32();
            var title = symbolTable[instruction.op1].GetValue().GetString();
            if (w <= 0 || h <= 0 || w > GraphicsUtil.MAX_WIDTH || h > GraphicsUtil.MAX_HEIGHT)
            {
                throw new PuffinBasicRuntimeError(GRAPHICS_ERROR, "Screen size out-of-bounds: " + w + ", " + h);
            }

            if (iw <= 0 || ih <= 0 || iw > GraphicsUtil.MAX_WIDTH || ih > GraphicsUtil.MAX_HEIGHT)
            {
                throw new PuffinBasicRuntimeError(GRAPHICS_ERROR, "Image size out-of-bounds: " + iw + ", " + ih);
            }

            var autoRepaint = symbolTable[i2.op1].GetValue().GetInt32() == -1;
            var doubleBuffer = symbolTable[i2.op2].GetValue().GetInt32() == -1;
            graphicsState.SetFrame(new BasicFrame(title, w, h, iw, ih, autoRepaint, doubleBuffer));
            EventQueue.InvokeLater(() => graphicsState.GetFrame().SetVisible(true));
        }

        public static void Hsb2rgb(PuffinBasicSymbolTable symbolTable, Instruction instr0, Instruction instruction)
        {
            var h = symbolTable[instr0.op1].GetValue().GetFloat32();
            var s = symbolTable[instr0.op2].GetValue().GetFloat32();
            var b = symbolTable[instruction.op1].GetValue().GetFloat32();
            var result = symbolTable[instruction.result].GetValue();
            result.SetInt32(Color.HSBtoRGB(h, s, b));
        }

        public static void Repaint(GraphicsState graphicsState)
        {
            graphicsState.GetFrame().GetDrawingCanvas().RenderAndRepaint();
        }

        public static void End(GraphicsState graphicsState)
        {
            SwingUtilities.InvokeLater(() =>
            {
                if (graphicsState.IsInitialized())
                {
                    var frame = graphicsState.GetFrame();
                    frame.DispatchEvent(new WindowEvent(frame, WindowEvent.WINDOW_CLOSING));
                }
            });
        }

        public static void Circle(GraphicsState graphicsState, PuffinBasicSymbolTable symbolTable, IList<Instruction> instr0, Instruction instruction)
        {
            var i0 = instr0[0];
            var i1 = instr0[1];
            var i2 = instr0[2];
            var x = symbolTable[i0.op1].GetValue().GetInt32();
            var y = symbolTable[i0.op2].GetValue().GetInt32();
            int s = i1.op1 != NULL_ID ? symbolTable[i1.op1].GetValue().GetInt32() : null;
            int e = i1.op1 != NULL_ID ? symbolTable[i1.op2].GetValue().GetInt32() : null;
            int r1 = Math.Max(0, symbolTable[instruction.op1].GetValue().GetInt32());
            int r2 = Math.Max(0, symbolTable[instruction.op2].GetValue().GetInt32());
            bool fill = i2.op1 != NULL_ID && symbolTable[i2.op1].GetValue().GetString().EqualsIgnoreCase("F");
            int w = r1 * 2;
            int h = r2 * 2;
            int sx = x - r1;
            int sy = y - r2;
            var g = graphicsState.GetGraphics2D();
            if (s == null || e == null)
            {
                if (fill)
                {
                    g.FillOval(sx, sy, w, h);
                }
                else
                {
                    g.DrawOval(sx, sy, w, h);
                }
            }
            else
            {
                if (fill)
                {
                    g.FillArc(sx, sy, w, h, s, e);
                }
                else
                {
                    g.DrawArc(sx, sy, w, h, s, e);
                }
            }
        }

        public static void Font(GraphicsState graphicsState, PuffinBasicSymbolTable symbolTable, Instruction instr0, Instruction instruction)
        {
            var style = symbolTable[instr0.op1].GetValue().GetString().ToLowerCase();
            var size = symbolTable[instr0.op2].GetValue().GetInt32();
            var name = symbolTable[instruction.op1].GetValue().GetString();
            if (name.IsEmpty() || size <= 0 || size > GraphicsUtil.MAX_WIDTH)
            {
                throw new PuffinBasicRuntimeError(GRAPHICS_ERROR, "Bad name/size: '" + name + "'/" + size);
            }

            int styleVal = 0;
            if (style.Contains("i"))
            {
                styleVal |= Font.ITALIC;
            }

            if (style.Contains("b"))
            {
                styleVal |= Font.BOLD;
            }

            graphicsState.GetGraphics2D().SetFont(new Font(name, styleVal, size));
        }

        public static void Drawstr(GraphicsState graphicsState, PuffinBasicSymbolTable symbolTable, Instruction instr0, Instruction instruction)
        {
            var x = symbolTable[instr0.op1].GetValue().GetInt32();
            var y = symbolTable[instr0.op2].GetValue().GetInt32();
            var text = symbolTable[instruction.op1].GetValue().GetString();
            graphicsState.GetGraphics2D().DrawString(text, x, y);
        }

        public static void Draw(GraphicsState graphicsState, PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            var str = symbolTable[instruction.op1].GetValue().GetString();
            if (str.IsEmpty())
            {
                throw new PuffinBasicRuntimeError(GRAPHICS_ERROR, "Found empty string in DRAW!");
            }

            var path = new GeneralPath();
            int w = graphicsState.GetImageWidth();
            int h = graphicsState.GetImageHeight();
            path.MoveTo(w / 2, h / 2);
            foreach (var i in str.Split(";"))
            {
                i = i.Trim();
                if (i.IsEmpty())
                {
                    continue;
                }

                var curr = path.GetCurrentPoint();
                if (i.CharAt(0) == 'M')
                {
                    var m = DRAW_ARG2.Match(i);

                    string x = m.Group(1);
                    string y = m.Group(2);
                    int newX = (int)curr.GetX();
                    int newY = (int)curr.GetY();
                    if (x.StartsWith("+") || x.StartsWith("-"))
                    {
                        newX += Integer.ParseInt(x);
                    }
                    else
                    {
                        newX = Integer.ParseInt(x);
                    }

                    if (y.StartsWith("+") || y.StartsWith("-"))
                    {
                        newY += Integer.ParseInt(y);
                    }
                    else
                    {
                        newY = Integer.ParseInt(y);
                    }

                    path.MoveTo(newX, newY);
                }
                else
                {
                    var m = DRAW_ARG1.Match(i);

                    char cmd = m.Group(1).CharAt(0);
                    string opts = m.Group(2) != null ? m.Group(2) : "";
                    int s = Integer.ParseInt(m.Group(3));
                    bool penUp = opts.Contains("B");
                    bool back = opts.Contains("N");
                    int newX = (int)curr.GetX();
                    int newY = (int)curr.GetY();
                    switch (cmd)
                    {
                        case 'U':
                            newY -= s;
                            break;
                        case 'D':
                            newY += s;
                            break;
                        case 'L':
                            newX -= s;
                            break;
                        case 'R':
                            newX += s;
                            break;
                        case 'E':
                            newY -= s;
                            newX += s;
                            break;
                        case 'F':
                            newY += s;
                            newX += s;
                            break;
                        case 'G':
                            newY += s;
                            newX -= s;
                            break;
                        case 'H':
                            newY -= s;
                            newX -= s;
                            break;
                    }

                    if (penUp)
                    {
                        path.MoveTo(newX, newY);
                    }
                    else
                    {
                        path.LineTo(newX, newY);
                    }

                    if (back)
                    {
                        path.MoveTo(curr.GetX(), curr.GetY());
                    }
                }
            }

            graphicsState.GetGraphics2D().Draw(path);
        }

        public static void Line(GraphicsState graphicsState, PuffinBasicSymbolTable symbolTable, IList<Instruction> instr0, Instruction instruction)
        {
            var i0 = instr0[0];
            var i1 = instr0[1];
            var x1 = symbolTable[i0.op1].GetValue().GetInt32();
            var y1 = symbolTable[i0.op2].GetValue().GetInt32();
            var x2 = symbolTable[i1.op1].GetValue().GetInt32();
            var y2 = symbolTable[i1.op2].GetValue().GetInt32();
            string bf = instruction.op1 != NULL_ID ? symbolTable[instruction.op1].GetValue().GetString().ToUpperCase() : "";
            if (bf.IsEmpty())
            {
                graphicsState.GetGraphics2D().DrawLine(x1, y1, x2, y2);
            }
            else if (bf.Equals("B"))
            {
                graphicsState.GetGraphics2D().DrawRect(x1, y1, Math.Abs(x1 - x2), Math.Abs(y1 - y2));
            }
            else if (bf.Equals("BF"))
            {
                graphicsState.GetGraphics2D().FillRect(x1, y1, Math.Abs(x1 - x2), Math.Abs(y1 - y2));
            }
            else
            {
                throw new PuffinBasicRuntimeError(GRAPHICS_ERROR, "Bad options: " + bf);
            }
        }

        public static void Color(GraphicsState graphicsState, PuffinBasicSymbolTable symbolTable, Instruction instr0, Instruction instruction)
        {
            var r = symbolTable[instr0.op1].GetValue().GetInt32();
            var g = symbolTable[instr0.op2].GetValue().GetInt32();
            var b = symbolTable[instruction.op1].GetValue().GetInt32();
            r = ApplyColorBounds(r);
            g = ApplyColorBounds(g);
            b = ApplyColorBounds(b);
            graphicsState.GetGraphics2D().SetColor(new Color(r, g, b));
        }

        private static int ApplyColorBounds(int c)
        {
            return Math.Min(255, Math.Max(0, c));
        }

        public static void Paint(GraphicsState graphicsState, PuffinBasicSymbolTable symbolTable, IList<Instruction> instr0, Instruction instruction)
        {
            var i0 = instr0[0];
            var i1 = instr0[1];
            var r = symbolTable[i0.op1].GetValue().GetInt32();
            var g = symbolTable[i0.op2].GetValue().GetInt32();
            var b = symbolTable[i1.op1].GetValue().GetInt32();
            var x = symbolTable[instruction.op1].GetValue().GetInt32();
            var y = symbolTable[instruction.op2].GetValue().GetInt32();
            if (x < 0 || y < 0 || x > graphicsState.GetImageWidth() || y > graphicsState.GetImageHeight())
            {
                throw new PuffinBasicRuntimeError(GRAPHICS_ERROR, "x/y out-of-bounds: " + x + ", " + y);
            }

            r = ApplyColorBounds(r);
            g = ApplyColorBounds(g);
            b = ApplyColorBounds(b);
            graphicsState.GetFrame().GetDrawingCanvas().FloodFill(x, y, r, g, b);
        }

        public static void Pset(GraphicsState graphicsState, PuffinBasicSymbolTable symbolTable, IList<Instruction> instr0, Instruction instruction)
        {
            var i0 = instr0[0];
            var i1 = instr0[1];
            var r = i0.op1 != NULL_ID ? symbolTable[i0.op1].GetValue().GetInt32() : -1;
            var g = i0.op2 != NULL_ID ? symbolTable[i0.op2].GetValue().GetInt32() : -1;
            var b = i1.op1 != NULL_ID ? symbolTable[i1.op1].GetValue().GetInt32() : -1;
            var x = symbolTable[instruction.op1].GetValue().GetInt32();
            var y = symbolTable[instruction.op2].GetValue().GetInt32();
            r = ApplyColorBounds(r);
            g = ApplyColorBounds(g);
            b = ApplyColorBounds(b);
            if (x < 0 || y < 0 || x > graphicsState.GetImageWidth() || y > graphicsState.GetImageHeight())
            {
                throw new PuffinBasicRuntimeError(GRAPHICS_ERROR, "x/y out-of-bounds: " + x + ", " + y);
            }

            graphicsState.GetFrame().GetDrawingCanvas().Point(x, y, r, g, b);
        }

        public static void BufferCopyHor(GraphicsState graphicsState, PuffinBasicSymbolTable symbolTable, Instruction instr0, Instruction instruction)
        {
            var srcx = symbolTable[instr0.op1].GetValue().GetInt32();
            var dstx = symbolTable[instr0.op2].GetValue().GetInt32();
            var w = symbolTable[instruction.op1].GetValue().GetInt32();
            if (srcx < 0 || dstx < 0 || w < 0 || srcx > graphicsState.GetImageWidth() || dstx > graphicsState.GetImageWidth() || w > graphicsState.GetImageWidth())
            {
                throw new PuffinBasicRuntimeError(GRAPHICS_ERROR, "srcx/dstx/w misaligned/out-of-bounds: (" + srcx + " -> " + dstx + "), " + w + ")");
            }

            graphicsState.GetFrame().GetDrawingCanvas().BufferCopyHor(srcx, dstx, w);
        }

        public static void Get(GraphicsState graphicsState, PuffinBasicSymbolTable symbolTable, IList<Instruction> instr0, Instruction instruction)
        {
            var i0 = instr0[0];
            var i1 = instr0[1];
            var x1 = symbolTable[i0.op1].GetValue().GetInt32();
            var y1 = symbolTable[i0.op2].GetValue().GetInt32();
            var x2 = symbolTable[i1.op1].GetValue().GetInt32();
            var y2 = symbolTable[i1.op2].GetValue().GetInt32();
            var variable = symbolTable.GetVariable(instruction.op1);
            if (variable.GetType().GetTypeId() != ARRAY || variable.GetValue().GetNumArrayDimensions() != 2 || variable.GetType().GetAtomTypeId() != INT32)
            {
                throw new PuffinBasicRuntimeError(GRAPHICS_ERROR, "Bad variable! Expected Int32 2D-Array variable: " + variable);
            }

            if (x1 < 0 || y1 < 0 || x2 < 0 || y2 < 0 || x1 > x2 || y1 > y2 || x1 > graphicsState.GetImageWidth() || y1 > graphicsState.GetImageHeight() || x2 > graphicsState.GetImageWidth() || y2 > graphicsState.GetImageHeight())
            {
                throw new PuffinBasicRuntimeError(GRAPHICS_ERROR, "x1/y1/x2/y2 misaligned/out-of-bounds: (" + x1 + ", " + y1 + "), " + x2 + ", " + y2 + ")");
            }

            int bufferNumber = symbolTable[instruction.op2].GetValue().GetInt32();
            graphicsState.GetFrame().GetDrawingCanvas().CopyGraphicsToArray(bufferNumber, x1, y1, x2, y2, variable.GetValue().GetInt32Array1D());
        }

        public static void Put(GraphicsState graphicsState, PuffinBasicSymbolTable symbolTable, Instruction instr0, Instruction instr1, Instruction instruction)
        {
            var x = symbolTable[instr0.op1].GetValue().GetInt32();
            var y = symbolTable[instr0.op2].GetValue().GetInt32();
            var action = instruction.op1 != NULL_ID ? symbolTable[instruction.op1].GetValue().GetString() : PUT_XOR;
            action = action.ToUpperCase();
            int bufferNumber = symbolTable[instr1.op1].GetValue().GetInt32();
            var variable = symbolTable.GetVariable(instruction.op2);
            var value = variable.GetValue();
            if (variable.GetType().GetTypeId() != ARRAY || value.GetNumArrayDimensions() != 2 || variable.GetType().GetAtomTypeId() != INT32)
            {
                throw new PuffinBasicRuntimeError(GRAPHICS_ERROR, "Bad variable! Expected Int32 2D-Array variable: " + variable);
            }

            int CW = graphicsState.GetImageWidth();
            int CH = graphicsState.GetImageHeight();
            var dims = value.GetArrayDimensions();
            int iw = dims.GetInt(0);
            int ih = dims.GetInt(1);
            int offset = 0;
            int w, h;
            int xx, yy;
            int srcx, srcy;
            if (x >= 0)
            {
                w = Math.Min(iw, CW - x);
                xx = x;
                srcx = 0;
            }
            else
            {
                w = Math.Min(iw, iw + x);
                xx = 0;
                srcx = Math.Abs(x);
            }

            if (y >= 0)
            {
                h = Math.Min(ih, CH - y);
                yy = y;
                srcy = 0;
            }
            else
            {
                h = Math.Min(ih, ih + y);
                yy = 0;
                srcy = Math.Abs(y);
            }


            // draw only if the image falls on the screen
            if (w > 0 && h > 0 && offset < iw * ih)
            {
                graphicsState.GetFrame().GetDrawingCanvas().CopyArrayToGraphics(bufferNumber, xx, yy, w, h, action, value.GetInt32Array1D(), srcx, srcy, iw);
            }
        }

        public static void Inkeydlr(GraphicsState graphicsState, PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            var key = graphicsState.GetFrame().GetDrawingCanvas().TakeNextKey();
            symbolTable[instruction.result].GetValue().SetString(key);
        }

        public static void Loadwav(SoundState soundState, PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            var file = symbolTable[instruction.op1].GetValue().GetString();
            var variable = symbolTable.GetVariable(instruction.op2).GetValue();
            variable.SetInt32(soundState.Load(file));
        }

        public static void Playwav(SoundState soundState, PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            var id = symbolTable[instruction.op1].GetValue().GetInt32();
            soundState.Play(id);
        }

        public static void Stopwav(SoundState soundState, PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            var id = symbolTable[instruction.op1].GetValue().GetInt32();
            soundState.Stop(id);
        }

        public static void Loopwav(SoundState soundState, PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            var id = symbolTable[instruction.op1].GetValue().GetInt32();
            soundState.Loop(id);
        }

        public static void MouseMovedX(GraphicsState graphicsState, PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            symbolTable[instruction.result].GetValue().SetInt32(graphicsState.GetFrame().GetDrawingCanvas().GetMouseState().GetMovedX());
        }

        public static void MouseMovedY(GraphicsState graphicsState, PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            symbolTable[instruction.result].GetValue().SetInt32(graphicsState.GetFrame().GetDrawingCanvas().GetMouseState().GetMovedY());
        }

        public static void MouseDraggedX(GraphicsState graphicsState, PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            symbolTable[instruction.result].GetValue().SetInt32(graphicsState.GetFrame().GetDrawingCanvas().GetMouseState().GetDraggedX());
        }

        public static void MouseDraggedY(GraphicsState graphicsState, PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            symbolTable[instruction.result].GetValue().SetInt32(graphicsState.GetFrame().GetDrawingCanvas().GetMouseState().GetDraggedY());
        }

        public static void MouseButtonClicked(GraphicsState graphicsState, PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            symbolTable[instruction.result].GetValue().SetInt32(graphicsState.GetFrame().GetDrawingCanvas().GetMouseState().GetButtonClicked());
        }

        public static void MouseButtonPressed(GraphicsState graphicsState, PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            symbolTable[instruction.result].GetValue().SetInt32(graphicsState.GetFrame().GetDrawingCanvas().GetMouseState().GetButtonPressed());
        }

        public static void MouseButtonReleased(GraphicsState graphicsState, PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            symbolTable[instruction.result].GetValue().SetInt32(graphicsState.GetFrame().GetDrawingCanvas().GetMouseState().GetButtonReleased());
        }

        public static void IsKeyPressed(GraphicsState graphicsState, PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            var key = symbolTable[instruction.op1].GetValue().GetString();
            symbolTable[instruction.result].GetValue().SetInt32(graphicsState.GetFrame().GetDrawingCanvas().IsKeyPressed(key) ? -1 : 0);
        }
        */
    }
}

