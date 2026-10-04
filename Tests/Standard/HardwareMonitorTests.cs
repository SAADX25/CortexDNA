using System.Collections.Immutable;
using System.Diagnostics;
using CortexDNA.Core;
using CortexDNA.Hardware;
using CortexDNA.Models;
using Xunit;
namespace CortexDNA.AutomatedTests;

public sealed class HardwareMonitorTests
{
    private static HardwareMonitorService Monitor(Source source) => new(source, initialDelay: TimeSpan.Zero);
    private static async Task Wait(Task task) => await task.WaitAsync(TimeSpan.FromSeconds(5));
    [Fact]
    public async Task InitializationCancellationIsTrackedAndClosesOnce()
    {
        var source = new Source { WaitInitialization = true }; var monitor = Monitor(source); monitor.SetMode(MonitoringMode.Hidden);
        using var cancel = new CancellationTokenSource(); var initialization = monitor.StartAsync(cancel.Token); await Wait(source.InitEntered.Task);
        Assert.Same(initialization, monitor.StartAsync()); cancel.Cancel(); await Assert.ThrowsAnyAsync<OperationCanceledException>(() => initialization);
        await Wait(monitor.ShutdownAsync()); Assert.Equal(1, source.Disposals); Assert.Equal(0, source.Reads);
    }
    [Fact]
    public async Task ShutdownWaitsForNonInterruptibleInitialization()
    {
        var source = new Source { WaitInitialization = true, IgnoreCancellation = true }; var monitor = Monitor(source);
        var initialization = monitor.StartAsync(); await Wait(source.InitEntered.Task); var shutdown = monitor.ShutdownAsync(); Assert.False(shutdown.IsCompleted);
        source.InitRelease.SetResult(); await Wait(shutdown); await Assert.ThrowsAnyAsync<OperationCanceledException>(() => initialization); Assert.Equal(1, source.Disposals);
    }
    [Fact]
    public async Task ShutdownWaitsForRefreshAndSuppressesLateSnapshot()
    {
        var source = new Source { WaitRead = true, IgnoreCancellation = true }; var monitor = Monitor(source); monitor.SetMode(MonitoringMode.Hidden); await monitor.StartAsync(); int published = 0; monitor.SnapshotAvailable += _ => published++;
        var refresh = monitor.RefreshAsync(); await Wait(source.ReadEntered.Task); var shutdown = monitor.ShutdownAsync(); Assert.False(shutdown.IsCompleted); Assert.Equal(0, source.Disposals);
        source.ReadRelease.SetResult(); await Assert.ThrowsAnyAsync<OperationCanceledException>(() => refresh); await Wait(shutdown); Assert.Equal(0, published); Assert.Equal(1, source.Disposals);
    }
    [Fact]
    public async Task ConcurrentRefreshesAreSerialized()
    {
        var source = new Source { Delay = TimeSpan.FromMilliseconds(10) }; var monitor = Monitor(source); monitor.SetMode(MonitoringMode.Hidden); await monitor.StartAsync();
        await Wait(Task.WhenAll(Enumerable.Range(0, 16).Select(_ => monitor.RefreshAsync()))); await monitor.ShutdownAsync(); Assert.Equal(16, source.Reads); Assert.Equal(1, source.MaxActive);
    }
    [Fact]
    public async Task CancelledQueuedRefreshDoesNotRead()
    {
        var source = new Source { WaitRead = true }; var monitor = Monitor(source); monitor.SetMode(MonitoringMode.Hidden); await monitor.StartAsync();
        var first = monitor.RefreshAsync(); await Wait(source.ReadEntered.Task); using var cancel = new CancellationTokenSource(); var queued = monitor.RefreshAsync(cancel.Token); cancel.Cancel(); await Assert.ThrowsAnyAsync<OperationCanceledException>(() => queued);
        source.ReadRelease.SetResult(); await first; await monitor.ShutdownAsync(); Assert.Equal(1, source.Reads);
    }
    [Fact]
    public async Task SlowAutomaticPollingDoesNotOverlapManualRefresh()
    {
        var source = new Source { WaitRead = true }; var monitor = Monitor(source); await monitor.StartAsync(); await Wait(source.ReadEntered.Task);
        var manual = monitor.RefreshAsync(); await Task.Delay(1100); Assert.Equal(1, source.Reads); Assert.Equal(1, source.MaxActive);
        source.ReadRelease.SetResult(); await manual; await monitor.ShutdownAsync(); Assert.Equal(1, source.MaxActive);
    }
    [Theory]
    [InlineData(MonitoringMode.Foreground, 1)]
    [InlineData(MonitoringMode.Minimized, 5)]
    public async Task VisibilityChangesUsePollingTargets(MonitoringMode mode, int seconds)
    {
        var source = new Source(); var monitor = Monitor(source); monitor.SetMode(MonitoringMode.Hidden); await monitor.StartAsync(); monitor.SetMode(mode); Assert.Equal(TimeSpan.FromSeconds(seconds), monitor.PollingInterval); await monitor.ShutdownAsync();
    }
    [Fact]
    public async Task GameModeUsesTenSecondsAndHiddenPauses()
    {
        var source = new Source { Snapshot = HardwareSnapshot.Unavailable(DateTimeOffset.UtcNow) with { IsGameDetected = true } }; var monitor = Monitor(source); monitor.SetMode(MonitoringMode.Hidden); await monitor.StartAsync(); await Task.Delay(60); Assert.Equal(0, source.Reads);
        await monitor.RefreshAsync(); monitor.SetMode(MonitoringMode.Foreground); Assert.Equal(TimeSpan.FromSeconds(10), monitor.PollingInterval); await monitor.ShutdownAsync();
    }
    [Fact]
    public async Task ShutdownBeforeStartIsIdempotent()
    {
        var source = new Source(); var monitor = Monitor(source); var first = monitor.ShutdownAsync(); Assert.Same(first, monitor.ShutdownAsync()); monitor.Dispose(); await monitor.DisposeAsync(); await monitor.StartAsync(); await monitor.RefreshAsync(); Assert.Equal(1, source.Disposals); Assert.Equal(0, source.Initializations);
    }
    [Fact]
    public async Task RepeatedStartsOwnOneInitializationAndLoop()
    {
        var source = new Source(); var monitor = Monitor(source); monitor.SetMode(MonitoringMode.Hidden); var first = monitor.StartAsync(); Assert.Same(first, monitor.StartAsync()); await first;
        for (int i = 0; i < 100; i++) { monitor.SetMode(MonitoringMode.Foreground); monitor.SetMode(MonitoringMode.Hidden); await monitor.StartAsync(); }
        await monitor.ShutdownAsync(); Assert.Equal(1, source.Initializations); Assert.Equal(1, source.Disposals);
    }
    [Fact]
    public async Task SnapshotReadRunsOnWorkerEvenWithCompletedInitialization()
    {
        var source = new Source(); var monitor = Monitor(source); monitor.SetMode(MonitoringMode.Hidden); await monitor.StartAsync(); await monitor.RefreshAsync(); Assert.True(source.Worker); await monitor.ShutdownAsync();
    }
    [Fact]
    public void MissingAndInvalidSensorsAreUnavailable()
    {
        DeviceReading[] devices = [new("cpu", "CPU", "CPU", []), new("gpu", "GPU", "GPU", [new("GPU Core", "Load", double.NaN), new("GPU Core", "Temperature", null)])];
        var cpu = Assert.Single(CpuMonitor.Project(devices)); var gpu = Assert.Single(new GpuMonitor().Read(devices, default)); Assert.Null(cpu.UsagePercent); Assert.Null(cpu.ClockGhz); Assert.Null(cpu.TemperatureC); Assert.Null(gpu.UsagePercent); Assert.Null(gpu.TemperatureC); Assert.Null(new MemorySnapshot(null, null).UsagePercent);
    }
    [Fact]
    public void ExtraGpuTemperaturesArePreservedWithoutVendorObjects()
    {
        DeviceReading[] devices = [new("gpu", "GPU", "GPU", [new("GPU Core", "Temperature", 45), new("GPU Hot Spot", "Temperature", 60), new("GPU Memory", "Temperature", 70)])];
        var gpu = Assert.Single(new GpuMonitor().Read(devices, default)); Assert.Equal(3, gpu.Temperatures.Length); Assert.Equal(60, gpu.Temperatures[1].Celsius);
    }
    [Fact]
    public void MultipleIdenticallyNamedGpusKeepSeparateStableIdentities()
    {
        DeviceReading[] devices = [new("/gpu/0", "Same GPU", "GPU", [new("GPU Core", "Load", 12)]), new("/gpu/1", "Same GPU", "GPU", [new("GPU Core", "Load", 91)])];
        var result = new GpuMonitor().Read(devices, default); Assert.Equal(2, result.Length); Assert.NotEqual(result[0].Id, result[1].Id); Assert.Equal(12, result[0].UsagePercent); Assert.Equal(91, result[1].UsagePercent);
    }
    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(10)]
    public void NetworkRatesUseRealElapsedTime(int elapsed)
    {
        var time = new SecondsClock(); var network = new NetworkMonitor(time); Assert.Null(network.Calculate([new("nic", 0, 0)], 0).DownloadBytesPerSecond);
        var rate = network.Calculate([new("nic", 1000 * elapsed, 400 * elapsed)], elapsed); Assert.Equal(1000, rate.DownloadBytesPerSecond); Assert.Equal(400, rate.UploadBytesPerSecond);
    }
    [Fact]
    public void NetworkResetsDoNotFabricateSpikes()
    {
        var network = new NetworkMonitor(new SecondsClock()); network.Calculate([new("nic", 1000, 1000)], 0);
        Assert.Null(network.Calculate([new("nic", 10, 10)], 1).DownloadBytesPerSecond);
        Assert.Null(network.Calculate([new("new", 100000, 100000)], 2).DownloadBytesPerSecond);
        Assert.Equal(0, network.Calculate([new("new", 100000, 100000)], 3).DownloadBytesPerSecond);
        network.Reset(); Assert.Null(network.Calculate([new("new", 200000, 200000)], 4).DownloadBytesPerSecond);
    }
    [Fact]
    public void CircularHistoryIsBoundedChronologicalAndImmutable()
    {
        var history = new HardwareHistory(90); var begin = DateTimeOffset.UtcNow;
        for (int i = 0; i < 150; i++) history.Add(HardwareSnapshot.Unavailable(begin.AddSeconds(i)) with { Cpus = [new("cpu", "CPU", i, null, i)], Memory = new(100, 50), Network = new(i, 2 * i), Gpus = [new("gpu", "GPU", i, i)] });
        var samples = history.Snapshot(); Assert.Equal(90, samples.Length); Assert.Equal(60, samples[0].CpuUsage); Assert.Equal(149, samples[^1].CpuUsage); Assert.Equal(50, samples[0].RamUsage); Assert.Equal(120, samples[0].Upload); Assert.Equal(60, samples[0].Gpus[0].TemperatureC);
        history.Add(HardwareSnapshot.Unavailable(begin)); Assert.Equal(60, samples[0].CpuUsage); Assert.Equal(90, history.Snapshot().Length);
    }
    [Fact]
    public void HistoryCapacityRejectsUnboundedConfiguration()
    { Assert.Throws<ArgumentOutOfRangeException>(() => new HardwareHistory(10000)); }
    [Fact]
    public async Task UnavailableSnapshotStillPublishesOtherSubsystems()
    {
        var source = new Source { Snapshot = HardwareSnapshot.Unavailable(DateTimeOffset.UtcNow) with { Memory = new(100, 25), Network = new(40, 20) } }; var monitor = Monitor(source); monitor.SetMode(MonitoringMode.Hidden); await monitor.StartAsync(); HardwareSnapshot? snapshot = null; monitor.SnapshotAvailable += value => snapshot = value; await monitor.RefreshAsync(); await monitor.ShutdownAsync(); Assert.NotNull(snapshot); Assert.Empty(snapshot.Cpus); Assert.Equal(75, snapshot.Memory.UsagePercent); Assert.Equal(40, snapshot.Network.DownloadBytesPerSecond);
    }
    [Fact]
    public async Task TemporarySourceFailurePublishesUnavailableAndRecovers()
    {
        var source = new Source { FailRead = true }; var monitor = Monitor(source); monitor.SetMode(MonitoringMode.Hidden); await monitor.StartAsync();
        await monitor.RefreshAsync(); Assert.Null(monitor.CurrentSnapshot.Memory.UsagePercent);
        source.FailRead = false; source.Snapshot = source.Snapshot with { Memory = new(100, 50) }; await monitor.RefreshAsync(); Assert.Equal(50, monitor.CurrentSnapshot.Memory.UsagePercent); await monitor.ShutdownAsync();
    }
    [Fact]
    public async Task VisibilityWakeResumesWithoutWaitingForGameInterval()
    {
        var source = new Source { Snapshot = HardwareSnapshot.Unavailable(DateTimeOffset.UtcNow) with { IsGameDetected = true } }; var monitor = Monitor(source); monitor.SetMode(MonitoringMode.Hidden); await monitor.StartAsync(); await monitor.RefreshAsync(); int before = source.Reads;
        var published = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); monitor.SnapshotAvailable += _ => published.TrySetResult(); monitor.SetMode(MonitoringMode.Foreground); await Wait(published.Task); Assert.True(source.Reads > before); await monitor.ShutdownAsync();
    }
    [Fact]
    public async Task ShutdownDrainsAllQueuedRefreshAdmissions()
    {
        var source = new Source { WaitRead = true, IgnoreCancellation = true }; var monitor = Monitor(source); monitor.SetMode(MonitoringMode.Hidden); await monitor.StartAsync(); var first = monitor.RefreshAsync(); await Wait(source.ReadEntered.Task);
        var queued = Enumerable.Range(0, 25).Select(_ => monitor.RefreshAsync()).ToArray(); var stop = monitor.ShutdownAsync(); source.ReadRelease.SetResult();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => first); foreach (var task in queued) await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task); await stop; Assert.Equal(1, source.Disposals); Assert.Equal(1, source.Reads);
    }
    [Fact]
    public void UiHardwareBoundaryContainsNoVendorOrLowLevelObjects()
    {
        var type = typeof(CortexDNA.ViewModels.HardwareViewModel);
        Assert.All(type.GetConstructors().SelectMany(c => c.GetParameters()), p => Assert.DoesNotContain("IHardwareSession", p.ParameterType.Name));
        Assert.All(type.GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic), f => Assert.DoesNotContain(f.FieldType.Namespace ?? "", new[] { "LibreHardwareMonitor.Hardware", "System.Management", "System.Net.NetworkInformation" }));
        Assert.All(typeof(HardwareSnapshot).Assembly.GetReferencedAssemblies(), a => Assert.DoesNotContain(a.Name!, new[] { "LibreHardwareMonitorLib", "System.Management", "PresentationFramework" }));
    }
    private sealed class SecondsClock : TimeProvider { public override long TimestampFrequency => 1; }
    private sealed class Source : IHardwareSnapshotSource
    {
        public readonly TaskCompletionSource InitEntered = new(TaskCreationOptions.RunContinuationsAsynchronously), InitRelease = new(TaskCreationOptions.RunContinuationsAsynchronously), ReadEntered = new(TaskCreationOptions.RunContinuationsAsynchronously), ReadRelease = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool WaitInitialization, WaitRead, IgnoreCancellation, Worker, FailRead; public TimeSpan Delay; public int Initializations, Reads, Disposals, MaxActive; private int _active;
        public HardwareSnapshot Snapshot = HardwareSnapshot.Unavailable(DateTimeOffset.UtcNow);
        public async Task InitializeAsync(CancellationToken token) { Interlocked.Increment(ref Initializations); InitEntered.TrySetResult(); if (WaitInitialization) { if (IgnoreCancellation) await InitRelease.Task; else await InitRelease.Task.WaitAsync(token); } token.ThrowIfCancellationRequested(); }
        public async Task<HardwareSnapshot> ReadAsync(CancellationToken token)
        {
            var active = Interlocked.Increment(ref _active); MaxActive = Math.Max(MaxActive, active); Interlocked.Increment(ref Reads); Worker = Thread.CurrentThread.IsThreadPoolThread; ReadEntered.TrySetResult();
            try { if (FailRead) throw new InvalidOperationException("Fixture temporary failure"); if (WaitRead) { if (IgnoreCancellation) await ReadRelease.Task; else await ReadRelease.Task.WaitAsync(token); } if (Delay > TimeSpan.Zero) await Task.Delay(Delay, token); return Snapshot; }
            finally { Interlocked.Decrement(ref _active); }
        }
        public void Dispose() { Assert.Equal(0, _active); Interlocked.Increment(ref Disposals); }
    }
}
