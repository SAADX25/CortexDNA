using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace CortexDNA.Core
{
    public static class RamOptimizer
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenProcess(uint processAccess, bool bInheritHandle, int processId);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CloseHandle(IntPtr hObject);

        [DllImport("psapi.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool EmptyWorkingSet(IntPtr hProcess);

        private const uint PROCESS_QUERY_INFORMATION = 0x0400;
        private const uint PROCESS_SET_QUOTA = 0x0100;

        // Skip critical OS processes and popular anti-cheat protected games
        private static readonly HashSet<string> ExcludedProcesses = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "svchost", "explorer", "System", "Idle", "csgo", "cs2", "tslGame", "vgc", "vgtray", "antimalware", "msmpeng", "registry"
        };

        public static async Task OptimizeMemoryAsync()
        {
            await Task.Run(() =>
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();

                Process[] processes = Process.GetProcesses();
                foreach (Process proc in processes)
                {
                    try
                    {
                        if (ExcludedProcesses.Contains(proc.ProcessName))
                            continue;

                        // Open process with MINIMAL rights
                        IntPtr handle = OpenProcess(PROCESS_QUERY_INFORMATION | PROCESS_SET_QUOTA, false, proc.Id);
                        if (handle != IntPtr.Zero)
                        {
                            try 
                            {
                                EmptyWorkingSet(handle);
                            }
                            finally 
                            {
                                CloseHandle(handle); // ALWAYS prevent memory leaks
                            }
                        }
                    }
                    catch { /* Silently skip processes we can't touch */ }
                    finally { proc.Dispose(); }
                }
            });
        }
    }
}
