using System.Collections.Immutable;
using CortexDNA.Models;
namespace CortexDNA.Core.Health;

public enum HealthState { Healthy, Attention, OptimizationAvailable, Error, Unavailable }
public enum HealthArea { Hardware, Storage, Startup, Updates, Maintenance, Security, Network }
public enum HealthNode { Cpu, Gpu, Memory, Storage, Network, Startup, Security, Cleanup, Updates }
public sealed record HealthCheckDefinition(HealthNode Node, HealthArea Area, int Weight);
public sealed record HealthCheckItem(HealthNode Node, HealthArea Area, HealthState State, string Summary,
    string Explanation, int Weight, int PointsDeducted = 0, string? Recommendation = null);
public sealed record HealthDeduction(string Reason, HealthState Severity, HealthArea Area, int PointsDeducted);
public sealed record HealthScore(int EarnedPoints, int AssessedPoints, ImmutableArray<HealthDeduction> Deductions)
{
    public int CoveragePercent => AssessedPoints;
    public int? Value => AssessedPoints == 100 ? EarnedPoints : null;
    public string Display => AssessedPoints == 0 ? "Not rated" : $"{EarnedPoints}/{AssessedPoints}";
    public string Coverage => AssessedPoints == 100 ? "Complete assessment · 100 points" : $"Partial assessment · {AssessedPoints}% coverage";
    public static HealthScore Calculate(IEnumerable<HealthCheckItem> source)
    {
        var items = source.ToArray();
        if (items.Sum(i => i.Weight) != 100 || items.Any(i => i.Weight < 0 ||
            i.PointsDeducted < 0 || i.PointsDeducted > i.Weight ||
            (i.State is HealthState.Healthy or HealthState.Unavailable && i.PointsDeducted != 0) ||
            (i.PointsDeducted > 0 && string.IsNullOrWhiteSpace(i.Explanation))))
            throw new ArgumentException("Health weights must total 100 and deductions must fit assessed budgets.");
        int assessed = items.Where(i => i.State != HealthState.Unavailable).Sum(i => i.Weight);
        var deductions = items.Where(i => i.PointsDeducted > 0)
            .Select(i => new HealthDeduction(i.Explanation, i.State, i.Area, i.PointsDeducted)).ToImmutableArray();
        return new(assessed - deductions.Sum(d => d.PointsDeducted), assessed, deductions);
    }
}
public sealed record StartupHealthFact(bool Enabled, StartupImpactLevel Impact);
public sealed record HealthCheckContext(HardwareSnapshot Hardware, DateTimeOffset ScanStarted, string SystemDrive)
{
    public ImmutableArray<StartupHealthFact> Startup { get; init; } = [];
}
public sealed record HealthCheckProgress(ImmutableArray<HealthNode> ActiveNodes, int CompletedProviders,
    int TotalProviders, ImmutableArray<HealthCheckItem> CompletedItems);
public sealed record HealthCheckResult(DateTimeOffset Timestamp, DateTimeOffset HardwareTimestamp,
    string OsSummary, string HardwareSummary, ImmutableArray<HealthCheckItem> Items, HealthScore Score)
{
    public int RecommendationCount => Items.Count(i => i.Recommendation != null);
}
public interface IHealthCheckProvider
{
    IReadOnlyList<HealthCheckDefinition> Definitions { get; }
    Task<ImmutableArray<HealthCheckItem>> CheckAsync(HealthCheckContext context, CancellationToken cancellationToken);
}
public interface IHealthCheckService
{
    Task<HealthCheckResult> ScanAsync(HealthCheckContext context, IProgress<HealthCheckProgress>? progress = null,
        CancellationToken cancellationToken = default);
}
/// <summary>Optional awaited presentation, so a fast read can still have a visible, cancellation-aware transition.</summary>
public interface IAsyncHealthCheckProgress : IProgress<HealthCheckProgress>
{
    ValueTask ReportAsync(HealthCheckProgress value, CancellationToken cancellationToken);
}
