namespace PuffinBasicUIWinForms
{
    using PuffinBasicCS.Domain;
    using PuffinBasicCS.Error;

    using PuffinBasicUI;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Drawing;
    using System.Drawing.Drawing2D;
    using System.IO;
    using System.Text.RegularExpressions;

    using static PuffinBasicCS.Domain.PuffinBasicSymbolTable;
    using static PuffinBasicCS.Parser.PuffinBasicIR;
    using static PuffinBasicUI.GraphicsUtil;
    using static PuffinBasicCS.Error.PuffinBasicRuntimeError.ErrorCode;

    using PuffinBasicCS.Runtime;
    using System.Threading.Tasks;
    using System.Windows.Forms;
    using System.Linq;

    internal class GraphicsRuntime : PuffinBasicCS.Runtime.GraphicsRuntime.IGraphicsRuntimeImplementation
    {
        private static readonly Regex DRAW_ARG1 = new Regex("([UDLREFGHA])([BN]+)?([0-9]+)", RegexOptions.Compiled);
        private static readonly Regex DRAW_ARG2 = new Regex("M([+\\-]?[0-9]+),([+\\-]?[0-9]+)", RegexOptions.Compiled);

        public GraphicsState graphicsState { get; private set; } = new GraphicsState();
        public SoundState soundState { get; private set; } = new SoundState();

        public class GraphicsState
        {
            private BasicFrame frame;

            public virtual bool IsInitialized => frame != null;

            public virtual BasicFrame Frame
            {
                get
                {
                    AssertScreenInitialized();
                    return frame;
                }

                set
                {
                    AssertNewScreen();
                    this.frame = value;
                }
            }

            public virtual Graphics Graphics => Frame.DrawingCanvas.GetGraphics2D();

            public virtual int ImageWidth => Frame.DrawingCanvas.GetImageWidth();

            public virtual int ImageHeight => Frame.DrawingCanvas.GetImageHeight();

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

        public void Cls()
        {
            graphicsState.Frame.DrawingCanvas.Clear();
        }

        public void Beep()
        {
            Console.Beep();
        }

        public void Saveimg(PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            var path = symbolTable[instruction.op1].Value.GetString();
            var entry = symbolTable.GetVariable(instruction.op2);
            var variableValue = entry.Value;
            if (variableValue.GetNumArrayDimensions() != 2 || entry.Type.AtomTypeId != STObjects.PuffinBasicAtomTypeId.INT32)
            {
                throw new PuffinBasicRuntimeError(GRAPHICS_ERROR, "Bad Array Variable, expected Int32 2D-Array Variable: " + entry);
            }

            var dims = variableValue.GetArrayDimensions();
            var varVal = variableValue.GetInt32Array1D();
            var image = ImageUtils.FromIntArray(varVal, dims[0], dims[1]);

            var ext = Path.GetExtension(path);
            try
            {
                image.Save(path);
            }
            catch (IOException e)
            {
                throw new PuffinBasicRuntimeError(IO_ERROR, $"Failed to save image: {path}, error: {e.Message}");
            }
        }

        public void Loadimg(PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            var path = symbolTable[instruction.op1].Value.GetString();
            var entry = symbolTable.GetVariable(instruction.op2);
            var variableValue = entry.Value;
            if (variableValue.GetNumArrayDimensions() != 2 || entry.Type.AtomTypeId != STObjects.PuffinBasicAtomTypeId.INT32)
            {
                throw new PuffinBasicRuntimeError(GRAPHICS_ERROR, "Bad Array Variable, expected Int32 2D-Array Variable: " + entry);
            }


            Image image;
            try
            {
                image = Image.FromFile(path);
            }
            catch (IOException e)
            {
                throw new PuffinBasicRuntimeError(IO_ERROR, $"Failed to load image: {path}, error: {e.Message}");
            }

            var dims = variableValue.GetArrayDimensions();
            if (image.Width != dims[0] || image.Height != dims[1])
            {
                throw new PuffinBasicRuntimeError(GRAPHICS_ERROR,
                    $"Image dimensions: {image.Width}, {image.Height} doesn't match with variable dimensions: {dims[0]}, {dims[1]}");
            }
            var arr = variableValue.GetInt32Array1D();
            var newArr = ((Bitmap)image).ToIntArray();
            Array.Copy(newArr, arr, arr.Length);
        }

