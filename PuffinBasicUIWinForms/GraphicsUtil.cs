// TODO: look at https://learn.microsoft.com/en-us/dotnet/api/system.drawing.bufferedgraphics?view=net-11.0-pp

//using It.Unimi.Dsi.Fastutil.Longs;
//using It.Unimi.Dsi.Fastutil.Objects;
namespace PuffinBasicUI
{
    using Org.Puffinbasic.Error;

    using System.Diagnostics;
    using System.Drawing.Drawing2D;
    using System.Drawing.Imaging;
    using System.Runtime.InteropServices;
    using System.Windows.Forms;

    using static PuffinBasicUI.GraphicsUtil;

    //using Javax.Swing;
    //using Java.Awt;
    //using Java.Awt.Event;
    //using Java.Awt.Image;
    //using Java.Util;
    //using Java.Util.Concurrent.Locks;

    public partial class BasicFrame
    {
        private readonly DrawingCanvas drawingCanvas;
        public BasicFrame(string title, int w, int h, int iw, int ih, bool autoRepaint, bool doubleBuffer)
        {
            InitializeComponent();

            this.Text = title;
            this.Width = w;
            this.Height = h;

            #pragma warning disable CS8601 // Possible null reference assignment.
            //drawingCanvas = this.pictureBox1;
            var mouseState = new BasicMouseState(this);
            drawingCanvas = new DrawingCanvas(this.pictureBox1, w, h, iw, ih, mouseState, autoRepaint, doubleBuffer);
            #pragma warning restore CS8601 // Possible null reference assignment.


            //drawingCanvas = Init(title, w, h, iw, ih, autoRepaint, doubleBuffer);
            #pragma warning disable CS8602
            drawingCanvas.StopRefresh();
            #pragma warning restore CS8602

            var keyListener = new InkeyDlrKeyListener(drawingCanvas);

            if (autoRepaint)
            {
                drawingCanvas.StartRefresh();
            }
        }

        public virtual DrawingCanvas GetDrawingCanvas()
        {
            return drawingCanvas;
        }

        private void HandleFormClosing(object? sender, FormClosingEventArgs e)
        {

        }

        private DrawingCanvas Init(string title, int w, int h, int iw, int ih, bool autoRepaint, bool doubleBuffer)
        {
            var mouseState = new BasicMouseState(this);
            drawingCanvas.StopRefresh();
            var keyListener = new InkeyDlrKeyListener(drawingCanvas);

            this.Text = title;

            // TODO: check if I need this
            this.Width = w;
            this.Height = h;

            //AddWindowListener(new AnonymousWindowAdapter(this));
            //AddKeyListener(new InkeyDlrKeyListener(drawingCanvas));
            //SetTitle(title);

            // Don't set size here.
            //Pack();
            //SetResizable(false);
            //SetLocationRelativeTo(null);
            //SetDefaultCloseOperation(JFrame.EXIT_ON_CLOSE);
            if (autoRepaint)
            {
                //drawingCanvas.StartRefresh();
            }

            return drawingCanvas;
        }

        //    private sealed class AnonymousWindowAdapter : WindowAdapter
        //    {
        //        public AnonymousWindowAdapter(BasicFrame parent)
        //        {
        //            this.parent = parent;
        //        }

        //        private readonly BasicFrame parent;
        //        public void WindowClosing(WindowEvent e)
        //        {
        //            drawingCanvas.StopRefresh();
        //        }
        //    }
    }

    public sealed class GraphicsUtil
    {
        public static readonly int MAX_WIDTH = 4000;
        public static readonly int MAX_HEIGHT = 4000;
        private static readonly int REFRESH_MILLIS = 40;
        private static readonly int KEY_BUFFER_SIZE = 16;
        public static readonly string PUT_XOR = "XOR";
        private static readonly string PUT_OR = "OR";
        private static readonly string PUT_AND = "AND";
        private static readonly string PUT_PSET = "PSET";
        private static readonly string PUT_MIX = "MIX";
        public static readonly int BUFFER_NUM_FRONT = 0;
        public static readonly int BUFFER_NUM_BACK1 = 1;

