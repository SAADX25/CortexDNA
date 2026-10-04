using System.Collections.Immutable;
using CortexDNA.Models;
namespace CortexDNA.Hardware;

public sealed class GpuMonitor
{
    public ImmutableArray<GpuSnapshot> Read(IEnumerable<DeviceReading> devices, CancellationToken token)
    { token.ThrowIfCancellationRequested(); return devices.Where(d => d.Kind == "GPU").Select(d => new GpuSnapshot(d.Id, d.Name, SensorProjection.Value(d, "Load", "GPU Core"), SensorProjection.Value(d, "Temperature", "GPU Core") ?? SensorProjection.Value(d, "Temperature")) { Temperatures = d.Sensors.Where(s => s.Kind == "Temperature").Select(s => new TemperatureSnapshot(s.Name, s.Value.HasValue && double.IsFinite(s.Value.Value) ? s.Value : null)).ToImmutableArray() }).ToImmutableArray(); }
}
