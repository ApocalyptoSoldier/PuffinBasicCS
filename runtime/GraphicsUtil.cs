using It.Unimi.Dsi.Fastutil.Longs;
using It.Unimi.Dsi.Fastutil.Objects;
using Org.Puffinbasic.Error;
using Javax.Swing;
using Java.Awt;
using Java.Awt.Event;
using Java.Awt.Image;
using Java.Util;
using Java.Util.Concurrent.Locks;
using Org.Puffinbasic.Error.PuffinBasicRuntimeError.ErrorCode;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;

namespace Org.Puffinbasic.Runtime
{
    public sealed class GraphicsUtil
    {
        static readonly int MAX_WIDTH = 4000;
        static readonly int MAX_HEIGHT = 4000;
        private static readonly int REFRESH_MILLIS = 40;
        private static readonly int KEY_BUFFER_SIZE = 16;
        static readonly string PUT_XOR = "XOR";
        private static readonly string PUT_OR = "OR";
        private static readonly string PUT_AND = "AND";
        private static readonly string PUT_PSET = "PSET";
        private static readonly string PUT_MIX = "MIX";
        public static readonly int BUFFER_NUM_FRONT = 0;
        public static readonly int BUFFER_NUM_BACK1 = 1;
        class BasicFrame : JFrame
        {
            private readonly DrawingCanvas drawingCanvas;
            BasicFrame(string title, int w, int h, int iw, int ih, bool autoRepaint, bool doubleBuffer)
            {
                drawingCanvas = Init(title, w, h, iw, ih, autoRepaint, doubleBuffer);
            }

            virtual DrawingCanvas GetDrawingCanvas()
            {
                return drawingCanvas;
            }

            private DrawingCanvas Init(string title, int w, int h, int iw, int ih, bool autoRepaint, bool doubleBuffer)
            {
                var mouseState = new BasicMouseState(this);
                var drawingCanvas = new DrawingCanvas(w, h, iw, ih, REFRESH_MILLIS, KEY_BUFFER_SIZE, mouseState, doubleBuffer);
                Add(drawingCanvas);
                AddWindowListener(new AnonymousWindowAdapter(this));
                AddKeyListener(new InkeyDlrKeyListener(drawingCanvas));
                SetTitle(title);

                // Don't set size here.
                Pack();
                SetResizable(false);
                SetLocationRelativeTo(null);
                SetDefaultCloseOperation(JFrame.EXIT_ON_CLOSE);
                if (autoRepaint)
                {
                    drawingCanvas.StartRefresh();
                }

                return drawingCanvas;
            }

            private sealed class AnonymousWindowAdapter : WindowAdapter
            {
                public AnonymousWindowAdapter(BasicFrame parent)
                {
                    this.parent = parent;
                }

                private readonly BasicFrame parent;
                public void WindowClosing(WindowEvent e)
                {
                    drawingCanvas.StopRefresh();
                }
            }
        }

        private static void CopyRect(int[] srcArray, int srcx, int srcy, int srcWidth, int[] dstArray, int dstx, int dsty, int dstWidth, int copyW, int copyH)
        {
            int srcVerticalOffset = srcy * srcWidth;
            int dstVerticalOffset = dsty * dstWidth;
            for (int yi = srcy; yi < srcy + copyH; yi++)
            {
                System.Arraycopy(srcArray, srcVerticalOffset + srcx, dstArray, dstVerticalOffset + dstx, copyW);
                srcVerticalOffset += srcWidth;
                dstVerticalOffset += dstWidth;
            }
        }

        private interface ICanvas
        {
            BufferedImage GetFront();
            BufferedImage GetBack1();
            Graphics2D GetFrontGraphics2D();
            Graphics2D GetBackGraphics2D();
            BufferedImage Get(int bufferNumber)
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

            void PrepareToRender();
        }

        private sealed class SingleImageCanvas : ICanvas
        {
            private readonly BufferedImage image;
            private readonly Graphics2D graphics;
            SingleImageCanvas(int imageWidth, int imageHeight)
            {
                this.image = new BufferedImage(imageWidth, imageHeight, BufferedImage.TYPE_INT_RGB);
                this.graphics = (Graphics2D)image.GetGraphics();
            }

            public BufferedImage GetBack1()
            {
                return image;
            }

