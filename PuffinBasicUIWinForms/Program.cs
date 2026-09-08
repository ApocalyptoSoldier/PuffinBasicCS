using Org.Puffinbasic;
using Org.Puffinbasic.Runtime;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DrawingQuickstartWinForms
{
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

            var graphicsRuntime = new PuffinBasicUI.GraphicsRuntime();
            Org.Puffinbasic.Runtime.GraphicsRuntime.Implementation = graphicsRuntime;

            PuffinBasicInterpreterMain.Main(args);

            //Application.Run(graphicsRuntime.graphicsState.GetFrame());
        }
    }
}
