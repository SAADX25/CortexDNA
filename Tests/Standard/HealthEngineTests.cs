using System.Collections.Immutable;
using CortexDNA.Core.Health;
using CortexDNA.Models;
using CortexDNA.SystemHealth;
using Xunit;
namespace CortexDNA.AutomatedTests;

public sealed class HealthEngineTests
{
    private static HealthCheckContext Context() => new(new(DateTimeOffset.UtcNow,
        [new("cpu", "CPU", 20, 3, 55)], [new("gpu", "GPU", 10, 60)], new(1000, 500),
        [new("C:\\", "System", 1000, 500)], new(0, 0), new(OsName: "Windows fixture"), false, TimeSpan.FromHours(1)), DateTimeOffset.UtcNow, "C:\\");
    private static IHealthCheckProvider Provider(HealthNode node, HealthArea area, int weight, HealthState state = HealthState.Healthy, int points = 0)
        => SnapshotHealthProviders.One(node, area, weight, _ => new(node, area, state, "Fixture observation", "Fixture deduction reason", weight, points));
    private static IHealthCheckProvider[] Extra() =>
    [
        Provider(HealthNode.Security,HealthArea.Security,25),
        Provider(HealthNode.Startup,HealthArea.Startup,20),
        Provider(HealthNode.Updates,HealthArea.Updates,15),
        Provider(HealthNode.Cleanup,HealthArea.Maintenance,10)
    ];
    private static HealthCheckService Service(IEnumerable<IHealthCheckProvider>? extras = null)
        => new(SnapshotHealthProviders.Create().Concat(extras ?? Extra()));

