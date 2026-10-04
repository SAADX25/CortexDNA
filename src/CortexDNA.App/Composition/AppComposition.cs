using CortexDNA.Core;
using CortexDNA.Core.Startup;
using CortexDNA.Hardware;
using CortexDNA.ViewModels;

namespace CortexDNA;

/// <summary>
/// Explicit composition root: one graph per window, no global service cache or new packages.
/// Parameterless constructors remain for WPF and the existing regression harness.
/// ViewModels accept interfaces; MainWindow owns their existing asynchronous shutdown.
/// </summary>
internal static class AppComposition
{
    internal static ICleanupService CreateCleanupService() => new DiskCleanupService();
    internal static IMemoryOptimizer CreateMemoryOptimizer() => new MemoryOptimizer();
    internal static IHardwareSession CreateHardwareSession() => new HardwareSession();
    internal static IStartupService CreateStartupService() => new StartupFeatureService(
        new StartupCatalogService(), new StartupApprovalService(),
        new StartupDelayService(), new StartupImpactService());

    internal static HardwareViewModel CreateHardwareViewModel() => new(
        CreateCleanupService(), CreateMemoryOptimizer(), CreateHardwareSession());
    internal static StartupViewModel CreateStartupViewModel() => new(CreateStartupService());
}