            public BufferedImage GetFront()
            {
                return image;
            }

            public Graphics2D GetFrontGraphics2D()
            {
                return graphics;
            }

            public Graphics2D GetBackGraphics2D()
            {
                return graphics;
            }

            public void PrepareToRender()
            {
            }
        }

        private sealed class DoubleBufferedImageCanvas : ICanvas
        {
            private readonly BufferedImage[] images;
            private readonly Graphics2D[] graphics;
            private int imageIndex;
            DoubleBufferedImageCanvas(int imageWidth, int imageHeight)
            {
                this.images = new BufferedImage[2];
                this.images[0] = new BufferedImage(imageWidth, imageHeight, BufferedImage.TYPE_INT_RGB);
                this.images[1] = new BufferedImage(imageWidth, imageHeight, BufferedImage.TYPE_INT_RGB);
                this.graphics = new Graphics2D[2];
                this.graphics[0] = (Graphics2D)images[0].GetGraphics();
                this.graphics[1] = (Graphics2D)images[1].GetGraphics();
            }

            public BufferedImage GetBack1()
            {
                return images[imageIndex];
            }

            public BufferedImage GetFront()
            {
                return images[(imageIndex + 1) % 2];
            }

            public Graphics2D GetBackGraphics2D()
            {
                return graphics[imageIndex];
            }

            public Graphics2D GetFrontGraphics2D()
            {
                return graphics[(imageIndex + 1) % 2];
            }

            public void PrepareToRender()
            {
                imageIndex = (imageIndex + 1) % 2;
            }
        }

