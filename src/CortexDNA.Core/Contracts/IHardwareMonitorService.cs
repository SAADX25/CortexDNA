using System.Collections.Immutable;
using CortexDNA.Models;
namespace CortexDNA.Core;

public enum MonitoringMode { Foreground, Minimized, Hidden }
public interface IHardwareMonitorService : IDisposable, IAsyncDisposable
{
    event Action<HardwareSnapshot>? SnapshotAvailable;
    Task StartAsync(CancellationToken cancellationToken = default);
    Task RefreshAsync(CancellationToken cancellationToken = default);
    Task ShutdownAsync();
    void SetMode(MonitoringMode mode);
    HardwareSnapshot CurrentSnapshot { get; }
    ImmutableArray<HardwareHistorySample> History { get; }
}
