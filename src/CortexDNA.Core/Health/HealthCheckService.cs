using System.Collections.Immutable;
namespace CortexDNA.Core.Health;

/// <summary>One serialized, manually requested scan. No polling, mutations or native resource ownership.</summary>
public sealed class HealthCheckService : IHealthCheckService
{
    private readonly IHealthCheckProvider[] _providers;
    private readonly SemaphoreSlim _scanGate = new(1, 1);
    public HealthCheckService(IEnumerable<IHealthCheckProvider> providers)
    {
        _providers = providers.ToArray();
        var definitions = _providers.SelectMany(p => p.Definitions).ToArray();
        if (definitions.Sum(d => d.Weight) != 100 || definitions.Any(d => d.Weight < 0) ||
            definitions.Select(d => d.Node).Distinct().Count() != definitions.Length)
            throw new ArgumentException("Provider nodes must be unique and weights total 100.");
    }
    public async Task<HealthCheckResult> ScanAsync(HealthCheckContext context,
        IProgress<HealthCheckProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        await _scanGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var items = ImmutableArray.CreateBuilder<HealthCheckItem>();
            for (int index = 0; index < _providers.Length; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var provider = _providers[index];
                await ReportAsync(new(provider.Definitions.Select(d => d.Node).ToImmutableArray(), index, _providers.Length, items.ToImmutable()));
                ImmutableArray<HealthCheckItem> batch;
                try
                {
                    batch = await provider.CheckAsync(context, cancellationToken).ConfigureAwait(false);
                    cancellationToken.ThrowIfCancellationRequested();
                    if (batch.Length != provider.Definitions.Count ||
                        provider.Definitions.Any(d => batch.Count(i => i.Node == d.Node && i.Weight == d.Weight && i.Area == d.Area) != 1) ||
                        batch.Any(i => !Enum.IsDefined(i.State) || i.PointsDeducted < 0 || i.PointsDeducted > i.Weight ||
                            (i.State is HealthState.Healthy or HealthState.Unavailable && i.PointsDeducted != 0) ||
                            (i.PointsDeducted > 0 && string.IsNullOrWhiteSpace(i.Explanation))))
                        throw new InvalidOperationException("Invalid provider result.");
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
                catch
                {
                    batch = provider.Definitions.Select(d => new HealthCheckItem(d.Node, d.Area, HealthState.Unavailable,
                        "Check unavailable", "The local provider could not read this area. No deduction was applied.", d.Weight)).ToImmutableArray();
                }
                items.AddRange(batch);
                await ReportAsync(new([], index + 1, _providers.Length, items.ToImmutable()));
            }
            cancellationToken.ThrowIfCancellationRequested();
            var completed = items.ToImmutable();
            var h = context.Hardware;
            return new(DateTimeOffset.Now, h.Timestamp, h.Info.OsName ?? "Windows version unavailable",
                $"{string.Join(" / ", h.Cpus.Select(x => x.Name))} · {string.Join(" / ", h.Gpus.Select(x => x.Name))} · RAM {(h.Memory.TotalBytes.HasValue ? $"{h.Memory.TotalBytes.Value / 1073741824d:F1} GB" : "unavailable")}",
                completed, HealthScore.Calculate(completed));
        }
        finally { _scanGate.Release(); }
        async ValueTask ReportAsync(HealthCheckProgress value)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (progress is IAsyncHealthCheckProgress asyncProgress)
                await asyncProgress.ReportAsync(value, cancellationToken).ConfigureAwait(false);
            else progress?.Report(value);
        }
    }
}
public sealed class DelegateHealthProvider(IReadOnlyList<HealthCheckDefinition> definitions,
    Func<HealthCheckContext, CancellationToken, Task<ImmutableArray<HealthCheckItem>>> check) : IHealthCheckProvider
{
    public IReadOnlyList<HealthCheckDefinition> Definitions { get; } = definitions;
    public Task<ImmutableArray<HealthCheckItem>> CheckAsync(HealthCheckContext context, CancellationToken cancellationToken)
        => check(context, cancellationToken);
}
