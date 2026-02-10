using System;
using System.IO;

namespace CortexDNA.Core
{
    public static class Logger
    {
        private static readonly string LogPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "log.txt");

        public static void Log(string message)
        {
            try
            {
                string logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}";
                File.AppendAllText(LogPath, logEntry);
            }
            catch
            {
                // If logging fails, we can't do much, but we shouldn't crash.
            }
        }

        public static void Log(Exception ex)
        {
            Log($"ERROR: {ex.Message}\nStackTrace: {ex.StackTrace}");
        }
    }
}
