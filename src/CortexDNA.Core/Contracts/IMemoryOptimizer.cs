using CortexDNA.Models;

namespace CortexDNA.Core;

public interface IMemoryOptimizer
{
    Task<RamOptimizeResult> OptimizeMemoryAsync(CancellationToken cancellationToken = default);
}