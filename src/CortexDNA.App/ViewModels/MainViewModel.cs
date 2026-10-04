using System.ComponentModel;
using CortexDNA.Core;
using CortexDNA.Models;
using CortexDNA.Navigation;
using CortexDNA.Services;
using CortexDNA.ViewModels.Pages;
namespace CortexDNA.ViewModels;

public sealed class MainViewModel : ViewModelBase, IDisposable
{
    private bool _disposed;
    public MainViewModel() : this(AppComposition.CreateShellDependencies()) { }
    private MainViewModel(ShellDependencies dependencies) : this(dependencies.Hardware, dependencies.Startup, dependencies.Appearance, dependencies.Notifications) { }
    public MainViewModel(HardwareViewModel hardware, StartupViewModel startup) : this(hardware, startup, new AppearanceService(), new NotificationCenter()) { }
    public MainViewModel(HardwareViewModel hardware, StartupViewModel startup, AppearanceService appearance, NotificationCenter notifications)
    {
        HardwareVM = hardware ?? throw new ArgumentNullException(nameof(hardware));
        StartupVM = startup ?? throw new ArgumentNullException(nameof(startup));
        Appearance = appearance; Notifications = notifications; Cleanup = hardware.CleanupService;
        ToolLauncher = new SystemToolService(notifications);
        Operations = new OperationPresentation(hardware, startup);
        Store = new NavigationStore();
        NavigationItems = Enum.GetValues<PageId>().Select((id, index) => new NavigationItemViewModel(id, id.ToString(), (index + 1).ToString("00"))).ToArray();
        Navigation = new NavigationService(Store, [new HomeViewModel(this),new HealthViewModel(this),new CleanupViewModel(this),new StorageViewModel(this),
            new StartupPageViewModel(this),new HardwarePageViewModel(this),new GamingViewModel(this),new SecurityViewModel(this),new ToolsViewModel(this),new SettingsViewModel(this),new DiagnosticsViewModel(this)]);
        NavigateCommand = new RelayCommand<string>(Navigate);
        ToggleThemeCommand = new RelayCommand(Appearance.ToggleTheme);
        SelectThemeCommand = new RelayCommand<string>(value => { if (value != null) Appearance.ThemeFileName = value; });
        OpenUninstallCommand = new RelayCommand(() => OpenExternal("ms-settings:appsfeatures"));
        OpenUpdatesCommand = new RelayCommand(() => OpenExternal("https://github.com/SAADX25/Cortex-DNA-Releases/releases"));
        OpenLogsCommand = new RelayCommand(ToolLauncher.OpenLogs);
        CopyDiagnosticsCommand = new RelayCommand(CopyDiagnostics);
        Store.PropertyChanged += NavigationChanged;
        StartupVM.PropertyChanged += StartupChanged;
        Navigation.Navigate(PageId.Home);
    }
    public HardwareViewModel HardwareVM { get; }
    public StartupViewModel StartupVM { get; }
    public ICleanupService Cleanup { get; }
    public AppearanceService Appearance { get; }
    public NotificationCenter Notifications { get; }
    public OperationPresentation Operations { get; }
    public SystemToolService ToolLauncher { get; }
    public NavigationStore Store { get; }
    public INavigationService Navigation { get; }
    public IReadOnlyList<NavigationItemViewModel> NavigationItems { get; }
    public RelayCommand<string> NavigateCommand { get; }
    public RelayCommand ToggleThemeCommand { get; }
    public RelayCommand<string> SelectThemeCommand { get; }
    public RelayCommand OpenUninstallCommand { get; }
    public RelayCommand OpenUpdatesCommand { get; }
    public RelayCommand OpenLogsCommand { get; }
    public RelayCommand CopyDiagnosticsCommand { get; }
    public PageViewModel? CurrentPage => Store.CurrentPage;
    public bool IsStartupVisible => CurrentPage?.Id == PageId.Startup;
    public bool IsOverviewVisible => CurrentPage?.Id == PageId.Home;
    public bool IsStartupSelected => IsStartupVisible;
    public bool IsOverviewSelected => IsOverviewVisible;
    public AppSection CurrentSection => IsStartupVisible ? AppSection.Startup : AppSection.Overview;
    private void Navigate(string? value)
    {
        if (string.Equals(value, "Overview", StringComparison.OrdinalIgnoreCase)) value = "Home";
        if (Enum.TryParse<PageId>(value, true, out var id) && Enum.IsDefined(id)) Navigation.Navigate(id);
    }
    private void NavigationChanged(object? sender, PropertyChangedEventArgs args)
    {
        foreach (var item in NavigationItems) item.IsSelected = item.Id == Store.CurrentPage?.Id;
        OnPropertyChanged(nameof(CurrentPage)); OnPropertyChanged(nameof(CurrentSection));
        OnPropertyChanged(nameof(IsStartupVisible)); OnPropertyChanged(nameof(IsOverviewVisible));
        OnPropertyChanged(nameof(IsStartupSelected)); OnPropertyChanged(nameof(IsOverviewSelected));
    }
    private void StartupChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName != nameof(StartupViewModel.StatusMessage)) return;
        string message = StartupVM.StatusMessage;
        if (message.StartsWith("Enabled ") || message.StartsWith("Disabled ") || message.Contains("will start 30 seconds") || message.Contains("starts immediately again"))
            Notifications.Publish(message);
        else if (message.StartsWith("Could not ")) Notifications.Publish(message, CortexDNA.UI.UiState.Error);
    }
    private void OpenExternal(string target)
    {
        try { using var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(target) { UseShellExecute = true }); }
        catch (Exception ex) { Logger.Log(ex); Notifications.Publish("Windows could not open this location.", CortexDNA.UI.UiState.Error); }
    }
    private void CopyDiagnostics()
    {
        try
        {
            System.Windows.Clipboard.SetText($"CortexDNA {typeof(App).Assembly.GetName().Version}\nWindows {Environment.OSVersion.Version}\nPrivilege: {HardwareVM.PrivilegeText}\nMonitoring: {HardwareVM.StatusMessage}\nStartup: {StartupVM.StatusMessage}");
            Notifications.Publish("Application diagnostics copied.");
        }
        catch (Exception ex) { Logger.Log(ex); Notifications.Publish("Diagnostics could not be copied.", CortexDNA.UI.UiState.Error); }
    }
    public void Dispose()
    {
        if (_disposed) return; _disposed = true;
        Store.PropertyChanged -= NavigationChanged; StartupVM.PropertyChanged -= StartupChanged;
        Navigation.Dispose(); Operations.Dispose(); Appearance.Dispose();
    }
}
