using CortexDNA.Models;

namespace CortexDNA.Core;

public interface ICleanupService
{
    IReadOnlyList<CleanupLocationItem> CreateDefaultLocations();
    Task<CleanupScanResult> ScanAsync(IReadOnlyList<CleanupLocationItem> locations,
        IProgress<CleanupProgress>? progress = null, CancellationToken cancellationToken = default);
    Task<CleanupCleanResult> CleanAsync(IReadOnlyList<CleanupLocationItem> selectedLocations,
        IProgress<CleanupProgress>? progress = null, CancellationToken cancellationToken = default);
}