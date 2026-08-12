using System.Diagnostics;
using System.IO;
using CortexDNA.Models;

namespace CortexDNA.Core.Startup
{
    internal static class StartupProcessProbe
    {
        public static void Apply(IEnumerable<StartupItem> items)
        {
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var process in Process.GetProcesses())
            {
                try
                {
                    if (!string.IsNullOrWhiteSpace(process.ProcessName))
                        names.Add(process.ProcessName);

                    try
                    {
                        string? file = process.MainModule?.FileName;
                        if (!string.IsNullOrWhiteSpace(file))
                            paths.Add(file);
                    }
                    catch
                    {
                        // Some system processes block MainModule even as admin.
                    }
                }
                finally
                {
                    process.Dispose();
                }
            }

            foreach (var item in items)
                item.IsRunning = IsRunning(item.ExecutablePath, names, paths);
        }

        private static bool IsRunning(string exe, HashSet<string> names, HashSet<string> paths)
        {
            if (string.IsNullOrWhiteSpace(exe))
                return false;

            if (paths.Contains(exe))
                return true;

            string name = Path.GetFileNameWithoutExtension(exe);
            if (string.IsNullOrWhiteSpace(name))
                return false;

            if (names.Contains(name))
                return true;

            if (name.EndsWith("64", StringComparison.OrdinalIgnoreCase) && names.Contains(name[..^2]))
                return true;
            if (name.EndsWith("32", StringComparison.OrdinalIgnoreCase) && names.Contains(name[..^2]))
                return true;

            return names.Contains(name + "64") || names.Contains(name + "32");
        }
    }
}