        public void Screen(PuffinBasicSymbolTable symbolTable, IList<Instruction> instr0, Instruction instruction)
        {
            var i0 = instr0[0];
            var i1 = instr0[1];
            var i2 = instr0[2];
            var w = symbolTable[i0.op1].Value.GetInt32();
            var h = symbolTable[i0.op2].Value.GetInt32();
            var iw = symbolTable[i1.op1].Value.GetInt32();
            var ih = symbolTable[i1.op2].Value.GetInt32();
            var title = symbolTable[instruction.op1].Value.GetString();
            if (w <= 0 || h <= 0 || w > MAX_WIDTH || h > MAX_HEIGHT)
            {
                throw new PuffinBasicRuntimeError(GRAPHICS_ERROR, $"Screen size out-of-bounds: {w}, {h}");
            }

            if (iw <= 0 || ih <= 0 || iw > MAX_WIDTH || ih > MAX_HEIGHT)
            {
                throw new PuffinBasicRuntimeError(GRAPHICS_ERROR, $"Image size out-of-bounds: {iw}, {ih}");
            }

            var autoRepaint = symbolTable[i2.op1].Value.GetInt32() == -1;
            var doubleBuffer = symbolTable[i2.op2].Value.GetInt32() == -1;

            graphicsState.Frame = new BasicFrame(title, w, h, iw, ih, autoRepaint, doubleBuffer);

            var start = new Task(() => Application.Run(graphicsState.Frame));
            start.Start();
        }

        public void Hsb2rgb(PuffinBasicSymbolTable symbolTable, Instruction instr0, Instruction instruction)
        {
            var h = symbolTable[instr0.op1].Value.GetFloat64();
            var s = symbolTable[instr0.op2].Value.GetFloat64();
            var b = symbolTable[instruction.op1].Value.GetFloat64();
            var result = symbolTable[instruction.result].Value;
            var col = HsbToRgb(h * 360, s, b);
            var argb = col.ToArgb();


            result.SetInt32(argb);
        }

        public void Repaint()
        {
            graphicsState.Frame.DrawingCanvas.RenderAndRepaint();
        }

        public void End()
        {
            //SwingUtilities.InvokeLater(() =>
            //{
            //    if (graphicsState.IsInitialized())
            //    {
            //        var frame = graphicsState.GetFrame();
            //        frame.DispatchEvent(new WindowEvent(frame, WindowEvent.WINDOW_CLOSING));
            //    }
            //});
            soundState.Dispose();
        }

        public void Circle(PuffinBasicSymbolTable symbolTable, IList<Instruction> instr0, Instruction instruction)
        {
            var i0 = instr0[0];
            var i1 = instr0[1];
            var i2 = instr0[2];
            var x = symbolTable[i0.op1].Value.GetInt32();
            var y = symbolTable[i0.op2].Value.GetInt32();
            int? startAngle = i1.op1 != NULL_ID ? symbolTable[i1.op1].Value.GetInt32() : null;
            int? sweepAngle = i1.op1 != NULL_ID ? symbolTable[i1.op2].Value.GetInt32() : null;
            int r1 = Math.Max(0, symbolTable[instruction.op1].Value.GetInt32());
            int r2 = Math.Max(0, symbolTable[instruction.op2].Value.GetInt32());
            bool fill = i2.op1 != NULL_ID && symbolTable[i2.op1].Value.GetString().Equals("F", StringComparison.InvariantCultureIgnoreCase);
            int w = r1 * 2;
            int h = r2 * 2;
            int sx = x - r1;
            int sy = y - r2;

            if (startAngle == null || sweepAngle == null)
            {
                if (fill)
                {
                    graphicsState.Frame.DrawingCanvas.FillOval(sx, sy, w, h);
                }
                else
                {
                    graphicsState.Frame.DrawingCanvas.DrawOval(sx, sy, w, h);
                }
            }
            else
            {
                if (fill)
                {
                    graphicsState.Frame.DrawingCanvas.FillArc(sx, sy, w, h, startAngle.Value, sweepAngle.Value);
                }
                else
                {
                    graphicsState.Frame.DrawingCanvas.DrawArc(sx, sy, w, h, startAngle.Value, sweepAngle.Value);
                }
            }
        }