        class DrawingCanvas : JPanel, ActionListener
        {
            private readonly Timer timer;
            private readonly Deque<string> keyBuffer;
            private readonly int keyBufferSize;
            private readonly int w;
            private readonly int h;
            private readonly int iw;
            private readonly int ih;
            private readonly int[] clearBuffer;
            private readonly BasicMouseState mouseState;
            private readonly ICanvas canvas;
            private readonly ObjectSet<string> keysPressed;
            DrawingCanvas(int w, int h, int iw, int ih, int refreshMillis, int keyBufferSize, BasicMouseState mouseState, bool doubleBuffer)
            {
                this.w = w;
                this.h = h;
                this.iw = iw;
                this.ih = ih;
                this.clearBuffer = new int[w * h];
                Arrays.Fill(clearBuffer, 0);

                // Always use setPreferredSize() here.
                SetPreferredSize(new Dimension(w, h));
                this.canvas = doubleBuffer ? new DoubleBufferedImageCanvas(iw, ih) : new SingleImageCanvas(iw, ih);
                this.timer = new Timer(refreshMillis, this);
                this.keyBuffer = new ArrayDeque();
                this.keyBufferSize = keyBufferSize;
                this.mouseState = mouseState;
                this.keysPressed = new ObjectOpenHashSet();
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
            virtual string TakeNextKey()
            {
                lock (keyBuffer)
                {
                    return keyBuffer.IsEmpty() ? "" : keyBuffer.RemoveFirst();
                }
            }

            // Always use setPreferredSize() here.
            virtual void SetKeyPressed(string key)
            {
                lock (keyBuffer)
                {
                    keysPressed.Add(key);
                    var lastKey = !keyBuffer.IsEmpty() ? keyBuffer.GetLast() : null;
                    if (!key.Equals(lastKey) && keyBuffer.Count < keyBufferSize)
                    {
                        keyBuffer.Add(key);
                    }
                }
            }

            // Always use setPreferredSize() here.
            virtual void SetKeyReleased(string key)
            {
                lock (keyBuffer)
                {
                    keysPressed.Remove(key);
                }
            }

            // Always use setPreferredSize() here.
            virtual bool IsKeyPressed(string key)
            {
                lock (keyBuffer)
                {
                    return keysPressed.Contains(key);
                }
            }

            // Always use setPreferredSize() here.
            virtual void StartRefresh()
            {
                timer.Start();
            }

            // Always use setPreferredSize() here.
            virtual void StopRefresh()
            {
                timer.Stop();
            }

            // Always use setPreferredSize() here.
            virtual Graphics2D GetGraphics2D()
            {
                return canvas.GetBackGraphics2D();
            }

            // Always use setPreferredSize() here.
            private void Draw(java.awt.Graphics g)
            {
                g.DrawImage(canvas.GetFront(), 0, 0, null);
            }

            // Always use setPreferredSize() here.
            protected override void PaintComponent(java.awt.Graphics g)
            {
                base.PaintComponent(g);
                lock (this)
                {
                    Draw(g);
                }
            }

            // Always use setPreferredSize() here.
            public override void ActionPerformed(ActionEvent e)
            {
                Repaint();
            }

            // Always use setPreferredSize() here.
            virtual void FloodFill(int x, int y, int r, int g, int b)
            {
                var image = canvas.GetBack1();
                IterativeFloodFill(image, x, y, canvas.GetBackGraphics2D().GetColor(), new Color(r, g, b));
            }

            // Always use setPreferredSize() here.
            virtual void Point(int x, int y, int r, int g, int b)
            {
                var image = canvas.GetBack1();
                var graphics = image.GetGraphics();
                Color color;
                if (r != -1 && g != -1 && b != -1)
                {
                    color = new Color(r, g, b);
                }
                else
                {
                    color = graphics.GetColor();
                }

                image.SetRGB(x, y, color.GetRGB());
            }

            // Always use setPreferredSize() here.
            virtual void BufferCopyHor(int srcx, int dstx, int copyW)
            {
                var src = canvas.GetFront();
                var dst = canvas.GetBack1();
                int[] srcArray = ((DataBufferInt)src.GetRaster().GetDataBuffer()).GetData();
                int[] dstArray = ((DataBufferInt)dst.GetRaster().GetDataBuffer()).GetData();
                CopyRect(srcArray, srcx, 0, src.GetWidth(), dstArray, dstx, 0, dst.GetWidth(), copyW, src.GetHeight());
            }

            // Always use setPreferredSize() here.
            virtual void CopyGraphicsToArray(int bufferNumber, int x1, int y1, int x2, int y2, int[] dest)
            {
                var image = canvas[bufferNumber];
                int[] srcArray = ((DataBufferInt)image.GetRaster().GetDataBuffer()).GetData();
                int w = Math.Abs(x1 - x2);
                int h = Math.Abs(y1 - y2);
                CopyRect(srcArray, x1, y1, image.GetWidth(), dest, 0, 0, w, w, h);
            }

            // Always use setPreferredSize() here.
            virtual void CopyArrayToGraphics(int bufferNumber, int x, int y, int w, int h, string action, int[] src, int srcx, int srcy, int scanWidth)
            {
                var image = canvas[bufferNumber];
                int[] dstArray = ((DataBufferInt)image.GetRaster().GetDataBuffer()).GetData();
                if (action.EqualsIgnoreCase(PUT_PSET))
                {
                    CopyRect(src, srcx, srcy, scanWidth, dstArray, x, y, image.GetWidth(), w, h);
                }
                else
                {
                    int srcVertOffset = srcy * iw + srcx;
                    int dstVertOffset = y * iw;
                    if (action.EqualsIgnoreCase(PUT_XOR))
                    {
                        for (int yi = 0; yi < h; yi++)
                        {
                            for (int xi = 0; xi < w; xi++)
                            {
                                int srcValue = src[srcVertOffset + xi];
                                int dstIdx = dstVertOffset + x + xi;
                                dstArray[dstIdx] = dstArray[dstIdx] ^ srcValue;
                            }

                            srcVertOffset += scanWidth;
                            dstVertOffset += iw;
                        }
                    }
                    else if (action.EqualsIgnoreCase(PUT_MIX))
                    {
                        for (int yi = 0; yi < h; yi++)
                        {
                            for (int xi = 0; xi < w; xi++)
                            {
                                int srcValue = src[srcVertOffset + xi];
                                if (srcValue != 0)
                                {
                                    dstArray[dstVertOffset + x + xi] = srcValue;
                                }
                            }

                            srcVertOffset += scanWidth;
                            dstVertOffset += iw;
                        }
                    }
                    else if (action.EqualsIgnoreCase(PUT_OR))
                    {
                        for (int yi = 0; yi < h; yi++)
                        {
                            for (int xi = 0; xi < w; xi++)
                            {
                                int srcValue = src[srcVertOffset + xi];
                                int dstIdx = dstVertOffset + x + xi;
                                dstArray[dstIdx] = dstArray[dstIdx] | srcValue;
                            }

                            srcVertOffset += scanWidth;
                            dstVertOffset += iw;
                        }
                    }
                    else if (action.Equals(PUT_AND))
                    {
                        for (int yi = 0; yi < h; yi++)
                        {
                            for (int xi = 0; xi < w; xi++)
                            {
                                int srcValue = src[srcVertOffset + xi];
                                int dstIdx = dstVertOffset + x + xi;
                                dstArray[dstIdx] = dstArray[dstIdx] & srcValue;
                            }

                            srcVertOffset += scanWidth;
                            dstVertOffset += iw;
                        }
                    }
                    else
                    {
                        throw new PuffinBasicRuntimeError(GRAPHICS_ERROR, "Bad PUT action: " + action);
                    }
                }
            }

            // Always use setPreferredSize() here.
            virtual void Clear()
            {
                var image = canvas.GetBack1();
                image.SetRGB(0, 0, w, h, clearBuffer, 0, w);
            }

            // Always use setPreferredSize() here.
            virtual void RenderAndRepaint()
            {
                canvas.PrepareToRender();
                Repaint();
            }
        }