        private static void CopyRect(int[] srcArray, int srcx, int srcy, int srcWidth, 
            int[] dstArray, int dstx, int dsty, int dstWidth, 
            int copyW, int copyH)
        {
            var s = srcArray[0];
            int minWidth = (srcy + copyH) * copyW;

            if (dstArray.Length < minWidth)
                Array.Resize(ref dstArray, minWidth);

            int srcVerticalOffset = srcy * srcWidth;
            int dstVerticalOffset = dsty * dstWidth;
            for (int yi = srcy; yi < srcy + copyH; yi++)
            {
                Array.Copy(srcArray, srcVerticalOffset + srcx, dstArray, dstVerticalOffset + dstx, copyW);
                srcVerticalOffset += srcWidth;
                dstVerticalOffset += dstWidth;
            }
        }

        private interface ICanvas
        {
            System.Drawing.Bitmap GetFront();
            Bitmap GetBack1();
            Graphics GetFrontGraphics2D();
            Graphics GetBackGraphics2D();

            Color GetFrontGraphicsColor();
            Color GetBackGraphicsColor();

            void SetFrontGraphicsColor(Color color);
            void SetBackGraphicsColor(Color color);

            public Font GetFont();

            public void SetFont(Font font);


            Image this[int bufferNumber] => Get(bufferNumber); 

            Image Get(int bufferNumber)
            {
                if (bufferNumber == BUFFER_NUM_FRONT)
                {
                    return GetFront();
                }
                else if (bufferNumber == BUFFER_NUM_BACK1)
                {
                    return GetBack1();
                }
                else
                {
                    throw new PuffinBasicInternalError("Bad bufferNumber: " + bufferNumber);
                }
            }

            Graphics GetGraphics(int bufferNumber)
            {
                if (bufferNumber == BUFFER_NUM_FRONT)
                    return GetFrontGraphics2D();
                else if (bufferNumber == BUFFER_NUM_BACK1)
                    return GetBackGraphics2D();
                else
                    throw new PuffinBasicInternalError("Bad bufferNumber: " + bufferNumber);
            }

            void PrepareToRender();
        }

        internal sealed class SingleImageCanvas : ICanvas
        {

            private readonly Bitmap image;
            private readonly Graphics graphics;
            private Color color;
            private Font font;
            public SingleImageCanvas(int imageWidth, int imageHeight)
            {
                this.image = new Bitmap(imageWidth, imageHeight, PixelFormat.Format32bppArgb);
                this.graphics = Graphics.FromImage(this.image);
            }

            public Font GetFont() => font;

            public void SetFont(Font font) => this.font = font;

            public Bitmap GetBack1()
            {
                return image;
            }

            public Bitmap GetFront()
            {
                return image;
            }

            public Graphics GetFrontGraphics2D()
            {
                return graphics;
            }

            public Color GetFrontGraphicsColor()
            {
                return color;
            }

            public Color GetBackGraphicsColor()
            { 
                return color;
            }

            public void SetFrontGraphicsColor(Color color)
            {
                this.color = color;
            }

            public void SetBackGraphicsColor(Color color)
            {
                this.color = color;
            }

            public Graphics GetBackGraphics2D()
            {
                return graphics;
            }

            public void PrepareToRender()
            {
            }
        }

        internal sealed class DoubleBufferedImageCanvas : ICanvas
        {
            private readonly Bitmap[] images = new Bitmap[2];
            private readonly Graphics[] graphics = new Graphics[2];
            private readonly Color[] color = new Color[2];
            private int imageIndex;

            private Font font = SystemFonts.DefaultFont;

            public DoubleBufferedImageCanvas(int imageWidth, int imageHeight)
            {
                this.images[0] = new Bitmap(imageWidth, imageHeight, PixelFormat.Format32bppArgb);
                this.images[1] = new Bitmap(imageWidth, imageHeight, PixelFormat.Format32bppArgb);
                this.graphics[0] = Graphics.FromImage(this.images[0]);
                this.graphics[1] = Graphics.FromImage(this.images[1]);
            }

            public Font GetFont() => font;
            public void SetFont(Font font) => this.font = font;

            public Bitmap GetBack1()
            {
                return images[imageIndex];
            }

            public Bitmap GetFront()
            {
                return images[(imageIndex + 1) % 2];
            }

            public Graphics GetBackGraphics2D()
            {
                return graphics[imageIndex];
            }

            public Graphics GetFrontGraphics2D()
            {
                return graphics[(imageIndex + 1) % 2];
            }

            public void PrepareToRender()
            {
                imageIndex = (imageIndex + 1) % 2;
            }

            public Color GetFrontGraphicsColor()
            {
                return color[(imageIndex + 1) % 2];
            }

            public Color GetBackGraphicsColor()
            {
                return color[imageIndex];
            }

