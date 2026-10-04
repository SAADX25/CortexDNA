using System.Collections.Immutable;
using System.Net.NetworkInformation;
using CortexDNA.Models;
namespace CortexDNA.Hardware;

public sealed record NetworkCounter(string Id, long Received, long Sent);
public sealed class NetworkMonitor(TimeProvider? time = null)
{
    private readonly TimeProvider _time = time ?? TimeProvider.System;
    private ImmutableDictionary<string, NetworkCounter> _previous = ImmutableDictionary<string, NetworkCounter>.Empty;
    private long _timestamp; private bool _hasSample;
    public void Reset() { _hasSample = false; _previous = ImmutableDictionary<string, NetworkCounter>.Empty; }
    public NetworkSnapshot Calculate(IEnumerable<NetworkCounter> counters, long timestamp)
    {
        var current = counters.ToImmutableDictionary(c => c.Id); double seconds = _hasSample ? _time.GetElapsedTime(_timestamp, timestamp).TotalSeconds : 0;
        double received = 0, sent = 0; bool comparable = false;
        if (seconds > 0) foreach (var pair in current) if (_previous.TryGetValue(pair.Key, out var old) && pair.Value.Received >= old.Received && pair.Value.Sent >= old.Sent)
        { comparable = true; received += (pair.Value.Received - old.Received) / seconds; sent += (pair.Value.Sent - old.Sent) / seconds; }
        _previous = current; _timestamp = timestamp; _hasSample = true;
        return comparable ? new(received, sent) : new(null, null);
    }
    public NetworkSnapshot Read(CancellationToken token)
    {
        token.ThrowIfCancellationRequested(); var counters = new List<NetworkCounter>();
        try { foreach (var adapter in NetworkInterface.GetAllNetworkInterfaces()) { token.ThrowIfCancellationRequested(); try { if (adapter.OperationalStatus == OperationalStatus.Up && adapter.NetworkInterfaceType is not (NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel)) { var stats = adapter.GetIPv4Statistics(); counters.Add(new(adapter.Id, stats.BytesReceived, stats.BytesSent)); } } catch { } } }
        catch (OperationCanceledException) { throw; }
        catch { Reset(); return new(null, null); }
        return Calculate(counters, _time.GetTimestamp());
    }
}
