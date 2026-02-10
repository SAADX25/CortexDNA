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

namespace CortexDNA
{
    public partial class MainWindow : Window
    {
        private NotifyIcon _notifyIcon;
        private bool _isExplicitExit = false;

        public MainWindow()
        {
            InitializeComponent();
            InitializeTrayIcon();
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

        private Image GenerateColoredCircle(System.Drawing.Color color)
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
                Process.Start(new ProcessStartInfo("ms-settings:appsfeatures") 
                { 
                    UseShellExecute = true 
                });
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
                Process.Start(new ProcessStartInfo
                {
                    FileName = "https://github.com/SAADX25/Cortex-DNA-Releases/releases",
                    UseShellExecute = true
                });
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
            try
            {
                var assembly = Assembly.GetEntryAssembly();
                var productName = assembly?.GetCustomAttribute<AssemblyProductAttribute>()?.Product ?? "Cortex DNA";
                var version = assembly?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "1.0.0";
                var company = assembly?.GetCustomAttribute<AssemblyCompanyAttribute>()?.Company ?? "Cortex";
                
                // Note: 'Authors' in csproj often maps to Company or isn't standard attribute. 
                // We'll use Company or fallback to hardcoded if needed, but per request we format it:
                
                string msg = $"Product Name: {productName}\n\n" +
                             $"Version: {version}\n\n" +
                             $"Developer/Author: SAADX25\n\n" +
                             $"Company: {company}";

                MessageBox.Show(msg, "About", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch { }
        }

        private void OpenSystemTool(string command, string args = "")
        {
            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo(command, args)
                {
                    UseShellExecute = true
                };
                System.Diagnostics.Process.Start(psi);
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
            var dialog = new CleanConfirmationWindow(scanResult.FileCount, scanResult.TotalSizeMB);
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

        private List<string> GetCleanPaths()
        {
            return new List<string>
            {
                System.IO.Path.GetTempPath(),
                @"C:\Windows\Temp",
                @"C:\Windows\Prefetch",
                Environment.GetFolderPath(Environment.SpecialFolder.Recent),
                @"C:\Windows\SoftwareDistribution\Download", // Windows Update
                @"C:\ProgramData\Microsoft\Windows\WER"      // Error Reporting
            };
        }

        private (int FileCount, double TotalSizeMB) ScanJunkFiles()
        {
            int count = 0;
            long totalBytes = 0;

            foreach (var path in GetCleanPaths())
            {
                if (!Directory.Exists(path)) continue;

                try
                {
                    // Recursive scan
                    var files = Directory.GetFiles(path, "*.*", SearchOption.AllDirectories);
                    foreach (var f in files)
                    {
                        try
                        {
                            count++;
                            totalBytes += new FileInfo(f).Length;
                        }
                        catch { }
                    }
                }
                catch { }
            }

            return (count, totalBytes / (1024.0 * 1024.0));
        }

        private double CleanJunkFiles()
        {
            long deletedBytes = 0;
            string recentPath = Environment.GetFolderPath(Environment.SpecialFolder.Recent);
            
            // A. Stop Services for deep clean
            StopUpdateServices();

            // B. Clean
            foreach (var path in GetCleanPaths())
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
                            var fi = new FileInfo(file);
                            long size = fi.Length;
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
                            try
                            {
                                Directory.Delete(dir, true);
                            }
                            catch { }
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
                System.Diagnostics.Process.Start(psi)?.WaitForExit(3000); // Wait max 3s
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
        private void UtilityButton_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (sender is System.Windows.Controls.Button btn && btn.Tag is string description)
            {
                TxtToolDescription.Text = description;
                TxtToolDescription.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 173, 239)); // Cyan
            }
        }

        private void UtilityButton_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            TxtToolDescription.Text = "Select a tool to launch.";
            TxtToolDescription.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(136, 136, 136)); // #888
        }

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
                this.DragMove();
            }
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
