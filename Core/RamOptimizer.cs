using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using CortexDNA.Models;

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

        private const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;
        private const uint PROCESS_SET_QUOTA = 0x0100;

        // Critical OS / security / anti-cheat — never touch
        private static readonly HashSet<string> ExcludedProcesses = new(StringComparer.OrdinalIgnoreCase)
        {
            "Idle", "System", "Registry", "smss", "csrss", "wininit", "services", "lsass", "winlogon",
            "svchost", "fontdrvhost", "dwm", "explorer", "Memory Compression",
            "MsMpEng", "NisSrv", "SecurityHealthService", "SecurityHealthSystray",
            "cs2", "csgo", "valorant-win64-shipping", "vgc", "vgtray", "easyanticheat",
            "easyanticheat_eos", "BEService", "BattleEye", "FaceitClient", "tslGame",
            "CortexDNA"
        };

        /// <summary>
        /// Trims process working sets. Reclaimed memory is often temporary —
        /// Windows may page data back in when apps need it.
        /// </summary>
        public static Task<RamOptimizeResult> OptimizeMemoryAsync()
        {
            return Task.Run(OptimizeMemory);
        }

        private static RamOptimizeResult OptimizeMemory()
        {
            float before = GetAvailableMb();
            int touched = 0;

            try
            {
                try
                {
                    GC.Collect(GC.MaxGeneration, GCCollectionMode.Optimized, blocking: false);
                }
                catch { }

                Process[] processes;
                try
                {
                    processes = Process.GetProcesses();
                }
                catch (Exception ex)
                {
                    Logger.Log($"RamOptimizer GetProcesses failed: {ex.Message}");
                    return new RamOptimizeResult
                    {
                        AvailableBeforeMb = before,
                        AvailableAfterMb = GetAvailableMb(),
                        Success = false,
                        ErrorMessage = "Could not enumerate processes"
                    };
                }

                int currentPid = Environment.ProcessId;

                foreach (Process proc in processes)
                {
                    try
                    {
                        if (proc.Id == 0 || proc.Id == 4 || proc.Id == currentPid)
                            continue;

                        string name;
                        try { name = proc.ProcessName; }
                        catch { continue; }

                        if (ExcludedProcesses.Contains(name))
                            continue;

                        IntPtr handle = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION | PROCESS_SET_QUOTA, false, proc.Id);
                        if (handle == IntPtr.Zero)
                            continue;

                        try
                        {
                            if (EmptyWorkingSet(handle))
                                touched++;
                        }
                        finally
                        {
                            CloseHandle(handle);
                        }
                    }
                    catch
                    {
                        // Access denied / exited — expected
                    }
                    finally
                    {
                        try { proc.Dispose(); } catch { }
                    }
                }

                float after = GetAvailableMb();
                return new RamOptimizeResult
                {
                    AvailableBeforeMb = before,
                    AvailableAfterMb = after,
                    ProcessesTouched = touched,
                    Success = true
                };
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
                return new RamOptimizeResult
                {
                    AvailableBeforeMb = before,
                    AvailableAfterMb = GetAvailableMb(),
                    ProcessesTouched = touched,
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
        }

        private static float GetAvailableMb()
        {
            try
            {
                NativeMethods.MEMORYSTATUSEX status = new NativeMethods.MEMORYSTATUSEX();
                status.dwLength = (uint)Marshal.SizeOf(typeof(NativeMethods.MEMORYSTATUSEX));
                if (NativeMethods.GlobalMemoryStatusEx(ref status))
                    return status.ullAvailPhys / (1024f * 1024f);
            }
            catch { }
            return 0;
        }
    }
}
