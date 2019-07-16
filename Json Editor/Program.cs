using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using ZTn.Json.Editor.Forms;

namespace ZTn.Json.Editor
{
    static class Program
    {
        [DllImport("Shcore.dll")]
        static extern int SetProcessDpiAwareness(int PROCESS_DPI_AWARENESS);

        // According to https://msdn.microsoft.com/en-us/library/windows/desktop/dn280512(v=vs.85).aspx
        private enum DpiAwareness
        {
            None = 0,
            SystemAware = 1,
            PerMonitorAware = 2
        }
        /// <summary>
        /// Point d'entrée principal de l'application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            try
            {
                SetProcessDpiAwareness((int)DpiAwareness.PerMonitorAware);
            }
            catch (Exception)
            {
            }
            Application.Run(new MainForm());
        }
    }
}
