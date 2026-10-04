using System.Collections.Immutable;
using CortexDNA.Models;
namespace CortexDNA.Hardware;

public sealed class HardwareHistory(int capacity = 90)
{
    private readonly HardwareHistorySample?[] _samples = new HardwareHistorySample?[capacity is >= 60 and <= 120 ? capacity : throw new ArgumentOutOfRangeException(nameof(capacity))];
    private readonly object _sync = new(); private int _next, _count;
    public void Add(HardwareSnapshot snapshot)
    {
        var cpu = snapshot.Cpus.FirstOrDefault();
        lock (_sync) { _samples[_next] = new(snapshot.Timestamp, cpu?.UsagePercent, snapshot.Memory.UsagePercent, snapshot.Network.DownloadBytesPerSecond, snapshot.Network.UploadBytesPerSecond, snapshot.Gpus, cpu?.TemperatureC); _next = (_next + 1) % _samples.Length; _count = Math.Min(_count + 1, _samples.Length); }
    }
    public ImmutableArray<HardwareHistorySample> Snapshot()
    {
        lock (_sync) { var result = ImmutableArray.CreateBuilder<HardwareHistorySample>(_count); for (int i = 0; i < _count; i++) result.Add(_samples[(_next - _count + i + _samples.Length) % _samples.Length]!); return result.MoveToImmutable(); }
    }
}
