using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using CortexDNA.Core;
using CortexDNA.Core.Health;
using CortexDNA.Controls;
using CortexDNA.Core.Startup;
using CortexDNA.Hardware;
using CortexDNA.Models;
using CortexDNA.Navigation;
using CortexDNA.Services;
using CortexDNA.UI;
using CortexDNA.ViewModels;
using CortexDNA.ViewModels.Pages;
using LibreHardwareMonitor.Hardware;

internal static class Program
{
    private static int _checks;
    private static readonly string Root = FindWorkspace();
    private static readonly string Output = Path.Combine(Root, "artifacts", "phase2-ui");
    [STAThread]
    private static int Main(string[] args)
    {
        Directory.CreateDirectory(Output);
        var app = new CortexDNA.App(); app.InitializeComponent(); typeof(System.Windows.Application).GetField("_startupUri", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(app, null); app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
        SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext());
        var frame = new DispatcherFrame(); var run = args.Contains("--hardware-metrics") ? MeasureHardwareAsync() :
            args.Contains("--health-local") ? ReadLocalHealthAsync() : Run();
        _ = run.ContinueWith(_ => app.Dispatcher.BeginInvoke(() => frame.Continue = false));
        Dispatcher.PushFrame(frame);
        try { run.GetAwaiter().GetResult(); Console.WriteLine($"UI SMOKE PASSED; CHECKS {_checks}"); return 0; }
        catch (Exception ex) { Console.WriteLine(ex); return 1; }
    }
    private static async Task MeasureHardwareAsync()
    {
        var output = Path.Combine(Root, "artifacts", "phase3-hardware-metrics.json");
        using var process = Process.GetCurrentProcess(); var clock = Stopwatch.StartNew();
        var monitor = new HardwareMonitorService(); int samples = 0; monitor.SnapshotAvailable += _ => Interlocked.Increment(ref samples);
        await monitor.StartAsync().WaitAsync(TimeSpan.FromSeconds(45));
        await Task.Delay(1500); process.Refresh(); double cpuBefore = process.TotalProcessorTime.TotalMilliseconds; long managedBefore = GC.GetTotalMemory(false); long workingBefore = process.WorkingSet64;
        var sampling = Stopwatch.StartNew(); await Task.Delay(5000); process.Refresh(); double foregroundCpuMs = process.TotalProcessorTime.TotalMilliseconds - cpuBefore; long workingAfter = process.WorkingSet64; long managedAfter = GC.GetTotalMemory(false);
        double foregroundWallMs = sampling.Elapsed.TotalMilliseconds; monitor.SetMode(MonitoringMode.Hidden); await Task.Delay(250); int hiddenBefore = samples; cpuBefore = process.TotalProcessorTime.TotalMilliseconds; var hidden = Stopwatch.StartNew(); await Task.Delay(2000); process.Refresh(); double hiddenCpuMs = process.TotalProcessorTime.TotalMilliseconds - cpuBefore;
        int historyCount = monitor.History.Length; await monitor.ShutdownAsync();
        var metrics = new { foregroundWallMs, foregroundCpuMs, workingSetBeforeBytes = workingBefore, workingSetAfterBytes = workingAfter, managedBeforeBytes = managedBefore, managedAfterBytes = managedAfter, hiddenWallMs = hidden.Elapsed.TotalMilliseconds, hiddenCpuMs, hiddenSnapshotDelta = samples - hiddenBefore, historyCount, samples, totalWallMs = clock.Elapsed.TotalMilliseconds, limit = "Short owned-process sample on this machine; no Update-32 comparative baseline. No sensor values/history persisted." };
        File.WriteAllText(output, System.Text.Json.JsonSerializer.Serialize(metrics, new System.Text.Json.JsonSerializerOptions { WriteIndented = true })); Console.WriteLine(File.ReadAllText(output));
    }
    private static async Task ReadLocalHealthAsync()
    {
        using var process=Process.GetCurrentProcess(); var cpu=process.TotalProcessorTime; var clock=Stopwatch.StartNew();
        var service=new HealthCheckService(SnapshotHealthProviders.Create().Concat(CortexDNA.SystemHealth.WindowsHealthProviders.Create()));
        var result=await service.ScanAsync(new(HardwareSnapshot.Unavailable(DateTimeOffset.MinValue),DateTimeOffset.Now,Path.GetPathRoot(Environment.SystemDirectory) ?? ""));
        Check(result.Items.Length==9,"local Windows read-only scan returns every node despite unavailable hardware");
        Check(result.Items.Where(i=>i.Area is HealthArea.Hardware or HealthArea.Storage or HealthArea.Network).All(i=>i.State==HealthState.Unavailable),"native health scan cannot invent absent hardware telemetry");
        Check(result.Items.Single(i=>i.Node==HealthNode.Cleanup).State==HealthState.Unavailable,"local health scan does not invent cleanup volume");
        Check(result.Score.Value==null,"incomplete native scan does not claim a full health score");
        process.Refresh();
        Console.WriteLine($"LOCAL HEALTH wallMs={clock.Elapsed.TotalMilliseconds:F1}; cpuMs={(process.TotalProcessorTime-cpu).TotalMilliseconds:F1}; coverage={result.Score.CoveragePercent}; states={string.Join(",",result.Items.Select(i=>$"{i.Node}:{i.State}"))}");
    }
    private static string FindWorkspace()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null) { if (File.Exists(Path.Combine(directory.FullName, "CortexDNA.slnx"))) return directory.FullName; directory = directory.Parent; }
        throw new InvalidOperationException("Workspace not found");
    }
    private static void Check(bool value, string name)
    { if (!value) throw new InvalidOperationException(name); _checks++; Console.WriteLine("PASS " + name); }
    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        yield return root;
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++) foreach (var child in Descendants(VisualTreeHelper.GetChild(root, i))) yield return child;
    }
    private static async Task Settle(CortexDNA.MainWindow window)
    { window.UpdateLayout(); await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle); await Task.Delay(30); window.UpdateLayout(); }
    private static void Capture(FrameworkElement view, string filename, double scale = 1)
    {
        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(view.ActualWidth * scale), (int)Math.Ceiling(view.ActualHeight * scale), 96 * scale, 96 * scale, PixelFormats.Pbgra32);
        bitmap.Render(view); var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var file = File.Create(Path.Combine(Output, filename)); encoder.Save(file);
    }
    private static async Task Run()
    {
        var errors = new BindingErrors(); PresentationTraceSources.DataBindingSource.Listeners.Add(errors);
        PresentationTraceSources.DataBindingSource.Switch.Level = SourceLevels.Error;
        var hardware = new FakeHardware(); var cleanup = new FakeCleanup(); var memory = new FakeMemory(); var dialogs = new FakeDialogs();
        var hardwareVm = new HardwareViewModel(cleanup, memory, new HardwareMonitorService(new WindowsHardwareSnapshotSource(hardware)), dialogs);
        // Cancel initialization immediately: the smoke exercises presentation without live monitoring or optimization.
        await hardwareVm.ShutdownAsync();
        var startupService = new FakeStartup(); var startup = new StartupViewModel(startupService);
        var preferences = Path.Combine(Output, "preferences.json");
        File.WriteAllText(preferences, "{\"ThemeFileName\":\"DarkTheme.xaml\",\"OpacityPercent\":100}");
        var appearance = new AppearanceService(preferences) { ReducedMotion = true };
        var healthService = new HealthFixtureService();
        var shell = new MainViewModel(hardwareVm, startup, appearance, new NotificationCenter(), healthService);
        Check(shell.NavigationItems.Count == 11 && shell.CurrentPage?.Id == PageId.Home, "eleven pages, Home initial, no excluded navigation");
        Check(shell.NavigationItems.Count(n => n.IsSelected) == 1, "single selected navigation item");
        var first = shell.CurrentPage!;
        Check(!shell.Navigation.Navigate(PageId.Home) && first.IsActive, "same-page navigation does not re-enter");
        Check(!shell.Navigation.Navigate((PageId)999), "invalid page fails closed");
        var trackHome = new TrackingPage(PageId.Home, shell); var trackHealth = new TrackingPage(PageId.Health, shell);
        var tracked = new NavigationService(new NavigationStore(), [trackHome, trackHealth]);
        tracked.Navigate(PageId.Home); tracked.Navigate(PageId.Home); tracked.Navigate(PageId.Health); tracked.Dispose(); tracked.Dispose();
        Check(trackHome.Entries == 1 && trackHealth.Entries == 1 && trackHome.Exits == 1 && trackHealth.Exits == 1 && trackHome.Disposals == 1 && trackHealth.Disposals == 1, "navigation lifecycle runs exactly once and disposal is idempotent");
        var gateDialogs = new FakeDialogs(); var gateCleanup = new FakeCleanup(); var gateMemory = new FakeMemory();
        var gateHardware = new HardwareViewModel(gateCleanup, gateMemory, new HardwareMonitorService(new WindowsHardwareSnapshotSource(new FakeHardware())), gateDialogs);
        gateHardware.BoostSystemCommand.Execute(null); gateHardware.CleanDiskCommand.Execute(null);
        Check(gateDialogs.ConfirmCalls == 1 && gateMemory.Calls == 0, "declining memory confirmation prevents optimization");
        Check(gateDialogs.CleanupConfirmCalls == 1 && gateCleanup.CleanCalls == 0 && !gateHardware.IsCleaningDisk, "declining cleanup selection prevents deletion and restores operation state");
        await gateHardware.ShutdownAsync();
        var snapshotMonitor = new FixtureMonitor();
        var snapshotVm = new HardwareViewModel(new FakeCleanup(), new FakeMemory(), snapshotMonitor, new FakeDialogs());
        var fixture = HardwareSnapshot.Unavailable(DateTimeOffset.UtcNow) with { Cpus = [new("cpu", "Fixture CPU", 30, 3.1, 45)], Gpus = [new("gpu0", "Same GPU", 12, 40), new("gpu1", "Same GPU", 80, 60)], Memory = new(100, 25), Network = new(1024, 2048) };
        snapshotMonitor.Publish(fixture);
        Check(snapshotVm.CpuList.Count == 1 && snapshotVm.GpuList.Count == 2 && snapshotVm.SystemInfo.RamUsagePercent == 75, "snapshot-only UI renders multiple GPUs and memory");
        var item = snapshotVm.GpuList[0]; snapshotMonitor.Publish(fixture with { Gpus = [new("gpu0", "Same GPU", null, null), new("gpu1", "Same GPU", 90, 65)] });
        Check(ReferenceEquals(item, snapshotVm.GpuList[0]) && item.Sensors.All(sensor => sensor.Value == "Unavailable"), "missing readings clear stale UI while stable GPU adapters are reused");
        snapshotMonitor.Publish(fixture with { Memory = new(0, 0) });
        Check(snapshotVm.SystemInfo.RamUsageText == "Unavailable", "zero-capacity memory is unavailable rather than fabricated telemetry");
        snapshotMonitor.Publish(fixture);
        using (var navigationStartup = new StartupViewModel(new FakeStartup()))
        using (var navigationShell = new MainViewModel(snapshotVm, navigationStartup, new AppearanceService(Path.Combine(Output, "navigation-fixture.json")), new NotificationCenter(), new HealthFixtureService { Hold = false }))
        {
            for (int i = 0; i < 110; i++) navigationShell.Navigation.Navigate((PageId)(i % 11));
            navigationShell.Navigation.Navigate(PageId.Health);
            await navigationShell.Health.StartScanAsync();
            Check(snapshotMonitor.Starts == 1 && snapshotMonitor.Refreshes == 0, "health scan consumes snapshots without starting or refreshing the hardware monitor");
            Check(snapshotMonitor.Starts == 1 && snapshotMonitor.Refreshes == 0, "repeated active navigation owns one monitor and never starts duplicate polling");
        }
        int afterDisposeChanges = 0; snapshotVm.PropertyChanged += (_, _) => afterDisposeChanges++;
        // Publish from a worker while the dispatcher is blocked so its UI update is queued.
        Task.Run(() => snapshotMonitor.Publish(fixture with { Timestamp = fixture.Timestamp.AddSeconds(5) })).GetAwaiter().GetResult();
        var before = snapshotVm.CurrentSnapshot; await snapshotVm.ShutdownAsync(); afterDisposeChanges = 0;
        await System.Windows.Application.Current.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
        snapshotMonitor.Publish(fixture);
        Check(snapshotVm.CurrentSnapshot == before && afterDisposeChanges == 0 && snapshotMonitor.Shutdowns == 1, "queued and late snapshot updates are suppressed after disposal");
        var window = new CortexDNA.MainWindow(shell); window.Show(); await Settle(window);
        var host = (ContentControl)window.FindName("PageHost");
        var pages = new Dictionary<PageId, PageViewModel>();
        foreach (var theme in new[] { "DarkTheme.xaml", "LightTheme.xaml" })
        {
            appearance.ThemeFileName = theme; appearance.Apply();
            foreach (var id in Enum.GetValues<PageId>())
            {
                var previous = shell.CurrentPage; shell.Navigation.Navigate(id); await Settle(window);
                var current = shell.CurrentPage!; pages[id] = current;
                Check(current.Id == id && current.IsActive && shell.NavigationItems.Count(n => n.IsSelected) == 1, $"{theme} {id} lifecycle and selection");
                if (previous != current) Check(!previous!.IsActive, $"{theme} {id} previous page deactivated");
                Check(Descendants(host).OfType<UserControl>().Any(c => c.GetType().Name == id + "Page"), $"{theme} {id} separate view resolves");
                if (theme == "DarkTheme.xaml" || id is PageId.Home or PageId.Settings) Capture(window, theme[..^5] + "-" + id + ".png");
            }
        }
        var watch = Stopwatch.StartNew();
        for (int i = 0; i < 110; i++) shell.Navigation.Navigate((PageId)(i % 11));
        watch.Stop(); await Settle(window);
        Check(pages.Values.All(p => ReferenceEquals(p.Hardware, hardwareVm)), "all pages share one hardware VM");
        Check(shell.CurrentPage == pages[PageId.Diagnostics], "navigation retains page instances");
        Check(startupService.Writes == 0 && cleanup.CleanCalls == 0 && memory.Calls == 0, "navigation does not invoke system writes or optimizers");
        shell.NavigateCommand.Execute("Overview"); Check(shell.CurrentPage?.Id == PageId.Home, "legacy Overview navigation alias");
        shell.Navigation.Navigate(PageId.Tools); var tools = (ToolsViewModel)shell.CurrentPage!;
        tools.Query = "registry"; Check(tools.Tools.Cast<ToolItem>().Count() == 1, "tools filter finds original allowlisted Registry Editor"); tools.Query = "";
        var cleanupPage = (CleanupViewModel)pages[PageId.Cleanup]; Check(cleanupPage.Locations.Count == 6, "cleanup shows only six original categories");
        await Phase4Smoke(shell, window, appearance, healthService);
        Check(errors.Errors.Count == 0, "all pages have zero WPF binding errors: " + string.Join("\n", errors.Errors));
        // Reduced motion disables both opacity and translation clocks, even during an active transition.
        appearance.ReducedMotion = false; Motion.Enter(host, true); appearance.ReducedMotion = true; Motion.Stop(host);
        Check(!host.HasAnimatedProperties && !((TranslateTransform)host.RenderTransform).HasAnimatedProperties && host.Opacity == 1, "Reduced Motion cancels transition clocks");
        Check(!appearance.MotionEnabled && !Motion.GetIsEnabled(host), "Reduced Motion inherited throughout shell");
        var observer = new OperationPresentation(hardwareVm, startup); int changes = 0; observer.PropertyChanged += (_, _) => changes++;
        typeof(HardwareViewModel).GetProperty("OperationProgress")!.SetValue(hardwareVm, 42d); Check(observer.Progress == 42 && changes > 0, "global progress observes real operation progress");
        observer.Dispose(); changes = 0; typeof(HardwareViewModel).GetProperty("OperationProgress")!.SetValue(hardwareVm, 43d); Check(changes == 0, "operation observer detaches on dispose");
        appearance.OpacityPercent = 1; Check(appearance.OpacityPercent == 45, "opacity remains clamped to legacy range");
        appearance.OpacityPercent = 100; appearance.Save();
        using (var loaded = new AppearanceService(preferences)) Check(loaded.ThemeFileName == "LightTheme.xaml" && loaded.ReducedMotion && loaded.OpacityPercent == 100, "theme opacity and Reduced Motion persist together");
        foreach (var dialog in new Window[] { new CortexDNA.ConfirmationWindow("Confirmation", "Continue only after explicit approval."), new CortexDNA.CleanConfirmationWindow(cleanup.CreateDefaultLocations()), new CortexDNA.CleanupResultsWindow(1024, 1), new CortexDNA.AboutWindow() })
        { dialog.Owner = window; dialog.Show(); await Settle(window); Check(dialog.IsLoaded, "shared design resources load " + dialog.GetType().Name); dialog.Close(); }
        window.Width = 1080; window.Height = 720; shell.Navigation.Navigate(PageId.Home); await Settle(window);
        Capture(window, "minimum-size.png"); Capture(window, "render-150-percent.png", 1.5); Capture(window, "render-200-percent.png", 2);
        Check(host.ActualWidth > 650 && host.ActualHeight > 0, "minimum window has usable page area");
        window.WindowState = WindowState.Minimized; await Settle(window);
        Check(!window.IsVisible && !host.HasAnimatedProperties, "minimize hides to tray and stops motion");
        window.Show(); window.WindowState = WindowState.Normal; await Settle(window);
        shell.Navigation.Navigate(PageId.Health); healthService.Reset(); var exitScan=shell.Health.StartScanAsync(); await Task.Delay(30);
        bool closed = false; window.Closed += (_, _) => closed = true;
        typeof(CortexDNA.MainWindow).GetMethod("RequestExit", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(window, null);
        for (int i = 0; i < 100 && !closed; i++) await Task.Delay(10);
        Check(closed && shell.CurrentPage == null && pages.Values.All(p => !p.IsActive), "explicit exit waits for shutdown and disposes navigation");
        Check(exitScan.IsCompleted && healthService.Cancellations > 0, "explicit exit awaits cancellation of the tracked health scan");
        Check(!shell.Navigation.Navigate(PageId.Home) && hardware.CloseCalls == 1, "disposed navigation refuses entry and hardware closes once");
        Check(errors.Errors.Count == 0, "dialogs and closing produce no binding errors: " + string.Join("\n", errors.Errors));
        PresentationTraceSources.DataBindingSource.Listeners.Remove(errors);
        File.WriteAllText(Path.Combine(Output, "metrics.txt"), $"Checks: {_checks}\n110 cached navigation changes (no layout waits): {watch.Elapsed.TotalMilliseconds:F2} ms\nRetained page VMs: {pages.Count}\nHardware sessions: 1\nSmoke monitor opens: {hardware.OpenCalls}\nSmoke system writes: {startupService.Writes + cleanup.CleanCalls + memory.Calls}\nPNG scaled renders do not emulate native Windows DPI settings.\n");
    }
    private sealed class FixtureMonitor : IHardwareMonitorService
    {
        public event Action<HardwareSnapshot>? SnapshotAvailable;
        public HardwareSnapshot CurrentSnapshot { get; private set; } = HardwareSnapshot.Unavailable(DateTimeOffset.UtcNow);
        public System.Collections.Immutable.ImmutableArray<HardwareHistorySample> History => [];
        public int Shutdowns, Starts, Refreshes; private Task? _shutdown;
        public void Publish(HardwareSnapshot value) { CurrentSnapshot = value; SnapshotAvailable?.Invoke(value); }
        public Task StartAsync(CancellationToken token = default) { Starts++; return Task.CompletedTask; }
        public Task RefreshAsync(CancellationToken token = default) { Refreshes++; return Task.CompletedTask; }
        public void SetMode(MonitoringMode mode) { }
        public Task ShutdownAsync() => _shutdown ??= Stop();
        private Task Stop() { Shutdowns++; return Task.CompletedTask; }
        public void Dispose() => _ = ShutdownAsync(); public async ValueTask DisposeAsync() => await ShutdownAsync();
    }
    private static async Task Phase4Smoke(MainViewModel shell,CortexDNA.MainWindow window,AppearanceService appearance,HealthFixtureService service)
    {
        appearance.ThemeFileName="DarkTheme.xaml"; appearance.Apply();
        appearance.ReducedMotion=false; shell.Navigation.Navigate(PageId.Health); await Settle(window);
        var host=(ContentControl)window.FindName("PageHost");
        var page=Descendants(host).OfType<CortexDNA.Views.Pages.HealthPage>().Single();
        var core=Descendants(page).OfType<CortexHealthCore>().Single();
        var nodes=Descendants(page).OfType<HealthCoreNode>().ToArray();
        Check(nodes.Length==9,"original health scene has nine real-state nodes");
        service.Reset(); var scan=shell.Health.StartScanAsync(); await Task.Delay(65);
        Check(ReferenceEquals(scan,shell.Health.StartScanAsync()) && service.Calls==1,"repeated scan requests share one tracked operation");
        Check(nodes.Any(n=>n.HasActiveAnimations) && core.HasActiveAnimations,"scan activates finite node and circuit storyboards");
        var activeCircuit=(System.Windows.Shapes.Path)core.FindName("Circuit");
        Check(activeCircuit.Data.Bounds.Left==388 && activeCircuit.Data.Bounds.Right==451,"circuit pulse follows only the current CPU route");
        appearance.ReducedMotion=true; await Settle(window);
        Check(nodes.All(n=>!n.HasActiveAnimations) && !core.HasActiveAnimations && !page.HasActiveAnimations,"Reduced Motion removes all health clocks during scan");
        Check(shell.Health.IsScanning,"Reduced Motion preserves the real read-only scan");
        shell.Navigation.Navigate(PageId.Home); await scan; await Settle(window);
        Check(!shell.Health.IsScanning && shell.Health.Result==null && service.Cancellations==1,"navigation away cancels scan and suppresses result publication");
        Check(nodes.All(n=>!n.HasActiveAnimations) && !core.HasActiveAnimations,"unloaded health scene releases animation clocks");
        int calls=service.Calls;
        for(int i=0;i<30;i++) { shell.Navigation.Navigate(PageId.Health); shell.Navigation.Navigate(PageId.Home); }
        Check(service.Calls==calls,"repeated health navigation creates no scans or polling");
        shell.Navigation.Navigate(PageId.Health); appearance.ReducedMotion=false; await Settle(window);
        service.Reset(); scan=shell.Health.StartScanAsync(); await Task.Delay(40); window.Hide(); await scan;
        page=Descendants(host).OfType<CortexDNA.Views.Pages.HealthPage>().Single();
        Check(!shell.Health.IsScanning && !page.HasActiveAnimations && Descendants(page).OfType<HealthCoreNode>().All(n=>!n.HasActiveAnimations),"hidden window cancels health scan and releases clocks");
        window.Show(); await Settle(window); Check(service.Calls==calls+1,"showing the window does not restart a canceled scan");
        service.Reset(); scan=shell.Health.StartScanAsync(); await Task.Delay(40);
        window.WindowState=WindowState.Minimized; await scan;
        Check(!window.IsVisible && !shell.Health.IsScanning,"minimized tray state cancels health scan without polling");
        window.Show(); window.WindowState=WindowState.Normal; await Settle(window);
        service.Reset(); scan=shell.Health.StartScanAsync(); await Task.Delay(50); service.Complete(); await scan; await Task.Delay(40);
        page=Descendants(host).OfType<CortexDNA.Views.Pages.HealthPage>().Single();
        Check(page.HasActiveAnimations && shell.Health.Result?.Score.Value==92,"completion animates the explainable score");
        await Task.Delay(750);
        Check(!page.HasActiveAnimations && Descendants(page).OfType<HealthCoreNode>().All(n=>!n.HasActiveAnimations) &&
            !Descendants(page).OfType<CortexHealthCore>().Single().HasActiveAnimations,"completed scene has no retained animation clocks");
        Capture(window,"Phase4-Health-Dark.png");
        appearance.ThemeFileName="LightTheme.xaml"; appearance.Apply(); await Settle(window); Capture(window,"Phase4-Health-Light.png");
        Check(shell.Health.Result?.Score.Deductions.Single().PointsDeducted==8 && shell.Health.Areas.Count==9,"report exposes all areas and exact score deductions");
        var previousProgress=service.LastProgress;
        service.Reset(); scan=shell.Health.StartScanAsync(); await Task.Delay(30); appearance.ReducedMotion=true; service.Complete(); await scan; await Settle(window);
        Check(!page.HasActiveAnimations,"Reduced Motion publishes completion without score clocks");
        service.Reset(); var nextScan=shell.Health.StartScanAsync(); await Task.Delay(30);
        previousProgress?.Report(new([HealthNode.Security],1,8,[])); await Task.Delay(30);
        Check(shell.Health.Nodes.Single(n=>n.Node==HealthNode.Cpu).IsCurrent && !shell.Health.Nodes.Single(n=>n.Node==HealthNode.Security).IsCurrent,
            "late progress from a completed scan cannot enter the next scan");
        service.Complete(); await nextScan;
        await Task.Delay(800);
        Console.WriteLine("PERF animated elements: "+string.Join(", ",Descendants(window).OfType<FrameworkElement>()
            .Where(e=>e.HasAnimatedProperties || e.RenderTransform.HasAnimatedProperties)
            .Select(e=>$"{e.GetType().Name}/{e.Name}/visible={e.IsVisible}")));
        using var ownedProcess=Process.GetCurrentProcess(); ownedProcess.Refresh();
        var cpu=ownedProcess.TotalProcessorTime; long memory=ownedProcess.WorkingSet64; long managed=GC.GetTotalMemory(false);
        var idle=Stopwatch.StartNew(); await Task.Delay(2000); ownedProcess.Refresh();
        double healthCpu=(ownedProcess.TotalProcessorTime-cpu).TotalMilliseconds;
        double healthWall=idle.Elapsed.TotalMilliseconds; long healthWorking=ownedProcess.WorkingSet64; long healthManaged=GC.GetTotalMemory(false);
        var currentCore=Descendants(page).OfType<CortexHealthCore>().Single();
        currentCore.Visibility=Visibility.Collapsed; await Settle(window); await Task.Delay(300);
        ownedProcess.Refresh(); cpu=ownedProcess.TotalProcessorTime; var collapsedIdle=Stopwatch.StartNew(); await Task.Delay(2000); ownedProcess.Refresh();
        double collapsedCpu=(ownedProcess.TotalProcessorTime-cpu).TotalMilliseconds; double collapsedWall=collapsedIdle.Elapsed.TotalMilliseconds;
        currentCore.Visibility=Visibility.Visible; await Settle(window);
        shell.Navigation.Navigate(PageId.Home); await Settle(window); await Task.Delay(800);
        ownedProcess.Refresh(); cpu=ownedProcess.TotalProcessorTime; var homeIdle=Stopwatch.StartNew(); await Task.Delay(2000); ownedProcess.Refresh();
        double homeCpu=(ownedProcess.TotalProcessorTime-cpu).TotalMilliseconds;
        double homeWall=homeIdle.Elapsed.TotalMilliseconds;
        window.Hide(); await Task.Delay(200); ownedProcess.Refresh(); cpu=ownedProcess.TotalProcessorTime; var hiddenIdle=Stopwatch.StartNew(); await Task.Delay(2000); ownedProcess.Refresh();
        double hiddenCpu=(ownedProcess.TotalProcessorTime-cpu).TotalMilliseconds;
        double hiddenWall=hiddenIdle.Elapsed.TotalMilliseconds;
        var isolatedScene=new Window { Width=950,Height=520,Content=new CortexHealthCore { DataContext=shell.Health },Background=System.Windows.Media.Brushes.Black };
        isolatedScene.Show(); await Task.Delay(500); ownedProcess.Refresh(); cpu=ownedProcess.TotalProcessorTime;
        var isolatedIdle=Stopwatch.StartNew(); await Task.Delay(2000); ownedProcess.Refresh();
        double isolatedCpu=(ownedProcess.TotalProcessorTime-cpu).TotalMilliseconds; double isolatedWall=isolatedIdle.Elapsed.TotalMilliseconds;
        isolatedScene.Close();
        window.Show(); await Settle(window);
        File.WriteAllText(Path.Combine(Root,"artifacts","phase4-ui-metrics.json"),
            System.Text.Json.JsonSerializer.Serialize(new { healthIdleWallMs=healthWall,healthIdleCpuMs=healthCpu,
                homeIdleWallMs=homeWall,homeIdleCpuMs=homeCpu,hiddenIdleWallMs=hiddenWall,hiddenIdleCpuMs=hiddenCpu,
                coreCollapsedWallMs=collapsedWall,coreCollapsedCpuMs=collapsedCpu,isolatedCoreWallMs=isolatedWall,isolatedCoreCpuMs=isolatedCpu,
                workingSetBeforeBytes=memory,workingSetAfterBytes=healthWorking,managedBeforeBytes=managed,managedAfterBytes=healthManaged,
                activeHealthClocks=0,scope="Short owned WPF fixture process samples; no live hardware monitoring or historical baseline. Home is an in-process comparison, not a controlled benchmark." },
                new System.Text.Json.JsonSerializerOptions { WriteIndented=true }));
        using(var disposed=new HealthViewModel(shell,service))
        {
            disposed.OnNavigatedTo(); service.Reset(); var disposedScan=disposed.StartScanAsync(); await Task.Delay(30);
            disposed.Dispose(); int changes=0; disposed.PropertyChanged+=(_,_)=>changes++;
            await disposedScan; service.EmitLateProgress(); await Task.Delay(30);
            Check(changes==0 && disposed.Result==null,"disposed health VM ignores cancellation completions and late progress");
            Check(ReferenceEquals(disposed.ActiveScan,disposed.StartScanAsync()),"disposed health VM cannot start another operation");
        }
        shell.Navigation.Navigate(PageId.Diagnostics);
    }
    private sealed class HealthFixtureService : IHealthCheckService
    {
        private TaskCompletionSource _release=new(TaskCreationOptions.RunContinuationsAsynchronously);
        private IProgress<HealthCheckProgress>? _progress;
        public IProgress<HealthCheckProgress>? LastProgress => _progress;
        public bool Hold=true;
        public int Calls,Cancellations;
        public void Reset() { _release=new(TaskCreationOptions.RunContinuationsAsynchronously); Hold=true; }
        public void Complete()=>_release.TrySetResult();
        public void EmitLateProgress()=>_progress?.Report(new([HealthNode.Cpu],0,8,[]));
        public async Task<HealthCheckResult> ScanAsync(HealthCheckContext context,IProgress<HealthCheckProgress>? progress=null,CancellationToken token=default)
        {
            Calls++; _progress=progress; progress?.Report(new([HealthNode.Cpu],0,8,[]));
            try { if(Hold) await _release.Task.WaitAsync(token); }
            catch(OperationCanceledException) { Cancellations++; throw; }
            (HealthNode Node,HealthArea Area,int Weight)[] definitions=[
                (HealthNode.Cpu,HealthArea.Hardware,4),(HealthNode.Gpu,HealthArea.Hardware,3),(HealthNode.Memory,HealthArea.Hardware,3),
                (HealthNode.Storage,HealthArea.Storage,20),(HealthNode.Network,HealthArea.Network,0),(HealthNode.Startup,HealthArea.Startup,20),
                (HealthNode.Security,HealthArea.Security,25),(HealthNode.Cleanup,HealthArea.Maintenance,10),(HealthNode.Updates,HealthArea.Updates,15)];
            var items=definitions.Select(d=>new HealthCheckItem(d.Node,d.Area,d.Node==HealthNode.Startup ? HealthState.OptimizationAvailable : HealthState.Healthy,
                "Owned fixture observation",d.Node==HealthNode.Startup ? "Two measured high-impact apps, four points each." : "Owned fixture explanation.",
                d.Weight,d.Node==HealthNode.Startup ? 8 : 0,d.Node==HealthNode.Startup ? "Review the existing Startup page." : null)).ToArray();
            return new(DateTimeOffset.Now,context.Hardware.Timestamp,"Owned Windows fixture","CPU / GPU / RAM fixture",
                System.Collections.Immutable.ImmutableArray.CreateRange(items),HealthScore.Calculate(items));
        }
    }
    private sealed class TrackingPage(PageId id, MainViewModel shell) : PageViewModel(id, id.ToString(), "Fixture", shell)
    { public int Entries, Exits, Disposals; public override void OnNavigatedTo() { Entries++; base.OnNavigatedTo(); } public override void OnNavigatedFrom() { Exits++; base.OnNavigatedFrom(); } public override void Dispose() { if (!IsDisposed) Disposals++; base.Dispose(); } }
    private sealed class BindingErrors : TraceListener
    { public List<string> Errors { get; } = []; public override void Write(string? message) { if (message != null) Errors.Add(message); } public override void WriteLine(string? message) => Write(message); }
    private sealed class FakeHardware : IHardwareSession
    { public int OpenCalls, CloseCalls; public IEnumerable<IHardware> Hardware => []; public void Open() => OpenCalls++; public void Refresh() { } public void Close() => CloseCalls++; }
    private sealed class FakeMemory : IMemoryOptimizer
    { public int Calls; public Task<RamOptimizeResult> OptimizeMemoryAsync(CancellationToken cancellationToken = default) { Calls++; throw new InvalidOperationException("Forbidden in UI smoke"); } }
    private sealed class FakeCleanup : ICleanupService
    {
        public int CleanCalls;
        public IReadOnlyList<CleanupLocationItem> CreateDefaultLocations() => Enum.GetValues<CleanupCategoryId>().Select(id => new CleanupLocationItem { Id = id, Name = id.ToString(), Warning = "Fixture only", Path = "fixture" }).ToArray();
        public Task<CleanupScanResult> ScanAsync(IReadOnlyList<CleanupLocationItem> locations, IProgress<CleanupProgress>? progress = null, CancellationToken cancellationToken = default) => Task.FromResult(new CleanupScanResult { Locations = locations });
        public Task<CleanupCleanResult> CleanAsync(IReadOnlyList<CleanupLocationItem> locations, IProgress<CleanupProgress>? progress = null, CancellationToken cancellationToken = default) { CleanCalls++; throw new InvalidOperationException("Forbidden in UI smoke"); }
    }
    private sealed class FakeStartup : IStartupService
    { public int Writes; public StartupSnapshot Load(bool migrateLegacy = true) => new(); public Task<StartupSnapshot> LoadAsync(bool migrateLegacy = true) => Task.FromResult(Load(migrateLegacy)); public void SetEnabled(StartupItem item, bool enabled) => Writes++; public void Delay(StartupItem item) => Writes++; public void RemoveDelay(StartupItem item) => Writes++; }
    private sealed class FakeDialogs : IDialogService
    { public int ConfirmCalls, CleanupConfirmCalls; public bool Confirm(string title, string message) { ConfirmCalls++; return false; } public IReadOnlyList<CleanupLocationItem>? ConfirmCleanup(IReadOnlyList<CleanupLocationItem> locations) { CleanupConfirmCalls++; return null; } public void ShowCleanupResult(CleanupCleanResult result) { } public void Notify(string message, UiState state) { } }
}
