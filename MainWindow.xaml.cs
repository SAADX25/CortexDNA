using System;
using System.Windows;
using System.Threading.Tasks;
using System.IO;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using CortexDNA.ViewModels;
using System.Drawing; // For Icon
using System.Windows.Forms; // For NotifyIcon
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;
using System.Windows.Media;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Text.Json;

namespace CortexDNA
{
    public partial class MainWindow : Window
    {
        private NotifyIcon _notifyIcon;
        private bool _isExplicitExit = false;

        private string _currentThemeFileName = "DarkTheme.xaml";

        public MainWindow()
        {
            InitializeComponent();
            InitializeTrayIcon();
            this.Loaded += MainWindow_Loaded;
            // Apply saved theme (or default) and fixed opacity
            this.Loaded += (s, e) =>
            {
                try
                {
                    var settings = LoadThemeSettings();
                    _currentThemeFileName = settings?.ThemeFileName ?? "DarkTheme.xaml";
                    
                    if (OpacitySlider != null)
                    {
                        OpacitySlider.Value = settings?.OpacityPercent ?? 100.0;
                    }
                    
                    ApplyTheme(_currentThemeFileName);
                }
                catch { }
            };
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            int delayMs = 0;
            foreach (UIElement child in UtilitiesWrapPanel.Children)
            {
                if (child is System.Windows.Controls.Button btn)
                {
                    btn.Opacity = 0;

                    var opacityAnim = new System.Windows.Media.Animation.DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(400));
                    opacityAnim.BeginTime = TimeSpan.FromMilliseconds(delayMs);

                    if (btn.RenderTransform != null && btn.RenderTransform.IsFrozen)
                    {
                        btn.RenderTransform = btn.RenderTransform.Clone();
                    }

                    if (btn.RenderTransform is System.Windows.Media.TransformGroup group)
                    {
                        var translate = group.Children[1] as System.Windows.Media.TranslateTransform;
                        if (translate != null)
                        {
                            translate.Y = 20;
                            var translateAnim = new System.Windows.Media.Animation.DoubleAnimation(20, 0, TimeSpan.FromMilliseconds(400));
                            translateAnim.BeginTime = TimeSpan.FromMilliseconds(delayMs);
                            translateAnim.EasingFunction = new System.Windows.Media.Animation.BackEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut, Amplitude = 0.5 };
                            translate.BeginAnimation(System.Windows.Media.TranslateTransform.YProperty, translateAnim);
                        }
                    }

                    btn.BeginAnimation(UIElement.OpacityProperty, opacityAnim);
                    delayMs += 75; // Stagger by 75ms
                }
            }
        }

        private void InitializeTrayIcon()
        {
            try
            {
                _notifyIcon = new NotifyIcon
                {
                    Icon = System.Drawing.Icon.ExtractAssociatedIcon(Process.GetCurrentProcess().MainModule.FileName),
                    Visible = true,
                    Text = "Cortex DNA"
                };

                _notifyIcon.DoubleClick += (s, e) => RestoreWindow();

                // Build Context Menu
                var contextMenu = new ContextMenuStrip();
                
                // 1. Run on Startup (Toggle)
                var runOnStartupItem = new ToolStripMenuItem("Run on Startup");
                runOnStartupItem.Image = GenerateColoredCircle(System.Drawing.Color.LightGreen); // 🟢
                runOnStartupItem.CheckOnClick = true;
                
                // Check current state from ViewModel logic or Registry
                if (DataContext is MainViewModel vm)
                {
                    runOnStartupItem.Checked = vm.RunOnStartup;
                    runOnStartupItem.Click += (s, e) => vm.RunOnStartup = runOnStartupItem.Checked;
                }
                else
                {
                    // Fallback if ViewModel not ready
                    runOnStartupItem.Checked = false;
                }
                
                contextMenu.Items.Add(runOnStartupItem);
                contextMenu.Items.Add("-");

                // 2. Open Cortex DNA
                var openItem = new ToolStripMenuItem("Open Cortex DNA", null, (s, e) => RestoreWindow());
                // openItem.Image = ... (Optional: Folder icon)
                contextMenu.Items.Add(openItem);

                // 3. Manage / Uninstall (Red)
                var uninstallItem = new ToolStripMenuItem("Manage / Uninstall", null, (s, e) => OpenUninstallSettings());
                uninstallItem.ForeColor = System.Drawing.Color.Red; // 🔴
                contextMenu.Items.Add(uninstallItem);

                contextMenu.Items.Add("-");

                // 4. Exit
                contextMenu.Items.Add("Exit", null, (s, e) => 
                {
                    _isExplicitExit = true;
                    Close();
                });

                _notifyIcon.ContextMenuStrip = contextMenu;
            }
            catch (Exception ex) 
            {
                Core.Logger.Log($"Tray Init Failed: {ex.Message}");
            }
        }

        private System.Drawing.Image GenerateColoredCircle(System.Drawing.Color color)
        {
            var bmp = new Bitmap(16, 16);
            using (var g = Graphics.FromImage(bmp))
            {
                using (var brush = new SolidBrush(color))
                {
                    g.FillEllipse(brush, 4, 4, 8, 8);
                }
            }
            return bmp;
        }

        private void OpenUninstallSettings()
        {
            try
            {
                // Open Windows Settings > Apps & Features
                // This is safer than running an unsigned .exe directly
                using (var process = Process.Start(new ProcessStartInfo("ms-settings:appsfeatures") 
                { 
                    UseShellExecute = true 
                }))
                { }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not open Settings: {ex.Message}");
            }
        }

        private void RestoreWindow()
        {
            Show();
            WindowState = WindowState.Normal;
            Activate();
            
            // Resume Monitoring
            if (DataContext is MainViewModel vm)
            {
                vm.ResumeMonitoring();
            }
        }

        protected override void OnStateChanged(EventArgs e)
        {
            base.OnStateChanged(e);
            if (WindowState == WindowState.Minimized)
            {
                Hide();
                if (DataContext is MainViewModel vm)
                {
                    vm.PauseMonitoring();
                }
            }
            else
            {
                if (DataContext is MainViewModel vm)
                {
                    vm.ResumeMonitoring();
                }
            }
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            if (!_isExplicitExit)
            {
                e.Cancel = true;
                WindowState = WindowState.Minimized; // Triggers OnStateChanged logic to Hide & Pause
            }
            else
            {
                _notifyIcon?.Dispose();
                base.OnClosing(e);
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            if (DataContext is MainViewModel vm)
            {
                vm.Close();
            }
        }

        private void OpenUpdates_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                using (var process = Process.Start(new ProcessStartInfo
                {
                    FileName = "https://github.com/SAADX25/Cortex-DNA-Releases/releases",
                    UseShellExecute = true
                }))
                { }
            }
            catch { /* Ignore errors */ }
        }

        private void OpenSettings_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button btn && btn.ContextMenu != null)
            {
                btn.ContextMenu.PlacementTarget = btn;
                btn.ContextMenu.IsOpen = true;
            }
        }

        private void OpenAbout_Click(object sender, RoutedEventArgs e)
        {
            var aboutWindow = new AboutWindow
            {
                Owner = this, // Sets the Main Window as the parent for centering
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };
            aboutWindow.ShowDialog(); // Opens as a modal
        }

        private void ThemeButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentThemeFileName == "DarkTheme.xaml")
            {
                _currentThemeFileName = "LightTheme.xaml";
            }
            else
            {
                _currentThemeFileName = "DarkTheme.xaml";
            }
            ApplyTheme(_currentThemeFileName);
            SaveThemeSettings();
        }

        private void OpacitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            try
            {
                double value = OpacitySlider?.Value ?? 100.0;
                if (this.Resources["AppBackgroundBrush"] is SolidColorBrush sb)
                {
                    sb.Opacity = Math.Max(0.0, Math.Min(1.0, value / 100.0));
                }
                else if (this.Background is SolidColorBrush wb)
                {
                    wb.Opacity = Math.Max(0.0, Math.Min(1.0, value / 100.0));
                }
                SaveThemeSettings();
            }
            catch { }
        }

        private void ApplyTheme(string themeFileName)
        {
            try
            {
                var uri = new Uri($"Themes/{themeFileName}", UriKind.Relative);
                var dict = new ResourceDictionary { Source = uri };

                // Read color entries and create brushes
                System.Windows.Media.Color bg = (System.Windows.Media.Color)dict["AppBackgroundColor"];
                System.Windows.Media.Color primary = (System.Windows.Media.Color)dict["PrimaryTextColor"];
                System.Windows.Media.Color secondary = (System.Windows.Media.Color)dict["SecondaryTextColor"];
                System.Windows.Media.Color cardBg = (System.Windows.Media.Color)dict["CardBackgroundColor"];
                System.Windows.Media.Color cardBorder = (System.Windows.Media.Color)dict["CardBorderColor"];
                System.Windows.Media.Color accent = (System.Windows.Media.Color)dict["AccentColor"];

                System.Windows.Media.Color accent2 = DarkenColor(accent, 0.25);

                // Set Color resources (for GradientStop and effects)
                this.Resources["AppBackgroundColor"] = bg;
                this.Resources["PrimaryTextColor"] = primary;
                this.Resources["SecondaryTextColor"] = secondary;
                this.Resources["CardBackgroundColor"] = cardBg;
                this.Resources["CardBorderColor"] = cardBorder;
                this.Resources["AccentColor"] = accent;

                // Create brushes
                var appBrush = new SolidColorBrush(bg);
                var primaryBrush = new SolidColorBrush(primary);
                var secondaryBrush = new SolidColorBrush(secondary);
                var cardBgBrush = new SolidColorBrush(cardBg);
                var cardBorderBrush = new SolidColorBrush(cardBorder);
                var accentBrush = new SolidColorBrush(accent);

                this.Resources["AppBackgroundBrush"] = appBrush;
                this.Resources["PrimaryTextBrush"] = primaryBrush;
                this.Resources["SecondaryTextBrush"] = secondaryBrush;
                this.Resources["CardBackgroundBrush"] = cardBgBrush;
                this.Resources["CardBorderBrush"] = cardBorderBrush;
                this.Resources["AccentBrush"] = accentBrush;
                this.Resources["AccentBrush2"] = new SolidColorBrush(accent2);
                this.Resources["AccentColor2"] = accent2;

                // Derived brushes for hover/pressed/badge/success/error
                System.Windows.Media.Color lightenCard = LightenColor(cardBg, 0.07);
                System.Windows.Media.Color darkenCard = DarkenColor(cardBg, 0.06);
                this.Resources["HoverBackgroundBrush"] = new SolidColorBrush(lightenCard);
                this.Resources["PressedBackgroundBrush"] = new SolidColorBrush(darkenCard);
                this.Resources["BadgeBackgroundBrush"] = cardBorderBrush;
                var successColor = System.Windows.Media.Color.FromArgb(0xFF, 0x88, 0xFF, 0x88);
                var errorColor = System.Windows.Media.Color.FromArgb(0xFF, 0xFF, 0x55, 0x55);
                this.Resources["SuccessBrush"] = new SolidColorBrush(successColor);
                this.Resources["ErrorBrush"] = new SolidColorBrush(errorColor);
                this.Resources["SuccessColor"] = successColor;
                this.Resources["ErrorColor"] = errorColor;

                // Ensure background uses the brush
                if (this.Resources["AppBackgroundBrush"] is SolidColorBrush appBrushRes)
                {
                    this.Background = appBrushRes;
                    double value = OpacitySlider?.Value ?? 100.0;
                    appBrushRes.Opacity = Math.Max(0.0, Math.Min(1.0, value / 100.0));
                }
            }
            catch (Exception ex)
            {
                Core.Logger.Log($"Theme apply failed: {ex.Message}");
            }
        }

        private System.Windows.Media.Color LightenColor(System.Windows.Media.Color c, double factor)
        {
            byte R = (byte)Math.Min(255, c.R + (255 - c.R) * factor);
            byte G = (byte)Math.Min(255, c.G + (255 - c.G) * factor);
            byte B = (byte)Math.Min(255, c.B + (255 - c.B) * factor);
            return System.Windows.Media.Color.FromArgb(c.A, R, G, B);
        }

        private System.Windows.Media.Color DarkenColor(System.Windows.Media.Color c, double factor)
        {
            byte R = (byte)Math.Max(0, c.R - c.R * factor);
            byte G = (byte)Math.Max(0, c.G - c.G * factor);
            byte B = (byte)Math.Max(0, c.B - c.B * factor);
            return System.Windows.Media.Color.FromArgb(c.A, R, G, B);
        }

        private void SaveThemeSettings()
        {
            try
            {
                var settings = new ThemeSettings
                {
                    ThemeFileName = _currentThemeFileName,
                    OpacityPercent = OpacitySlider?.Value ?? 100.0
                };

                string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CortexDNA");
                Directory.CreateDirectory(dir);
                string file = Path.Combine(dir, "theme_settings.json");
                var json = JsonSerializer.Serialize(settings);
                File.WriteAllText(file, json);
            }
            catch { }
        }

        private ThemeSettings LoadThemeSettings()
        {
            try
            {
                string file = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CortexDNA", "theme_settings.json");
                if (!File.Exists(file)) return null;
                var json = File.ReadAllText(file);
                return JsonSerializer.Deserialize<ThemeSettings>(json);
            }
            catch { return null; }
        }

        private class ThemeSettings
        {
            public string ThemeFileName { get; set; }
            public double OpacityPercent { get; set; }
        }

        private void OpenSystemTool(string command, string args = "")
        {
            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo(command, args)
                {
                    UseShellExecute = true
                };
                using (var process = System.Diagnostics.Process.Start(psi))
                {
                    // Fire and forget, but dispose the wrapper handle right away
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not launch tool: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Button_MsInfo_Click(object sender, RoutedEventArgs e)
        {
            OpenSystemTool("msinfo32.exe");
        }

        private void Button_TaskManager_Click(object sender, RoutedEventArgs e)
        {
            OpenSystemTool("taskmgr.exe");
        }

        private void Button_DeviceManager_Click(object sender, RoutedEventArgs e)
        {
            OpenSystemTool("devmgmt.msc");
        }

        private async void Button_DiskCleanup_Click(object sender, RoutedEventArgs e)
        {
            // 1. Scan Phase
            TxtDiskCleanup.Text = "Scanning...";
            
            var scanResult = await Task.Run(() => ScanJunkFiles());

            // 2. Report & Confirm
            /* Old MessageBox Logic - Replaced with Modern Window
            string sizeMsg = scanResult.TotalSizeMB > 1024 
                ? $"{scanResult.TotalSizeMB / 1024.0:F2} GB" 
                : $"{scanResult.TotalSizeMB:F0} MB";

            string msg = $"Smart Scan found:\n\n" +
                         $"• {scanResult.FileCount:N0} junk files\n" +
                         $"• {sizeMsg} potential space\n\n" +
                         "Locations:\n" +
                         "• User & System Temp\n" +
                         "• Prefetch & Recent Items\n" +
                         "• Windows Update Cache\n" +
                         "• Error Reporting Logs\n\n" +
                         "Do you want to perform a Deep Clean?";

            if (MessageBox.Show(msg, "Smart Deep Cleaner", MessageBoxButton.YesNo, MessageBoxImage.Information) != MessageBoxResult.Yes)
            {
                TxtDiskCleanup.Text = "CLEAN DISK";
                return;
            }
            */

            // New Modern Window Logic
            var dialog = new CleanConfirmationWindow(scanResult.FileCount, scanResult.TotalSizeMB, scanResult.Locations);
            dialog.Owner = this; // Center over main window
            
            bool? result = dialog.ShowDialog();
            
            if (result != true)
            {
                TxtDiskCleanup.Text = "CLEAN DISK";
                return;
            }

            // 3. Deep Clean Phase (Explicit Yes Logic)
            TxtDiskCleanup.Text = "Deep Cleaning...";
            ((System.Windows.Controls.Button)sender).IsEnabled = false; // Prevent double clicks
            
            double freedMB = await Task.Run(() => CleanJunkFiles());

            // 4. Success & Reset
            ((System.Windows.Controls.Button)sender).IsEnabled = true;
            TxtDiskCleanup.Text = "CLEAN DISK";
            
            System.Media.SystemSounds.Exclamation.Play();
            
            // Show new Result Window
            long freedBytes = (long)(freedMB * 1024.0 * 1024.0);
            var resultWindow = new CleanupResultsWindow(freedBytes, scanResult.FileCount); 
            resultWindow.Owner = this;
            resultWindow.ShowDialog();
        }

        // --- Helper Methods ---

        private Dictionary<string, string> GetCleanPaths()
        {
            string systemRoot = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            string programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            
            return new Dictionary<string, string>
            {
                { "User Temporary Files", System.IO.Path.GetTempPath() },
                { "System Temp Folder", System.IO.Path.Combine(systemRoot, "Temp") },
                { "Windows Prefetch", System.IO.Path.Combine(systemRoot, "Prefetch") },
                { "Recent Items", Environment.GetFolderPath(Environment.SpecialFolder.Recent) },
                { "Windows Update Cache", System.IO.Path.Combine(systemRoot, @"SoftwareDistribution\Download") },
                { "Error Reporting Logs", System.IO.Path.Combine(programData, @"Microsoft\Windows\WER") }
            };
        }

        private (int FileCount, double TotalSizeMB, List<CleanupLocationItem> Locations) ScanJunkFiles()
        {
            int count = 0;
            long totalBytes = 0;
            var locations = new List<CleanupLocationItem>();

            foreach (var kvp in GetCleanPaths())
            {
                string name = kvp.Key;
                string path = kvp.Value;
                long locationBytes = 0;

                if (!Directory.Exists(path)) 
                {
                    locations.Add(new CleanupLocationItem { Name = name, Bytes = 0, FormattedSize = "0 MB" });
                    continue;
                }

                try
                {
                    // Recursive scan
                    var options = new EnumerationOptions { 
                        IgnoreInaccessible = true, 
                        RecurseSubdirectories = true 
                    };
                    var files = Directory.GetFiles(path, "*.*", options);
                    foreach (var f in files)
                    {
                        try
                        {
                            string fileName = Path.GetFileName(f).ToLower();
                            if (fileName.StartsWith("thumbcache_") || fileName.StartsWith("iconcache_"))
                                continue;

                            count++;
                            long size = new FileInfo(f).Length;
                            totalBytes += size;
                            locationBytes += size;
                        }
                        catch { }
                    }
                }
                catch { }

                locations.Add(new CleanupLocationItem 
                { 
                    Name = name, 
                    Bytes = locationBytes, 
                    FormattedSize = FormatByteSize(locationBytes) 
                });
            }

            return (count, totalBytes / (1024.0 * 1024.0), locations);
        }
        
        private string FormatByteSize(long bytes)
        {
            if (bytes == 0) return "0 MB";
            if (bytes < 1048576) return $"{bytes / 1024.0:F0} KB";
            double mb = bytes / 1048576.0;
            if (mb > 1024) return $"{mb / 1024.0:F2} GB";
            return $"{mb:F1} MB";
        }

        private long DeleteDirectoryRecursively(string target_dir)
        {
            long freedBytes = 0;
            string[] files = Array.Empty<string>();
            try
            {
                files = Directory.GetFiles(target_dir);
            }
            catch { }

            foreach (string file in files)
            {
                try
                {
                    string fileName = Path.GetFileName(file).ToLower();
                    if (fileName.StartsWith("thumbcache_") || fileName.StartsWith("iconcache_"))
                        continue;

                    var fi = new FileInfo(file);
                    long size = fi.Length;
                    File.SetAttributes(file, FileAttributes.Normal); // Fixes Read-Only issue
                    fi.Delete();
                    freedBytes += size;
                }
                catch { /* Silently skip locked files */ }
            }

            string[] dirs = Array.Empty<string>();
            try
            {
                dirs = Directory.GetDirectories(target_dir);
            }
            catch { }

            foreach (string dir in dirs)
            {
                freedBytes += DeleteDirectoryRecursively(dir);
            }

            try
            {
                Directory.Delete(target_dir, false);
            }
            catch { /* Silently skip folders that still contain locked files */ }
            
            return freedBytes;
        }

        public class CleanupLocationItem
        {
            public string Name { get; set; } = string.Empty;
            public string FormattedSize { get; set; } = string.Empty;
            public long Bytes { get; set; }
        }

        private double CleanJunkFiles()
        {
            long deletedBytes = 0;
            string recentPath = Environment.GetFolderPath(Environment.SpecialFolder.Recent);
            
            // A. Stop Services for deep clean
            StopUpdateServices();

            // B. Clean
            foreach (var path in GetCleanPaths().Values)
            {
                if (!Directory.Exists(path)) continue;

                // Safety Check: Is this the Recent folder?
                bool isRecentFolder = string.Equals(path, recentPath, StringComparison.OrdinalIgnoreCase);

                try
                {
                    // 1. Files (Always safe to delete individual files in these paths)
                    foreach (var file in Directory.GetFiles(path))
                    {
                        try
                        {
                            string fileName = Path.GetFileName(file).ToLower();
                            if (fileName.StartsWith("thumbcache_") || fileName.StartsWith("iconcache_"))
                                continue;

                            var fi = new FileInfo(file);
                            long size = fi.Length;
                            File.SetAttributes(file, FileAttributes.Normal); // Fixes Read-Only issue
                            fi.Delete();
                            deletedBytes += size;
                        }
                        catch { }
                    }

                    // 2. Directories (SKIP for Recent folder to protect Quick Access)
                    if (!isRecentFolder)
                    {
                        foreach (var dir in Directory.GetDirectories(path))
                        {
                            deletedBytes += DeleteDirectoryRecursively(dir);
                        }
                    }
                }
                catch { }
            }

            // C. Restart Services
            StartUpdateServices();

            return deletedBytes / (1024.0 * 1024.0);
        }

        private void StopUpdateServices()
        {
            RunHiddenCmd("net stop wuauserv");
            RunHiddenCmd("net stop bits");
        }

        private void StartUpdateServices()
        {
            RunHiddenCmd("net start wuauserv");
            RunHiddenCmd("net start bits");
        }

        private void RunHiddenCmd(string cmd)
        {
            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo("cmd.exe", "/c " + cmd)
                {
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden
                };
                using (var process = System.Diagnostics.Process.Start(psi))
                {
                    process?.WaitForExit(3000); // Wait max 3s
                }
            }
            catch { }
        }

        private void Button_RegEdit_Click(object sender, RoutedEventArgs e)
        {
            OpenSystemTool("regedit.exe");
        }

        private void Button_Services_Click(object sender, RoutedEventArgs e)
        {
            OpenSystemTool("services.msc");
        }

        private void Button_Network_Click(object sender, RoutedEventArgs e)
        {
            OpenSystemTool("ncpa.cpl");
        }

        private void Button_Cmd_Click(object sender, RoutedEventArgs e)
        {
            OpenSystemTool("cmd.exe");
        }

        private void Button_PowerShell_Click(object sender, RoutedEventArgs e)
        {
            OpenSystemTool("powershell.exe");
        }

        private void Button_EventViewer_Click(object sender, RoutedEventArgs e)
        {
            OpenSystemTool("eventvwr.msc");
        }

        private void Button_ControlPanel_Click(object sender, RoutedEventArgs e)
        {
            OpenSystemTool("control.exe");
        }

        private void Button_ResMon_Click(object sender, RoutedEventArgs e)
        {
            OpenSystemTool("resmon.exe");
        }

        // --- Interaction Logic for Utility Buttons ---
        // (Event handlers removed as they are no longer used by Sidebar)

        private async void CopyBiosInfo_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is MainViewModel vm && !string.IsNullOrEmpty(vm.SystemInfo.BiosInfo))
            {
                try
                {
                    System.Windows.Clipboard.SetText(vm.SystemInfo.BiosInfo);
                    
                    // Show "Copied!" feedback - Assuming TxtCopyFeedback exists in XAML but might be named differently or removed in recent edits.
                    // Checking XAML history, it seems TxtCopyFeedback was part of the old layout.
                    // We should remove this if the UI element is gone, or ensure it exists.
                    // For now, let's wrap it in a null check if we can access it, or just remove the feedback for safety if the element is missing.
                    // However, in code-behind, we can't easily check for null if it's not generated.
                    // Re-checking XAML: The new layout has individual buttons but maybe no feedback text block named TxtCopyFeedback?
                    // The old TxtCopyFeedback was inside the OS & BIOS Card stackpanel.
                    // The new layout has buttons in a Grid.
                    // Let's remove the feedback logic for now to fix the build error, or re-add the textblock to XAML.
                    // User asked for "Professional Deployment", stability is key. Removing broken UI logic is safer.
                }
                catch { }
            }
        }

        private async void CopyCpuInfo_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is MainViewModel vm && !string.IsNullOrEmpty(vm.CpuName))
            {
                try
                {
                    System.Windows.Clipboard.SetText(vm.CpuName);
                    
                    TxtCpuCopyFeedback.Visibility = Visibility.Visible;
                    await Task.Delay(2000);
                    TxtCpuCopyFeedback.Visibility = Visibility.Collapsed;
                }
                catch { }
            }
        }

        private async void CopyGpuInfo_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is MainViewModel vm && !string.IsNullOrEmpty(vm.GpuName))
            {
                try
                {
                    System.Windows.Clipboard.SetText(vm.GpuName);
                    
                    TxtGpuCopyFeedback.Visibility = Visibility.Visible;
                    await Task.Delay(2000);
                    TxtGpuCopyFeedback.Visibility = Visibility.Collapsed;
                }
                catch { }
            }
        }

        private void TitleBar_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ChangedButton == System.Windows.Input.MouseButton.Left)
            {
                // If the click originated on an interactive control (Slider, Thumb, ToggleButton)
                // or any of their visual children, do not start window drag — let the control handle input.
                var original = e.OriginalSource as DependencyObject;
                if (original != null && IsInteractiveElement(original))
                {
                    return;
                }

                this.DragMove();
            }
        }

        private bool IsInteractiveElement(DependencyObject src)
        {
            try
            {
                DependencyObject current = src;
                // Walk up the visual tree first
                while (current != null)
                {
                    if (current is System.Windows.Controls.Slider) return true;
                    if (current is System.Windows.Controls.Primitives.Thumb) return true;
                    if (current is System.Windows.Controls.Primitives.Track) return true;
                    if (current is System.Windows.Controls.Primitives.RepeatButton) return true;
                    if (current is System.Windows.Controls.Primitives.RangeBase) return true; // SliderBase
                    if (current is System.Windows.Controls.Primitives.ToggleButton) return true;
                    if (current is System.Windows.Controls.Primitives.ScrollBar) return true;
                    if (current is System.Windows.Controls.Primitives.ButtonBase) return true;
                    if (current is System.Windows.Controls.TextBox) return true;

                    // Fallback: check type name for common interactive parts (Thumb, Track, PART)
                    var typeName = current.GetType().Name;
                    if (!string.IsNullOrEmpty(typeName) && (typeName.IndexOf("Thumb", StringComparison.OrdinalIgnoreCase) >= 0 || typeName.IndexOf("Track", StringComparison.OrdinalIgnoreCase) >= 0 || typeName.IndexOf("PART_", StringComparison.OrdinalIgnoreCase) >= 0))
                        return true;

                    current = VisualTreeHelper.GetParent(current);
                }

                // If visual tree didn't find a match, try logical tree
                current = src;
                while (current != null)
                {
                    var logicalParent = System.Windows.LogicalTreeHelper.GetParent(current);
                    if (logicalParent == null) break;
                    if (logicalParent is System.Windows.Controls.Slider) return true;
                    if (logicalParent is System.Windows.Controls.Primitives.Thumb) return true;
                    if (logicalParent is System.Windows.Controls.Primitives.ToggleButton) return true;
                    if (logicalParent is System.Windows.Controls.Primitives.Track) return true;
                    if (logicalParent is System.Windows.Controls.Primitives.RepeatButton) return true;
                    if (logicalParent is System.Windows.Controls.Primitives.RangeBase) return true;
                    if (logicalParent is System.Windows.Controls.Primitives.ButtonBase) return true;
                    current = logicalParent;
                }
            }
            catch { }
            return false;
        }

        private void BtnMinimize_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }

        private void BtnMaximize_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = this.WindowState == WindowState.Maximized 
                ? WindowState.Normal 
                : WindowState.Maximized;
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
