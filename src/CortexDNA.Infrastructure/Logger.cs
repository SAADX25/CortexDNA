using System;
using System.IO;

namespace CortexDNA.Core
{
    public static class Logger
    {
        private static readonly object _sync = new();
        internal static readonly string LogPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CortexDNA", "Logs", "log.txt");

        public static void Log(string message)
        {
            try
            {
                string logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}";
                lock (_sync)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
                    if (File.Exists(LogPath) && new FileInfo(LogPath).Length > 1024 * 1024)
                        File.Move(LogPath, LogPath + ".1", overwrite: true);
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

            Log($"ERROR: {ex}");
        }
    }
}