            public void SetFrontGraphicsColor(Color color)
            {
                this.color[(imageIndex + 1) % 2] = color;
            }

            public void SetBackGraphicsColor(Color color)
            {
                this.color[imageIndex] = color;
            }
        }

        //public class DrawingCanvas : JPanel, ActionListener
        public class DrawingCanvas
        {
            private readonly Timer timer;
            private readonly LinkedList<string> keyBuffer;
            private readonly int keyBufferSize;
            private readonly int w;
            private readonly int h;
            private readonly int iw;
            private readonly int ih;
            private readonly int[] clearBuffer;
            private readonly BasicMouseState mouseState;
            private readonly ICanvas canvas;
            private readonly HashSet<string> keysPressed;
            public readonly PictureBox pictureBox; // TODO: fix this
            private Color color;
            //public DrawingCanvas(int w, int h, int iw, int ih, int refreshMillis, int keyBufferSize, BasicMouseState mouseState, bool doubleBuffer)
            public DrawingCanvas(PictureBox pictureBox, int w, int h, int iw, int ih, BasicMouseState mouseState, bool autoRepaint, bool doubleBuffer)
            {
                this.w = w;
                this.h = h;
                this.iw = iw;
                this.ih = ih;
                this.clearBuffer = new int[w * h];
                Array.Fill(clearBuffer, 0);

                this.pictureBox = pictureBox;

                // Always use setPreferredSize() here.
                //SetPreferredSize(new Dimension(w, h));
                pictureBox.Width = w;
                pictureBox.Height = h;


                this.canvas = doubleBuffer ? new DoubleBufferedImageCanvas(iw, ih) : new SingleImageCanvas(iw, ih);
                this.timer = new Timer();
                this.keyBuffer = new LinkedList<string>();
                this.keyBufferSize = KEY_BUFFER_SIZE;
                this.mouseState = mouseState;
                this.keysPressed = new HashSet<string>();

                if (autoRepaint)
                {
                    timer.Interval = REFRESH_MILLIS;
                    timer.Tick += AutoRefresh;
                    timer.Start();
                }
            }

            private void AutoRefresh(object? sender, EventArgs e)
            {
                this.RenderAndRepaint();
            }

            public void DrawString(string text, float x, float y)
            {
                this.canvas.GetBackGraphics2D().DrawString(text,
                    this.canvas.GetFont(),
                    new SolidBrush(color),
                    x, y);
            }

            public void DrawLine(float x1, float y1, float x2, float y2)
            {
                this.canvas.GetBackGraphics2D().DrawLine(
                    new Pen(GetColor(), 5), x1, y1, x2, y2);
            }

            public void DrawRect(float x1, float y1, float x2, float y2)
            {
                this.canvas.GetBackGraphics2D().DrawRectangle(
                    new Pen(GetColor()), x1, y1, x2, y2);
            }

            public void FillRect(float x1, float y1, float x2, float y2)
            {
                this.canvas.GetBackGraphics2D().FillRectangle(
                    new SolidBrush(GetColor()), x1, y1, x2, y2);
            }

            public void DrawPath(GraphicsPath path)
            {
                this.canvas.GetBackGraphics2D().DrawPath(
                    new Pen(GetColor(), 5), path);
            }

            internal void FillOval(int sx, int sy, int w, int h)
            {
                this.canvas.GetBackGraphics2D().FillEllipse(
                    new SolidBrush(GetColor()), sx, sy, w, h);
            }

            internal void DrawOval(int sx, int sy, int w, int h)
            {
                this.canvas.GetBackGraphics2D().DrawEllipse(
                    new Pen(GetColor()), sx, sy, w, h);
            }

            internal void FillArc(int sx, int sy, int w, int h, int s, int e)
            {
                this.canvas.GetBackGraphics2D().FillPie(
                    new SolidBrush(GetColor()), sx, sy, w, h, s, e);

            }

            internal void DrawArc(int sx, int sy, int w, int h, int s, int e)
            {
                this.canvas.GetBackGraphics2D().DrawArc(
                    new Pen(GetColor(), 5), sx,sy, w, h, s, e);
            }

            // TODO: rework everything to use the picture box font perhaps?
            public Font Font { get => canvas.GetFont(); set => canvas.SetFont(value);  }
            public Color GetColor()
            {
                return color;
            }
            public void SetColor(Color color)
            {
                // TODO: revisit this
                this.color = color;
                this.canvas.SetBackGraphicsColor(color);
                this.canvas.SetFrontGraphicsColor(color);
            }

