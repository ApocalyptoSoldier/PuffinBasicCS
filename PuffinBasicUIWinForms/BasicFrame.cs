namespace PuffinBasicUI
{
    using System;
    using System.Drawing;
    using System.Windows.Forms;

    public partial class BasicFrame : Form
    {
        public BasicFrame()
        {
            InitializeComponent();
            Render();
        }

        public BasicFrame(string title, int width, int height) {
            InitializeComponent();
            this.Text = title;
            this.Width = width + this.DefaultPadding.Right + this.DefaultPadding.Left;
            this.Height = height + this.DefaultPadding.Top + this.DefaultPadding.Bottom;
        }

        public void Render(Bitmap bmp)
        {
            // copy the bitmap to the picturebox (double buffered)
            pictureBox1.Image?.Dispose();
            pictureBox1.Image = (Bitmap)bmp.Clone();
        }

        public void Render()
        {
            Random rand = new Random();
            using (var bmp = new Bitmap(pictureBox1.Width, pictureBox1.Height))
            using (var gfx = Graphics.FromImage(bmp))
            using (var pen = new Pen(Color.White))
            {
                // draw one thousand random white lines on a dark blue background
                gfx.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                gfx.Clear(Color.Navy);
                for (int i = 0; i < 1000; i++)
                {
                    var pt1 = new Point(rand.Next(bmp.Width), rand.Next(bmp.Height));
                    var pt2 = new Point(rand.Next(bmp.Width), rand.Next(bmp.Height));
                    gfx.DrawLine(pen, pt1, pt2);
                }

                // copy the bitmap to the picturebox (double buffered)
                pictureBox1.Image?.Dispose();
                pictureBox1.Image = (Bitmap)bmp.Clone();
            }
        }

        private void pictureBox1_SizeChanged(object sender, EventArgs e)
        {
            //Render();
        }

        private void pictureBox1_MouseDown(object sender, MouseEventArgs e)
        {
            //Render();
        }
    }
}
