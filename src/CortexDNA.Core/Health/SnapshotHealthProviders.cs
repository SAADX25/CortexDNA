using System.Collections.Immutable;
namespace CortexDNA.Core.Health;

/// <summary>Assessment policies describe observations, not diagnoses. No new sensor reads.</summary>
public static class SnapshotHealthProviders
{
    public static IReadOnlyList<IHealthCheckProvider> Create() =>
    [
        new DelegateHealthProvider([new(HealthNode.Cpu,HealthArea.Hardware,4),new(HealthNode.Gpu,HealthArea.Hardware,3)], (c,t) =>
        {
            t.ThrowIfCancellationRequested();
            return Task.FromResult<ImmutableArray<HealthCheckItem>>([
                Fresh(c) ? Thermal(HealthNode.Cpu,4,c.Hardware.Cpus.Select(x=>x.TemperatureC).ToArray()) : Missing(HealthNode.Cpu,HealthArea.Hardware,4,"Telemetry is older than 30 seconds."),
                Fresh(c) ? Thermal(HealthNode.Gpu,3,c.Hardware.Gpus.Select(x=>x.TemperatureC).ToArray()) : Missing(HealthNode.Gpu,HealthArea.Hardware,3,"Telemetry is older than 30 seconds.")]);
        }),
        One(HealthNode.Memory,HealthArea.Hardware,3, c =>
        {
            var usage = Fresh(c) ? c.Hardware.Memory.UsagePercent : null;
            if (usage == null) return Missing(HealthNode.Memory,HealthArea.Hardware,3,"Memory telemetry unavailable or stale.");
            bool pressure = usage >= 90;
            return new(HealthNode.Memory,HealthArea.Hardware,pressure ? HealthState.Attention : HealthState.Healthy,
                $"Memory usage {usage:F0}%",pressure ? "Memory usage is at least 90%; this is a point-in-time observation." : "Memory usage is below the 90% pressure threshold.",
                3,pressure ? 2 : 0,pressure ? "Review application memory usage; no memory optimization was performed." : null);
        }),
        One(HealthNode.Storage,HealthArea.Storage,20,c =>
        {
            var drive = Fresh(c) ? c.Hardware.Storage.FirstOrDefault(d=>string.Equals(d.Id,c.SystemDrive,StringComparison.OrdinalIgnoreCase)) : null;
            if (drive?.UsagePercent is not double usage) return Missing(HealthNode.Storage,HealthArea.Storage,20,"System-drive capacity unavailable or stale.");
            double free = 100-usage; int deduction = free < 5 ? 8 : free < 15 ? 2 : 0;
            return new(HealthNode.Storage,HealthArea.Storage,deduction > 0 ? HealthState.Attention : HealthState.Healthy,
                $"System drive {free:F1}% free",free < 5 ? "System drive has less than 5% free space." : free < 15 ? "System drive has less than 15% free space." : "System drive has at least 15% free space.",
                20,deduction,deduction > 0 ? "Review Storage and the existing cleanup preview before deciding on any action." : null);
        }),
        One(HealthNode.Network,HealthArea.Network,0,c =>
        {
            var n = c.Hardware.Network;
            if (!Fresh(c) || n.DownloadBytesPerSecond == null || n.UploadBytesPerSecond == null)
                return Missing(HealthNode.Network,HealthArea.Network,0,"Local network counters unavailable or stale.");
            return new(HealthNode.Network,HealthArea.Network,HealthState.Healthy,
                $"↓ {n.DownloadBytesPerSecond / 1024:F1} KB/s · ↑ {n.UploadBytesPerSecond / 1024:F1} KB/s",
                "Existing local traffic counters only; zero traffic is normal. Internet reachability is not tested.",0);
        })
    ];
    private static bool Fresh(HealthCheckContext c) => DateTimeOffset.UtcNow - c.Hardware.Timestamp <= TimeSpan.FromSeconds(30);
    private static HealthCheckItem Thermal(HealthNode node, int weight, double?[] readings)
    {
        var known = readings.Where(x => x.HasValue && double.IsFinite(x.Value)).Select(x => x!.Value).ToArray();
        if (known.Length == 0) return Missing(node, HealthArea.Hardware, weight, "Temperature sensors are unavailable; no temperature conclusion is made.");
        double max = known.Max(); bool hot = max >= 90;
        if (!hot && known.Length != readings.Length)
            return Missing(node, HealthArea.Hardware, weight, $"Peak available temperature {max:F0} °C; only {known.Length}/{readings.Length} devices have sensors. Unmeasured devices prevent a complete thermal assessment.");
        return new(node, HealthArea.Hardware, hot ? HealthState.Attention : HealthState.Healthy, $"Peak temperature {max:F0} °C",
            hot ? $"A reported {node} temperature is at least 90 °C; limits vary by model." : $"{known.Length}/{readings.Length} temperature sensor(s) available; reported readings are below 90 °C.",
            weight, hot ? Math.Min(weight, 2) : 0, hot ? "Review sustained temperatures in Hardware and the device manufacturer's thermal limits." : null);
    }
    public static HealthCheckItem Missing(HealthNode node, HealthArea area, int weight, string reason)
        => new(node, area, HealthState.Unavailable, "Unavailable", reason, weight);
    public static IHealthCheckProvider One(HealthNode node, HealthArea area, int weight, Func<HealthCheckContext, HealthCheckItem> check)
        => new DelegateHealthProvider([new(node, area, weight)], (c, t) => { t.ThrowIfCancellationRequested(); return Task.FromResult<ImmutableArray<HealthCheckItem>>([check(c)]); });
}