            // Always use setPreferredSize() here.
            public virtual int GetScreenWidth()
            {
                return w;
            }

            // Always use setPreferredSize() here.
            public virtual int GetScreenHeight()
            {
                return h;
            }

            // Always use setPreferredSize() here.
            public virtual int GetImageWidth()
            {
                return iw;
            }

            // Always use setPreferredSize() here.
            public virtual int GetImageHeight()
            {
                return ih;
            }

            // Always use setPreferredSize() here.
            public virtual BasicMouseState GetMouseState()
            {
                return mouseState;
            }

            // Always use setPreferredSize() here.
            public virtual string TakeNextKey()
            {
                lock (keyBuffer)
                {
                    if (keyBuffer.Count == 0)
                        return "";

                    var first = keyBuffer.First();
                    keyBuffer.RemoveFirst();
                    return first;
                }
            }

            // Always use setPreferredSize() here.
            public virtual void SetKeyPressed(string key)
            {
                lock (keyBuffer)
                {
                    keysPressed.Add(key);

                    if (keyBuffer.Count == 0)
                        keyBuffer.AddLast(key);
                    else if (keyBuffer.Count < keyBufferSize)
                    {
                        if (keyBuffer.Last() != key)
                        {
                            keyBuffer.AddLast(key);
                        }
                    }
                }
            }

            // Always use setPreferredSize() here.
            public virtual void SetKeyReleased(string key)
            {
                lock (keyBuffer)
                {
                    keysPressed.Remove(key);
                }
            }

            // Always use setPreferredSize() here.
            public virtual bool IsKeyPressed(string key)
            {
                lock (keyBuffer)
                {
                    return keysPressed.Contains(key);
                }
            }

            // Always use setPreferredSize() here.
            public virtual void StartRefresh()
            {
                timer.Start();
            }

            // Always use setPreferredSize() here.
            public virtual void StopRefresh()
            {
                timer.Stop();
            }

            // Always use setPreferredSize() here.
            public virtual Graphics GetGraphics2D()
            {
                return canvas.GetBackGraphics2D();
            }

            // Always use setPreferredSize() here.
            //private void Draw(java.awt.Graphics g)
            private void Draw(Graphics g)
            {
                //g.DrawImage(canvas.GetFront(), 0, 0, null);
                g.DrawImage(canvas.GetFront(), 0, 0);
            }

            private void Draw()
            {
                this.pictureBox.Image?.Dispose();
                this.pictureBox.Image = (Bitmap)(canvas.GetBack1().Clone());
            }

            // Always use setPreferredSize() here.
            //protected override void PaintComponent(java.awt.Graphics g)
            //protected void PaintComponent(Graphics g)
            //{
            //    base.PaintComponent(g);
            //    lock (this)
            //    {
            //        Draw(g);
            //    }
            //}

            // Always use setPreferredSize() here.
            //public override void ActionPerformed(ActionEvent e)
            //{
            //    Repaint();
            //}

            // Always use setPreferredSize() here.
            public virtual void FloodFill(int x, int y, int r, int g, int b)
            {
                var image = canvas.GetBack1();
                IterativeFloodFill(image, x, y, canvas.GetBackGraphicsColor(), Color.FromArgb(r, g, b));
            }

            // Always use setPreferredSize() here.
            public virtual void Point(int x, int y, int r, int g, int b)
            {
                var image = canvas.GetBack1();

                Color color;
                if (r != -1 && g != -1 && b != -1)
                {
                    color = Color.FromArgb(r, g, b);
                }
                else
                {
                    // TODO: revisit this
                    //color = graphics.GetColor();
                    color = this.color;
                }

                image.SetPixel(x, y, color);
            }

            // Always use setPreferredSize() here.
            public virtual void BufferCopyHor(int srcx, int dstx, int copyW)
            {
                var src = canvas.GetFront();
                var dst = canvas.GetBack1();

                //int[] srcArray = ((DataBufferInt)src.GetRaster().GetDataBuffer()).GetData();
                //int[] dstArray = ((DataBufferInt)dst.GetRaster().GetDataBuffer()).GetData();
                int[] srcArray = src.ToIntArray();
                int[] dstArray = dst.ToIntArray();
                CopyRect(srcArray, srcx, 0, src.Width, dstArray, dstx, 0, dst.Width, copyW, src.Height);
            }