        private class InkeyDlrKeyListener : KeyAdapter
        {
            private readonly DrawingCanvas drawingCanvas;
            InkeyDlrKeyListener(DrawingCanvas drawingCanvas)
            {
                this.drawingCanvas = drawingCanvas;
            }

            private string GetKeyString(KeyEvent e)
            {
                int charCode = e.GetKeyChar();
                int keyCode = e.GetKeyCode();
                if (charCode == 65535)
                {
                    return ((char)0) + String.ValueOf((char)keyCode);
                }
                else
                {

                    // Always store lower case
                    if (charCode >= 65 && charCode <= 90)
                    {
                        charCode += 32;
                    }

                    return String.ValueOf((char)charCode);
                }
            }

            // Always store lower case
            public override void KeyPressed(KeyEvent e)
            {
                drawingCanvas.SetKeyPressed(GetKeyString(e));
            }

            // Always store lower case
            public override void KeyReleased(KeyEvent e)
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

        private static void IterativeFloodFill(BufferedImage image, int px, int py, Color fill, Color boundary)
        {
            var visited = new LongOpenHashSet();
            var queue = new LongArrayFIFOQueue();
            queue.Enqueue(CreatePoint(px, py));
            while (!queue.IsEmpty())
            {
                long point = queue.DequeueLong();
                int x = GetX(point);
                int y = GetY(point);
                if (x < 0 || y < 0 || x >= image.GetWidth() || y >= image.GetHeight() || visited.Contains(point))
                {
                    continue;
                }

                var atXY = new Color(image.GetRGB(x, y));
                if (atXY.GetRed() == boundary.GetRed() && atXY.GetGreen() == boundary.GetGreen() && atXY.GetBlue() == boundary.GetBlue())
                {
                    continue;
                }

                if (atXY.GetRed() == fill.GetRed() && atXY.GetGreen() == fill.GetGreen() && atXY.GetBlue() == fill.GetBlue())
                {
                    continue;
                }

                visited.Add(point);
                image.SetRGB(x, y, fill.GetRGB());
                if (x > 0)
                {
                    var nextC = new Color(image.GetRGB(x - 1, y));
                    if (nextC.GetRed() != fill.GetRed() || nextC.GetGreen() != fill.GetGreen() || nextC.GetBlue() != fill.GetBlue())
                    {
                        queue.Enqueue(CreatePoint(x - 1, y));
                    }
                }

                if (x < image.GetWidth() - 1)
                {
                    var nextC = new Color(image.GetRGB(x + 1, y));
                    if (nextC.GetRed() != fill.GetRed() || nextC.GetGreen() != fill.GetGreen() || nextC.GetBlue() != fill.GetBlue())
                    {
                        queue.Enqueue(CreatePoint(x + 1, y));
                    }
                }

                if (y > 0)
                {
                    var nextC = new Color(image.GetRGB(x, y - 1));
                    if (nextC.GetRed() != fill.GetRed() || nextC.GetGreen() != fill.GetGreen() || nextC.GetBlue() != fill.GetBlue())
                    {
                        queue.Enqueue(CreatePoint(x, y - 1));
                    }
                }

                if (y < image.GetHeight() - 1)
                {
                    var nextC = new Color(image.GetRGB(x, y + 1));
                    if (nextC.GetRed() != fill.GetRed() || nextC.GetGreen() != fill.GetGreen() || nextC.GetBlue() != fill.GetBlue())
                    {
                        queue.Enqueue(CreatePoint(x, y + 1));
                    }
                }
            }
        }

