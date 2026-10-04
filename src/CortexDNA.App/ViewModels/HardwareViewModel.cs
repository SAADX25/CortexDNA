using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using CortexDNA.Models;
using System.Windows.Input;
using CortexDNA.Core;
using CortexDNA.Services;
using CortexDNA.UI;

namespace CortexDNA.ViewModels
{
    public class HardwareViewModel : ViewModelBase, IDisposable
    {
        private readonly IHardwareMonitorService _monitor;
        private volatile bool _disposed;
        private readonly CancellationTokenSource _lifetime = new();
        private readonly object _shutdownLock = new();
        private readonly Dispatcher _dispatcher;
        private Task _initialization = Task.CompletedTask;
        private Task _boostTask = Task.CompletedTask;
        private Task _cleanupTask = Task.CompletedTask;
        private Task? _shutdownTask;
        public HardwareSnapshot? CurrentSnapshot { get; private set; }
        public System.Collections.Immutable.ImmutableArray<HardwareHistorySample> History => _monitor.History;

        public ICommand CopyAllSpecsCommand { get; }
        public ICommand CopyMotherboardCommand { get; }
        public ICommand CopyBiosVersionCommand { get; }
        public ICommand CopyBiosDateCommand { get; }
        public ICommand BoostSystemCommand { get; }
        public ICommand CleanDiskCommand { get; }
        public ICommand RefreshCommand { get; }

        public AppSystemInfo SystemInfo { get; set; } = new AppSystemInfo();

        private bool _isBoosting = false;
        public bool IsBoosting
        {
            get => _isBoosting;
            set => SetProperty(ref _isBoosting, value);
        }

        private string _boostButtonText = "BOOST";
        public string BoostButtonText
        {
            get => _boostButtonText;
            set => SetProperty(ref _boostButtonText, value);
        }

        private UiState _boostState = UiState.Idle;
        public UiState BoostState { get => _boostState; private set => SetProperty(ref _boostState, value); }

        private bool _isBoostEnabled = true;
        public bool IsBoostEnabled
        {
            get => _isBoostEnabled;
            set => SetProperty(ref _isBoostEnabled, value);
        }

        private string _cleanDiskButtonText = "CLEAN DISK";
        public string CleanDiskButtonText
        {
            get => _cleanDiskButtonText;
            set => SetProperty(ref _cleanDiskButtonText, value);
        }

        private bool _isCleanDiskEnabled = true;
        public bool IsCleanDiskEnabled
        {
            get => _isCleanDiskEnabled;
            set => SetProperty(ref _isCleanDiskEnabled, value);
        }

        private bool _isCleaningDisk;
        public bool IsCleaningDisk { get => _isCleaningDisk; private set => SetProperty(ref _isCleaningDisk, value); }
        private double _operationProgress;
        public double OperationProgress { get => _operationProgress; private set => SetProperty(ref _operationProgress, value); }
        public bool IsGameModeActive => _isGameModeActive;
        internal ICleanupService CleanupService => _diskCleanup;
        private readonly IDialogService _dialogs;


        private string _cpuName = "Detecting CPU...";
        public string CpuName
        {
            get => _cpuName;
            set => SetProperty(ref _cpuName, value);
        }

        private string _gpuName = "Detecting GPU...";
        public string GpuName
        {
            get => _gpuName;
            set => SetProperty(ref _gpuName, value);
        }

        public ObservableCollection<HardwareItem> CpuList { get; set; } = new ObservableCollection<HardwareItem>();
        public ObservableCollection<HardwareItem> GpuList { get; set; } = new ObservableCollection<HardwareItem>();
        public ObservableCollection<StorageDrive> StorageList { get; set; } = new ObservableCollection<StorageDrive>();

        private string _statusMessage = "Initializing...";
        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        private string _computerName = Environment.MachineName;
        public string ComputerName
        {
            get => _computerName;
            set => SetProperty(ref _computerName, value);
        }

        private bool _isAdmin;
        public bool IsAdmin
        {
            get => _isAdmin;
            private set
            {
                if (SetProperty(ref _isAdmin, value))
                    OnPropertyChanged(nameof(PrivilegeText));
            }
        }

        public string PrivilegeText => IsAdmin ? "Administrator" : "Standard user";
        private bool _isGameModeActive = false;
        private readonly ICleanupService _diskCleanup;
        private readonly IMemoryOptimizer _memoryOptimizer;