            // Always use setPreferredSize() here.
            public virtual void CopyGraphicsToArray(int bufferNumber, int x1, int y1, int x2, int y2, int[] dest)
            {
                var image = canvas[bufferNumber] as Bitmap;
                //int[] srcArray = ((DataBufferInt)image.GetRaster().GetDataBuffer()).GetData();
                int[] srcArray = image.ToIntArray();
                int w = Math.Abs(x1 - x2);
                int h = Math.Abs(y1 - y2);
                CopyRect(srcArray, x1, y1, image.Width, dest, 0, 0, w, w, h);
            }

            // Always use setPreferredSize() here.
            public virtual void CopyArrayToGraphics(int bufferNumber, int x, int y, int w, int h, string action, int[] src, int srcx, int srcy, int scanWidth)
            {
                var image = canvas[bufferNumber] as Bitmap;

                // TODO: use bitmapdata for all of these

                //int[] dstArray = ((DataBufferInt)image.GetRaster().GetDataBuffer()).GetData();
                if (action.Equals(PUT_PSET, StringComparison.OrdinalIgnoreCase))
                {
                    var newImage = ImageUtils.FromIntArray(src, scanWidth, h);
                    //var newImage = ImageUtils.FromIntArray(src, srcx, srcy, scanWidth, h);

                    canvas.GetGraphics(bufferNumber).DrawImage(newImage, x, y);//, w, h);
                    newImage.Dispose();
                    //int[] dstArray = image.ToIntArray();
                    //CopyRect(src, srcx, srcy, scanWidth, dstArray, x, y, image.Width, w, h);
                }
                else
                {
                    //int[] dstArray = image.ToIntArray(x, y, w, h);
                    var dstArray = image.ToIntSpan(x, y, w, h);

                    int srcVertOffset = srcy * iw + srcx;
                    int dstVertOffset = 0;
                    //if (String.Compare(action, PUT_XOR, true) == 0)
                    if (action.Equals(PUT_XOR, StringComparison.OrdinalIgnoreCase))
                    {
                        for (int yi = 0; yi < h; yi++)
                        {
                            for (int xi = 0; xi < w; xi++)
                            {
                                int srcValue = src[srcVertOffset + xi];
                                int dstIdx = dstVertOffset + xi;
                                dstArray[dstIdx] = dstArray[dstIdx] ^ srcValue;
                            }

                            srcVertOffset += scanWidth;
                            dstVertOffset += w;
                        }
                    }
                    // TODO: check if I can do this with draw image composition mode instead
                    else if (action.Equals(PUT_MIX, StringComparison.OrdinalIgnoreCase))
                    {
                        for (int yi = 0; yi < h; yi++)
                        {
                            for (int xi = 0; xi < w; xi++)
                            {
                                int srcValue = src[srcVertOffset + xi];
                                if (srcValue != 0)
                                {
                                    dstArray[dstVertOffset + xi] = srcValue;
                                }
                            }

                            srcVertOffset += scanWidth;
                            dstVertOffset += w;
                        }
                    }
                    else if (action.Equals(PUT_OR, StringComparison.OrdinalIgnoreCase))
                    {
                        for (int yi = 0; yi < h; yi++)
                        {
                            for (int xi = 0; xi < w; xi++)
                            {
                                int srcValue = src[srcVertOffset + xi];
                                int dstIdx = dstVertOffset + xi;
                                dstArray[dstIdx] = dstArray[dstIdx] | srcValue;
                            }

                            srcVertOffset += scanWidth;
                            dstVertOffset += w;
                        }
                    }
                    else if (action.Equals(PUT_AND))
                    {
                        for (int yi = 0; yi < h; yi++)
                        {
                            for (int xi = 0; xi < w; xi++)
                            {
                                int srcValue = src[srcVertOffset + xi];
                                int dstIdx = dstVertOffset + xi;
                                dstArray[dstIdx] = dstArray[dstIdx] & srcValue;
                            }

                            srcVertOffset += scanWidth;
                            dstVertOffset += w;
                        }
                    }
                    else
                    {
                        throw new PuffinBasicRuntimeError(PuffinBasicRuntimeError.ErrorCode.GRAPHICS_ERROR, "Bad PUT action: " + action);
                    }

                    //var newImage = ImageUtils.FromIntArray(dstArray.ToArray(), w, h);
                    var newImage = ImageUtils.FromIntSpan(dstArray, w, h);
                    canvas.GetGraphics(bufferNumber).DrawImage(newImage, x, y);
                    newImage.Dispose();
                }
            }

