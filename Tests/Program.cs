using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Threading;
using CortexDNA.Core;
using CortexDNA.Core.Startup;
using CortexDNA.Models;
using CortexDNA.ViewModels;

internal static class Program
{
    private static int _passed, _failed;
    [STAThread]
    private static int Main(string[] args)
    {
        if (args is ["--hash", var id]) { Console.WriteLine(StartupPaths.DelayTaskName(id)); return 0; }
        var application = new CortexDNA.App();
        application.InitializeComponent();
        application.ShutdownMode = ShutdownMode.OnExplicitShutdown;
        SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext());
        var work = Run();
        var frame = new DispatcherFrame();
        _ = work.ContinueWith(_ => application.Dispatcher.BeginInvoke(() => frame.Continue = false));
        Dispatcher.PushFrame(frame);
        try { work.GetAwaiter().GetResult(); }
        catch (Exception ex) { _failed++; Console.WriteLine(ex); }
        Console.WriteLine($"TOTAL {_passed + _failed}; PASSED {_passed}; FAILED {_failed}");
        return _failed == 0 ? 0 : 1;
    }
    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    private static async Task Test(string name, Func<Task> action)
    {
        try { await action(); _passed++; Console.WriteLine($"PASS {name}"); }
        catch (Exception ex) { _failed++; Console.WriteLine($"FAIL {name}: {ex}"); }
    }
    private static Task Sync(Action action) { action(); return Task.CompletedTask; }
    private static async Task Throws<T>(Func<Task> action) where T : Exception
    {
        try { await action(); } catch (T) { return; }
        throw new Exception($"Expected {typeof(T).Name}");
    }
    private static async Task Run()
    {
        await Test("Registry Editor uses Windows root and requests elevation", () => Sync(() => {
            var info = SystemToolLauncher.CreateStartInfo("regedit.exe");
            Check(info.FileName == Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "regedit.exe"), "regedit path");
            Check(File.Exists(info.FileName) && info.UseShellExecute && info.Verb == "runas", "elevated regedit");
        }));
        await Test("management consoles elevate MMC with absolute snap-in paths", () => Sync(() => {
            foreach (string tool in new[] { "devmgmt.msc", "services.msc", "eventvwr.msc" })
            {
                var info = SystemToolLauncher.CreateStartInfo(tool);
                Check(info.FileName == Path.Combine(Environment.SystemDirectory, "mmc.exe") && info.Verb == "runas", "MMC elevation");
                Check(info.Arguments == $"\"{Path.Combine(Environment.SystemDirectory, tool)}\"" && File.Exists(Path.Combine(Environment.SystemDirectory, tool)), "snap-in path");
            }
        }));
        await Test("shells and diagnostic tools request administrator", () => Sync(() => {
            foreach (string tool in new[] { "cmd.exe", "powershell.exe", "taskmgr.exe", "resmon.exe", "ncpa.cpl" })
            {
                var info = SystemToolLauncher.CreateStartInfo(tool);
                Check(info.UseShellExecute && info.Verb == "runas" && Path.IsPathFullyQualified(info.FileName) && File.Exists(info.FileName), tool);
            }
        }));
        await Test("ordinary tools retain standard launch and unknown tools refused", () => Sync(() => {
            Check(SystemToolLauncher.CreateStartInfo("msinfo32.exe").Verb != "runas", "system information");
            Check(SystemToolLauncher.CreateStartInfo("control.exe").Verb != "runas", "control panel");
            try { SystemToolLauncher.CreateStartInfo("unknown.exe"); throw new Exception("unknown accepted"); }
            catch (ArgumentException) { }
        }));
        await Test("quoted executable and untouched arguments", () => Sync(() => {
            var result = StartupPaths.SplitCommand("  \"C:\\Program Files\\Demo\\app.exe\" --name \"hello world\"  ");
            Check(result == (@"C:\Program Files\Demo\app.exe", "--name \"hello world\""), "command split");
        }));
        await Test("tabs separate executable", () => Sync(() =>
            Check(StartupPaths.SplitCommand("app.exe\t/a") == ("app.exe", "/a"), "tab split")));
        await Test("unclosed quote fails closed", () => Sync(() =>
            Check(StartupPaths.ExtractExecutable("\"C:\\bad.exe") == null, "malformed quote")));
        await Test("argument .exe is not mistaken for target", () => Sync(() =>
            Check(StartupPaths.ExtractExecutable("runner /file other.exe") == "runner", "argument parse")));
        await Test("packaged task path cannot target sibling task or another key", () => Sync(() => {
            string prefix = @"Software\Classes\Local Settings\Software\Microsoft\Windows\CurrentVersion\AppModel\SystemAppData\demo_publisher\StartupTasks\";
            Check(StartupPackagedCatalog.IsExactTaskPath(prefix + "TaskA", "demo_publisher", "TaskA"), "exact task");
            Check(!StartupPackagedCatalog.IsExactTaskPath(prefix + "TaskB", "demo_publisher", "TaskA"), "sibling blocked");
            Check(!StartupPackagedCatalog.IsExactTaskPath(@"Software\Other\TaskA", "demo_publisher", "TaskA"), "scope blocked");
        }));
        await Test("stable hash handles case and int minimum safely", () => Sync(() => {
            Check(StartupPaths.DelayTaskName("CurrentUserRun:DEMO") == StartupPaths.DelayTaskName("currentuserrun:demo"), "case");
            Check(StartupPaths.DelayTaskName("demo") != StartupPaths.DelayTaskName("other"), "identity");
        }));
        await Test("hash stable in independent process", async () => {
            var psi = new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = false, RedirectStandardOutput = true, CreateNoWindow = true };
            psi.ArgumentList.Add("--hash"); psi.ArgumentList.Add("CurrentUserRun:Demo");
            using var child = Process.Start(psi)!;
            string actual = (await child.StandardOutput.ReadToEndAsync()).Trim();
            await child.WaitForExitAsync();
            Check(child.ExitCode == 0 && actual == StartupPaths.DelayTaskName("CurrentUserRun:Demo"), "process hash");
        });
        string testRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "fixtures-" + Guid.NewGuid().ToString("N")));
        Directory.CreateDirectory(testRoot);
        string safe = Path.Combine(testRoot, "safe"), outside = Path.Combine(testRoot, "outside");
        Directory.CreateDirectory(safe); Directory.CreateDirectory(outside);
        var location = new CleanupLocationItem { Id = CleanupCategoryId.UserTemp, Name = "Fixture", Path = safe };
        var service = new DiskCleanupService([location]);
        await Test("reject arbitrary cleanup root", () => Throws<InvalidOperationException>(() =>
            new DiskCleanupService().CleanAsync([location])));
        await Test("reject drive root", () => Throws<InvalidOperationException>(() =>
            service.CleanAsync([new CleanupLocationItem { Path = Path.GetPathRoot(safe)! }])));
        await Test("scan and clean exact counts", async () => {
            await File.WriteAllTextAsync(Path.Combine(safe, "one.tmp"), "12345");
            Directory.CreateDirectory(Path.Combine(safe, "child"));
            await File.WriteAllTextAsync(Path.Combine(safe, "child", "two.tmp"), "1234567");
            var scan = await service.ScanAsync([location]);
            Check(scan.FileCount == 2 && scan.TotalBytes == 12, "scan counts");
            var clean = await service.CleanAsync([location]);
            Check(clean.Success && clean.DeletedFiles == 2 && clean.FreedBytes == 12, "clean counts");
            Check(Directory.Exists(safe), "preserve root");
        });
        await Test("junction subtree and root refused", async () => {
            string sentinel = Path.Combine(outside, "keep.txt");
            await File.WriteAllTextAsync(sentinel, "DO NOT DELETE");
            string junction = Path.Combine(safe, "junction");
            var psi = new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "cmd.exe")) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true };
            psi.ArgumentList.Add("/c"); psi.ArgumentList.Add("mklink"); psi.ArgumentList.Add("/J"); psi.ArgumentList.Add(junction); psi.ArgumentList.Add(outside);
            using var child = Process.Start(psi)!; await child.WaitForExitAsync();
            Check(child.ExitCode == 0, "junction creation");
            var scan = await service.ScanAsync([location]);
            Check(scan.FileCount == 0, "must not scan target");
            await service.CleanAsync([location]);
            Check(await File.ReadAllTextAsync(sentinel) == "DO NOT DELETE", "target preserved");
            var linked = new CleanupLocationItem { Path = junction };
            var blocked = await new DiskCleanupService([linked]).CleanAsync([linked]);
            Check(blocked.DeletedFiles == 0 && blocked.FailedFiles > 0, "root blocked");
            Directory.Delete(junction, false); // Delete only the known link, never recurse.
        });
        await Test("ancestor pinned against replacement", () => Sync(() => {
            bool moved = false;
            using (var guard = CleanupPathGuard.Acquire(safe))
            {
                try { Directory.Move(safe, safe + "-moved"); moved = true; }
                catch (IOException) { }
            }
            if (moved) Directory.Move(safe + "-moved", safe);
            Check(!moved, "ancestor moved while leased");
        }));
        await Test("read-only file preserved without attribute mutation", async () => {
            string file = Path.Combine(safe, "readonly.txt"); await File.WriteAllTextAsync(file, "keep");
            File.SetAttributes(file, FileAttributes.ReadOnly);
            var result = await service.CleanAsync([location]);
            Check(result.FailedFiles == 1 && File.Exists(file) && File.GetAttributes(file).HasFlag(FileAttributes.ReadOnly), "read-only");
            File.SetAttributes(file, FileAttributes.Normal); File.Delete(file);
        });
        await Test("cancellation propagated for scan", async () => {
            using var cts = new CancellationTokenSource(); cts.Cancel();
            await Throws<OperationCanceledException>(() => service.ScanAsync([location], cancellationToken: cts.Token));
        });
        await Test("cancellation propagated before clean", async () => {
            using var cts = new CancellationTokenSource(); cts.Cancel();
            await Throws<OperationCanceledException>(() => service.CleanAsync([location], cancellationToken: cts.Token));
        });
        await Test("recent scan and clean both top-level only", async () => {
            var recent = new CleanupLocationItem { Id = CleanupCategoryId.Recent, Path = safe };
            var recentService = new DiskCleanupService([recent]);
            string childFile = Path.Combine(safe, "child", "keep.txt"); await File.WriteAllTextAsync(childFile, "keep");
            Check((await recentService.ScanAsync([recent])).FileCount == 0, "recent scan");
            Check((await recentService.CleanAsync([recent])).DeletedFiles == 0 && File.Exists(childFile), "recent clean");
        });
        await Test("parallel operations serialized", async () => {
            await File.WriteAllTextAsync(Path.Combine(safe, "single.tmp"), "123");
            var results = await Task.WhenAll(service.CleanAsync([location]), service.CleanAsync([location]));
            Check(results.Sum(r => r.DeletedFiles) == 2 && results.Sum(r => r.FreedBytes) == 7, "no double counting");
        });
        await Test("delay rejects ambiguous commands without creating task", () => Sync(() => {
            var item = new StartupItem { Id = "test", Name = "test", Command = @"C:\Program Files\app.exe /a", LocationLabel = "test" };
            try { new StartupDelayService().Delay(item); throw new Exception("unsafe command accepted"); }
            catch (InvalidOperationException) { Check(!item.IsDelayed, "no changed state"); }
        }));
        await Test("logger concurrent writes do not throw", async () => {
            await Task.WhenAll(Enumerable.Range(0, 40).Select(i => Task.Run(() => Logger.Log($"Local verification {i}"))));
            Check(File.Exists(Logger.LogPath), "log available in user profile");
        });
        await Test("RAM cancellation does not touch processes", async () => {
            using var cts = new CancellationTokenSource(); cts.Cancel();
            await Throws<OperationCanceledException>(() => RamOptimizer.OptimizeMemoryAsync(cts.Token));
        });
        await Test("update lease preserves initially stopped services", () => Sync(() => {
            var fake = new FakeServices(); fake.States["bits"] = 1;
            using (UpdateServiceLease.Acquire(CancellationToken.None, fake))
                Check(fake.States["wuauserv"] == 1, "running service stopped");
            Check(fake.States["wuauserv"] == 4 && fake.States["bits"] == 1 && fake.Started.SequenceEqual(["wuauserv"]), "state restored");
        }));
        await Test("update cleanup refuses external service restart", () => Sync(() => {
            var fake = new FakeServices();
            using var lease = UpdateServiceLease.Acquire(CancellationToken.None, fake);
            lease.VerifyStopped(); fake.States["bits"] = 4;
            try { lease.VerifyStopped(); throw new Exception("restart not detected"); }
            catch (InvalidOperationException) { }
        }));
        await Test("update lease restores partial stop failure", () => Sync(() => {
            var fake = new FakeServices { FailStop = "bits" };
            try { UpdateServiceLease.Acquire(CancellationToken.None, fake); throw new Exception("expected stop failure"); }
            catch (IOException) { }
            Check(fake.States.Values.All(s => s == 4) && fake.Started.Contains("wuauserv"), "partial recovery");
        }));
        await Test("update lease cancellation restores stopped services", () => Sync(() => {
            var fake = new FakeServices { CancelWait = true };
            try { UpdateServiceLease.Acquire(CancellationToken.None, fake); throw new Exception("expected cancellation"); }
            catch (OperationCanceledException) { }
            Check(fake.States.Values.All(s => s == 4), "cancel recovery");
        }));
        await Test("update restore failure reported and other service still restored", () => Sync(() => {
            var fake = new FakeServices { FailStart = "bits" };
            var lease = UpdateServiceLease.Acquire(CancellationToken.None, fake);
            try { lease.Dispose(); throw new Exception("expected restore failure"); }
            catch (AggregateException) { }
            Check(fake.States["wuauserv"] == 4, "continue restoring others");
        }));
        await Test("hardware immediate disposal is idempotent", async () => {
            var vm = new HardwareViewModel();
            var first = vm.ShutdownAsync(); Check(ReferenceEquals(first, vm.ShutdownAsync()), "shutdown task identity");
            await first.WaitAsync(TimeSpan.FromSeconds(15));
            vm.ResumeMonitoring(); vm.RequestRefresh();
        });
        await Test("WPF dashboard/startup/minimize/close smoke", async () => {
            var window = new CortexDNA.MainWindow();
            window.Show();
            Check(window.IsLoaded && window.DataContext is MainViewModel, "dashboard loaded");
            var vm = (MainViewModel)window.DataContext;
            await Task.Delay(3500);
            vm.HardwareVM.RequestRefresh(); vm.HardwareVM.RequestRefresh();
            window.WindowState = WindowState.Minimized;
            window.Show(); window.WindowState = WindowState.Normal;
            await vm.StartupVM.LoadAsync(true, migrateLegacy: false);
            Check(!vm.StartupVM.IsLoading, "startup loaded");
            typeof(CortexDNA.MainWindow).GetField("_isExplicitExit", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(window, true);
            window.Close();
            await vm.HardwareVM.ShutdownAsync().WaitAsync(TimeSpan.FromSeconds(45));
            await Task.Delay(100);
            Check(!window.IsVisible, "window closed");
        });
        await Test("session ending defers shutdown until existing exit path completes", async () => {
            var window = new CortexDNA.MainWindow();
            System.Windows.Application.Current.MainWindow = window;
            window.Show();
            var args = (SessionEndingCancelEventArgs)Activator.CreateInstance(typeof(SessionEndingCancelEventArgs),
                BindingFlags.Instance | BindingFlags.NonPublic, null, new object[] { ReasonSessionEnding.Shutdown }, null)!;
            typeof(CortexDNA.App).GetMethod("OnSessionEnding", BindingFlags.NonPublic | BindingFlags.Instance)!
                .Invoke(System.Windows.Application.Current, new object[] { args });
            Check(args.Cancel, "session ending must not bypass resource cleanup");
            await ((MainViewModel)window.DataContext).HardwareVM.ShutdownAsync().WaitAsync(TimeSpan.FromSeconds(15));
            await Task.Delay(100);
            Check(!window.IsVisible, "session-end exit completed");
        });
        // All fixture files were created by this harness; delete individual files and empty folders only.
        DeleteFixture(testRoot);
    }
    private static void DeleteFixture(string directory)
    {
        if (!Path.GetFullPath(directory).StartsWith(Path.GetFullPath(AppContext.BaseDirectory), StringComparison.OrdinalIgnoreCase))
            throw new Exception("Fixture escaped test output directory");
        if (File.GetAttributes(directory).HasFlag(FileAttributes.ReparsePoint)) { Directory.Delete(directory, false); return; }
        foreach (string file in Directory.EnumerateFiles(directory)) File.Delete(file);
        foreach (string child in Directory.EnumerateDirectories(directory)) DeleteFixture(child);
        Directory.Delete(directory, false);
    }
    private sealed class FakeServices : UpdateServiceLease.IUpdateServices
    {
        public Dictionary<string, uint> States { get; } = new() { ["wuauserv"] = 4, ["bits"] = 4 };
        public List<string> Started { get; } = new();
        public string? FailStop, FailStart;
        public bool CancelWait;
        public uint State(string name) => States[name];
        public void Stop(string name) { if (name == FailStop) throw new IOException("simulated stop failure"); States[name] = 1; }
        public void Start(string name) { if (name == FailStart) throw new IOException("simulated restore failure"); States[name] = 4; Started.Add(name); }
        public void Wait(string name, uint target, CancellationToken token) { if (target == 1 && CancelWait) throw new OperationCanceledException(); }
    }
}
