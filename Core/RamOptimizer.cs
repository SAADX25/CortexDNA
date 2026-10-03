using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Threading;
using System.Security.Principal;
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

        [DllImport("advapi32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool OpenProcessToken(IntPtr process, uint access, out IntPtr token);
        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr window, out uint pid);

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
        public static Task<RamOptimizeResult> OptimizeMemoryAsync(CancellationToken cancellationToken = default)
        {
            return Task.Run(() => OptimizeMemory(cancellationToken), cancellationToken);
        }

        private static RamOptimizeResult OptimizeMemory(CancellationToken cancellationToken)
        {
            float before = GetAvailableMb();
            int touched = 0;

            try
            {
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
                using var current = Process.GetCurrentProcess();
                using var identity = WindowsIdentity.GetCurrent();
                GetWindowThreadProcessId(GetForegroundWindow(), out uint foregroundPid);

                try
                {
                foreach (Process proc in processes)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    try
                    {
                        if (proc.Id == 0 || proc.Id == 4 || proc.Id == currentPid || proc.Id == foregroundPid ||
                            proc.SessionId != current.SessionId)
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
                            if (!OpenProcessToken(handle, 8, out IntPtr token)) continue;
                            try
                            {
                                using var owner = new WindowsIdentity(token);
                                if (owner.User != identity.User) continue;
                            }
                            finally { CloseHandle(token); }
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
                }
                finally { foreach (var process in processes) process.Dispose(); }

                float after = GetAvailableMb();
                return new RamOptimizeResult
                {
                    AvailableBeforeMb = before,
                    AvailableAfterMb = after,
                    ProcessesTouched = touched,
                    Success = true
                };
            }
            catch (OperationCanceledException) { throw; }
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