            // Always use setPreferredSize() here.
            public virtual void Clear()
            {
                canvas.GetBackGraphics2D().Clear(Color.Black);
            }

            // Always use setPreferredSize() here.
            public virtual void RenderAndRepaint()
            {
                canvas.PrepareToRender();
                Draw();
                //Repaint();
            }

        }

        internal class InkeyDlrKeyListener// : KeyAdapter
        {
            private readonly DrawingCanvas drawingCanvas;
            public InkeyDlrKeyListener(DrawingCanvas drawingCanvas)
            {
                this.drawingCanvas = drawingCanvas;

                this.drawingCanvas.pictureBox.Parent.KeyDown += KeyPressed;
                this.drawingCanvas.pictureBox.Parent.KeyUp += KeyReleased;
            }

            private string GetKeyString(KeyEventArgs e)
            {
                //int charCode = e.getKeyChar();
                //int keyCode = e.getKeyCode();
                int charCode = e.KeyValue;
                int keyCode = (int)e.KeyCode;
                //if (charCode == 65535)

                var k = e.KeyCode;
                //KeysConverter kc = new KeysConverter();

                bool isPrintable = k == Keys.Back || k == Keys.Tab || k == Keys.LineFeed || k == Keys.Clear || k == Keys.Enter || k == Keys.Return
                    || (Keys.D0 <= k && k <= Keys.D9)
                    || (Keys.A <= k && k <= Keys.Z)
                    || (Keys.NumPad0 <= k && k <= Keys.NumPad9)
                    || k == Keys.Multiply || k == Keys.Add || k == Keys.Separator || k == Keys.Subtract || k == Keys.Decimal || k == Keys.Divide
                    || (Keys.Oem1 <= k && k <= Keys.OemBackslash);
 
                if (!isPrintable)
                {
                    return ((char)0) + ((char)keyCode).ToString();
                }
                else
                {
                    // Always store lower case
                    if (charCode >= 65 && charCode <= 90)
                    {
                        charCode += 32;
                    }

                    return ((char)charCode).ToString();
                }
            }

            // Always store lower case
            public void KeyPressed(object? sender, KeyEventArgs e)
            {
                drawingCanvas.SetKeyPressed(GetKeyString(e));
            }

            // Always store lower case
            public void KeyReleased(object? sender, KeyEventArgs e)
            {
                drawingCanvas.SetKeyReleased(GetKeyString(e));
            }
        }

        private static long CreatePoint(int x, int y)
        {
            return (((long)x) << 32) | y;
        }

        private static int GetX(long point)
        {
            return (int)(point >>> 32);
        }

        private static int GetY(long point)
        {
            return (int)(point & 0xffffffff);
        }

        private static void IterativeFloodFill(Bitmap image, int px, int py, Color fill, Color boundary)
        {
            var visited = new HashSet<long>();
            var queue = new Queue<long>();

            queue.Enqueue(CreatePoint(px, py));
            while (queue.Count > 0)
            {
                long point = queue.Dequeue();
                int x = GetX(point);
                int y = GetY(point);
                if (x < 0 || y < 0 || x >= image.Width || y >= image.Height || visited.Contains(point))
                {
                    continue;
                }

                var atXY = image.GetPixel(x, y);
                if (atXY.R == boundary.R && atXY.G == boundary.G && atXY.B == boundary.B)
                {
                    continue;
                }

                if (atXY.R == fill.R && atXY.G == fill.G && atXY.B == fill.B)
                {
                    continue;
                }

                visited.Add(point);
                image.SetPixel(x, y, fill);
                if (x > 0)
                {
                    var nextC = image.GetPixel(x - 1, y);
                    if (nextC.R != fill.R || nextC.G != fill.G || nextC.B != fill.B)
                    {
                        queue.Enqueue(CreatePoint(x - 1, y));
                    }
                }

                if (x < image.Width - 1)
                {
                    var nextC = image.GetPixel(x + 1, y);
                    if (nextC.R != fill.R || nextC.G != fill.G || nextC.B != fill.B)
                    {
                        queue.Enqueue(CreatePoint(x + 1, y));
                    }
                }

                if (y > 0)
                {
                    var nextC = image.GetPixel(x, y - 1);
                    if (nextC.R != fill.R || nextC.G != fill.G || nextC.B != fill.B)
                    {
                        queue.Enqueue(CreatePoint(x, y - 1));
                    }
                }

                if (y < image.Height - 1)
                {
                    var nextC = image.GetPixel(x, y + 1);
                    if (nextC.R != fill.R || nextC.G != fill.G || nextC.B != fill.B)
                    {
                        queue.Enqueue(CreatePoint(x, y + 1));
                    }
                }
            }
        }

