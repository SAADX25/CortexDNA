using System.Collections.ObjectModel;
using System.Windows.Input;
using System.Windows.Media;
using CortexDNA.Core;
using CortexDNA.Core.Startup;
using CortexDNA.Models;

namespace CortexDNA.ViewModels
{
    public sealed class StartupItemViewModel : ViewModelBase
    {
        private readonly IStartupService _service;
        private readonly Action<string> _setStatus;
        private bool _isEnabled;
        private bool _isDelayed;
        private bool _isBusy;

        public StartupItemViewModel(StartupItem model, IStartupService service, Action<string> setStatus)
        {
            Model = model;
            _service = service;
            _setStatus = setStatus;
            _isEnabled = model.IsEnabled;
            _isDelayed = model.IsDelayed;
            Icon = StartupIconLoader.Load(model.IconPath);

            ToggleCommand = new RelayCommand(Toggle, () => !_isBusy && Model.CanModify);
            DelayCommand = new RelayCommand(Delay, () => !_isBusy && Model.CanModify && !_isDelayed && CanDelay);
            RemoveDelayCommand = new RelayCommand(RemoveDelay, () => !_isBusy && _isDelayed);
        }

        public StartupItem Model { get; }
        public ICommand ToggleCommand { get; }
        public ICommand DelayCommand { get; }
        public ICommand RemoveDelayCommand { get; }

        public string Name => Model.Name;
        public string Initial => string.IsNullOrWhiteSpace(Model.Name) ? "?" : Model.Name[..1].ToUpperInvariant();
        public ImageSource? Icon { get; }
        public bool HasIcon => Icon != null;
        public bool IsRunning => Model.IsRunning;
        public string Publisher => string.IsNullOrWhiteSpace(Model.Publisher) ? Model.LocationLabel : Model.Publisher;
        public string LocationLabel => Model.LocationLabel;
        public string Command => Model.Command;
        public bool CanDelay => Model.Location is StartupLocationKind.CurrentUserRun or StartupLocationKind.UserStartupFolder;
        public bool ShowDelayButton => CanDelay && !_isDelayed;

        public bool IsEnabled
        {
            get => _isEnabled;
            set
            {
                if (_isEnabled == value) return;
                ToggleTo(value);
            }
        }

        public bool IsDelayed
        {
            get => _isDelayed;
            private set
            {
                if (SetProperty(ref _isDelayed, value))
                {
                    OnPropertyChanged(nameof(StateText));
                    OnPropertyChanged(nameof(ShowDelayButton));
                }
            }
        }

        public string ImpactText => Model.Impact switch
        {
            StartupImpactLevel.High => "High",
            StartupImpactLevel.Medium => "Medium",
            StartupImpactLevel.Low => "Low",
            _ when !IsEnabled => "None",
            _ => "Not measured"
        };

        public CortexDNA.UI.UiState ImpactState => Model.Impact switch
        {
            StartupImpactLevel.High or StartupImpactLevel.Medium => CortexDNA.UI.UiState.Warning,
            StartupImpactLevel.Low => CortexDNA.UI.UiState.Success,
            _ => CortexDNA.UI.UiState.Idle
        };

        public string DurationText => Model.DurationMs is double ms
            ? $"{ms / 1000.0:0.0}s"
            : "—";

        public string StateText => IsDelayed ? "Starts 30s after logon" : IsEnabled
            ? (IsRunning ? "Enabled · open now" : IsDelayed ? "Starts 30s after logon" : "Enabled")
            : (IsRunning ? "Disabled · still open now" : "Disabled");

        private void Toggle() => ToggleTo(!_isEnabled);

        private void ToggleTo(bool enabled)
        {
            if (_isBusy) return;
            _isBusy = true;
            try
            {
                _service.SetEnabled(Model, enabled);
                _isEnabled = enabled;
                IsDelayed = Model.IsDelayed;
                OnPropertyChanged(nameof(IsEnabled));
                OnPropertyChanged(nameof(StateText));
                OnPropertyChanged(nameof(ImpactText));
                _setStatus(enabled ? $"Enabled {Name}" : $"Disabled {Name}");
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
                _setStatus("Could not change this startup item");
                OnPropertyChanged(nameof(IsEnabled));
            }
            finally
            {
                _isBusy = false;
            }
        }

        private void Delay()
        {
            if (_isBusy) return;
            _isBusy = true;
            try
            {
                _service.Delay(Model);
                _isEnabled = false;
                IsDelayed = true;
                OnPropertyChanged(nameof(IsEnabled));
                _setStatus($"{Name} will start 30 seconds after logon");
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
                _setStatus(ex.Message);
            }
            finally
            {
                _isBusy = false;
            }
        }

