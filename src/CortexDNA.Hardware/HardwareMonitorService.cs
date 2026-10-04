using System.Collections.Immutable;
using CortexDNA.Core;
using CortexDNA.Models;
namespace CortexDNA.Hardware;

public interface IHardwareSnapshotSource : IDisposable
{
    Task InitializeAsync(CancellationToken token);
    Task<HardwareSnapshot> ReadAsync(CancellationToken token);
    HardwareSnapshot InitialSnapshot => HardwareSnapshot.Unavailable(DateTimeOffset.UtcNow);
}
/// <summary>One owner, one polling loop, serialized reads and tracked shutdown. No UI or vendor types cross this contract.</summary>
public sealed class HardwareMonitorService : IHardwareMonitorService
{
    private readonly IHardwareSnapshotSource _source;
    private readonly TimeProvider _time;
    private readonly TimeSpan _initialDelay;
    private readonly SemaphoreSlim _readGate = new(1, 1);
    private readonly object _sync = new();
    private readonly CancellationTokenSource _lifetime = new();
    private readonly HardwareHistory _history = new();
    private Task? _initialization, _loop, _shutdown;
    private volatile bool _stopping, _game;
    private int _mode;
    // Admissions include queued reads, so shutdown cannot dispose a gate still used by a racing caller.
    private int _activeRefreshes;
    private TaskCompletionSource? _refreshesDrained;
    private readonly SemaphoreSlim _wake = new(0, 1);
    private HardwareSnapshot _current;
    public HardwareMonitorService(IHardwareSnapshotSource? source = null, TimeProvider? time = null, TimeSpan? initialDelay = null)
    { _source = source ?? new WindowsHardwareSnapshotSource(); _time = time ?? TimeProvider.System; _initialDelay = initialDelay ?? TimeSpan.FromMilliseconds(1500); _current = _source.InitialSnapshot; }
    public event Action<HardwareSnapshot>? SnapshotAvailable;
    public ImmutableArray<HardwareHistorySample> History => _history.Snapshot();
    public HardwareSnapshot CurrentSnapshot { get { lock (_sync) return _current; } }
    public void SetMode(MonitoringMode mode)
    {
        if (!Enum.IsDefined(mode)) throw new ArgumentOutOfRangeException(nameof(mode));
        lock (_sync) { if (_stopping) return; if (_mode == (int)mode) return; _mode = (int)mode; if (_wake.CurrentCount == 0) _wake.Release(); }
    }
    public TimeSpan PollingInterval => (MonitoringMode)Volatile.Read(ref _mode) == MonitoringMode.Minimized ? TimeSpan.FromSeconds(5) : _game ? TimeSpan.FromSeconds(10) : TimeSpan.FromSeconds(1);
    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        lock (_sync)
        {
            if (_stopping) return Task.CompletedTask;
            if (_initialization != null) return _initialization;
            _initialization = Task.Run(() => InitializeAsync(cancellationToken));
            _loop = Task.Run(PollAsync);
            return _initialization;
        }
    }
    private async Task InitializeAsync(CancellationToken caller)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(caller, _lifetime.Token);
        try { await Task.Delay(_initialDelay, _time, linked.Token).ConfigureAwait(false); await _source.InitializeAsync(linked.Token).ConfigureAwait(false); linked.Token.ThrowIfCancellationRequested(); }
        catch { _lifetime.Cancel(); throw; }
    }
    private async Task PollAsync()
    {
        try
        {
            await _initialization!.ConfigureAwait(false);
            while (!_lifetime.IsCancellationRequested)
            {
                if ((MonitoringMode)Volatile.Read(ref _mode) != MonitoringMode.Hidden) await RefreshAsync(_lifetime.Token).ConfigureAwait(false);
                await WaitForNextPollAsync().ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { CortexDNA.Core.Logger.Log(ex); }
    }
    // Hidden mode owns no polling timer. Visibility wakes this same loop rather than starting another.
    private async Task WaitForNextPollAsync()
    {
        if ((MonitoringMode)Volatile.Read(ref _mode) == MonitoringMode.Hidden) { await _wake.WaitAsync(_lifetime.Token).ConfigureAwait(false); return; }
        using var wait = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
        var delay = Task.Delay(PollingInterval, _time, wait.Token); var wake = _wake.WaitAsync(wait.Token);
        await Task.WhenAny(delay, wake).ConfigureAwait(false); wait.Cancel();
        try { await Task.WhenAll(delay, wake).ConfigureAwait(false); } catch (OperationCanceledException) { _lifetime.Token.ThrowIfCancellationRequested(); }
    }
    public Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        lock (_sync)
        {
            if (_stopping || _initialization == null) return Task.CompletedTask;
            var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _lifetime.Token); _activeRefreshes++;
            return RefreshCoreAsync(_initialization, linked);
        }
    }
    private async Task RefreshCoreAsync(Task initialization, CancellationTokenSource linked)
    {
        try
        {
            await initialization.WaitAsync(linked.Token).ConfigureAwait(false);
            await _readGate.WaitAsync(linked.Token).ConfigureAwait(false);
            try
            {
                linked.Token.ThrowIfCancellationRequested();
                HardwareSnapshot snapshot;
                try { snapshot = await Task.Run(() => _source.ReadAsync(linked.Token), linked.Token).ConfigureAwait(false); }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex) { CortexDNA.Core.Logger.Log(ex); snapshot = HardwareSnapshot.Unavailable(_time.GetUtcNow()) with { Info = CurrentSnapshot.Info }; }
                linked.Token.ThrowIfCancellationRequested();
                lock (_sync)
                {
                    if (_stopping) return;
                    _current = snapshot; _game = snapshot.IsGameDetected; _history.Add(snapshot);
                    // Subscribers must enqueue UI work; they must not synchronously await shutdown.
                    SnapshotAvailable?.Invoke(snapshot);
                }
            }
            finally { _readGate.Release(); }
        }
        finally
        {
            linked.Dispose();
            lock (_sync) { _activeRefreshes--; if (_activeRefreshes == 0) _refreshesDrained?.TrySetResult(); }
        }
    }
    public Task ShutdownAsync()
    {
        lock (_sync)
        {
            if (_shutdown != null) return _shutdown;
            _stopping = true; _lifetime.Cancel(); SnapshotAvailable = null;
            if (_activeRefreshes > 0) _refreshesDrained = new(TaskCreationOptions.RunContinuationsAsynchronously);
            return _shutdown = Task.Run(ReleaseAsync);
        }
    }
    // Await synchronous vendor/WMI calls as well as cancellable work before releasing handles.
    private async Task ReleaseAsync()
    {
        try { await Task.WhenAll(_initialization ?? Task.CompletedTask, _loop ?? Task.CompletedTask).ConfigureAwait(false); }
        catch (OperationCanceledException) { }
        catch (Exception ex) { CortexDNA.Core.Logger.Log(ex); }
        if (_refreshesDrained != null) await _refreshesDrained.Task.ConfigureAwait(false);
        await _readGate.WaitAsync().ConfigureAwait(false);
        try { _source.Dispose(); } catch (Exception ex) { CortexDNA.Core.Logger.Log(ex); } finally { _readGate.Release(); }
        _readGate.Dispose(); _wake.Dispose(); _lifetime.Dispose();
    }
    public void Dispose() => _ = ShutdownAsync();
    public async ValueTask DisposeAsync() => await ShutdownAsync().ConfigureAwait(false);
}
