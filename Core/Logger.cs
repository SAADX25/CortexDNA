using System;
using System.IO;

namespace CortexDNA.Core
{
    public static class Logger
    {
        private static readonly object _sync = new();
        private static readonly string LogPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "log.txt");

        public static void Log(string message)
        {
            try
            {
                string logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}";
                lock (_sync)
                {
                    File.AppendAllText(LogPath, logEntry);
                }
            }
            catch
            {
                // Logging must never crash the app
            }
        }

        public static void Log(Exception? ex)
        {
            if (ex == null)
            {
                Log("ERROR: (null exception)");
                return;
            }

            Log($"ERROR: {ex.Message}{Environment.NewLine}StackTrace: {ex.StackTrace}");
        }
    }
}