        public HardwareViewModel() : this(AppComposition.CreateCleanupService(), AppComposition.CreateMemoryOptimizer(), AppComposition.CreateHardwareMonitor()) { }
        public HardwareViewModel(ICleanupService cleanup, IMemoryOptimizer memoryOptimizer, IHardwareMonitorService monitor)
            : this(cleanup, memoryOptimizer, monitor, AppComposition.CreateDialogService()) { }
        public HardwareViewModel(ICleanupService cleanup, IMemoryOptimizer memoryOptimizer, IHardwareMonitorService monitor, IDialogService dialogs)
        {
            _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
            _diskCleanup = cleanup ?? throw new ArgumentNullException(nameof(cleanup));
            _memoryOptimizer = memoryOptimizer ?? throw new ArgumentNullException(nameof(memoryOptimizer));
            _monitor = monitor ?? throw new ArgumentNullException(nameof(monitor));
            _dispatcher = System.Windows.Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;
            CopyAllSpecsCommand = new RelayCommand(CopyAllSpecs);
            CopyMotherboardCommand = new RelayCommand(() => CopyToClipboard(SystemInfo.MotherboardModel, "Motherboard Model"));
            CopyBiosVersionCommand = new RelayCommand(() => CopyToClipboard(SystemInfo.BiosVersion, "BIOS Version"));
            CopyBiosDateCommand = new RelayCommand(() => CopyToClipboard(SystemInfo.BiosDate, "BIOS Date"));
            BoostSystemCommand = new RelayCommand(BoostSystem); CleanDiskCommand = new RelayCommand(ExecuteCleanDisk); RefreshCommand = new RelayCommand(RequestRefresh);
            ApplySnapshot(_monitor.CurrentSnapshot);
            StatusMessage = "Initializing...";
            _monitor.SnapshotAvailable += SnapshotReceived;
            _initialization = ObserveInitializationAsync();
        }
        private async Task ObserveInitializationAsync()
        { try { await _monitor.StartAsync(); } catch (OperationCanceledException) { } catch (Exception ex) { Logger.Log(ex); if (!_disposed) StatusMessage = "Hardware monitoring unavailable; see local log."; } }

        private void CopyToClipboard(string text, string label)
        {
            if (!string.IsNullOrEmpty(text) && text != "Detecting...")
            {
                try
                {
                    System.Windows.Clipboard.SetText(text);
                    StatusMessage = $"{label} copied!";
                    _ = ResetStatusAfterDelayAsync();
                }
                catch { }
            }
        }

        private void CopyAllSpecs()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Device Name: {ComputerName}");
            sb.AppendLine($"Processor: {CpuName}");
            sb.AppendLine($"Installed RAM: {SystemInfo.RamInfo}");
            sb.AppendLine($"GPU: {GpuName}");
            sb.AppendLine($"Motherboard: {SystemInfo.MotherboardModel}");
            sb.AppendLine($"BIOS Version: {SystemInfo.BiosVersion}");
            sb.AppendLine($"BIOS Date: {SystemInfo.BiosDate}");
            sb.AppendLine($"Edition: {SystemInfo.OsName}");

            try
            {
                System.Windows.Clipboard.SetText(sb.ToString());
                StatusMessage = "Specs copied to clipboard!";
                _ = ResetStatusAfterDelayAsync();
            }
            catch { }
        }

        private async Task ResetStatusAfterDelayAsync()
        {
            try { await Task.Delay(2000, _lifetime.Token); if (!_disposed) StatusMessage = "Monitoring Active"; } catch (OperationCanceledException) { }
        }