        public void Font(PuffinBasicSymbolTable symbolTable, Instruction instr0, Instruction instruction)
        {
            var style = symbolTable[instr0.op1].Value.GetString().ToLower();
            var size = symbolTable[instr0.op2].Value.GetInt32();
            var name = symbolTable[instruction.op1].Value.GetString();
            if (String.IsNullOrEmpty(name) || size <= 0 || size > MAX_WIDTH)
            {
                throw new PuffinBasicRuntimeError(GRAPHICS_ERROR, $"Bad name/size: '{name}'/{size}");
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

            graphicsState.Frame.DrawingCanvas.Font = new Font(name, size, styleVal);
        }

        public void Drawstr(PuffinBasicSymbolTable symbolTable, Instruction instr0, Instruction instruction)
        {
            var x = symbolTable[instr0.op1].Value.GetFloat32();
            var y = symbolTable[instr0.op2].Value.GetFloat32();
            var text = symbolTable[instruction.op1].Value.GetString();

            graphicsState.Frame.DrawingCanvas
                .DrawString(text, x, y);
        }

        public void Draw(PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            var str = symbolTable[instruction.op1].Value.GetString();
            if (String.IsNullOrEmpty(str))
            {
                throw new PuffinBasicRuntimeError(GRAPHICS_ERROR, "Found empty string in DRAW!");
            }

            var path = new GraphicsPath();

            int w = graphicsState.ImageWidth;
            int h = graphicsState.ImageHeight;

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
                    bool penUp = opts.Contains('B');
                    bool back = opts.Contains('N');
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

            graphicsState.Frame.DrawingCanvas
                .DrawPath(path);
        }

        public void Line(PuffinBasicSymbolTable symbolTable, IList<Instruction> instr0, Instruction instruction)
        {
            var i0 = instr0[0];
            var i1 = instr0[1];
            var x1 = symbolTable[i0.op1].Value.GetFloat32();
            var y1 = symbolTable[i0.op2].Value.GetFloat32();
            var x2 = symbolTable[i1.op1].Value.GetFloat32();
            var y2 = symbolTable[i1.op2].Value.GetFloat32();
            string bf = instruction.op1 != NULL_ID ? symbolTable[instruction.op1].Value.GetString().ToUpper() : "";

            if (String.IsNullOrEmpty(bf))
            {
                graphicsState.Frame.DrawingCanvas
                    .DrawLine(x1, y1, x2, y2);
            }
            else if (bf.Equals("B"))
            {
                graphicsState.Frame.DrawingCanvas
                    .DrawRect(x1, y1, Math.Abs(x1 - x2), Math.Abs(y1 - y2));
            }
            else if (bf.Equals("BF"))
            {
                graphicsState.Frame.DrawingCanvas
                    .FillRect(x1, y1, Math.Abs(x1 - x2), Math.Abs(y1 - y2));
            }
            else
            {
                throw new PuffinBasicRuntimeError(GRAPHICS_ERROR, "Bad options: " + bf);
            }
        }

        public void Color(PuffinBasicSymbolTable symbolTable, Instruction instr0, Instruction instruction)
        {
            var r = symbolTable[instr0.op1].Value.GetInt32();
            var g = symbolTable[instr0.op2].Value.GetInt32();
            var b = symbolTable[instruction.op1].Value.GetInt32();
            r = ApplyColorBounds(r);
            g = ApplyColorBounds(g);
            b = ApplyColorBounds(b);
            graphicsState.Frame.DrawingCanvas.SetColor(System.Drawing.Color.FromArgb(r, g, b));
        }

        private static int ApplyColorBounds(int c)
        {
            return Math.Min(255, Math.Max(0, c));
        }

        public void Paint(PuffinBasicSymbolTable symbolTable, IList<Instruction> instr0, Instruction instruction)
        {
            var i0 = instr0[0];
            var i1 = instr0[1];
            var r = symbolTable[i0.op1].Value.GetInt32();
            var g = symbolTable[i0.op2].Value.GetInt32();
            var b = symbolTable[i1.op1].Value.GetInt32();
            var x = symbolTable[instruction.op1].Value.GetInt32();
            var y = symbolTable[instruction.op2].Value.GetInt32();
            if (x < 0 || y < 0 || x > graphicsState.ImageWidth || y > graphicsState.ImageHeight)
            {
                throw new PuffinBasicRuntimeError(GRAPHICS_ERROR, $"x/y out-of-bounds: {x}, {y}");
            }

            r = ApplyColorBounds(r);
            g = ApplyColorBounds(g);
            b = ApplyColorBounds(b);

            graphicsState.Frame.DrawingCanvas.FloodFill(x, y, r, g, b);
        }

        public void Pset(PuffinBasicSymbolTable symbolTable, IList<Instruction> instr0, Instruction instruction)
        {
            var i0 = instr0[0];
            var i1 = instr0[1];
            var r = i0.op1 != NULL_ID ? symbolTable[i0.op1].Value.GetInt32() : -1;
            var g = i0.op2 != NULL_ID ? symbolTable[i0.op2].Value.GetInt32() : -1;
            var b = i1.op1 != NULL_ID ? symbolTable[i1.op1].Value.GetInt32() : -1;
            var x = symbolTable[instruction.op1].Value.GetInt32();
            var y = symbolTable[instruction.op2].Value.GetInt32();
            r = ApplyColorBounds(r);
            g = ApplyColorBounds(g);
            b = ApplyColorBounds(b);
            if (x < 0 || y < 0 || x > graphicsState.ImageWidth || y > graphicsState.ImageHeight)
            {
                throw new PuffinBasicRuntimeError(GRAPHICS_ERROR, $"x/y out-of-bounds: {x}, {y}");
            }

  
            graphicsState.Frame.DrawingCanvas.Point(x, y, r, g, b);
        }

        public void BufferCopyHor(PuffinBasicSymbolTable symbolTable, Instruction instr0, Instruction instruction)
        {
            var srcx = symbolTable[instr0.op1].Value.GetInt32();
            var dstx = symbolTable[instr0.op2].Value.GetInt32();
            var w = symbolTable[instruction.op1].Value.GetInt32();
            if (srcx < 0 || dstx < 0 || w < 0 || srcx > graphicsState.ImageWidth || dstx > graphicsState.ImageWidth || w > graphicsState.ImageWidth)
            {
                throw new PuffinBasicRuntimeError(GRAPHICS_ERROR, $"srcx/dstx/w misaligned/out-of-bounds: ({srcx} -> {dstx}), {w})");
            }

            graphicsState.Frame.DrawingCanvas.BufferCopyHor(srcx, dstx, w);
        }

        public void Get(PuffinBasicSymbolTable symbolTable, IList<Instruction> instr0, Instruction instruction)
        {
            var i0 = instr0[0];
            var i1 = instr0[1];
            var x1 = symbolTable[i0.op1].Value.GetInt32();
            var y1 = symbolTable[i0.op2].Value.GetInt32();
            var x2 = symbolTable[i1.op1].Value.GetInt32();
            var y2 = symbolTable[i1.op2].Value.GetInt32();
            var variable = symbolTable.GetVariable(instruction.op1);
            if (variable.Type.TypeId != STObjects.PuffinBasicTypeId.ARRAY
                || variable.Value.GetNumArrayDimensions() != 2
                || variable.Type.AtomTypeId != STObjects.PuffinBasicAtomTypeId.INT32)
            {
                throw new PuffinBasicRuntimeError(GRAPHICS_ERROR, "Bad variable! Expected Int32 2D-Array variable: " + variable);
            }

            if (x1 < 0 || y1 < 0 || x2 < 0 || y2 < 0 || x1 > x2 || y1 > y2
                || x1 > graphicsState.ImageWidth
                || y1 > graphicsState.ImageHeight
                || x2 > graphicsState.ImageWidth
                || y2 > graphicsState.ImageHeight)
            {
                throw new PuffinBasicRuntimeError(GRAPHICS_ERROR, $"x1/y1/x2/y2 misaligned/out-of-bounds: ({x1}, {y1}), {x2}, {y2})");
            }

            int bufferNumber = symbolTable[instruction.op2].Value.GetInt32();

            graphicsState.Frame.DrawingCanvas
                .CopyGraphicsToArray(bufferNumber, x1, y1, x2, y2, variable.Value.GetInt32Array1D());
        }

        public void Put(PuffinBasicSymbolTable symbolTable, Instruction instr0, Instruction instr1, Instruction instruction)
        {
            var x = symbolTable[instr0.op1].Value.GetInt32();
            var y = symbolTable[instr0.op2].Value.GetInt32();
            var action = instruction.op1 != NULL_ID ? symbolTable[instruction.op1].Value.GetString() : PUT_XOR;
            action = action.ToUpper();
            int bufferNumber = symbolTable[instr1.op1].Value.GetInt32();
            var variable = symbolTable.GetVariable(instruction.op2);
            var value = variable.Value;
            if (variable.Type.TypeId != STObjects.PuffinBasicTypeId.ARRAY
                || value.GetNumArrayDimensions() != 2
                || variable.Type.AtomTypeId != STObjects.PuffinBasicAtomTypeId.INT32)
            {
                throw new PuffinBasicRuntimeError(GRAPHICS_ERROR, "Bad variable! Expected Int32 2D-Array variable: " + variable);
            }

            int CW = graphicsState.ImageWidth;
            int CH = graphicsState.ImageHeight;
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
                //graphicsState.GetGraphics().DrawImage()
                graphicsState.Frame.DrawingCanvas
                    .CopyArrayToGraphics(bufferNumber, xx, yy, w, h, action, value.GetInt32Array1D(), srcx, srcy, iw);
            }
        }