        private void RemoveDelay()
        {
            if (_isBusy) return;
            _isBusy = true;
            try
            {
                _service.RemoveDelay(Model);
                _isEnabled = true;
                IsDelayed = false;
                OnPropertyChanged(nameof(IsEnabled));
                _setStatus($"{Name} starts immediately again");
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
                _setStatus("Could not remove delay");
            }
            finally
            {
                _isBusy = false;
            }
        }
    }

    public sealed class StartupViewModel : ViewModelBase, IDisposable
    {
        private readonly IStartupService _service;
        private bool _isLoading;
        private bool _loaded;
        private bool _disposed;
        private Task _loadTask = Task.CompletedTask;
        public void Dispose() => _disposed = true;
        public async Task ShutdownAsync() { _disposed = true; await _loadTask; }
        private string _statusMessage = "Ready";
        private string _bootSummary = "Measuring last boot…";
        private string _diagnosticsNote = string.Empty;

        private DateTime _lastLoadUtc;

        public StartupViewModel() : this(AppComposition.CreateStartupService()) { }

        public StartupViewModel(IStartupService service)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
            RefreshCommand = new RelayCommand(async () => await LoadAsync(force: true), () => !_isLoading);
        }

        public ObservableCollection<StartupItemViewModel> Items { get; } = new();
        public ICommand RefreshCommand { get; }

        public bool IsLoading
        {
            get => _isLoading;
            private set
            {
                if (SetProperty(ref _isLoading, value))
                    OnPropertyChanged(nameof(ShowEmpty));
            }
        }

        public bool ShowEmpty => !_isLoading && Items.Count == 0;

        public string StatusMessage
        {
            get => _statusMessage;
            private set => SetProperty(ref _statusMessage, value);
        }

        public string BootSummary
        {
            get => _bootSummary;
            private set => SetProperty(ref _bootSummary, value);
        }

        public string DiagnosticsNote
        {
            get => _diagnosticsNote;
            private set => SetProperty(ref _diagnosticsNote, value);
        }

        public int EnabledCount => Items.Count(i => i.IsEnabled);
        public int DisabledCount => Items.Count(i => !i.IsEnabled);
        public int HighImpactCount => Items.Count(i => i.Model.Impact == StartupImpactLevel.High);

        public async void EnsureLoaded()
        {
            if (_disposed || _isLoading) return;
            if (_loaded && DateTime.UtcNow - _lastLoadUtc < TimeSpan.FromSeconds(2))
                return;
            await LoadAsync(force: true);
        }

        public Task LoadAsync(bool force, bool migrateLegacy = true)
        {
            if (_disposed || _isLoading || (_loaded && !force)) return Task.CompletedTask;
            return _loadTask = LoadCoreAsync(migrateLegacy);
        }

        private async Task LoadCoreAsync(bool migrateLegacy)
        {
            if (_disposed || _isLoading) return;

            IsLoading = true;
            StatusMessage = "Reading startup programs…";

            try
            {
                var snapshot = await _service.LoadAsync(migrateLegacy).ConfigureAwait(true);
                if (_disposed) return;
                Items.Clear();
                foreach (var item in snapshot.Items)
                {
                    Items.Add(new StartupItemViewModel(item, _service, msg =>
                    {
                        StatusMessage = msg;
                        OnPropertyChanged(nameof(EnabledCount));
                        OnPropertyChanged(nameof(DisabledCount));
                    }));
                }

                BootSummary = BuildBootSummary(snapshot);
                DiagnosticsNote = snapshot.DiagnosticsNote ?? "Enabled apps are listed first, same as Task Manager.";
                _loaded = true;
                _lastLoadUtc = DateTime.UtcNow;
                StatusMessage = $"{EnabledCount} enabled · {DisabledCount} disabled";
                OnPropertyChanged(nameof(EnabledCount));
                OnPropertyChanged(nameof(DisabledCount));
                OnPropertyChanged(nameof(HighImpactCount));
                OnPropertyChanged(nameof(ShowEmpty));
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
                StatusMessage = "Could not load startup programs";
                DiagnosticsNote = "Windows blocked part of the startup scan. Try running Cortex DNA as Administrator.";
            }
            finally
            {
                IsLoading = false;
            }
        }

        private static string BuildBootSummary(StartupSnapshot snapshot)
        {
            var parts = new List<string>();
            if (snapshot.LastBootDuration is TimeSpan boot)
                parts.Add($"Last boot {FormatDuration(boot)}");
            if (snapshot.LastBiosDuration is TimeSpan bios)
                parts.Add($"Last BIOS {bios.TotalSeconds:0.0}s");
            return parts.Count > 0 ? string.Join("  ·  ", parts) : "Last boot time not recorded yet";
        }

        private static string FormatDuration(TimeSpan value)
        {
            if (value.TotalSeconds < 60)
                return $"{value.TotalSeconds:0.0}s";
            return $"{value.Minutes}m {value.Seconds}s";
        }
    }
}