        public sealed class BasicMouseState
        {
            //private readonly ReadWriteLock @lock;
            private static readonly ReaderWriterLock @lock = new ReaderWriterLock();
            private int buttonClicked = -1;
            private int buttonPressed = -1;
            private int buttonReleased = -1;
            private int draggedX = -1;
            private int draggedY = -1;
            private int movedX = -1;
            private int movedY = -1;
            public BasicMouseState(Control component)
            {
                //this.@lock = new ReentrantReadWriteLock();

                component.MouseDown += OnPressed;
                component.MouseUp += OnReleased;
                component.MouseMove += OnMoved;
                component.DragDrop += OnDragged;
                component.MouseClick += OnClicked;

                //component.AddMouseListener(new BasicMouseAdapter());
                //component.AddMouseMotionListener(new BasicMouseMotionAdapter());
            }

            public void OnMoved(object? sender, MouseEventArgs e)
            {
                @lock.AcquireWriterLock(100);
                try
                {
                    movedX = e.X;
                    movedY = e.Y;
                }
                finally
                {
                    @lock.ReleaseWriterLock();
                }
            }

            public void OnDragged(object? sender, DragEventArgs e)
            {
                @lock.AcquireWriterLock(100);
                try
                {
                    draggedX = e.X;
                    draggedY = e.Y;
                }
                finally
                {
                    @lock.ReleaseWriterLock();
                }
            }

            public void OnClicked(object? sender, MouseEventArgs e)
            {
                @lock.AcquireWriterLock(100);
                try
                {
                    buttonClicked = ((int)e.Button);
                }
                finally
                {
                    @lock.ReleaseWriterLock();
                }
            }

            public void OnPressed(object? sender, MouseEventArgs e)
            {
                @lock.AcquireWriterLock(100);
                try
                {
                    buttonPressed = ((int)e.Button);
                }
                finally
                {
                    @lock.ReleaseWriterLock();
                }
            }

            void OnReleased(object? sender, MouseEventArgs e)
            {
                @lock.AcquireWriterLock(100);
                try
                {
                    buttonReleased = ((int)e.Button);
                }
                finally
                {
                    @lock.ReleaseWriterLock();
                }
            }

            public int GetButtonClicked()
            {
                @lock.AcquireWriterLock(100);
                try
                {
                    var result = buttonClicked;
                    buttonClicked = -1;
                    return result;
                }
                finally
                {
                    @lock.ReleaseWriterLock();
                }
            }

            public int GetButtonPressed()
            {
                @lock.AcquireWriterLock(100);
                try
                {
                    var result = buttonPressed;
                    buttonPressed = -1;
                    return result;
                }
                finally
                {
                    @lock.ReleaseWriterLock();
                }
            }

            public int GetButtonReleased()
            {
                @lock.AcquireWriterLock(100);
                try
                {
                    var result = buttonReleased;
                    buttonReleased = -1;
                    return result;
                }
                finally
                {
                    @lock.ReleaseWriterLock();
                }
            }

            public int GetMovedX()
            {
                @lock.AcquireReaderLock(100);
                try
                {
                    return movedX;
                }
                finally
                {
                    @lock.ReleaseReaderLock();
                }
            }

            public int GetMovedY()
            {
                @lock.AcquireReaderLock(100);
                try
                {
                    return movedY;
                }
                finally
                {
                    @lock.ReleaseReaderLock();
                }
            }

            public int GetDraggedX()
            {
                @lock.AcquireReaderLock(100);
                try
                {
                    return draggedX;
                }
                finally
                {
                    @lock.ReleaseReaderLock();
                }
            }

            public int GetDraggedY()
            {
                @lock.AcquireReaderLock(100);
                try
                {
                    return draggedY;
                }
                finally
                {
                    @lock.ReleaseReaderLock();
                }
            }

            //private sealed class BasicMouseMotionAdapter : MouseMotionAdapter
            //{
            //    public override void MouseDragged(MouseEventArgs e)
            //    {
            //        OnDragged(e);
            //    }