        public void Inkeydlr(PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            var key = graphicsState.Frame.DrawingCanvas.TakeNextKey();

            symbolTable[instruction.result].Value.SetString(key);
        }

        public void Loadwav(PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            var file = symbolTable[instruction.op1].Value.GetString();
            var variable = symbolTable.GetVariable(instruction.op2).Value;

            variable.SetInt32(soundState.Load(file));
        }

        public void Playwav(PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            var id = symbolTable[instruction.op1].Value.GetInt32();
            soundState.Play(id);
        }

        public void Stopwav(PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            var id = symbolTable[instruction.op1].Value.GetInt32();
            soundState.Stop(id);
        }

        public void Loopwav(PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            var id = symbolTable[instruction.op1].Value.GetInt32();
            soundState.Loop(id);
        }

        public void MouseMovedX(PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            symbolTable[instruction.result].Value.SetInt32(graphicsState.Frame.DrawingCanvas.GetMouseState().GetMovedX());
        }

        public void MouseMovedY(PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            symbolTable[instruction.result].Value.SetInt32(graphicsState.Frame.DrawingCanvas.GetMouseState().GetMovedY());
        }

        public void MouseDraggedX(PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            symbolTable[instruction.result].Value.SetInt32(graphicsState.Frame.DrawingCanvas.GetMouseState().GetDraggedX());
        }