        sealed class BasicMouseState
        {
            private readonly ReadWriteLock lock;
            private int buttonClicked = -1;
            private int buttonPressed = -1;
            private int buttonReleased = -1;
            private int draggedX = -1;
            private int draggedY = -1;
            private int movedX = -1;
            private int movedY = -1;
            BasicMouseState(Component component)
            {
                this.@lock = new ReentrantReadWriteLock();
                component.AddMouseListener(new BasicMouseAdapter());
                component.AddMouseMotionListener(new BasicMouseMotionAdapter());
            }

            void OnMoved(MouseEvent e)
            {
                @lock.WriteLock().Lock();
                try
                {
                    movedX = e.GetX();
                    movedY = e.GetY();
                }
                finally
                {
                    @lock.WriteLock().Unlock();
                }
            }

            void OnDragged(MouseEvent e)
            {
                @lock.WriteLock().Lock();
                try
                {
                    draggedX = e.GetX();
                    draggedY = e.GetY();
                }
                finally
                {
                    @lock.WriteLock().Unlock();
                }
            }

            void OnClicked(MouseEvent e)
            {
                @lock.WriteLock().Lock();
                try
                {
                    buttonClicked = e.GetButton();
                }
                finally
                {
                    @lock.WriteLock().Unlock();
                }
            }

            void OnPressed(MouseEvent e)
            {
                @lock.WriteLock().Lock();
                try
                {
                    buttonPressed = e.GetButton();
                }
                finally
                {
                    @lock.WriteLock().Unlock();
                }
            }

            void OnReleased(MouseEvent e)
            {
                @lock.WriteLock().Lock();
                try
                {
                    buttonReleased = e.GetButton();
                }
                finally
                {
                    @lock.WriteLock().Unlock();
                }
            }

            int GetButtonClicked()
            {
                @lock.WriteLock().Lock();
                try
                {
                    var result = buttonClicked;
                    buttonClicked = -1;
                    return result;
                }
                finally
                {
                    @lock.WriteLock().Unlock();
                }
            }

            int GetButtonPressed()
            {
                @lock.WriteLock().Lock();
                try
                {
                    var result = buttonPressed;
                    buttonPressed = -1;
                    return result;
                }
                finally
                {
                    @lock.WriteLock().Unlock();
                }
            }

            int GetButtonReleased()
            {
                @lock.WriteLock().Lock();
                try
                {
                    var result = buttonReleased;
                    buttonReleased = -1;
                    return result;
                }
                finally
                {
                    @lock.WriteLock().Unlock();
                }
            }

            int GetMovedX()
            {
                @lock.ReadLock().Lock();
                try
                {
                    return movedX;
                }
                finally
                {
                    @lock.ReadLock().Unlock();
                }
            }

            int GetMovedY()
            {
                @lock.ReadLock().Lock();
                try
                {
                    return movedY;
                }
                finally
                {
                    @lock.ReadLock().Unlock();
                }
            }

            int GetDraggedX()
            {
                @lock.ReadLock().Lock();
                try
                {
                    return draggedX;
                }
                finally
                {
                    @lock.ReadLock().Unlock();
                }
            }

            int GetDraggedY()
            {
                @lock.ReadLock().Lock();
                try
                {
                    return draggedY;
                }
                finally
                {
                    @lock.ReadLock().Unlock();
                }
            }

            private sealed class BasicMouseMotionAdapter : MouseMotionAdapter
            {
                public override void MouseDragged(MouseEvent e)
                {
                    OnDragged(e);
                }

                public override void MouseMoved(MouseEvent e)
                {
                    OnMoved(e);
                }
            }

            private sealed class BasicMouseAdapter : MouseAdapter
            {
                public override void MouseClicked(MouseEvent e)
                {
                    OnClicked(e);
                }

                public override void MousePressed(MouseEvent e)
                {
                    OnPressed(e);
                }

                public override void MouseReleased(MouseEvent e)
                {
                    OnReleased(e);
                }
            }
        }
    }
}

