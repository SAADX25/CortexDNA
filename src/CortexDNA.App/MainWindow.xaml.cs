using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using CortexDNA.Core;
using CortexDNA.Navigation;
using CortexDNA.UI;
using CortexDNA.ViewModels;
using Forms = System.Windows.Forms;
namespace CortexDNA;

public partial class MainWindow : Window
{
    private Forms.NotifyIcon? _notifyIcon;
    private bool _isExplicitExit;
    private bool _shutdownStarted;
    private bool _shutdownComplete;
    public MainWindow() : this(new MainViewModel()) { }
    public MainWindow(MainViewModel viewModel)
    {
        DataContext = viewModel;
        InitializeComponent();
        viewModel.Appearance.Apply();
        viewModel.PropertyChanged += ShellChanged;
        viewModel.Appearance.PropertyChanged += AppearanceChanged;
        Loaded += WindowLoaded;
        IsVisibleChanged += VisibilityChanged;
        InitializeTrayIcon();
    }
    private MainViewModel Shell => (MainViewModel)DataContext;
    private void WindowLoaded(object sender, RoutedEventArgs args) => Motion.Enter(PageHost, Shell.Appearance.MotionEnabled);
    private void ShellChanged(object? sender, PropertyChangedEventArgs args)
    { if (args.PropertyName == nameof(MainViewModel.CurrentPage)) Motion.Enter(PageHost, Shell.Appearance.MotionEnabled); }
    private void AppearanceChanged(object? sender, PropertyChangedEventArgs args)
    { if (args.PropertyName == nameof(Services.AppearanceService.MotionEnabled) && !Shell.Appearance.MotionEnabled) Motion.Stop(PageHost); }
    private void VisibilityChanged(object sender, DependencyPropertyChangedEventArgs args)
    { if (args.NewValue is false) Motion.Stop(PageHost); }
    private void Window_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs args)
    {
        if (args.Key == Key.F && Keyboard.Modifiers == ModifierKeys.Control)
        {
            Shell.Navigation.Navigate(PageId.Tools);
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () => { var search = FindSearch(PageHost); search?.Focus(); search?.SelectAll(); });
            args.Handled = true;
        }
        else if (args.Key == Key.F5 && Shell.IsStartupVisible) { Shell.StartupVM.RefreshCommand.Execute(null); args.Handled = true; }
    }
    private static System.Windows.Controls.TextBox? FindSearch(DependencyObject node)
    {
        if (node is System.Windows.Controls.TextBox search && search.Name == "ToolSearchBox") return search;
        for (int index = 0; index < VisualTreeHelper.GetChildrenCount(node); index++)
        { var result = FindSearch(VisualTreeHelper.GetChild(node, index)); if (result != null) return result; }
        return null;
    }
    private void Notifications_Click(object sender, RoutedEventArgs args)
    { NotificationPanel.Visibility = NotificationPanel.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible; }
    private void OpenAbout_Click(object sender, RoutedEventArgs args) => new AboutWindow { Owner = this, WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
    private void TitleBar_MouseDown(object sender, MouseButtonEventArgs args)
    {
        if (args.ChangedButton != MouseButton.Left) return;
        if (args.ClickCount == 2) { ToggleMaximize(); return; }
        DragMove();
    }
    private void BtnMinimize_Click(object sender, RoutedEventArgs args) => WindowState = WindowState.Minimized;
    private void ToggleMaximize() => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    private void BtnMaximize_Click(object sender, RoutedEventArgs args) => ToggleMaximize();
    private void BtnClose_Click(object sender, RoutedEventArgs args) => Close();
    private void InitializeTrayIcon()
    {
        try
        {
            _notifyIcon = new Forms.NotifyIcon { Icon = System.Drawing.Icon.ExtractAssociatedIcon(Environment.ProcessPath!), Visible = true, Text = "Cortex DNA" };
            _notifyIcon.DoubleClick += TrayRestore;
            var menu = new Forms.ContextMenuStrip();
            menu.Items.Add("Open Cortex DNA", null, (_, _) => RestoreWindow());
            menu.Items.Add("Manage / Uninstall", null, (_, _) => Shell.OpenUninstallCommand.Execute(null));
            menu.Items.Add("-"); menu.Items.Add("Exit", null, (_, _) => RequestExit());
            _notifyIcon.ContextMenuStrip = menu;
        }
        catch (Exception ex) { Logger.Log(ex); }
    }
    private void TrayRestore(object? sender, EventArgs args) => RestoreWindow();
    private void RestoreWindow() { Show(); WindowState = WindowState.Normal; Activate(); Shell.HardwareVM.ResumeMonitoring(); }
    protected override void OnStateChanged(EventArgs args)
    {
        base.OnStateChanged(args);
        if (RootChrome != null)
        { bool maximized = WindowState == WindowState.Maximized; RootChrome.Margin = new Thickness(maximized ? 0 : 8); RootChrome.CornerRadius = new CornerRadius(maximized ? 0 : 16); }
        if (DataContext is not MainViewModel vm) return;
        if (WindowState == WindowState.Minimized) { Hide(); Motion.Stop(PageHost); vm.HardwareVM.PauseMonitoring(); }
        else vm.HardwareVM.ResumeMonitoring();
    }
    internal void RequestExit() { _isExplicitExit = true; Close(); }
    protected override async void OnClosing(CancelEventArgs args)
    {
        if (!_isExplicitExit) { args.Cancel = true; WindowState = WindowState.Minimized; return; }
        if (!_shutdownComplete)
        {
            args.Cancel = true;
            if (_shutdownStarted) return;
            _shutdownStarted = true; IsEnabled = false; Motion.Stop(PageHost);
            await Shell.HardwareVM.ShutdownAsync();
            await Shell.StartupVM.ShutdownAsync();
            _shutdownComplete = true; _ = Dispatcher.BeginInvoke(Close); return;
        }
        Shell.Appearance.Save();
        if (_notifyIcon != null)
        { _notifyIcon.DoubleClick -= TrayRestore; _notifyIcon.ContextMenuStrip?.Dispose(); _notifyIcon.Dispose(); _notifyIcon = null; }
        base.OnClosing(args);
    }
    protected override void OnClosed(EventArgs args)
    {
        Loaded -= WindowLoaded; IsVisibleChanged -= VisibilityChanged;
        Shell.PropertyChanged -= ShellChanged; Shell.Appearance.PropertyChanged -= AppearanceChanged;
        Motion.Stop(PageHost); Shell.Dispose(); Shell.HardwareVM.Close();
        base.OnClosed(args);
    }
}