        public void MouseDraggedY(PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            symbolTable[instruction.result].Value.SetInt32(graphicsState.Frame.DrawingCanvas.GetMouseState().GetDraggedY());
        }

        public void MouseButtonClicked(PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            symbolTable[instruction.result].Value.SetInt32(graphicsState.Frame.DrawingCanvas.GetMouseState().GetButtonClicked());
        }

        public void MouseButtonPressed(PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            symbolTable[instruction.result].Value.SetInt32(graphicsState.Frame.DrawingCanvas.GetMouseState().GetButtonPressed());
        }

        public void MouseButtonReleased(PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            symbolTable[instruction.result].Value.SetInt32(graphicsState.Frame.DrawingCanvas.GetMouseState().GetButtonReleased());
        }

        public void IsKeyPressed(PuffinBasicSymbolTable symbolTable, Instruction instruction)
        {
            var key = symbolTable[instruction.op1].Value.GetString();
            symbolTable[instruction.result].Value.SetInt32(
                graphicsState.Frame.DrawingCanvas.IsKeyPressed(key) ? -1 : 0);
        }

        public static Color HsbToRgb(double hue, double saturation, double brightness)
        {
            int hi = Convert.ToInt32(Math.Floor(hue / 60)) % 6;
            double f = hue / 60 - Math.Floor(hue / 60);

            brightness = brightness * 255;
            int v = Convert.ToInt32(brightness);
            int p = Convert.ToInt32(brightness * (1 - saturation));
            int q = Convert.ToInt32(brightness * (1 - f * saturation));
            int t = Convert.ToInt32(brightness * (1 - (1 - f) * saturation));

            if (hi == 0)
                return System.Drawing.Color.FromArgb(255, v, t, p);
            else if (hi == 1)
                return System.Drawing.Color.FromArgb(255, q, v, p);
            else if (hi == 2)
                return System.Drawing.Color.FromArgb(255, p, v, t);
            else if (hi == 3)
                return System.Drawing.Color.FromArgb(255, p, q, v);
            else if (hi == 4)
                return System.Drawing.Color.FromArgb(255, t, p, v);
            else
                return System.Drawing.Color.FromArgb(255, v, p, q);
        }
    }

}
