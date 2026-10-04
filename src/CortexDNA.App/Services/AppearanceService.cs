using System.ComponentModel;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using SystemColors = System.Windows.SystemColors;
using CortexDNA.Core;
namespace CortexDNA.Services;

public sealed class AppearanceService : ObservableObject, IDisposable
{
    private readonly string _settingsPath;
    private bool _disposed;
    private System.Windows.Threading.DispatcherTimer? _saveTimer;
    private string _themeFileName = "DarkTheme.xaml";
    private double _opacityPercent = 100;
    private bool _reducedMotion;
    public AppearanceService(string? settingsPath = null)
    {
        _settingsPath = settingsPath ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CortexDNA", "theme_settings.json");
        try
        {
            if (File.Exists(_settingsPath))
            {
                var settings = JsonSerializer.Deserialize<Preferences>(File.ReadAllText(_settingsPath));
                _themeFileName = settings?.ThemeFileName == "LightTheme.xaml" ? "LightTheme.xaml" : "DarkTheme.xaml";
                _opacityPercent = settings?.OpacityPercent > 0 ? Clamp(settings.OpacityPercent) : 100;
                _reducedMotion = settings?.ReducedMotion ?? false;
            }
        }
        catch (Exception ex) { Logger.Log(ex); }
        SystemParameters.StaticPropertyChanged += SystemPreferenceChanged;
    }
    public string ThemeFileName { get => _themeFileName; set { if (value is not ("DarkTheme.xaml" or "LightTheme.xaml")) return; if (SetProperty(ref _themeFileName, value)) Update(); } }
    public double OpacityPercent { get => _opacityPercent; set { if (SetProperty(ref _opacityPercent, Clamp(value))) { OnPropertyChanged(nameof(OpacityLabel)); Update(); } } }
    public string OpacityLabel => OpacityPercent >= 96 ? $"{OpacityPercent:0}% · solid" : $"{OpacityPercent:0}% · glass";
    public bool ReducedMotion { get => _reducedMotion; set { if (SetProperty(ref _reducedMotion, value)) { OnPropertyChanged(nameof(MotionEnabled)); Update(); } } }
    public bool MotionEnabled => !ReducedMotion && SystemParameters.ClientAreaAnimation && !SystemParameters.HighContrast;
    public void ToggleTheme() => ThemeFileName = ThemeFileName == "DarkTheme.xaml" ? "LightTheme.xaml" : "DarkTheme.xaml";
    private static double Clamp(double value) => double.IsFinite(value) ? Math.Clamp(value, 45, 100) : 100;
    private void Update()
    {
        if (_disposed) return; Apply();
        if (System.Windows.Application.Current == null) { Save(); return; }
        if (_saveTimer == null)
        {
            _saveTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
            _saveTimer.Tick += SaveTick;
        }
        _saveTimer.Stop(); _saveTimer.Start();
    }
    private void SaveTick(object? sender, EventArgs args) { _saveTimer?.Stop(); Save(); }
    private void SystemPreferenceChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName is not (nameof(SystemParameters.ClientAreaAnimation) or nameof(SystemParameters.HighContrast))) return;
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher == null) { OnPropertyChanged(nameof(MotionEnabled)); return; }
        if (dispatcher.HasShutdownStarted) return;
        dispatcher.BeginInvoke(() => { if (!_disposed) { OnPropertyChanged(nameof(MotionEnabled)); Apply(); } });
    }
    public void Apply()
    {
        if (_disposed || System.Windows.Application.Current == null) return;
        var resources = System.Windows.Application.Current.Resources;
        try
        {
            var palette = new ResourceDictionary { Source = new Uri($"/CortexDNA;component/Themes/{ThemeFileName}", UriKind.Relative) };
            foreach (string key in palette.Keys) resources[key] = palette[key];
            if (SystemParameters.HighContrast)
            {
                resources["AppBackgroundColor"] = SystemColors.WindowColor;
                resources["CardBackgroundColor"] = SystemColors.WindowColor;
                resources["SurfaceColor"] = SystemColors.WindowColor;
                resources["PrimaryTextColor"] = SystemColors.WindowTextColor;
                resources["SecondaryTextColor"] = SystemColors.WindowTextColor;
                resources["MutedTextColor"] = SystemColors.WindowTextColor;
                resources["AccentColor"] = SystemColors.HighlightColor;
                resources["OnAccentColor"] = SystemColors.HighlightTextColor;
                resources["AccentColor2"] = SystemColors.HighlightColor;
                resources["AccentSoftColor"] = SystemColors.WindowColor;
                resources["CardBorderColor"] = SystemColors.WindowTextColor;
                resources["HoverBackgroundColor"] = SystemColors.ControlColor;
                resources["PressedBackgroundColor"] = SystemColors.ControlColor;
                resources["BadgeBackgroundColor"] = SystemColors.ControlColor;
            }
            foreach (string name in new[] { "OnAccent", "AppBackground", "PrimaryText", "SecondaryText", "MutedText", "CardBackground", "CardBorder", "Surface", "Accent", "AccentSoft", "Success", "Warning", "Error", "HoverBackground", "PressedBackground", "BadgeBackground" })
                resources[name + "Brush"] = new SolidColorBrush((System.Windows.Media.Color)resources[name + "Color"]);
            resources["AccentBrush2"] = new SolidColorBrush((System.Windows.Media.Color)resources["AccentColor2"]);
            if (!SystemParameters.HighContrast)
            {
                ApplyAlpha(resources, "AppBackground", 42); ApplyAlpha(resources, "CardBackground", 90); ApplyAlpha(resources, "CardBorder", 75);
            }
        }
        catch (Exception ex) { Logger.Log(ex); }
    }
    private void ApplyAlpha(ResourceDictionary resources, string name, double min)
    {
        var color = (System.Windows.Media.Color)resources[name + "Color"];
        double t = (OpacityPercent - 45) / 55;
        byte alpha = (byte)Math.Round(255 * (min + t * (100 - min)) / 100);
        resources[name + "Brush"] = new SolidColorBrush(System.Windows.Media.Color.FromArgb(alpha, color.R, color.G, color.B));
    }
    public void Save()
    {
        if (_disposed) return;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_settingsPath)!);
            File.WriteAllText(_settingsPath + ".tmp", JsonSerializer.Serialize(new Preferences { ThemeFileName = ThemeFileName, OpacityPercent = OpacityPercent, ReducedMotion = ReducedMotion }));
            File.Move(_settingsPath + ".tmp", _settingsPath, overwrite: true);
        }
        catch (Exception ex) { Logger.Log(ex); }
    }
    public void Dispose() { if (_disposed) return; _disposed = true; SystemParameters.StaticPropertyChanged -= SystemPreferenceChanged; if (_saveTimer != null) { _saveTimer.Stop(); _saveTimer.Tick -= SaveTick; } }
    private sealed class Preferences
    {
        public string ThemeFileName { get; set; } = "DarkTheme.xaml";
        public double OpacityPercent { get; set; } = 100;
        public bool ReducedMotion { get; set; }
    }
}