        public void PauseMonitoring() { if (!_disposed) _monitor.SetMode(MonitoringMode.Hidden); }
        public void ResumeMonitoring() { if (!_disposed) _monitor.SetMode(MonitoringMode.Foreground); }
        public void SetMonitoringMode(MonitoringMode mode) { if (!_disposed) _monitor.SetMode(mode); }
        public void RequestRefresh() { if (!_disposed) _ = RefreshAsync(); }
        private void RefreshData() => RequestRefresh();
        private async Task RefreshAsync()
        { try { await _monitor.RefreshAsync(_lifetime.Token); } catch (OperationCanceledException) { } catch (Exception ex) { Logger.Log(ex); } }
        private void SnapshotReceived(HardwareSnapshot snapshot)
        {
            if (_disposed || _dispatcher.HasShutdownStarted) return;
            if (_dispatcher.CheckAccess()) ApplySnapshot(snapshot);
            else _dispatcher.BeginInvoke(() => { if (!_disposed) ApplySnapshot(snapshot); });
        }
        private readonly Dictionary<string, HardwareItem> _cpuItems = new();
        private readonly Dictionary<string, HardwareItem> _gpuItems = new();
        private void ApplySnapshot(HardwareSnapshot snapshot)
        {
            if (_disposed) return;
            CurrentSnapshot = snapshot; OnPropertyChanged(nameof(CurrentSnapshot)); OnPropertyChanged(nameof(History));
            var info = snapshot.Info;
            IsAdmin = info.IsAdmin;
            SystemInfo.OsName = info.OsName ?? "Unavailable"; SystemInfo.BiosInfo = info.BiosInfo ?? "Unavailable";
            SystemInfo.MotherboardModel = info.MotherboardModel ?? "Unavailable"; SystemInfo.BiosVersion = info.BiosVersion ?? "Unavailable"; SystemInfo.BiosDate = info.BiosDate ?? "Unavailable";
            SystemInfo.RamInfo = info.RamInfo ?? "Unavailable"; SystemInfo.RamTotal = info.RamTotal ?? "Unavailable"; SystemInfo.RamType = info.RamType ?? "Unavailable";
            CpuName = info.CpuName ?? snapshot.Cpus.FirstOrDefault()?.Name ?? "Unavailable";
            GpuName = snapshot.Gpus.IsEmpty ? info.GpuName ?? "Unavailable" : string.Join(" / ", snapshot.Gpus.Select(g => g.Name));
            SystemInfo.Uptime = $"{snapshot.Uptime.Days}d {snapshot.Uptime.Hours}h {snapshot.Uptime.Minutes}m";
            var memory = snapshot.Memory;
            SystemInfo.RamUsagePercent = memory.UsagePercent ?? 0;
            SystemInfo.RamUsageText = memory.UsagePercent.HasValue && memory.TotalBytes.HasValue && memory.AvailableBytes.HasValue ? $"{(memory.TotalBytes.Value - Math.Min(memory.TotalBytes.Value, memory.AvailableBytes.Value)) / (1024d * 1024 * 1024):F1} / {memory.TotalBytes.Value / (1024d * 1024 * 1024):F1} GB ({memory.UsagePercent:F0}%)" : "Unavailable";
            SystemInfo.NetworkDownload = FormatSpeed(snapshot.Network.DownloadBytesPerSecond); SystemInfo.NetworkUpload = FormatSpeed(snapshot.Network.UploadBytesPerSecond);
            var cpus = snapshot.Cpus.Select(c => Item(_cpuItems, c.Id, c.Name, "CPU", ("CPU Total", "Load", c.UsagePercent, "F1", " %"), ("CPU Speed", "Clock", c.ClockGhz, "F2", " GHz"), ("CPU Temperature", "Temperature", c.TemperatureC, "F0", " °C"))).ToArray();
            var gpus = snapshot.Gpus.Select(g => Item(_gpuItems, g.Id, g.Name, "GPU", new[] { ("GPU Core", "Load", g.UsagePercent, "F1", " %") }.Concat(g.Temperatures.IsEmpty ? new[] { ("GPU Core", "Temperature", g.TemperatureC, "F0", " °C") } : g.Temperatures.Select(t => (t.Name, "Temperature", t.Celsius, "F0", " °C"))).ToArray())).ToArray();
            Reconcile(CpuList, cpus); Reconcile(GpuList, gpus);
            foreach (var key in _cpuItems.Keys.Except(snapshot.Cpus.Select(c => c.Id)).ToArray()) _cpuItems.Remove(key);
            foreach (var key in _gpuItems.Keys.Except(snapshot.Gpus.Select(g => g.Id)).ToArray()) _gpuItems.Remove(key);
            var drives = snapshot.Storage.Select(d =>
            {
                var item = StorageList.FirstOrDefault(x => x.Name == d.Id) ?? new StorageDrive { Name = d.Id };
                item.Label = d.Label; item.TotalSize = d.TotalBytes.HasValue ? $"{d.TotalBytes / (1024d * 1024 * 1024):F0} GB" : "Unavailable";
                item.FreeSpace = d.AvailableBytes.HasValue ? $"{d.AvailableBytes / (1024d * 1024 * 1024):F0} GB free" : "Unavailable";
                item.UsagePercentage = d.UsagePercent ?? 0; item.UsageText = d.UsagePercent.HasValue ? $"{d.UsagePercent:F1}%" : "Unavailable"; return item;
            }).ToArray(); Reconcile(StorageList, drives);
            if (_isGameModeActive != snapshot.IsGameDetected) { _isGameModeActive = snapshot.IsGameDetected; OnPropertyChanged(nameof(IsGameModeActive)); }
            if (!IsBoosting && !IsCleaningDisk) StatusMessage = snapshot.IsGameDetected ? "Gaming Mode Active - Sensors Throttled" : "Monitoring Active";
        }
        private static string FormatSpeed(double? rate) => rate.HasValue ? rate > 1024 * 1024 ? $"{rate / (1024 * 1024):F1} MB/s" : $"{rate / 1024:F1} KB/s" : "Unavailable";
        private static HardwareItem Item(Dictionary<string, HardwareItem> cache, string id, string name, string type, params (string Name, string Type, double? Value, string Format, string Unit)[] sensors)
        {
            if (!cache.TryGetValue(id, out var item)) { item = new HardwareItem { Name = name, Type = type }; cache.Add(id, item); }
            item.Name = name; item.Type = type;
            foreach (var sensor in sensors) { var existing = item.Sensors.FirstOrDefault(s => s.Name == sensor.Name && s.Type == sensor.Type); if (existing == null) { existing = new SensorInfo { Name = sensor.Name, Type = sensor.Type }; item.Sensors.Add(existing); } existing.Value = sensor.Value.HasValue ? sensor.Value.Value.ToString(sensor.Format) + sensor.Unit : "Unavailable"; }
            for (int i = item.Sensors.Count - 1; i >= 0; i--) if (!sensors.Any(s => s.Name == item.Sensors[i].Name && s.Type == item.Sensors[i].Type)) item.Sensors.RemoveAt(i);
            return item;
        }
        private static void Reconcile<T>(ObservableCollection<T> target, IReadOnlyList<T> source)
        { for (int i = target.Count - 1; i >= 0; i--) if (!source.Contains(target[i])) target.RemoveAt(i); for (int i = 0; i < source.Count; i++) { int current = target.IndexOf(source[i]); if (current < 0) target.Insert(i, source[i]); else if (current != i) target.Move(current, i); } }


