using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace CortexDNA.Core;

// Record only services initially running; restore even after a partial stop or cancellation.
internal sealed class UpdateServiceLease : IDisposable
{
    private readonly List<string> _restore = new();
    private readonly IUpdateServices _services;
    private UpdateServiceLease(IUpdateServices services) => _services = services;
    internal static UpdateServiceLease Acquire(CancellationToken token, IUpdateServices? services = null)
    {
        var lease = new UpdateServiceLease(services ?? new NativeServices());
        try
        {
            foreach (string name in new[] { "wuauserv", "bits" })
            {
                token.ThrowIfCancellationRequested();
                    uint state = lease._services.State(name);
                    if (state == 1) continue;
                    if (state != 4) throw new InvalidOperationException($"{name} is changing state; retry later.");
                    lease._restore.Add(name);
                    lease._services.Stop(name);
                    lease._services.Wait(name, 1, token);
            }
            return lease;
        }
        catch { lease.Dispose(); throw; }
    }

    public void Dispose()
    {
        var errors = new List<Exception>();
        foreach (string name in _restore.AsEnumerable().Reverse())
        {
            try
            {
                    if (_services.State(name) == 3) _services.Wait(name, 1, CancellationToken.None);
                    if (_services.State(name) != 4) _services.Start(name);
                    _services.Wait(name, 4, CancellationToken.None);
            }
            catch (Exception ex) { Logger.Log($"Restore {name} failed: {ex}"); errors.Add(ex); }
        }
        _restore.Clear();
        if (errors.Count > 0) throw new AggregateException("Windows Update services could not be restored. Check Windows Services.", errors);
    }

    internal void VerifyStopped()
    {
        foreach (string name in new[] { "wuauserv", "bits" })
            if (_services.State(name) != 1)
                throw new InvalidOperationException("An update service restarted; cache cleanup was refused.");
    }

    internal interface IUpdateServices
    {
        uint State(string name);
        void Stop(string name);
        void Start(string name);
        void Wait(string name, uint target, CancellationToken token);
    }
    private sealed class NativeServices : IUpdateServices
    {
        public uint State(string name)
        {
            uint state = 0;
            WithService(name, handle => state = UpdateServiceLease.State(handle));
            return state;
        }
        public void Stop(string name) => WithService(name, handle => {
            if (!ControlService(handle, 1, out _)) throw new Win32Exception();
        });
        public void Start(string name) => WithService(name, handle => {
            if (!StartServiceW(handle, 0, IntPtr.Zero)) throw new Win32Exception();
        });
        public void Wait(string name, uint target, CancellationToken token) =>
            WithService(name, handle => UpdateServiceLease.Wait(handle, target, token));
    }

    private static void WithService(string name, Action<IntPtr> action)
    {
        IntPtr manager = OpenSCManagerW(null, null, 1);
        if (manager == IntPtr.Zero) throw new Win32Exception();
        try
        {
            IntPtr service = OpenServiceW(manager, name, 0x34); // query/start/stop only
            if (service == IntPtr.Zero) throw new Win32Exception();
            try { action(service); } finally { CloseServiceHandle(service); }
        }
        finally { CloseServiceHandle(manager); }
    }
    private static uint State(IntPtr service)
    {
        if (!QueryServiceStatus(service, out var status)) throw new Win32Exception();
        return status.State;
    }
    private static void Wait(IntPtr service, uint state, CancellationToken token)
    {
        var timer = Stopwatch.StartNew();
        while (State(service) != state)
        {
            token.ThrowIfCancellationRequested();
            if (timer.Elapsed > TimeSpan.FromSeconds(30)) throw new TimeoutException("Service state change timed out.");
            Thread.Sleep(100);
        }
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct ServiceStatus { public uint Type, State, Accepted, Error, SpecificError, Checkpoint, WaitHint; }
    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr OpenSCManagerW(string? machine, string? database, uint access);
    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr OpenServiceW(IntPtr manager, string name, uint access);
    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool QueryServiceStatus(IntPtr service, out ServiceStatus status);
    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ControlService(IntPtr service, uint control, out ServiceStatus status);
    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool StartServiceW(IntPtr service, uint count, IntPtr arguments);
    [DllImport("advapi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseServiceHandle(IntPtr handle);
}
