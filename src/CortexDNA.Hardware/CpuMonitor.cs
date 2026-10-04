using System.Collections.Immutable;
using System.Diagnostics;
using System.Management;
using CortexDNA.Models;
namespace CortexDNA.Hardware;

public sealed class CpuMonitor : IDisposable
{
    private PerformanceCounter? _performance; private double? _baseGhz;
    public void Initialize(CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        try { using var query = new ManagementObjectSearcher("SELECT MaxClockSpeed FROM Win32_Processor"); query.Options.Timeout = TimeSpan.FromSeconds(5); using var results = query.Get(); foreach (ManagementObject obj in results) { using (obj) { token.ThrowIfCancellationRequested(); if (obj["MaxClockSpeed"] != null) { _baseGhz = Convert.ToDouble(obj["MaxClockSpeed"]) / 1000; break; } } } } catch (OperationCanceledException) { throw; } catch { }
        token.ThrowIfCancellationRequested();
        try { _performance = new("Processor Information", "% Processor Performance", "_Total"); _performance.NextValue(); } catch { _performance?.Dispose(); _performance = null; }
    }
    public ImmutableArray<CpuSnapshot> Read(IEnumerable<DeviceReading> devices, CancellationToken token)
    {
        token.ThrowIfCancellationRequested(); double? clock = null;
        try { if (_performance != null && _baseGhz.HasValue) { var value = _performance.NextValue(); if (float.IsFinite(value) && value > 0) clock = _baseGhz * value / 100; } } catch { }
        return Project(devices, clock);
    }
    public static ImmutableArray<CpuSnapshot> Project(IEnumerable<DeviceReading> devices, double? calculatedClock = null) => devices.Where(d => d.Kind == "CPU").Select(d => new CpuSnapshot(d.Id, d.Name, SensorProjection.Value(d, "Load", "CPU Total"), SensorProjection.Value(d, "Clock", "CPU Average") / 1000 ?? calculatedClock, SensorProjection.Value(d, "Temperature", "CPU Package") ?? SensorProjection.Value(d, "Temperature"))).ToImmutableArray();
    public void Dispose() { _performance?.Dispose(); _performance = null; }
}
