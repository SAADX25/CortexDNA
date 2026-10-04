using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Input;
using System.Windows.Threading;
using CortexDNA.Core;
using CortexDNA.Core.Health;
using CortexDNA.Navigation;
using CortexDNA.Models;
using CortexDNA.SystemHealth;
namespace CortexDNA.ViewModels.Pages;

public sealed class HealthNodeViewModel(HealthNode node) : ViewModelBase
{
    private HealthState _state = HealthState.Unavailable;
    private bool _current;
    private string _summary = "Not checked";
    public HealthNode Node { get; } = node;
    public string Label => Node switch { HealthNode.Cpu => "CPU", HealthNode.Gpu => "GPU", HealthNode.Memory => "RAM", HealthNode.Updates => "Windows Update", _ => Node.ToString() };
    public HealthState State { get => _state; private set => SetProperty(ref _state, value); }
    public bool IsCurrent { get => _current; set => SetProperty(ref _current, value); }
    public string Summary { get => _summary; private set => SetProperty(ref _summary, value); }
    public void Apply(HealthCheckItem item) { State = item.State; Summary = item.Summary; }
    public void Reset() { State = HealthState.Unavailable; Summary = "Not checked"; IsCurrent = false; }
}

public sealed class HealthViewModel : PageViewModel
{
    private readonly IHealthCheckService _service;
    private CancellationTokenSource? _scanCancellation;
    private Task _activeScan = Task.CompletedTask;
    private bool _scanning, _shutdown;
    private double _progress;
    private string _status = "Ready for a read-only assessment";
    private HealthCheckResult? _result;
    public HealthViewModel(MainViewModel shell, IHealthCheckService? service = null)
        : base(PageId.Health, "Cortex Health Core", "An explainable assessment. Every scan is read-only.", shell)
    {
        _service = service ?? new HealthCheckService(SnapshotHealthProviders.Create().Concat(WindowsHealthProviders.Create()));
        Nodes = Enum.GetValues<HealthNode>().Select(n => new HealthNodeViewModel(n)).ToArray();
        ScanCommand = new RelayCommand(() => _ = StartScanAsync(), () => IsActive && !_shutdown && !IsScanning && !IsDisposed);
        CancelCommand = new RelayCommand(CancelScan, () => IsScanning);
    }
    public IReadOnlyList<HealthNodeViewModel> Nodes { get; }
    public ObservableCollection<HealthCheckItem> Areas { get; } = [];
    public RelayCommand ScanCommand { get; }
    public RelayCommand CancelCommand { get; }
    public Task ActiveScan => _activeScan;
    public bool IsScanning { get => _scanning; private set { if (SetProperty(ref _scanning, value)) CommandManager.InvalidateRequerySuggested(); } }
    public double Progress { get => _progress; private set => SetProperty(ref _progress, value); }
    public string Status { get => _status; private set => SetProperty(ref _status, value); }
    public HealthCheckResult? Result { get => _result; private set => SetProperty(ref _result, value); }
    public Task StartScanAsync()
    {
        if (IsDisposed || _shutdown || !IsActive || !_activeScan.IsCompleted || IsScanning) return _activeScan;
        _scanCancellation = new CancellationTokenSource();
        var context = new HealthCheckContext(Hardware.CurrentSnapshot ?? HardwareSnapshot.Unavailable(DateTimeOffset.MinValue), DateTimeOffset.Now, Path.GetPathRoot(Environment.SystemDirectory) ?? "")
        { Startup = Shell.StartupVM.Items.Select(i => new StartupHealthFact(i.IsEnabled, i.Model.Impact)).ToImmutableArray() };
        _activeScan = RunAsync(context, _scanCancellation);
        return _activeScan;
    }
    private async Task RunAsync(HealthCheckContext context, CancellationTokenSource cancellation)
    {
        IsScanning = true; Result = null; Areas.Clear(); Progress = 0;
        foreach (var node in Nodes) node.Reset();
        Status = "Checking local observations…";
        bool Live() => !IsDisposed && IsActive && IsScanning && Result == null &&
            ReferenceEquals(_scanCancellation, cancellation) && !cancellation.IsCancellationRequested;
        var progress = new HealthProgress(Dispatcher.CurrentDispatcher, p =>
        {
            if (!Live()) return;
            Progress = 100d * p.CompletedProviders / p.TotalProviders;
            foreach (var node in Nodes)
            {
                node.IsCurrent = p.ActiveNodes.Contains(node.Node);
                var item = p.CompletedItems.FirstOrDefault(i => i.Node == node.Node);
                if (item != null) node.Apply(item);
            }
            Status = p.ActiveNodes.Length > 0 ? $"Checking {string.Join(" / ", p.ActiveNodes)}…" : "Settling assessment…";
        }, () => Shell.Appearance.MotionEnabled && Live());
        try
        {
            var result = await _service.ScanAsync(context, progress, cancellation.Token);
            if (!Live()) return;
            Result = result; Progress = 100;
            foreach (var item in result.Items) { Areas.Add(item); Nodes.First(n => n.Node == item.Node).Apply(item); }
            Status = $"{result.RecommendationCount} recommendations · {result.Score.Coverage}";
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
        catch { if (Live()) Status = "Assessment unavailable. No system changes were made."; }
        finally
        {
            if (!IsDisposed) { foreach (var node in Nodes) node.IsCurrent = false; IsScanning = false; }
            if (ReferenceEquals(_scanCancellation, cancellation)) _scanCancellation = null;
            cancellation.Dispose();
        }
    }
    public void CancelScan()
    {
        _scanCancellation?.Cancel();
        if (!IsDisposed)
        {
            foreach (var node in Nodes) node.IsCurrent = false;
            if (IsScanning) Status = "Scan canceled; waiting for the current local read to release resources.";
        }
    }
    public async Task ShutdownAsync() { _shutdown = true; CancelScan(); await _activeScan; }
    public override void OnNavigatedTo() { base.OnNavigatedTo(); CommandManager.InvalidateRequerySuggested(); }
    public override void OnNavigatedFrom() { CancelScan(); base.OnNavigatedFrom(); }
    public override void Dispose() { if (IsDisposed) return; CancelScan(); _shutdown = true; base.Dispose(); }
    private sealed class HealthProgress(Dispatcher dispatcher, Action<HealthCheckProgress> update, Func<bool> motionEnabled) : IAsyncHealthCheckProgress
    {
        public void Report(HealthCheckProgress value) => dispatcher.BeginInvoke(() => update(value));
        public async ValueTask ReportAsync(HealthCheckProgress value, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            await dispatcher.InvokeAsync(() => update(value), DispatcherPriority.Normal, token);
            token.ThrowIfCancellationRequested();
            // One finite presentation interval per provider, never a polling or rendering loop.
            if (value.ActiveNodes.Length > 0 && motionEnabled()) await Task.Delay(140, token);
        }
    }
}