        private void BoostSystem()
        {
            if (!_disposed && !IsBoosting && !_isCleaningDisk) _boostTask = BoostSystemAsync();
        }
        private async Task BoostSystemAsync()
        {
            if (_disposed || IsBoosting || _isCleaningDisk) return;
            if (!_dialogs.Confirm("RAM Boost", "Trimming your background apps can temporarily free RAM but may slow their next use. Continue?")) return;
            IsBoosting = true;
            IsBoostEnabled = false;
            IsCleanDiskEnabled = false;

            BoostButtonText = "Optimizing...";
            BoostState = UiState.Running;

            try
            {
                var result = await _memoryOptimizer.OptimizeMemoryAsync(_lifetime.Token).ConfigureAwait(true);

                if (_disposed) return;
                if (!result.Success)
                {
                    StatusMessage = result.ErrorMessage ?? "Boost failed";
                    BoostButtonText = "Error";
                    BoostState = UiState.Error;
                    await Task.Delay(1800, _lifetime.Token).ConfigureAwait(true);
                    return;
                }

                int reclaimed = (int)Math.Round(result.ReclaimedMb);
                _dialogs.Notify(reclaimed > 0 ? $"Temporarily reclaimed ~{reclaimed} MB of RAM" : "Working sets trimmed (little free RAM change)", UiState.Success);
                BoostButtonText = reclaimed > 0 ? $"~{reclaimed} MB" : "Done";
                BoostState = UiState.Success;
                StatusMessage = reclaimed > 0
                    ? $"Temporarily reclaimed ~{reclaimed} MB of RAM"
                    : "Working sets trimmed (little free RAM change)";

                RefreshData();
                await Task.Delay(2000, _lifetime.Token).ConfigureAwait(true);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                Logger.Log(ex);
                if (_disposed) return;
                StatusMessage = "Boost failed unexpectedly";
                BoostButtonText = "Error";
                BoostState = UiState.Error;
            }
            finally
            {
                if (!_disposed)
                {
                    BoostButtonText = "BOOST";
                    BoostState = UiState.Idle;
                    IsBoosting = false;
                    IsBoostEnabled = true;
                    IsCleanDiskEnabled = !_isCleaningDisk;
                }
            }
        }

