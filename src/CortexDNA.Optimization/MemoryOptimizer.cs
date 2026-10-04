using CortexDNA.Models;

namespace CortexDNA.Core;

/// <summary>Injectable adapter retaining the Update-30 working-set safety implementation.</summary>
public sealed class MemoryOptimizer : IMemoryOptimizer
{
    public Task<RamOptimizeResult> OptimizeMemoryAsync(CancellationToken cancellationToken = default) =>
        RamOptimizer.OptimizeMemoryAsync(cancellationToken);
}