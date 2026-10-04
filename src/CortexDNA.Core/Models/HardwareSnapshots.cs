using System.Collections.Immutable;
namespace CortexDNA.Models;

public sealed record CpuSnapshot(string Id, string Name, double? UsagePercent, double? ClockGhz, double? TemperatureC);
public sealed record TemperatureSnapshot(string Name, double? Celsius);
public sealed record GpuSnapshot(string Id, string Name, double? UsagePercent, double? TemperatureC)
{ public ImmutableArray<TemperatureSnapshot> Temperatures { get; init; } = []; }
public sealed record MemorySnapshot(ulong? TotalBytes, ulong? AvailableBytes)
{
    public double? UsagePercent => TotalBytes is > 0 && AvailableBytes.HasValue ? Math.Clamp(100d * (TotalBytes.Value - Math.Min(TotalBytes.Value, AvailableBytes.Value)) / TotalBytes.Value, 0, 100) : null;
}
public sealed record StorageSnapshot(string Id, string Label, long? TotalBytes, long? AvailableBytes)
{
    public double? UsagePercent => TotalBytes is > 0 && AvailableBytes.HasValue ? Math.Clamp(100d * (TotalBytes.Value - AvailableBytes.Value) / TotalBytes.Value, 0, 100) : null;
}
public sealed record NetworkSnapshot(double? DownloadBytesPerSecond, double? UploadBytesPerSecond);
public sealed record HardwareInfoSnapshot(string? OsName = null, string? BiosInfo = null, string? MotherboardModel = null, string? BiosVersion = null, string? BiosDate = null, string? CpuName = null, string? GpuName = null, string? RamInfo = null, string? RamTotal = null, string? RamType = null, double TotalRamBytes = 0, bool IsAdmin = false);
public sealed record HardwareSnapshot(DateTimeOffset Timestamp, ImmutableArray<CpuSnapshot> Cpus, ImmutableArray<GpuSnapshot> Gpus, MemorySnapshot Memory, ImmutableArray<StorageSnapshot> Storage, NetworkSnapshot Network, HardwareInfoSnapshot Info, bool IsGameDetected, TimeSpan Uptime)
{
    public static HardwareSnapshot Unavailable(DateTimeOffset timestamp) => new(timestamp, [], [], new(null, null), [], new(null, null), new(), false, TimeSpan.Zero);
}
public sealed record HardwareHistorySample(DateTimeOffset Timestamp, double? CpuUsage, double? RamUsage, double? Download, double? Upload, ImmutableArray<GpuSnapshot> Gpus, double? CpuTemperature);