        private void ExecuteCleanDisk()
        {
            if (!_disposed && !_isCleaningDisk && !IsBoosting) _cleanupTask = ExecuteCleanDiskAsync();
        }
        private async Task ExecuteCleanDiskAsync()
        {
            if (_disposed || _isCleaningDisk || IsBoosting) return;
            IsCleaningDisk = true;
            OperationProgress = 0;
            IsCleanDiskEnabled = false;
            IsBoostEnabled = false;

            try
            {
                CleanDiskButtonText = "Scanning...";
                StatusMessage = "Scanning junk files...";

                var locations = _diskCleanup.CreateDefaultLocations().ToList();
                var progress = new Progress<CleanupProgress>(p =>
                {
                    if (_disposed) return;
                    OperationProgress = p.Percent;
                    if (!string.IsNullOrWhiteSpace(p.Message))
                        CleanDiskButtonText = p.Percent > 0 && p.Percent < 100
                            ? $"{p.Percent}%"
                            : "Scanning...";
                    StatusMessage = p.Message;
                });

                CleanupScanResult scanResult;
                try
                {
                    scanResult = await _diskCleanup.ScanAsync(locations, progress, _lifetime.Token).ConfigureAwait(true);
                }
                catch (Exception ex)
                {
                    Logger.Log(ex);
                    if (!_disposed) StatusMessage = "Scan failed";
                    return;
                }

                if (_disposed) return;
                var selectedLocations = _dialogs.ConfirmCleanup(scanResult.Locations);
                if (selectedLocations == null || selectedLocations.Count == 0)
                {
                    StatusMessage = "Monitoring Active";
                    return;
                }

                CleanDiskButtonText = "Cleaning...";
                StatusMessage = "Cleaning selected locations...";

                var cleanProgress = new Progress<CleanupProgress>(p =>
                {
                    if (_disposed) return;
                    OperationProgress = p.Percent;
                    CleanDiskButtonText = p.Percent is > 0 and < 100 ? $"{p.Percent}%" : "Cleaning...";
                    if (_disposed) return;
                    if (!string.IsNullOrWhiteSpace(p.Message))
                        StatusMessage = p.Message;
                });

                CleanupCleanResult cleanResult;
                try
                {
                    cleanResult = await _diskCleanup.CleanAsync(selectedLocations, cleanProgress, _lifetime.Token).ConfigureAwait(true);
                }
                catch (Exception ex)
                {
                    Logger.Log(ex);
                    if (!_disposed) StatusMessage = "Cleanup failed";
                    return;
                }

                if (_disposed) return;
                if (!cleanResult.Success)
                {
                    StatusMessage = cleanResult.ErrorMessage ?? "Cleanup failed";
                    return;
                }

                try { System.Media.SystemSounds.Exclamation.Play(); } catch { }

                _dialogs.ShowCleanupResult(cleanResult);
                if (_disposed) return;

                StatusMessage = cleanResult.FailedFiles > 0
                    ? $"Freed {DiskCleanupService.FormatByteSize(cleanResult.FreedBytes)} ({cleanResult.FailedFiles} files skipped)"
                    : $"Freed {DiskCleanupService.FormatByteSize(cleanResult.FreedBytes)}";
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
                if (!_disposed) StatusMessage = "Cleanup error - see %LocalAppData%/CortexDNA/Logs/log.txt";
            }
            finally
            {
                if (!_disposed)
                {
                    IsCleaningDisk = false;
                    CleanDiskButtonText = "CLEAN DISK";
                    IsCleanDiskEnabled = true;
                    IsBoostEnabled = !IsBoosting;
                    if (StatusMessage.StartsWith("Scanning") || StatusMessage.StartsWith("Cleaning"))
                        StatusMessage = "Monitoring Active";
                }
            }
        }

        /// <summary>
        /// Stops all timers and releases hardware monitoring resources.
        /// Call this from the application shutdown handler.
        /// </summary>
        public void Dispose() => _ = ShutdownAsync();
        public void Close() => Dispose();

        public Task ShutdownAsync()
        {
            lock (_shutdownLock)
            {
                if (_shutdownTask != null) return _shutdownTask;
                _disposed = true; _monitor.SnapshotAvailable -= SnapshotReceived; _lifetime.Cancel();
                return _shutdownTask = ReleaseResourcesAsync();
            }
        }
        private async Task ReleaseResourcesAsync()
        {
            var shutdown = _monitor.ShutdownAsync();
            try { await Task.WhenAll(_initialization, _boostTask, _cleanupTask, shutdown); }
            catch (OperationCanceledException) { }
            catch (Exception ex) { Logger.Log(ex); }
            _lifetime.Dispose();
        }
    }
}
