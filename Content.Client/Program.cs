using System.Threading;
using Robust.Client;

namespace Content.Client
{
    internal static class Program
    {
        // Main thread has to be STA on Windows, or OLE-dependent things (clipboard, IME,
        // native file dialogs, CEF) break. Clyde logs an error about this during SDL3 init.
        [STAThread]
        public static void Main(string[] args)
        {
            ContentStart.Start(args);
        }
    }
}
