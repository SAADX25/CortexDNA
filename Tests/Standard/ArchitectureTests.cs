using System.IO;
using CortexDNA.Core;
using CortexDNA.Core.Startup;
using CortexDNA.Hardware;
using CortexDNA.Models;
using CortexDNA.ViewModels;
using LibreHardwareMonitor.Hardware;
using Xunit;

namespace CortexDNA.AutomatedTests;

public sealed class ArchitectureTests
{
    [Fact]
    public void CoreHasNoWindowsOrPresentationDependencies()
    {
        var assembly = typeof(ICleanupService).Assembly;
        Assert.Equal("CortexDNA.Core", assembly.GetName().Name);
        Assert.All(assembly.GetReferencedAssemblies(), reference => {
            Assert.DoesNotContain(reference.Name!, new[] { "CortexDNA", "PresentationFramework",
                "PresentationCore", "WindowsBase", "System.Windows.Forms", "System.Management",
                "Microsoft.Win32.Registry", "LibreHardwareMonitorLib" });
            Assert.False(reference.Name!.StartsWith("CortexDNA.", StringComparison.Ordinal));
        });
    }

    [Fact]
    public void ImplementationsBelongToIndependentModulesWithoutUiReferences()
    {
        (Type Type, string Assembly)[] modules = [
            (typeof(DiskCleanupService), "CortexDNA.Cleanup"),
            (typeof(StartupFeatureService), "CortexDNA.Startup"),
            (typeof(NativeMethods), "CortexDNA.System"),
            (typeof(Logger), "CortexDNA.Infrastructure"),
            (typeof(RamOptimizer), "CortexDNA.Optimization"),
            (typeof(HardwareSession), "CortexDNA.Hardware")];
        foreach (var module in modules)
        {
            Assert.Equal(module.Assembly, module.Type.Assembly.GetName().Name);
            Assert.All(module.Type.Assembly.GetReferencedAssemblies(), reference =>
                Assert.DoesNotContain(reference.Name!, new[] { "CortexDNA", "PresentationFramework",
                    "PresentationCore", "WindowsBase" }));
        }
    }

