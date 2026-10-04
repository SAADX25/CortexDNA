using CortexDNA.Core;
using CortexDNA.Core.Startup;
using CortexDNA.Hardware;
using CortexDNA.Services;
using CortexDNA.ViewModels;
namespace CortexDNA;

internal sealed record ShellDependencies(HardwareViewModel Hardware, StartupViewModel Startup, AppearanceService Appearance, NotificationCenter Notifications);
internal static class AppComposition
{
    internal static ICleanupService CreateCleanupService() => new DiskCleanupService();
    internal static IMemoryOptimizer CreateMemoryOptimizer() => new MemoryOptimizer();
    internal static IHardwareSession CreateHardwareSession() => new HardwareSession();
    internal static IStartupService CreateStartupService() => new StartupFeatureService(new StartupCatalogService(), new StartupApprovalService(), new StartupDelayService(), new StartupImpactService());
    internal static IDialogService CreateDialogService() => new WpfDialogService(new NotificationCenter());
    internal static HardwareViewModel CreateHardwareViewModel() => new(CreateCleanupService(), CreateMemoryOptimizer(), CreateHardwareSession(), CreateDialogService());
    internal static StartupViewModel CreateStartupViewModel() => new(CreateStartupService());
    internal static ShellDependencies CreateShellDependencies()
    {
        var notifications = new NotificationCenter();
        var hardware = new HardwareViewModel(CreateCleanupService(), CreateMemoryOptimizer(), CreateHardwareSession(), new WpfDialogService(notifications));
        return new(hardware, CreateStartupViewModel(), new AppearanceService(), notifications);
    }
}