    [Fact]
    public async Task AggregatesNineNodesAndReportsProviderProgress()
    {
        var progress = new RecordingProgress();
        var result = await Service().ScanAsync(Context(), progress);
        Assert.Equal(9, result.Items.Length); Assert.Equal(100, result.Score.Value);
        Assert.Equal(16, progress.Items.Count);
        Assert.Equal(8, progress.Items.Last().CompletedProviders);
        Assert.Empty(progress.Items.Last().ActiveNodes);
        Assert.Equal(9, progress.Items.Last().CompletedItems.Length);
    }
    [Fact]
    public async Task ProviderFailureIsUnavailableAndDoesNotLoseOtherResults()
    {
        var extras = Extra();
        extras[0] = new DelegateHealthProvider([new(HealthNode.Security, HealthArea.Security, 25)], (_, _) => throw new InvalidOperationException("sensitive native details"));
        var result = await Service(extras).ScanAsync(Context());
        Assert.Equal(9, result.Items.Length); Assert.Null(result.Score.Value);
        Assert.Equal(75, result.Score.AssessedPoints);
        Assert.Equal(HealthState.Unavailable, result.Items.Single(i => i.Node == HealthNode.Security).State);
        Assert.DoesNotContain("sensitive", string.Join(" ", result.Items.Select(i => i.Explanation)));
    }
    [Fact]
    public async Task CancellationBeforeScanCallsNoProvider()
    {
        int calls = 0;
        var service = new HealthCheckService([new DelegateHealthProvider([new(HealthNode.Cpu, HealthArea.Hardware, 100)], (_, _) => { calls++; throw new Exception(); })]);
        using var cts = new CancellationTokenSource(); cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.ScanAsync(Context(), cancellationToken: cts.Token));
        Assert.Equal(0, calls);
    }
    [Fact]
    public async Task CancellationDuringScanReleasesGateAndDoesNotRunFollowingProviders()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); int calls = 0;
        var first = new DelegateHealthProvider([new(HealthNode.Cpu, HealthArea.Hardware, 100)], async (_, t) =>
        {
            calls++; if (calls == 1) { entered.SetResult(); await Task.Delay(Timeout.Infinite, t); }
            return [new(HealthNode.Cpu, HealthArea.Hardware, HealthState.Healthy, "OK", "Observed", 100)];
        });
        var service = new HealthCheckService([first]); using var cts = new CancellationTokenSource();
        var scan = service.ScanAsync(Context(), cancellationToken: cts.Token);
        await entered.Task; cts.Cancel(); await Assert.ThrowsAnyAsync<OperationCanceledException>(() => scan);
        Assert.Equal(100, (await service.ScanAsync(Context())).Score.Value);
    }
    [Fact]
    public async Task ConcurrentScansAreSerialized()
    {
        int active = 0, max = 0, calls = 0;
        var service = new HealthCheckService([new DelegateHealthProvider([new(HealthNode.Cpu,HealthArea.Hardware,100)],async(_,t)=>
        {
            int count=Interlocked.Increment(ref active); max=Math.Max(max,count); Interlocked.Increment(ref calls);
            await Task.Delay(15,t); Interlocked.Decrement(ref active);
            return [new(HealthNode.Cpu,HealthArea.Hardware,HealthState.Healthy,"OK","Observed",100)];
        })]);
        await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => service.ScanAsync(Context())));
        Assert.Equal(1, max); Assert.Equal(8, calls);
    }
    [Fact]
    public async Task QueuedScanCancellationDoesNotAffectActiveScan()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var service = new HealthCheckService([new DelegateHealthProvider([new(HealthNode.Cpu,HealthArea.Hardware,100)],async(_,t)=>
        { entered.TrySetResult(); await release.Task.WaitAsync(t); return [new(HealthNode.Cpu,HealthArea.Hardware,HealthState.Healthy,"OK","Observed",100)]; })]);
        var active = service.ScanAsync(Context()); await entered.Task;
        using var cts = new CancellationTokenSource(); var queued = service.ScanAsync(Context(), cancellationToken: cts.Token); cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => queued); release.SetResult();
        Assert.Equal(100, (await active).Score.Value);
    }
    [Fact]
    public async Task MissingSnapshotCannotProduceFalseHealthyScore()
    {
        var c = Context() with { Hardware = HardwareSnapshot.Unavailable(DateTimeOffset.UtcNow) };
        var result = await Service(Extra().Select(p => new DelegateHealthProvider(p.Definitions, (_, _) => Task.FromResult(
            p.Definitions.Select(d => SnapshotHealthProviders.Missing(d.Node, d.Area, d.Weight, "Unknown")).ToImmutableArray())))).ScanAsync(c);
        Assert.All(result.Items, i => Assert.Equal(HealthState.Unavailable, i.State));
        Assert.Equal(0, result.Score.AssessedPoints); Assert.Equal("Not rated", result.Score.Display); Assert.Empty(result.Score.Deductions);
    }
    [Fact]
    public async Task StaleSnapshotIsUnavailableWithoutNewRead()
    {
        var c = Context(); var old = c.Hardware with { Timestamp = DateTimeOffset.UtcNow.AddMinutes(-1) };
        var result = await Service().ScanAsync(c with { Hardware = old });
        Assert.All(result.Items.Where(i => i.Area is HealthArea.Hardware or HealthArea.Storage or HealthArea.Network), i => Assert.Equal(HealthState.Unavailable, i.State));
        Assert.Equal(old.Timestamp, result.HardwareTimestamp);
    }
    [Theory]
    [InlineData(500, 0)]
    [InlineData(150, 0)]
    [InlineData(149, 2)]
    [InlineData(50, 2)]
    [InlineData(49, 8)]
    public async Task SystemDriveFreeSpaceUsesDocumentedThresholds(long free, int deduction)
    {
        var c = Context(); var h = c.Hardware with { Storage = [new("C:\\", "System", 1000, free), new("D:\\", "Other", 1000, 1)] };
        var result = await Service().ScanAsync(c with { Hardware = h });
        Assert.Equal(deduction, result.Items.Single(i => i.Node == HealthNode.Storage).PointsDeducted);
        Assert.Equal(100 - deduction, result.Score.Value);
    }
    [Fact]
    public async Task MultipleGpusUseHottestKnownTemperature()
    {
        var c = Context(); var h = c.Hardware with { Gpus = [new("a", "GPU A", 10, 60), new("b", "GPU B", 10, 94)] };
        var result = await Service().ScanAsync(c with { Hardware = h });
        var gpu = result.Items.Single(i => i.Node == HealthNode.Gpu);
        Assert.Equal(HealthState.Attention, gpu.State); Assert.Equal(2, gpu.PointsDeducted); Assert.Contains("94", gpu.Summary);
    }
    [Fact]
    public async Task PartialGpuSensorCoverageDoesNotAssumeUnmeasuredDeviceHealthy()
    {
        var c = Context(); var h = c.Hardware with { Gpus = [new("a", "GPU A", 10, 60), new("b", "GPU B", 10, null)] };
        var result = await Service().ScanAsync(c with { Hardware = h });
        Assert.Equal(HealthState.Unavailable, result.Items.Single(i => i.Node == HealthNode.Gpu).State);
        Assert.Equal(97, result.Score.AssessedPoints); Assert.Null(result.Score.Value);
    }
    [Fact]
    public async Task ScoreDeductionIncludesReasonSeverityAreaAndPoints()
    {
        var extra = Extra(); extra[0] = Provider(HealthNode.Security, HealthArea.Security, 25, HealthState.Error, 15);
        var result = await Service(extra).ScanAsync(Context()); var d = Assert.Single(result.Score.Deductions);
        Assert.Equal(85, result.Score.Value); Assert.Equal(15, d.PointsDeducted); Assert.Equal(HealthState.Error, d.Severity);
        Assert.Equal(HealthArea.Security, d.Area); Assert.Equal("Fixture deduction reason", d.Reason);
    }
    [Fact]
    public async Task HarmlessOptimizationDoesNotBecomeErrorOrDeduction()
    {
        var extra = Extra(); extra[3] = Provider(HealthNode.Cleanup, HealthArea.Maintenance, 10, HealthState.OptimizationAvailable);
        var result = await Service(extra).ScanAsync(Context());
        Assert.Equal(100, result.Score.Value); Assert.Empty(result.Score.Deductions);
        Assert.DoesNotContain(result.Items, i => i.State == HealthState.Error);
    }
    [Fact]
    public async Task InvalidProviderResultsAreIsolated()
    {
        var extra = Extra(); extra[0] = Provider(HealthNode.Security, HealthArea.Security, 25, HealthState.Healthy, 40);
        var result = await Service(extra).ScanAsync(Context());
        Assert.Equal(75, result.Score.AssessedPoints); Assert.Empty(result.Score.Deductions);
    }
    [Fact]
    public void InvalidBudgetAndDuplicateNodesFailAtComposition()
    {
        Assert.Throws<ArgumentException>(() => new HealthCheckService([Provider(HealthNode.Cpu, HealthArea.Hardware, 90)]));
        Assert.Throws<ArgumentException>(() => new HealthCheckService([Provider(HealthNode.Cpu, HealthArea.Hardware, 50), Provider(HealthNode.Cpu, HealthArea.Hardware, 50)]));
    }
    [Fact]
    public async Task ReadOnlyScanDoesNotMutateInputSnapshotsOrStartupFacts()
    {
        var c = Context() with { Startup = [new(true, StartupImpactLevel.High), new(false, StartupImpactLevel.Low)] };
        var before = c.Hardware; var startup = c.Startup;
        var result = await new HealthCheckService(SnapshotHealthProviders.Create().Concat(WindowsHealthProviders.Create().Take(2))
            .Concat([Extra()[0], Extra()[2]])).ScanAsync(c);
        Assert.Same(before, c.Hardware); Assert.Equal(startup, c.Startup);
        Assert.Equal(4, result.Items.Single(i => i.Node == HealthNode.Startup).PointsDeducted);
        Assert.Equal(HealthState.OptimizationAvailable, result.Items.Single(i => i.Node == HealthNode.Startup).State);
        Assert.Equal(HealthState.Unavailable, result.Items.Single(i => i.Node == HealthNode.Cleanup).State);
    }
    [Fact]
    public async Task IncompleteStartupImpactIsUnavailableWithoutInventedPoints()
    {
        var c = Context() with { Startup = [new(true, StartupImpactLevel.High), new(true, StartupImpactLevel.NotMeasured)] };
        var p = WindowsHealthProviders.Create()[0]; var item = Assert.Single(await p.CheckAsync(c, CancellationToken.None));
        Assert.Equal(HealthState.Unavailable, item.State); Assert.Equal(0, item.PointsDeducted);
    }
    [Fact]
    public async Task StartupDeductionsAreCappedAtItsAreaBudget()
    {
        var c = Context() with { Startup = Enumerable.Repeat(new StartupHealthFact(true, StartupImpactLevel.High), 12).ToImmutableArray() };
        var item = Assert.Single(await WindowsHealthProviders.Create()[0].CheckAsync(c, CancellationToken.None));
        Assert.Equal(20, item.PointsDeducted); Assert.Equal(HealthState.OptimizationAvailable, item.State);
    }
    private sealed class RecordingProgress : IProgress<HealthCheckProgress>
    {
        public List<HealthCheckProgress> Items { get; } = [];
        public void Report(HealthCheckProgress value) => Items.Add(value);
    }
    [Fact]
    public async Task DeductionWithoutExplanationIsUnavailableEvidence()
    {
        var extra = Extra();
        extra[0] = SnapshotHealthProviders.One(HealthNode.Security, HealthArea.Security, 25, _ =>
            new(HealthNode.Security, HealthArea.Security, HealthState.Error, "Observed", "", 25, 10));
        var result = await Service(extra).ScanAsync(Context());
        Assert.Equal(HealthState.Unavailable, result.Items.Single(i => i.Node == HealthNode.Security).State);
        Assert.Empty(result.Score.Deductions);
    }
    [Fact]
    public async Task PreCanceledWindowsReadsCannotStartNativeQueries()
    {
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        foreach (var provider in WindowsHealthProviders.Create().Skip(2))
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => provider.CheckAsync(Context(), cancellation.Token));
    }
    [Fact]
    public async Task FastProviderWaitsForItsFinitePresentationStep()
    {
        int calls = 0;
        var service = new HealthCheckService([SnapshotHealthProviders.One(HealthNode.Cpu,HealthArea.Hardware,100,_=>
        { calls++; return new(HealthNode.Cpu,HealthArea.Hardware,HealthState.Healthy,"Observed","Observed",100); })]);
        var progress = new AwaitedProgress(); var scan = service.ScanAsync(Context(), progress);
        await progress.Entered.Task; Assert.Equal(0, calls); progress.Release.SetResult();
        Assert.Equal(100, (await scan).Score.Value); Assert.Equal(1, calls);
    }
    [Fact]
    public async Task CancellationDuringPresentationReleasesScanWithoutCallingProvider()
    {
        int calls = 0;
        var service = new HealthCheckService([SnapshotHealthProviders.One(HealthNode.Cpu,HealthArea.Hardware,100,_=>
        { calls++; return new(HealthNode.Cpu,HealthArea.Hardware,HealthState.Healthy,"Observed","Observed",100); })]);
        using var cancellation = new CancellationTokenSource();
        var progress = new AwaitedProgress(); var scan = service.ScanAsync(Context(), progress, cancellation.Token);
        await progress.Entered.Task; cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => scan); Assert.Equal(0, calls);
        Assert.Equal(100, (await service.ScanAsync(Context())).Score.Value);
    }
    private sealed class AwaitedProgress : IAsyncHealthCheckProgress
    {
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public void Report(HealthCheckProgress value) => throw new InvalidOperationException("The awaited path is required.");
        public async ValueTask ReportAsync(HealthCheckProgress value, CancellationToken cancellationToken)
        {
            if (value.ActiveNodes.Length == 0) return;
            Entered.TrySetResult(); await Release.Task.WaitAsync(cancellationToken);
        }
    }
}