            //    public override void MouseMoved(MouseEventArgs e)
            //    {
            //        OnMoved(e);
            //    }
            //}

            //private sealed class BasicMouseAdapter : MouseAdapter
            //{
            //    public override void MouseClicked(MouseEvent e)
            //    {
            //        OnClicked(e);
            //    }

            //    public override void MousePressed(MouseEvent e)
            //    {
            //        OnPressed(e);
            //    }

            //    public override void MouseReleased(MouseEvent e)
            //    {
            //        OnReleased(e);
            //    }
            //}
        }
    }

    public static class ImageUtils
    {
        public static Bitmap FromIntSpan(Span<int> span, int width, int height)
        {
            const int imageHeaderSize = 54;
            int fileSize = (span.Length * 4) + imageHeaderSize;
            byte[] header = new byte[imageHeaderSize];
            header[0] = (byte)'B';
            header[1] = (byte)'M';

            BitConverter.GetBytes(fileSize)       .CopyTo(header, 0x02);
            BitConverter.GetBytes(imageHeaderSize).CopyTo(header, 0x0A);
            BitConverter.GetBytes(40)             .CopyTo(header, 0x0E);
            BitConverter.GetBytes(width)          .CopyTo(header, 0x12);
            BitConverter.GetBytes(height)         .CopyTo(header, 0x16);
            BitConverter.GetBytes(32)             .CopyTo(header, 0x1C);
            BitConverter.GetBytes(width * height) .CopyTo(header, 0x22);

            using (MemoryStream ms = new MemoryStream())
            {
                ms.Write(header);
                ms.Write(MemoryMarshal.AsBytes(span));

                Bitmap bmp = Bitmap.FromStream(ms) as Bitmap;
                return bmp;
            }
        }

        public static Bitmap FromIntArray(int[] array, int width, int height)
        {
            // https://swharden.com/blog/2022-11-04-csharp-create-bitmap/
            return FromIntSpan(array.AsSpan(), width, height);

            const int imageHeaderSize = 54;
            byte[] bytes = new byte[(array.Length * 4) + imageHeaderSize];
            bytes[0] = (byte)'B';
            bytes[1] = (byte)'M';

            BitConverter.GetBytes(bytes.Length)   .CopyTo(bytes, 0x02);
            BitConverter.GetBytes(imageHeaderSize).CopyTo(bytes, 0x0A);
            BitConverter.GetBytes(40)             .CopyTo(bytes, 0x0E);
            BitConverter.GetBytes(width)          .CopyTo(bytes, 0x12);
            BitConverter.GetBytes(height)         .CopyTo(bytes, 0x16);
            BitConverter.GetBytes(32)             .CopyTo(bytes, 0x1C);
            BitConverter.GetBytes(width * height) .CopyTo(bytes, 0x22);

            using (var ms = new MemoryStream(bytes))
            {
                using (var br = new BinaryWriter(ms))
                {
                    br.BaseStream.Position = 54;

                    foreach (var p in array)
                        br.Write(p);

                    Bitmap bmp = Bitmap.FromStream(ms) as Bitmap;

                    return bmp;
                }

            }
        }

        public static int[] ToIntArray(this Bitmap image, int x, int y, int w, int h)
        {
            using var subImage = image.Clone(new RectangleF((float)x, (float)y, (float)w, (float)h), PixelFormat.Format32bppArgb);

            return subImage.ToIntArray();
        }

        public static Span<int> ToIntSpan(this Bitmap image, int x, int y, int w, int h)
        {
            using var subImage = image.Clone(new RectangleF((float)x, (float)y, (float)w, (float)h), PixelFormat.Format32bppArgb);

            return subImage.ToIntSpan();
        }

        public static Span<int> ToIntSpan(this Bitmap image)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                image.Save(ms, ImageFormat.Bmp);
                var bytes = ms.ToArray().AsSpan(54);
                return MemoryMarshal.Cast<byte, int>(bytes);
            }
        }

        public static int[] ToIntArray(this Bitmap image)
        {
            return ToIntSpan(image).ToArray();

            using (MemoryStream ms = new MemoryStream())
            {
                image.Save(ms, ImageFormat.Bmp);
                int[] ints = new int[image.Width * image.Height];

                using (BinaryReader br = new BinaryReader(ms)) {
                    br.BaseStream.Position = 54;

                    for (int i = 0; i < ints.Length; i++)
                        ints[i] = br.ReadInt32();
                }

                return ints;
            }
        }
    }
}

