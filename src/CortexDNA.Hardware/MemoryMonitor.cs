using System.Runtime.InteropServices;
using CortexDNA.Models;
namespace CortexDNA.Hardware;
/// <summary>Physical memory readings only; no working-set trimming or process access.</summary>
public sealed class MemoryMonitor
{
    public MemorySnapshot Read(CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        try { var status = new Status { Length = (uint)Marshal.SizeOf<Status>() }; if (GlobalMemoryStatusEx(ref status) && status.Total > 0) return new(status.Total, status.Available); } catch { }
        return new(null, null);
    }
    [DllImport("kernel32.dll", SetLastError = true)][return: MarshalAs(UnmanagedType.Bool)] private static extern bool GlobalMemoryStatusEx(ref Status status);
    [StructLayout(LayoutKind.Sequential)]
    private struct Status
    { public uint Length, Load; public ulong Total, Available, TotalPage, AvailablePage, TotalVirtual, AvailableVirtual, Extended; }
}