    [Fact]
    public void ProjectGraphHasNoCyclesAndCoreHasNoFrameworkOrPackageReferences()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "CortexDNA.slnx")))
            directory = directory.Parent;
        Assert.NotNull(directory);
        var root = directory!.FullName;
        var projects = Directory.GetFiles(Path.Combine(root, "src"), "*.csproj", SearchOption.AllDirectories)
            .Where(path => !path.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar))
            .Select(Path.GetFullPath).ToArray();
        Assert.Equal(8, projects.Length);
        var visiting = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        void Visit(string path)
        {
            if (visited.Contains(path)) return;
            Assert.True(visiting.Add(path), $"Dependency cycle: {path}");
            var document = System.Xml.Linq.XDocument.Load(path);
            foreach (var reference in document.Descendants("ProjectReference"))
            {
                var target = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(path)!,
                    reference.Attribute("Include")!.Value.Replace('/', Path.DirectorySeparatorChar)));
                Assert.Contains(target, projects);
                Assert.NotEqual(Path.Combine(root, "src", "CortexDNA.App", "CortexDNA.App.csproj"), target);
                Visit(target);
            }
            visiting.Remove(path);
            visited.Add(path);
        }
        foreach (var project in projects) Visit(project);
        var core = System.Xml.Linq.XDocument.Load(Path.Combine(root, "src", "CortexDNA.Core", "CortexDNA.Core.csproj"));
        Assert.Empty(core.Descendants("ProjectReference"));
        Assert.Empty(core.Descendants("FrameworkReference"));
        Assert.Empty(core.Descendants("PackageReference"));
        Assert.Empty(core.Descendants("UseWPF"));
    }

    [Fact]
    public async Task StartupViewModelUsesInjectedServiceAndForwardsMigrationChoice()
    {
        var service = new FakeStartup();
        using var vm = new StartupViewModel(service);
        await vm.LoadAsync(force: true, migrateLegacy: false);
        Assert.False(service.MigrateLegacy);
        Assert.Single(vm.Items);
        var item = vm.Items[0];
        item.IsEnabled = false;
        Assert.Equal(1, service.EnableCalls);
        Assert.False(item.Model.IsEnabled);
        item.DelayCommand.Execute(null);
        Assert.Equal(1, service.DelayCalls);
        Assert.True(item.IsDelayed);
        item.RemoveDelayCommand.Execute(null);
        Assert.Equal(1, service.RemoveCalls);
        Assert.True(item.IsEnabled);
        Assert.False(item.IsDelayed);
    }

    [Fact]
    public async Task StartupFailureKeepsDisplayedStateAndReportsFailure()
    {
        var service = new FakeStartup { RefuseWrites = true };
        using var vm = new StartupViewModel(service);
        await vm.LoadAsync(true, false);
        var item = Assert.Single(vm.Items);
        item.IsEnabled = false;
        Assert.True(item.IsEnabled);
        Assert.True(item.Model.IsEnabled);
        Assert.Equal("Could not change this startup item", vm.StatusMessage);
    }

    [Fact]
    public async Task StartupShutdownWaitsForLoadAndSuppressesLateItems()
    {
        var completion = new TaskCompletionSource<StartupSnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);
        var service = new FakeStartup { Pending = completion.Task };
        using var vm = new StartupViewModel(service);
        var load = vm.LoadAsync(true, false);
        var shutdown = vm.ShutdownAsync();
        Assert.False(shutdown.IsCompleted);
        completion.SetResult(service.Snapshot);
        await shutdown.WaitAsync(TimeSpan.FromSeconds(5));
        await load;
        Assert.Empty(vm.Items);
        Assert.False(vm.IsLoading);
    }

    [Fact]
    public async Task InjectedHardwareSessionClosesOnceWhenInitializationIsCancelled()
    {
        var hardware = new FakeHardware();
        var cleanup = new FakeCleanup();
        var memory = new FakeMemory();
        var vm = new HardwareViewModel(cleanup, memory, hardware);
        var shutdown = vm.ShutdownAsync();
        Assert.Same(shutdown, vm.ShutdownAsync());
        await shutdown.WaitAsync(TimeSpan.FromSeconds(5));
        vm.ResumeMonitoring();
        vm.RequestRefresh();
        Assert.Equal(0, hardware.OpenCalls);
        Assert.Equal(0, hardware.RefreshCalls);
        Assert.Equal(1, hardware.CloseCalls);
        Assert.Equal(0, cleanup.Calls);
        Assert.Equal(0, memory.Calls);
    }

    private sealed class FakeStartup : IStartupService
    {
        public bool MigrateLegacy { get; private set; } = true;
        public int EnableCalls, DelayCalls, RemoveCalls;
        public bool RefuseWrites;
        public Task<StartupSnapshot>? Pending;
        public StartupSnapshot Snapshot { get; } = new() { Items = [new StartupItem {
            Id = "fixture", Name = "Fixture", Command = "fixture.exe", LocationLabel = "Fixture",
            Location = StartupLocationKind.CurrentUserRun }] };
        public StartupSnapshot Load(bool migrateLegacy = true) { MigrateLegacy = migrateLegacy; return Snapshot; }
        public Task<StartupSnapshot> LoadAsync(bool migrateLegacy = true) => Pending ?? Task.FromResult(Load(migrateLegacy));
        public void SetEnabled(StartupItem item, bool enabled) {
            EnableCalls++;
            if (RefuseWrites) throw new InvalidOperationException("Fixture refusal");
            item.IsEnabled = enabled;
        }
        public void Delay(StartupItem item) { DelayCalls++; item.IsDelayed = true; item.IsEnabled = false; }
        public void RemoveDelay(StartupItem item) { RemoveCalls++; item.IsDelayed = false; item.IsEnabled = true; }
    }
    private sealed class FakeHardware : IHardwareSession
    {
        public int OpenCalls, RefreshCalls, CloseCalls;
        public IEnumerable<IHardware> Hardware => [];
        public void Open() => Interlocked.Increment(ref OpenCalls);
        public void Refresh() => Interlocked.Increment(ref RefreshCalls);
        public void Close() => Interlocked.Increment(ref CloseCalls);
    }
    private sealed class FakeCleanup : ICleanupService
    {
        public int Calls;
        public IReadOnlyList<CleanupLocationItem> CreateDefaultLocations() { Calls++; throw new InvalidOperationException(); }
        public Task<CleanupScanResult> ScanAsync(IReadOnlyList<CleanupLocationItem> locations,
            IProgress<CleanupProgress>? progress = null, CancellationToken cancellationToken = default) {
            Calls++; throw new InvalidOperationException();
        }
        public Task<CleanupCleanResult> CleanAsync(IReadOnlyList<CleanupLocationItem> locations,
            IProgress<CleanupProgress>? progress = null, CancellationToken cancellationToken = default) {
            Calls++; throw new InvalidOperationException();
        }
    }
    private sealed class FakeMemory : IMemoryOptimizer
    {
        public int Calls;
        public Task<RamOptimizeResult> OptimizeMemoryAsync(CancellationToken cancellationToken = default) {
            Calls++; throw new InvalidOperationException();
        }
    }
}
