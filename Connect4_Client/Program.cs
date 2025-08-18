using System;
using System.Windows.Forms;

namespace Connect4_Client
{
    // WinForms entry point.
    // NOTE: The server may launch the client with CLI args:
    //   --gameId=<id> --playerId=<external-id> --api="<base-url>"
    // We keep Program.Main minimal; Form1 can read Environment.GetCommandLineArgs() if needed.
    internal static class Program
    {
        /// <summary>The main entry point for the application.</summary>
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // If Form1 supports parsing CLI args internally, it can read Environment.GetCommandLineArgs().
            // We intentionally do not change the constructor signature to avoid altering behavior.
            Application.Run(new Form1());
        }
    }
}
