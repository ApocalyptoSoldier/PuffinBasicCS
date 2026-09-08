namespace PuffinBasicUI
{
    using Org.Puffinbasic.Domain;
    using Org.Puffinbasic.Error;
    using Org.Puffinbasic.Runtime;

    using System;
    using System.Collections.Generic;
    using System.Drawing;
    using System.IO;
    using System.Text;
    using System.Text.RegularExpressions;

    using static Org.Puffinbasic.Parser.PuffinBasicIR;
    //using static System.Runtime.InteropServices.JavaScript.JSType;
    using static Org.Puffinbasic.Domain.PuffinBasicSymbolTable;

    //using String = String;
    using System.Windows;
    using System.Security.Principal;
    using System.Runtime.CompilerServices;
    using System.Drawing.Drawing2D;
    using System.Windows.Input;
    using DrawingQuickstartWinForms;

    internal class GraphicsRuntimeStatic
    {
        private static readonly Regex DRAW_ARG1 = new Regex("([UDLREFGHA])([BN]+)?([0-9]+)", RegexOptions.Compiled);
        private static readonly Regex DRAW_ARG2 = new Regex("M([+\\-]?[0-9]+),([+\\-]?[0-9]+)", RegexOptions.Compiled);

        public static Font CurrentFont { get; private set; }
        public static Color CurrentColor { get; private set; }

        public class GraphicsState
        {

            private BasicFrame frame;
            private Bitmap curBitmap;
            private Graphics curGraphics;

            public virtual Bitmap GetBitmap()
            {
                return curBitmap;
            }

            public virtual void SetBitmap(Bitmap bitmap)
            { 
                curBitmap = bitmap;    
            }

            public virtual bool IsInitialized()
            {
                return frame != null;
            }

            public virtual BasicFrame GetFrame()
            {
                AssertScreenInitialized();
                return frame;
            }

            public virtual Graphics GetGraphics()
            {
                throw new NotImplementedException();
                //return GetFrame().GetDrawingCanvas().GetGraphics2D();
            }

            public virtual int GetImageWidth()
            {
                return GetBitmap().Width;
                //return GetFrame().GetDrawingCanvas().GetImageWidth();
            }

            public virtual int GetImageHeight()
            {
                return GetBitmap().Height;
                //return GetFrame().GetDrawingCanvas().GetImageHeight();
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
                    throw new PuffinBasicRuntimeError(PuffinBasicRuntimeError.ErrorCode.GRAPHICS_ERROR, "Screen cannot be called again!");
                }
            }

            private void AssertScreenInitialized()
            {
                if (frame == null)
                {
                    throw new PuffinBasicRuntimeError(PuffinBasicRuntimeError.ErrorCode.GRAPHICS_ERROR, "Screen has already been created!");
                }
            }
        }

        public void Cls(GraphicsState graphicsState)
        {
            graphicsState.GetGraphics().Clear(System.Drawing.Color.Transparent);
            //graphicsState.GetFrame().GetDrawingCanvas().Clear();
        }

        public void Beep()
        {
            Console.Beep();
            //Toolkit.GetDefaultToolkit().Beep();
        }

        public void Saveimg(PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            var path = symbolTable[instruction.op1].GetValue().GetString();
            var entry = symbolTable.GetVariable(instruction.op2);
            var variableValue = entry.GetValue();
            if (variableValue.GetNumArrayDimensions() != 2 || entry.GetType().GetAtomTypeId() != STObjects.PuffinBasicAtomTypeId.INT32)
            {
                throw new PuffinBasicRuntimeError(PuffinBasicRuntimeError.ErrorCode.GRAPHICS_ERROR, "Bad Array Variable, expected Int32 2D-Array Variable: " + entry);
            }

            var dims = variableValue.GetArrayDimensions();
            var v = new Bitmap(dims[0], dims[1], System.Drawing.Imaging.PixelFormat.Format24bppRgb);
            var varVal = variableValue.GetInt32Array1D();

            for (int x = 0; x < v.Height; x++)
            {
                for (int y = 0; y < v.Width; y++)
                {
                    var c = varVal[v.Height * v.Width + y * v.Width + x];
                    v.SetPixel(x, y, System.Drawing.Color.FromArgb(c));
                }
            }

            //BufferedImage image = new BufferedImage(dims[0], dims[1], BufferedImage.TYPE_3BYTE_BGR);
            //image.SetRGB(0, 0, image.GetWidth(), image.GetHeight(), variableValue.GetInt32Array1D(), 0, image.GetWidth());
            var ext = Path.GetExtension(path);
            try
            {
                v.Save(path);
                //ImageIO.Write(image, ext, new File(path));
            }
            catch (IOException e)
            {
                throw new PuffinBasicRuntimeError(PuffinBasicRuntimeError.ErrorCode.IO_ERROR, "Failed to save image: " + path + ", error: " + e.Message);
            }
        }

        public void Loadimg(PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            var path = symbolTable[instruction.op1].GetValue().GetString();
            var entry = symbolTable.GetVariable(instruction.op2);
            var variableValue = entry.GetValue();
            if (variableValue.GetNumArrayDimensions() != 2 || entry.GetType().GetAtomTypeId() != STObjects.PuffinBasicAtomTypeId.INT32)
            {
                throw new PuffinBasicRuntimeError(PuffinBasicRuntimeError.ErrorCode.GRAPHICS_ERROR, "Bad Array Variable, expected Int32 2D-Array Variable: " + entry);
            }


            Image image;
            //BufferedImage image;
            try
            {
                image = Image.FromFile(path);
                //image = ImageIO.Read(new File(path));
            }
            catch (IOException e)
            {
                throw new PuffinBasicRuntimeError(PuffinBasicRuntimeError.ErrorCode.IO_ERROR, "Failed to load image: " + path + ", error: " + e.Message);
            }

            var dims = variableValue.GetArrayDimensions();
            if (image.Width != dims[0] || image.Height != dims[1])
            {
                throw new PuffinBasicRuntimeError(PuffinBasicRuntimeError.ErrorCode.GRAPHICS_ERROR, 
                    "Image dimensions: " + image.Width + ", " + image.Height + " doesn't match with variable dimensions: " + dims[0] + ", " + dims[1]);
            }
            using (var buffer = new MemoryStream())
            {
                var arr = variableValue.GetInt32Array1D();
                var newArr = Array.ConvertAll(buffer.ToArray(), Convert.ToInt32);
                Array.Copy(newArr, arr, arr.Length);
            }
            //image.GetRGB(0, 0, image.GetWidth(), image.GetHeight(), variableValue., 0, image.GetWidth());
        }

        public void Screen(GraphicsState graphicsState, PuffinBasicSymbolTable symbolTable, IList<Instruction> instr0, Instruction instruction)
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
                throw new PuffinBasicRuntimeError(PuffinBasicRuntimeError.ErrorCode.GRAPHICS_ERROR, "Screen size out-of-bounds: " + w + ", " + h);
            }

            if (iw <= 0 || ih <= 0 || iw > GraphicsUtil.MAX_WIDTH || ih > GraphicsUtil.MAX_HEIGHT)
            {
                throw new PuffinBasicRuntimeError(PuffinBasicRuntimeError.ErrorCode.GRAPHICS_ERROR, "Image size out-of-bounds: " + iw + ", " + ih);
            }

            var autoRepaint = symbolTable[i2.op1].GetValue().GetInt32() == -1;
            var doubleBuffer = symbolTable[i2.op2].GetValue().GetInt32() == -1;
       
            graphicsState.SetFrame(new BasicFrame(title, w, h));
            //graphicsState.SetFrame(new BasicFrame(title, w, h, iw, ih, autoRepaint, doubleBuffer));
            //EventQueue.InvokeLater(() => graphicsState.GetFrame().SetVisible(true));
        }

        public void Hsb2rgb(PuffinBasicSymbolTable symbolTable, Instruction instr0, Instruction instruction)
        {
            var h = symbolTable[instr0.op1].GetValue().GetFloat32();
            var s = symbolTable[instr0.op2].GetValue().GetFloat32();
            var b = symbolTable[instruction.op1].GetValue().GetFloat32();
            var result = symbolTable[instruction.result].GetValue();

            throw new NotImplementedException();

            //result.SetInt32(Color.HSBtoRGB(h, s, b));
        }

        public void Repaint(GraphicsState graphicsState)
        {
            //graphicsState.GetFrame().GetDrawingCanvas().RenderAndRepaint();
            graphicsState.GetFrame().Render(graphicsState.GetBitmap());
        }

        public void End(GraphicsState graphicsState)
        {
            //SwingUtilities.InvokeLater(() =>
            //{
            //    if (graphicsState.IsInitialized())
            //    {
            //        var frame = graphicsState.GetFrame();
            //        frame.DispatchEvent(new WindowEvent(frame, WindowEvent.WINDOW_CLOSING));
            //    }
            //});
        }

        public void Circle(GraphicsState graphicsState, PuffinBasicSymbolTable symbolTable, IList<Instruction> instr0, Instruction instruction)
        {
            var i0 = instr0[0];
            var i1 = instr0[1];
            var i2 = instr0[2];
            var x = symbolTable[i0.op1].GetValue().GetInt32();
            var y = symbolTable[i0.op2].GetValue().GetInt32();
            float? s = i1.op1 != NULL_ID ? symbolTable[i1.op1].GetValue().GetFloat32() : null;
            float? e = i1.op1 != NULL_ID ? symbolTable[i1.op2].GetValue().GetFloat32() : null;
            int r1 = Math.Max(0, symbolTable[instruction.op1].GetValue().GetInt32());
            int r2 = Math.Max(0, symbolTable[instruction.op2].GetValue().GetInt32());
            bool fill = i2.op1 != NULL_ID && symbolTable[i2.op1].GetValue().GetString().Equals("F", StringComparison.InvariantCultureIgnoreCase);
            int w = r1 * 2;
            int h = r2 * 2;
            int sx = x - r1;
            int sy = y - r2;
            var g = graphicsState.GetGraphics();
            if (s == null || e == null)
            {
                if (fill)
                {
                    //g.FillOval(sx, sy, w, h);
                    g.FillEllipse(Brushes.AliceBlue, sx, sy, w, h);
                }
                else
                {
                    g.DrawEllipse(Pens.AliceBlue, sx, sy, w, h);
                }
            }
            else
            {
                if (fill)
                {
                    throw new NotImplementedException();
                    //g.FillArc(sx, sy, w, h, s, e);
                }
                else
                {
                    if (s.HasValue && e.HasValue)
                        g.DrawArc(Pens.Aqua, sx, sy, w, h, s.Value, e.Value);
                    else
                        g.DrawArc(Pens.Aqua, sx, sy, w, h, 0, 0);
                    //g.DrawArc(sx, sy, w, h, s, e);
                }
            }
        }

        public void Font(GraphicsState graphicsState, PuffinBasicSymbolTable symbolTable, Instruction instr0, Instruction instruction)
        {
            var style = symbolTable[instr0.op1].GetValue().GetString().ToLower();
            var size = symbolTable[instr0.op2].GetValue().GetInt32();
            var name = symbolTable[instruction.op1].GetValue().GetString();
            if (String.IsNullOrEmpty(name) || size <= 0 || size > GraphicsUtil.MAX_WIDTH)
            {
                throw new PuffinBasicRuntimeError(PuffinBasicRuntimeError.ErrorCode.GRAPHICS_ERROR, "Bad name/size: '" + name + "'/" + size);
            }
            FontStyle styleVal = FontStyle.Regular;
            if (style.Contains('i'))
            {
                styleVal |= FontStyle.Italic;
            }

            if (style.Contains('b'))
            {
                styleVal |= FontStyle.Bold;
            }

            CurrentFont = new Font(name, size, styleVal);
        }

        public void Drawstr(GraphicsState graphicsState, PuffinBasicSymbolTable symbolTable, Instruction instr0, Instruction instruction)
        {
            var x = symbolTable[instr0.op1].GetValue().GetFloat32();
            var y = symbolTable[instr0.op2].GetValue().GetFloat32();
            var text = symbolTable[instruction.op1].GetValue().GetString();

            var b = new SolidBrush(CurrentColor);

            graphicsState.GetGraphics().DrawString(text, CurrentFont, b, x, y);
        }

        public void Draw(GraphicsState graphicsState, PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            var str = symbolTable[instruction.op1].GetValue().GetString();
            if (String.IsNullOrEmpty(str))
            {
                throw new PuffinBasicRuntimeError(PuffinBasicRuntimeError.ErrorCode.GRAPHICS_ERROR, "Found empty string in DRAW!");
            }

            var path = new GraphicsPath();

            int w = graphicsState.GetImageWidth();
            int h = graphicsState.GetImageHeight();

            path.PathPoints.Append(new PointF(w / 2, h / 2));
            foreach (var ii in str.Split(";"))
            {
                var i = ii.Trim();
                if (String.IsNullOrEmpty(i))
                {
                    continue;
                }

                var curr = path.GetLastPoint();
                if (i[0] == 'M')
                {
                    var m = DRAW_ARG2.Match(i);

                    string x = m.Groups[1].Value;
                    string y = m.Groups[2].Value;
                    int newX = (int)curr.X;
                    int newY = (int)curr.Y;
                    if (x.StartsWith('+') || x.StartsWith('-'))
                    {
                        newX += Int32.Parse(x);
                    }
                    else
                    {
                        newX = Int32.Parse(x);
                    }

                    if (y.StartsWith('+') || y.StartsWith('-'))
                    {
                        newY += Int32.Parse(x);
                    }
                    else
                    {
                        newY = Int32.Parse(x);
                    }

                    path.PathPoints.Append(new PointF(newX, newY));
                }
                else
                {
                    var m = DRAW_ARG1.Match(i);

                    char cmd = m.Groups[1].Value[0];
                    string opts = m.Groups[2] != null ? m.Groups[2].Value : "";
                    int s = Int32.Parse(m.Groups[3].Value);
                    bool penUp = opts.Contains("B");
                    bool back = opts.Contains("N");
                    int newX = (int)curr.X;
                    int newY = (int)curr.Y;
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
                        path.PathPoints.Append(new PointF(newX, newY));
                    }
                    else
                    {
                        path.AddLine(path.GetLastPoint(), new PointF(newX, newY));
                    }

                    if (back)
                    {
                        //path.PathPoints.Append(curr.GetX(), curr.GetY());
                        path.PathPoints.Append(new PointF(curr.X, curr.Y));
                    }
                }
            }

            graphicsState.GetGraphics().DrawPath(Pens.Black, path);
        }

        public void Line(GraphicsState graphicsState, PuffinBasicSymbolTable symbolTable, IList<Instruction> instr0, Instruction instruction)
        {
            var i0 = instr0[0];
            var i1 = instr0[1];
            var x1 = symbolTable[i0.op1].GetValue().GetFloat32();
            var y1 = symbolTable[i0.op2].GetValue().GetFloat32();
            var x2 = symbolTable[i1.op1].GetValue().GetFloat32();
            var y2 = symbolTable[i1.op2].GetValue().GetFloat32();
            string bf = instruction.op1 != NULL_ID ? symbolTable[instruction.op1].GetValue().GetString().ToUpper() : "";
            var p = Pens.Aquamarine; // TODO: impement this
            if (String.IsNullOrEmpty(bf))
            {
                graphicsState.GetGraphics().DrawLine(p, x1, y1, x2, y2);
            }
            else if (bf.Equals("B"))
            {
                graphicsState.GetGraphics().DrawRectangle(p, x1, y1, Math.Abs(x1 - x2), Math.Abs(y1 - y2));
            }
            else if (bf.Equals("BF"))
            {
                graphicsState.GetGraphics().FillRectangle(
                    Brushes.Beige
                    , x1, y1, Math.Abs(x1 - x2), Math.Abs(y1 - y2));
            }
            else
            {
                throw new PuffinBasicRuntimeError(PuffinBasicRuntimeError.ErrorCode.GRAPHICS_ERROR, "Bad options: " + bf);
            }
        }

        public void Color(GraphicsState graphicsState, PuffinBasicSymbolTable symbolTable, Instruction instr0, Instruction instruction)
        {
            var r = symbolTable[instr0.op1].GetValue().GetInt32();
            var g = symbolTable[instr0.op2].GetValue().GetInt32();
            var b = symbolTable[instruction.op1].GetValue().GetInt32();
            r = ApplyColorBounds(r);
            g = ApplyColorBounds(g);
            b = ApplyColorBounds(b);

            CurrentColor = System.Drawing.Color.FromArgb(r, g, b);
        }

        private static int ApplyColorBounds(int c)
        {
            return Math.Min(255, Math.Max(0, c));
        }

        public void Paint(GraphicsState graphicsState, PuffinBasicSymbolTable symbolTable, IList<Instruction> instr0, Instruction instruction)
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
                throw new PuffinBasicRuntimeError(PuffinBasicRuntimeError.ErrorCode.GRAPHICS_ERROR, "x/y out-of-bounds: " + x + ", " + y);
            }

            r = ApplyColorBounds(r);
            g = ApplyColorBounds(g);
            b = ApplyColorBounds(b);

            var br = new SolidBrush(System.Drawing.Color.FromArgb(r, g, b));
            graphicsState.GetGraphics().FillRectangle(br, 0, 0, (float)x, (float)y);

            //graphicsState.GetFrame().GetDrawingCanvas().FloodFill(x, y, r, g, b);

            // We still have to actually draw it to the screen
            throw new NotImplementedException();
        }

        public void Pset(GraphicsState graphicsState, PuffinBasicSymbolTable symbolTable, IList<Instruction> instr0, Instruction instruction)
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
                throw new PuffinBasicRuntimeError(PuffinBasicRuntimeError.ErrorCode.GRAPHICS_ERROR, "x/y out-of-bounds: " + x + ", " + y);
            }

            throw new NotImplementedException();
            //graphicsState.GetGraphics().dra
            //graphicsState.GetFrame().GetDrawingCanvas().Point(x, y, r, g, b);
        }

        public void BufferCopyHor(GraphicsState graphicsState, PuffinBasicSymbolTable symbolTable, Instruction instr0, Instruction instruction)
        {
            var srcx = symbolTable[instr0.op1].GetValue().GetInt32();
            var dstx = symbolTable[instr0.op2].GetValue().GetInt32();
            var w = symbolTable[instruction.op1].GetValue().GetInt32();
            if (srcx < 0 || dstx < 0 || w < 0 || srcx > graphicsState.GetImageWidth() || dstx > graphicsState.GetImageWidth() || w > graphicsState.GetImageWidth())
            {
                throw new PuffinBasicRuntimeError(PuffinBasicRuntimeError.ErrorCode.GRAPHICS_ERROR, "srcx/dstx/w misaligned/out-of-bounds: (" + srcx + " -> " + dstx + "), " + w + ")");
            }

            throw new NotImplementedException();
            //graphicsState.GetFrame().GetDrawingCanvas().BufferCopyHor(srcx, dstx, w);
        }

        public void Get(GraphicsState graphicsState, PuffinBasicSymbolTable symbolTable, IList<Instruction> instr0, Instruction instruction)
        {
            var i0 = instr0[0];
            var i1 = instr0[1];
            var x1 = symbolTable[i0.op1].GetValue().GetInt32();
            var y1 = symbolTable[i0.op2].GetValue().GetInt32();
            var x2 = symbolTable[i1.op1].GetValue().GetInt32();
            var y2 = symbolTable[i1.op2].GetValue().GetInt32();
            var variable = symbolTable.GetVariable(instruction.op1);
            if (variable.GetType().GetTypeId() != STObjects.PuffinBasicTypeId.ARRAY || variable.GetValue().GetNumArrayDimensions() != 2 || variable.GetType().GetAtomTypeId() != STObjects.PuffinBasicAtomTypeId.INT32)
            {
                throw new PuffinBasicRuntimeError(PuffinBasicRuntimeError.ErrorCode.GRAPHICS_ERROR, "Bad variable! Expected Int32 2D-Array variable: " + variable);
            }

            if (x1 < 0 || y1 < 0 || x2 < 0 || y2 < 0 || x1 > x2 || y1 > y2 || x1 > graphicsState.GetImageWidth() || y1 > graphicsState.GetImageHeight() || x2 > graphicsState.GetImageWidth() || y2 > graphicsState.GetImageHeight())
            {
                throw new PuffinBasicRuntimeError(PuffinBasicRuntimeError.ErrorCode.GRAPHICS_ERROR, "x1/y1/x2/y2 misaligned/out-of-bounds: (" + x1 + ", " + y1 + "), " + x2 + ", " + y2 + ")");
            }

            int bufferNumber = symbolTable[instruction.op2].GetValue().GetInt32();
            throw new NotImplementedException();
            //graphicsState.GetFrame().GetDrawingCanvas().CopyGraphicsToArray(bufferNumber, x1, y1, x2, y2, variable.GetValue().GetInt32Array1D());
        }

        public void Put(GraphicsState graphicsState, PuffinBasicSymbolTable symbolTable, Instruction instr0, Instruction instr1, Instruction instruction)
        {
            var x = symbolTable[instr0.op1].GetValue().GetInt32();
            var y = symbolTable[instr0.op2].GetValue().GetInt32();
            var action = instruction.op1 != NULL_ID ? symbolTable[instruction.op1].GetValue().GetString() : GraphicsUtil.PUT_XOR;
            action = action.ToUpper();
            int bufferNumber = symbolTable[instr1.op1].GetValue().GetInt32();
            var variable = symbolTable.GetVariable(instruction.op2);
            var value = variable.GetValue();
            if (variable.GetType().GetTypeId() != STObjects.PuffinBasicTypeId.ARRAY 
                || value.GetNumArrayDimensions() != 2 
                || variable.GetType().GetAtomTypeId() != STObjects.PuffinBasicAtomTypeId.INT32)
            {
                throw new PuffinBasicRuntimeError(PuffinBasicRuntimeError.ErrorCode.GRAPHICS_ERROR, "Bad variable! Expected Int32 2D-Array variable: " + variable);
            }

            int CW = graphicsState.GetImageWidth();
            int CH = graphicsState.GetImageHeight();
            var dims = value.GetArrayDimensions();
            int iw = dims[0];
            int ih = dims[1];
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
                throw new NotImplementedException();
                //graphicsState.GetGraphics().DrawImage()
                //graphicsState.GetFrame().GetDrawingCanvas().CopyArrayToGraphics(bufferNumber, xx, yy, w, h, action, value.GetInt32Array1D(), srcx, srcy, iw);
            }
        }

        public void Inkeydlr(GraphicsState graphicsState, PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            //var key = graphicsState.GetFrame().KeysPressed.First();
            //if (!String.IsNullOrEmpty(key))
            //    graphicsState.GetFrame().KeysPressed.Remove(key);

            ////var key = graphicsState.GetFrame().GetDrawingCanvas().TakeNextKey();
            //symbolTable[instruction.result].GetValue().SetString(key);
        }

        public void Loadwav(SoundState soundState, PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            var file = symbolTable[instruction.op1].GetValue().GetString();
            var variable = symbolTable.GetVariable(instruction.op2).GetValue();
            //variable.SetInt32(soundState.Load(file));
            throw new NotImplementedException();
        }

        public void Playwav(SoundState soundState, PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            var id = symbolTable[instruction.op1].GetValue().GetInt32();
            //soundState.Play(id);
            throw new NotImplementedException();
        }

        public void Stopwav(SoundState soundState, PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            var id = symbolTable[instruction.op1].GetValue().GetInt32();
            //soundState.Stop(id);
            throw new NotImplementedException();
        }

        public void Loopwav(SoundState soundState, PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            var id = symbolTable[instruction.op1].GetValue().GetInt32();
            //soundState.Loop(id);
            throw new NotImplementedException();
        }

        public void MouseMovedX(GraphicsState graphicsState, PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            //symbolTable[instruction.result].GetValue().SetInt32(graphicsState.GetFrame().GetDrawingCanvas().GetMouseState().GetMovedX());
            throw new NotImplementedException();
        }

        public void MouseMovedY(GraphicsState graphicsState, PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            //symbolTable[instruction.result].GetValue().SetInt32(graphicsState.GetFrame().GetDrawingCanvas().GetMouseState().GetMovedY());
            throw new NotImplementedException();
        }

        public void MouseDraggedX(GraphicsState graphicsState, PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            //symbolTable[instruction.result].GetValue().SetInt32(graphicsState.GetFrame().GetDrawingCanvas().GetMouseState().GetDraggedX());
            throw new NotImplementedException();
        }

        public void MouseDraggedY(GraphicsState graphicsState, PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            //symbolTable[instruction.result].GetValue().SetInt32(graphicsState.GetFrame().GetDrawingCanvas().GetMouseState().GetDraggedY());
            throw new NotImplementedException();
        }

        public void MouseButtonClicked(GraphicsState graphicsState, PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            //symbolTable[instruction.result].GetValue().SetInt32(graphicsState.GetFrame().GetDrawingCanvas().GetMouseState().GetButtonClicked());
            throw new NotImplementedException();
        }

        public void MouseButtonPressed(GraphicsState graphicsState, PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            //symbolTable[instruction.result].GetValue().SetInt32(graphicsState.GetFrame().GetDrawingCanvas().GetMouseState().GetButtonPressed());
            throw new NotImplementedException();
        }

        public void MouseButtonReleased(GraphicsState graphicsState, PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            //symbolTable[instruction.result].GetValue().SetInt32(graphicsState.GetFrame().GetDrawingCanvas().GetMouseState().GetButtonReleased());
            throw new NotImplementedException();
        }

        public void IsKeyPressed(GraphicsState graphicsState, PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            var key = symbolTable[instruction.op1].GetValue().GetString();
            //var keyPressed = graphicsState.GetFrame().KeysPressed.Contains(key);
            //symbolTable[instruction.result].GetValue().SetInt32(keyPressed ? -1 : 0);
        }
    }
}
