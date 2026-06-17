using System;
using System.Threading;
using WpfApplication = System.Windows.Application;

namespace PromptBar
{
    internal static class Program
    {
        private const string SingleInstanceMutexName = "PromptBar.SingleInstance";

        [STAThread]
        private static void Main()
        {
            bool ownsMutex;
            using (Mutex mutex = new Mutex(true, SingleInstanceMutexName, out ownsMutex))
            {
                if (!ownsMutex)
                {
                    System.Windows.Forms.MessageBox.Show(
                        "PromptBar is already running. Use the tray icon to open settings or quit.",
                        "PromptBar",
                        System.Windows.Forms.MessageBoxButtons.OK,
                        System.Windows.Forms.MessageBoxIcon.Information);
                    return;
                }

                RunApplication();
            }
        }

        private static void RunApplication()
        {
            try
            {
                NativeMethods.SetProcessDPIAware();
            }
            catch
            {
            }

            System.Windows.Forms.Application.EnableVisualStyles();

            WpfApplication app = new WpfApplication();
            app.ShutdownMode = System.Windows.ShutdownMode.OnExplicitShutdown;

            AppController controller = new AppController(app);
            app.Startup += delegate { controller.Start(); };
            app.Exit += delegate { controller.Dispose(); };
            app.Run();
        }
    }
}
