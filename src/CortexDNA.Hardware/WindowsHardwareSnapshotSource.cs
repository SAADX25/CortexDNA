using System.Collections.Immutable;
using CortexDNA.Models;
namespace CortexDNA.Hardware;

public sealed class WindowsHardwareSnapshotSource : IHardwareSnapshotSource
{
    private readonly IHardwareSession _session;
    private readonly CpuMonitor _cpu = new(); private readonly GpuMonitor _gpu = new(); private readonly MemoryMonitor _memory = new(); private readonly StorageMonitor _storage = new(); private readonly NetworkMonitor _network = new(); private readonly HardwareInfoService _infoService = new(); private readonly GameDetectionService _games = new();
    private HardwareInfoSnapshot _info = new(); private bool _available, _disposed;
    public WindowsHardwareSnapshotSource(IHardwareSession? session = null) { _session = session ?? new HardwareSession(); _info = _infoService.ReadCached(); }
    public HardwareSnapshot InitialSnapshot => HardwareSnapshot.Unavailable(DateTimeOffset.UtcNow) with { Info = _info };
    public Task InitializeAsync(CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        try { _session.Open(); _available = true; } catch { _available = false; }
        token.ThrowIfCancellationRequested(); _cpu.Initialize(token); _info = _infoService.Read(token); return Task.CompletedTask;
    }
    public Task<HardwareSnapshot> ReadAsync(CancellationToken token)
    {
        token.ThrowIfCancellationRequested(); bool game = _games.Read(token);
        ImmutableArray<DeviceReading> devices = [];
        if (_available) try { _session.Refresh(token); token.ThrowIfCancellationRequested(); devices = SensorProjection.Read(_session, token); } catch (OperationCanceledException) { throw; } catch { }
        var snapshot = new HardwareSnapshot(DateTimeOffset.UtcNow, _cpu.Read(devices, token), _gpu.Read(devices, token), _memory.Read(token), _storage.Read(token), _network.Read(token), _info, game, TimeSpan.FromMilliseconds(Environment.TickCount64));
        token.ThrowIfCancellationRequested(); return Task.FromResult(snapshot);
    }
    public void Dispose() { if (_disposed) return; _disposed = true; try { _session.Close(); } finally { try { _cpu.Dispose(); } finally { _games.Dispose(); } } }
}
