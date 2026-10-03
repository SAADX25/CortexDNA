using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace CortexDNA.Core;

// Pin every ancestor while inspecting/deleting a child. No write/delete sharing:
// another process cannot replace an ancestor with a junction while this lease exists.
internal sealed class CleanupPathGuard : IDisposable
{
    private readonly List<SafeFileHandle> _handles = new();

    internal static CleanupPathGuard Acquire(string directory)
    {
        var guard = new CleanupPathGuard();
        try
        {
            string full = Path.GetFullPath(directory);
            var ancestors = new Stack<string>();
            for (string? current = full; current != null; current = Directory.GetParent(current)?.FullName)
                ancestors.Push(current);
            while (ancestors.TryPop(out string? current))
            {
                // Attribute-only opens do not enforce directory sharing constraints on Windows.
                var handle = CreateFileW(current, 0x80000000, 1, IntPtr.Zero, 3,
                    0x02000000 | 0x00200000, IntPtr.Zero);
                if (handle.IsInvalid)
                {
                    handle.Dispose();
                    throw new IOException("Cannot safely lock cleanup directory.");
                }
                guard._handles.Add(handle);
                if (!GetFileInformationByHandle(handle, out var info) ||
                    (info.Attributes & (uint)FileAttributes.ReparsePoint) != 0)
                    throw new IOException("Cleanup refuses reparse points or unreadable directories.");
            }
            return guard;
        }
        catch { guard.Dispose(); throw; }
    }

    public void Dispose()
    {
        foreach (var handle in _handles.AsEnumerable().Reverse()) handle.Dispose();
        _handles.Clear();
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFileW(string name, uint access, uint share,
        IntPtr security, uint disposition, uint flags, IntPtr template);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetFileInformationByHandle(SafeFileHandle handle, out FileInformation info);

    [StructLayout(LayoutKind.Sequential)]
    private struct FileInformation
    {
        public uint Attributes;
        public System.Runtime.InteropServices.ComTypes.FILETIME Creation, Access, Write;
        public uint Volume, SizeHigh, SizeLow, Links, IndexHigh, IndexLow;
    }
}
