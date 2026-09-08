namespace PuffinBasicUIWinForms
{
    using PuffinBasicCS;

    using System;
    using System.Windows.Forms;

    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main(params string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            var graphicsRuntime = new GraphicsRuntime();
            PuffinBasicCS.Runtime.GraphicsRuntime.Implementation = graphicsRuntime;

            PuffinBasicInterpreterMain.Main(args);

            //Application.Run(graphicsRuntime.graphicsState.GetFrame());
        }
    }
}
