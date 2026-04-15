using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using System.Management;
using System.Security.Principal;
using System.Diagnostics;
using System.Net.NetworkInformation;
using CortexDNA.Models;
using LibreHardwareMonitor.Hardware;
using System.Windows.Input;
using System.Text.Json;
using CortexDNA.Core;
using Microsoft.Win32;
using System.Runtime.InteropServices;

namespace CortexDNA.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        private readonly Computer _computer;
        private DispatcherTimer _timer;
        private PerformanceCounter? _cpuPerfCounter;
        private double _baseClockGHz = 3.2; // Default for i7-8700
        private long _prevBytesReceived = 0;
        private long _prevBytesSent = 0;
        private double _totalRamBytes = 0;
        private bool _isPaused = false; // For Tray/Minimize Logic

        public ICommand CopyAllSpecsCommand { get; }
        public ICommand CopyMotherboardCommand { get; }
        public ICommand CopyBiosVersionCommand { get; }
        public ICommand CopyBiosDateCommand { get; }
        public ICommand BoostSystemCommand { get; }

        public AppSystemInfo SystemInfo { get; set; } = new AppSystemInfo();

        private bool _isBoosting = false;
        public bool IsBoosting
        {
            get => _isBoosting;
            set
            {
                if (SetProperty(ref _isBoosting, value))
                {
                    BoostButtonText = value ? "Cleaning..." : "BOOST";
                }
            }
        }

        private string _boostButtonText = "BOOST";
        public string BoostButtonText
        {
            get => _boostButtonText;
            set => SetProperty(ref _boostButtonText, value);
        }

        private string _boostButtonColor = "#00ADEF";
        public string BoostButtonColor
        {
            get => _boostButtonColor;
            set => SetProperty(ref _boostButtonColor, value);
        }

        private bool _isBoostEnabled = true;
        public bool IsBoostEnabled
        {
            get => _isBoostEnabled;
            set => SetProperty(ref _isBoostEnabled, value);
        }

        private bool _runOnStartup;
        public bool RunOnStartup
        {
            get => _runOnStartup;
            set
            {
                if (SetProperty(ref _runOnStartup, value))
                {
                    SetStartupRegistry(value);
                }
            }
        }
        
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
        private bool _isGameModeActive = false;
        
        private readonly string _specsCachePath;

        // List of processes that trigger Game Mode
        private readonly string[] _gameProcesses = new[] 
        { 
            "cs2", 
            "valorant-win64-shipping", 
            "vgc",
            "r5apex", 
            "fortnite-win64-shipping",
            "cod", 
            "gta5",
            "overwatch"
        };

        public MainViewModel()
        {
            _specsCachePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CortexDNA", "specs.json");
            _isAdmin = IsRunningAsAdmin();

            // Load Startup State
            _runOnStartup = CheckStartupRegistry();

            CopyAllSpecsCommand = new RelayCommand(CopyAllSpecs);
            CopyMotherboardCommand = new RelayCommand(() => CopyToClipboard(SystemInfo.MotherboardModel, "Motherboard Model"));
            CopyBiosVersionCommand = new RelayCommand(() => CopyToClipboard(SystemInfo.BiosVersion, "BIOS Version"));
            CopyBiosDateCommand = new RelayCommand(() => CopyToClipboard(SystemInfo.BiosDate, "BIOS Date"));
            BoostSystemCommand = new RelayCommand(BoostSystem);

            // 1. Immediate Cache Load (Fast) - Step 1
            LoadCachedSpecs();

            _computer = new Computer
            {
                IsCpuEnabled = true,
                IsGpuEnabled = true,
                IsMemoryEnabled = true,
                IsMotherboardEnabled = true,
                IsControllerEnabled = true,
                IsNetworkEnabled = false,
                IsStorageEnabled = true
            };

            // 2. Background Refresh (Slow, but ensures data is fresh) - Step 2 & 3
            // We move hardware initialization here to avoid blocking UI startup
            Task.Run(InitializeAndRefresh);

            // Setup Timer (Start it, but it might just be idle until hardware is ready)
            _timer = new DispatcherTimer(DispatcherPriority.Background);
            _timer.Interval = TimeSpan.FromSeconds(1);
            _timer.Tick += (s, e) => RefreshData();
            _timer.Start();
        }

        private void CopyToClipboard(string text, string label)
        {
            if (!string.IsNullOrEmpty(text) && text != "Detecting...")
            {
                try
                {
                    System.Windows.Clipboard.SetText(text);
                    StatusMessage = $"{label} copied!";
                    Task.Delay(2000).ContinueWith(_ => StatusMessage = "Monitoring Active");
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
                Task.Delay(2000).ContinueWith(_ => StatusMessage = "Monitoring Active");
            }
            catch { }
        }

        public void PauseMonitoring()
        {
            if (_isPaused) return;
            _isPaused = true;
            _timer?.Stop();
            // Optional: Close hardware handles if needed, but keeping them open is faster for resume
            Logger.Log("Monitoring Paused (Tray/Minimized)");
        }

        public void ResumeMonitoring()
        {
            if (!_isPaused) return;
            _isPaused = false;
            _timer?.Start();
            Logger.Log("Monitoring Resumed");
            
            // Force immediate update
            RefreshData(); 
        }

        private bool CheckStartupRegistry()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", false))
                {
                    return key?.GetValue("CortexDNA") != null;
                }
            }
            catch { return false; }
        }

        private void SetStartupRegistry(bool enable)
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true))
                {
                    if (enable)
                    {
                        // Use Process.GetCurrentProcess().MainModule.FileName for self-contained exe
                        string path = Process.GetCurrentProcess().MainModule.FileName;
                        key.SetValue("CortexDNA", $"\"{path}\"");
                        Logger.Log($"Startup Enabled: {path}");
                    }
                    else
                    {
                        key.DeleteValue("CortexDNA", false);
                        Logger.Log("Startup Disabled");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"Startup Registry Error: {ex.Message}");
                // Ideally show a message to user if UAC blocks it, but this runs silently in VM.
            }
        }

        private async Task InitializeAndRefresh()
        {
            // 3. Delay the Background Scan Slightly (Stabilize WMI/Services)
            await Task.Delay(1500);

             try
            {
                _computer.Open();
                System.Windows.Application.Current.Dispatcher.Invoke(() => StatusMessage = "Monitoring Active");
            }
            catch (Exception ex)
            {
                 System.Windows.Application.Current.Dispatcher.Invoke(() => StatusMessage = $"Error: {ex.Message} (Run as Admin?)");
            }

            // Initialize Performance Counters
            InitializeCounters();

            // Fetch WMI Info and Save Cache
            RefreshSystemInfo();
            
            // Initial Scan
            RefreshData();
        }

        private void InitializeCounters()
        {
            try
            {
                // CPU Speed Base Clock
                using (var searcher = new ManagementObjectSearcher("SELECT MaxClockSpeed FROM Win32_Processor"))
                {
                    foreach (var obj in searcher.Get())
                    {
                        if (obj["MaxClockSpeed"] != null)
                        {
                            _baseClockGHz = Convert.ToDouble(obj["MaxClockSpeed"]) / 1000.0;
                            break; 
                        }
                    }
                }
                
                _cpuPerfCounter = new PerformanceCounter("Processor Information", "% Processor Performance", "_Total");
                _cpuPerfCounter.NextValue(); // First call always returns 0

                // Network baseline
            }
            catch (Exception ex)
            {
                StatusMessage = $"Warning: Counters init failed ({ex.Message})";
            }
        }
        
        private void UpdateUptime()
        {
            TimeSpan uptime = TimeSpan.FromMilliseconds(Environment.TickCount64);
            SystemInfo.Uptime = $"{uptime.Days}d {uptime.Hours}h {uptime.Minutes}m";
        }

        private string FormatSpeed(long bytesPerSec)
        {
            if (bytesPerSec > 1024 * 1024)
                return $"{bytesPerSec / (1024.0 * 1024):F1} MB/s";
            else
                return $"{bytesPerSec / 1024.0:F1} KB/s";
        }

        private void UpdateOrAddSensor(HardwareItem item, string name, string type, string value)
        {
             var existing = item.Sensors.FirstOrDefault(s => s.Name == name && s.Type == type);
             if (existing == null)
             {
                 item.Sensors.Add(new SensorInfo { Name = name, Type = type, Value = value });
             }
             else
             {
                 existing.Value = value;
             }
        }
        
        private bool IsRunningAsAdmin()
        {
            using (var identity = WindowsIdentity.GetCurrent())
            {
                var principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
        }

        private void LoadCachedSpecs()
        {
            try
            {
                if (File.Exists(_specsCachePath))
                {
                    string json = File.ReadAllText(_specsCachePath);
                    var specs = JsonSerializer.Deserialize<SystemSpecs>(json);
                    
                    // 2. Add a Null Check
                    if (specs != null)
                    {
                        SystemInfo.OsName = specs.OsName ?? "Detecting...";
                        SystemInfo.BiosInfo = specs.BiosInfo ?? "Detecting...";
                        SystemInfo.MotherboardModel = specs.MotherboardModel ?? "Detecting...";
                        SystemInfo.BiosVersion = specs.BiosVersion ?? "Detecting...";
                        SystemInfo.BiosDate = specs.BiosDate ?? "Detecting...";
                        
                        CpuName = specs.CpuName ?? "Detecting...";
                        GpuName = specs.GpuName ?? "Detecting...";
                        SystemInfo.RamInfo = specs.RamInfo ?? "Detecting...";
                        SystemInfo.RamTotal = specs.RamTotal ?? "Detecting...";
                        SystemInfo.RamType = specs.RamType ?? "Detecting...";
                        _totalRamBytes = specs.TotalRamBytes;
                    }
                }
            }
            catch 
            { 
                // 1. Wrap the Cache Loading in Try-Catch
                // If it fails or the file is corrupted, just ignore it and proceed.
            }
        }

        private void RefreshSystemInfo()
        {
            try
            {
                // 1. OS Info
                using (var searcher = new ManagementObjectSearcher("SELECT Caption, BuildNumber FROM Win32_OperatingSystem"))
                {
                    foreach (var obj in searcher.Get())
                    {
                        SystemInfo.OsName = $"{obj["Caption"]} (Build {obj["BuildNumber"]})";
                    }
                }

                // 2. BIOS & Motherboard Info
                string manufacturer = "Unknown";
                string version = "Unknown";
                string date = "Unknown";
                string boardProduct = "Unknown";
                string boardManuf = "Unknown";

                // BIOS
                using (var searcher = new ManagementObjectSearcher("SELECT Manufacturer, SMBIOSBIOSVersion, ReleaseDate FROM Win32_BIOS"))
                {
                    foreach (var obj in searcher.Get())
                    {
                        manufacturer = obj["Manufacturer"]?.ToString() ?? "Unknown";
                        version = obj["SMBIOSBIOSVersion"]?.ToString() ?? "Unknown";
                        
                        string rawDate = obj["ReleaseDate"]?.ToString() ?? "";
                        if (rawDate.Length >= 8)
                        {
                            // WMI Date Format: yyyymmdd...
                            date = $"{rawDate.Substring(0, 4)}-{rawDate.Substring(4, 2)}-{rawDate.Substring(6, 2)}";
                        }
                    }
                }

                // Motherboard
                using (var searcher = new ManagementObjectSearcher("SELECT Product, Manufacturer FROM Win32_BaseBoard"))
                {
                    foreach (var obj in searcher.Get())
                    {
                        boardProduct = obj["Product"]?.ToString() ?? "";
                        boardManuf = obj["Manufacturer"]?.ToString() ?? "";
                    }
                }

                SystemInfo.MotherboardModel = !string.IsNullOrEmpty(boardProduct) 
                    ? $"{boardManuf} {boardProduct}" 
                    : $"{boardManuf} (Unknown Model)";
                
                SystemInfo.BiosVersion = version;
                SystemInfo.BiosDate = date;
                SystemInfo.BiosInfo = $"{manufacturer} (v{version})"; // Legacy fallback

                // CPU Info
                // Win32_Processor is standard for both Intel and AMD.
                // We take the first CPU found (multi-socket systems will just show the first one here)
                using (var searcher = new ManagementObjectSearcher("SELECT Name FROM Win32_Processor"))
                {
                    foreach (var obj in searcher.Get())
                    {
                        if (obj["Name"] != null)
                        {
                            CpuName = obj["Name"].ToString()?.Trim() ?? "Unknown CPU";
                            break; 
                        }
                    }
                }
                
                // GPU Info (Robust Multi-GPU Logic)
                // 1. Fetch ALL Video Controllers
                // 2. Filter out basic display adapters if possible
                // 3. Prioritize by VRAM size or "NVIDIA/AMD" keywords
                var gpus = new System.Collections.Generic.List<(string Name, long VRam)>();

                using (var searcher = new ManagementObjectSearcher("SELECT Name, AdapterRAM FROM Win32_VideoController"))
                {
                    foreach (var obj in searcher.Get())
                    {
                        string name = obj["Name"]?.ToString() ?? "Unknown GPU";
                        long vram = 0;
                        if (obj["AdapterRAM"] != null)
                        {
                            long.TryParse(obj["AdapterRAM"].ToString(), out vram);
                        }
                        gpus.Add((name, vram));
                    }
                }

                if (gpus.Count > 0)
                {
                    // Sort by VRAM descending, then by Name length (longer usually means more specific/dedicated)
                    // Also prioritize known dedicated brands keywords if VRAM is missing/equal
                    var bestGpu = gpus
                        .OrderByDescending(g => g.VRam)
                        .ThenByDescending(g => g.Name.Contains("NVIDIA") || g.Name.Contains("AMD") || g.Name.Contains("Radeon"))
                        .ThenByDescending(g => g.Name.Length) 
                        .First();

                    GpuName = bestGpu.Name;
                }
                else
                {
                    GpuName = "No GPU Detected";
                }

                // 3. RAM Info (Total & Speed)
                long totalCapacity = 0;
                uint speed = 0;

                // Total Memory (More accurate from ComputerSystem)
                using (var searcher = new ManagementObjectSearcher("SELECT TotalPhysicalMemory FROM Win32_ComputerSystem"))
                {
                    foreach (var obj in searcher.Get())
                    {
                        if (obj["TotalPhysicalMemory"] != null)
                            totalCapacity = Convert.ToInt64(obj["TotalPhysicalMemory"]);
                    }
                }

                // RAM Speed
                using (var searcher = new ManagementObjectSearcher("SELECT Speed FROM Win32_PhysicalMemory"))
                {
                    foreach (var obj in searcher.Get())
                    {
                        if (obj["Speed"] != null && speed == 0)
                        {
                            speed = Convert.ToUInt32(obj["Speed"]);
                            break; // Assume all sticks match or take the first one
                        }
                    }
                }
                
                _totalRamBytes = totalCapacity;
                double gb = totalCapacity / (1024.0 * 1024 * 1024);
                
                SystemInfo.RamInfo = speed > 0 
                    ? $"{gb:F1} GB @ {speed} MHz" 
                    : $"{gb:F1} GB";
                
                // Keep these for backward compatibility if needed, or legacy display
                SystemInfo.RamTotal = $"{gb:F1} GB";
                SystemInfo.RamType = speed > 0 ? $"{speed} MHz" : "Unknown";

                // Save to cache
                try
                {
                    var specs = new SystemSpecs
                    {
                        OsName = SystemInfo.OsName,
                        BiosInfo = SystemInfo.BiosInfo,
                        MotherboardModel = SystemInfo.MotherboardModel,
                        BiosVersion = SystemInfo.BiosVersion,
                        BiosDate = SystemInfo.BiosDate,
                        CpuName = CpuName,
                        GpuName = GpuName,
                        RamInfo = SystemInfo.RamInfo,
                        RamTotal = SystemInfo.RamTotal,
                        RamType = SystemInfo.RamType,
                        TotalRamBytes = _totalRamBytes
                    };

                    string dir = Path.GetDirectoryName(_specsCachePath);
                    if (!Directory.Exists(dir))
                    {
                        Directory.CreateDirectory(dir);
                    }

                    string json = JsonSerializer.Serialize(specs, new JsonSerializerOptions { WriteIndented = true });
                    File.WriteAllText(_specsCachePath, json);
                }
                catch (Exception ex)
                {
                    // Log or ignore saving error
                    System.Diagnostics.Debug.WriteLine($"Failed to save specs: {ex.Message}");
                }

            }
            catch (Exception ex)
            {
                SystemInfo.OsName = "Error fetching info";
                SystemInfo.BiosInfo = ex.Message;
            }
        }

        private bool _isUpdating = false;

        private async void RefreshData()
        {
            if (_isUpdating) return;
            _isUpdating = true;

            try
            {
                // Check for Games first
                CheckGameMode();

                // 1. Background Work: Fetch all heavy data
                var data = await Task.Run(() => 
                {
                    // If Game Mode is active, SKIP GPU polling entirely to save resources
                    if (!_isGameModeActive)
                    {
                        // Update Hardware Monitor (WMI/Driver Access)
                        _computer.Accept(new CortexDNA.Core.UpdateVisitor());
                        foreach (var hw in _computer.Hardware)
                        {
                            if (hw.HardwareType == HardwareType.Cpu) hw.Update();
                        }
                    }
                    else
                    {
                        // Minimal CPU update only if really needed, or skip everything
                        // We'll skip LibreHardwareMonitor updates entirely in Game Mode
                    }

                    // Performance Counters (Lightweight, keep running but slower)
                    float cpuPerf = 0;
                    if (_cpuPerfCounter != null) cpuPerf = _cpuPerfCounter.NextValue();

                    NativeMethods.MEMORYSTATUSEX memStatus = new NativeMethods.MEMORYSTATUSEX();
                    memStatus.dwLength = (uint)Marshal.SizeOf(typeof(NativeMethods.MEMORYSTATUSEX));
                    float ramAvailable = 0;
                    if (NativeMethods.GlobalMemoryStatusEx(ref memStatus))
                    {
                        ramAvailable = memStatus.ullAvailPhys / (1024f * 1024f); 
                        // Only set total RAM bytes if it wasn't statically loaded correctly
                        if (_totalRamBytes == 0) _totalRamBytes = memStatus.ullTotalPhys;
                    }

                    // Network & Storage Snapshots (Lightweight)
                    var netStats = GetNetworkStatsSnapshot();
                    
                    // Skip Storage in Game Mode (I/O heavy)
                    var storageStats = _isGameModeActive ? new System.Collections.Generic.List<StorageDto>() : GetStorageStatsSnapshot();

                    return new { CpuPerf = cpuPerf, RamAvailable = ramAvailable, NetStats = netStats, StorageStats = storageStats };
                });

                // 2. UI Thread Updates (Fast Property Assignments)
                if (!_isGameModeActive)
                {
                    UpdateHardwareUI(data.CpuPerf);
                }
                
                UpdateRamUI(data.RamAvailable);
                UpdateNetworkUI(data.NetStats);
                
                if (!_isGameModeActive)
                {
                    UpdateStorageUI(data.StorageStats);
                }
                
                UpdateUptime();
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error updating: {ex.Message}";
            }
            finally
            {
                _isUpdating = false;
            }
        }

        private void CheckGameMode()
        {
            bool gameFound = false;
            try
            {
                var processes = Process.GetProcesses();
                foreach (var p in processes)
                {
                    if (_gameProcesses.Contains(p.ProcessName.ToLower()))
                    {
                        gameFound = true;
                        break;
                    }
                }
            }
            catch { }

            if (gameFound && !_isGameModeActive)
            {
                // Enter Game Mode
                _isGameModeActive = true;
                _timer.Interval = TimeSpan.FromSeconds(10); // Slow down refresh
                StatusMessage = "Gaming Mode Active - Sensors Throttled";
                
                // Lower Process Priority
                try 
                { 
                    using (Process p = Process.GetCurrentProcess())
                        p.PriorityClass = ProcessPriorityClass.BelowNormal; 
                } 
                catch { }
            }
            else if (!gameFound && _isGameModeActive)
            {
                // Exit Game Mode
                _isGameModeActive = false;
                _timer.Interval = TimeSpan.FromSeconds(1); // Restore speed
                StatusMessage = "Monitoring Active";
                
                // Restore Priority
                try 
                { 
                    using (Process p = Process.GetCurrentProcess())
                        p.PriorityClass = ProcessPriorityClass.Normal; 
                } 
                catch { }
            }
        }

        // --- Background Helper Methods ---

        private (string Download, string Upload) GetNetworkStatsSnapshot()
        {
            long currentReceived = 0;
            long currentSent = 0;

            // Universal Hardware-Agnostic Logic
            // Aggregates ALL active physical/virtual network interfaces (Wi-Fi, Ethernet, 5G/LTE, VPN)
            // Uses a Denylist approach to ensure we capture any valid internet connection
            foreach (NetworkInterface ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                // Must be Up and not a Loopback (localhost) or Tunnel
                if (ni.OperationalStatus == OperationalStatus.Up &&
                    ni.NetworkInterfaceType != NetworkInterfaceType.Loopback &&
                    ni.NetworkInterfaceType != NetworkInterfaceType.Tunnel)
                {
                    var stats = ni.GetIPv4Statistics();
                    currentReceived += stats.BytesReceived;
                    currentSent += stats.BytesSent;
                }
            }

            string down = "0 KB/s";
            string up = "0 KB/s";

            // Only calculate speed if we have a previous sample to compare against
            if (_prevBytesReceived > 0)
            {
                long downBytes = currentReceived - _prevBytesReceived;
                long upBytes = currentSent - _prevBytesSent;

                // Handle overflow (long.MaxValue reset) or negative delta
                if (downBytes < 0) downBytes = 0;
                if (upBytes < 0) upBytes = 0;

                down = FormatSpeed(downBytes);
                up = FormatSpeed(upBytes);
            }

            _prevBytesReceived = currentReceived;
            _prevBytesSent = currentSent;

            return (down, up);
        }

        private System.Collections.Generic.List<StorageDto> GetStorageStatsSnapshot()
        {
            var results = new System.Collections.Generic.List<StorageDto>();
            try 
            {
                var drives = DriveInfo.GetDrives().Where(d => d.IsReady).ToList();
                foreach (var drive in drives)
                {
                    double totalSizeGb = drive.TotalSize / (1024.0 * 1024 * 1024);
                    double freeSpaceGb = drive.AvailableFreeSpace / (1024.0 * 1024 * 1024);
                    double usedSpaceGb = totalSizeGb - freeSpaceGb;
                    double usagePercent = (usedSpaceGb / totalSizeGb) * 100;
                    string color = usagePercent > 90 ? "#FF5555" : "#00ADEF";

                    results.Add(new StorageDto
                    {
                        Name = drive.Name,
                        Label = drive.VolumeLabel,
                        TotalSize = $"{totalSizeGb:F0} GB",
                        FreeSpace = $"{freeSpaceGb:F0} GB free",
                        UsagePercentage = usagePercent,
                        UsageText = $"{usagePercent:F1}%",
                        UsedColor = color
                    });
                }
            }
            catch { } // Drive access can fail
            return results;
        }

        // --- UI Helper Methods ---

        private void UpdateHardwareUI(float cpuPerfPercent)
        {
            // Update ObservableCollections from the already-updated _computer object
            var cpus = _computer.Hardware.Where(h => h.HardwareType == HardwareType.Cpu).ToList();
            UpdateHardwareCollection(cpus, CpuList, "CPU", cpuPerfPercent);

            var gpus = _computer.Hardware.Where(h => 
                h.HardwareType == HardwareType.GpuNvidia || 
                h.HardwareType == HardwareType.GpuAmd || 
                h.HardwareType == HardwareType.GpuIntel).ToList();
            UpdateHardwareCollection(gpus, GpuList, "GPU", 0);
        }

        private void UpdateRamUI(float availableMb)
        {
            if (_totalRamBytes > 0)
            {
                double totalMb = _totalRamBytes / (1024.0 * 1024);
                double usedMb = totalMb - availableMb;
                double percent = (usedMb / totalMb) * 100;

                SystemInfo.RamUsagePercent = percent;
                SystemInfo.RamUsageText = $"{usedMb/1024.0:F1} / {totalMb/1024.0:F1} GB ({percent:F0}%)";
            }
        }

        private void UpdateNetworkUI((string Download, string Upload) stats)
        {
            SystemInfo.NetworkDownload = stats.Download;
            SystemInfo.NetworkUpload = stats.Upload;
        }

        private void UpdateStorageUI(System.Collections.Generic.List<StorageDto> snapshot)
        {
            foreach (var dto in snapshot)
            {
                var existing = StorageList.FirstOrDefault(d => d.Name == dto.Name);
                if (existing == null)
                {
                    StorageList.Add(new StorageDrive
                    {
                        Name = dto.Name,
                        Label = dto.Label,
                        TotalSize = dto.TotalSize,
                        FreeSpace = dto.FreeSpace,
                        UsagePercentage = dto.UsagePercentage,
                        UsageText = dto.UsageText,
                        UsedColor = dto.UsedColor
                    });
                }
                else
                {
                    existing.FreeSpace = dto.FreeSpace;
                    existing.UsagePercentage = dto.UsagePercentage;
                    existing.UsageText = dto.UsageText;
                    existing.UsedColor = dto.UsedColor;
                }
            }
        }

        // Updated signature to accept cpuPerf
        private void UpdateHardwareCollection(System.Collections.Generic.List<IHardware> hardwareSource, ObservableCollection<HardwareItem> targetCollection, string typeLabel, float cpuPerf)
        {
            foreach (var hw in hardwareSource)
            {
                var existingItem = targetCollection.FirstOrDefault(x => x.Name == hw.Name);
                if (existingItem == null)
                {
                    existingItem = new HardwareItem { Name = hw.Name, Type = typeLabel };
                    targetCollection.Add(existingItem);
                }

                UpdateSensors(hw, existingItem, cpuPerf);
            }
            
             for (int i = targetCollection.Count - 1; i >= 0; i--)
            {
                if (!hardwareSource.Any(h => h.Name == targetCollection[i].Name))
                {
                    targetCollection.RemoveAt(i);
                }
            }
        }

        // Updated to use passed cpuPerf instead of calling NextValue()
        private void UpdateSensors(IHardware hw, HardwareItem item, float cpuPerf)
        {
            var sensors = hw.Sensors.OrderBy(s => s.Index).ToList();

            if (hw.HardwareType == HardwareType.Cpu)
            {
                // 1. CPU Load
                var loadSensor = sensors.FirstOrDefault(s => s.SensorType == SensorType.Load && s.Name == "CPU Total");
                if (loadSensor != null && loadSensor.Value.HasValue)
                {
                    UpdateOrAddSensor(item, loadSensor.Name, "Load", $"{loadSensor.Value.Value:F1} %");
                }

                // 2. CPU Speed (Calculated)
                double currentGHz = _baseClockGHz * (cpuPerf / 100.0);
                UpdateOrAddSensor(item, "CPU Speed", "Clock", $"{currentGHz:F2} GHz");
            }
            else
            {
                // GPU Handling
                foreach (var sensor in sensors)
                {
                    if (!sensor.Value.HasValue || float.IsNaN(sensor.Value.Value)) continue;

                    bool isInteresting = false;
                    if (sensor.SensorType == SensorType.Temperature) isInteresting = true;
                    if (sensor.SensorType == SensorType.Load && (sensor.Name == "GPU Core")) isInteresting = true;

                    if (isInteresting)
                    {
                        string val = "--"; // Default robust fallback
                        
                        if (sensor.Value.HasValue && !float.IsNaN(sensor.Value.Value))
                        {
                            val = sensor.SensorType == SensorType.Temperature 
                                ? $"{sensor.Value.Value:F0} °C" 
                                : $"{sensor.Value.Value:F1} %";
                        }
                        
                        UpdateOrAddSensor(item, sensor.Name, sensor.SensorType.ToString(), val);
                    }
                }
            }
            
            // Cleanup Logic...
            for (int i = item.Sensors.Count - 1; i >= 0; i--)
            {
                var s = item.Sensors[i];
                if (hw.HardwareType == HardwareType.Cpu && (s.Name == "CPU Speed" || s.Name == "CPU Total")) continue;

                if (hw.HardwareType != HardwareType.Cpu)
                {
                    // Strict Match Check
                    var sourceSensor = sensors.FirstOrDefault(src => src.Name == s.Name && src.SensorType.ToString() == s.Type);
                    
                    // If source is gone, OR if value is invalid/null, mark it as stale or remove it.
                    // Here we remove it to keep UI clean, but could also set to "--"
                    if (sourceSensor == null)
                    {
                         item.Sensors.RemoveAt(i);
                    }
                }
            }
        }

        // DTO for passing data from bg thread
        private struct StorageDto
        {
            public string Name { get; set; }
            public string Label { get; set; }
            public string TotalSize { get; set; }
            public string FreeSpace { get; set; }
            public double UsagePercentage { get; set; }
            public string UsageText { get; set; }
            public string UsedColor { get; set; }
        }

        private async void BoostSystem()
        {
            if (IsBoosting) return;
            IsBoosting = true;
            IsBoostEnabled = false;

            // 1. The 'During' State (Loading)
            BoostButtonText = "Cleaning...";
            BoostButtonColor = "#444444"; // Dark gray to indicate activity

            try
            {
                // Initial memory check
                float ramAvailableBefore = 0;
                NativeMethods.MEMORYSTATUSEX memStatusBefore = new NativeMethods.MEMORYSTATUSEX();
                memStatusBefore.dwLength = (uint)Marshal.SizeOf(typeof(NativeMethods.MEMORYSTATUSEX));
                if (NativeMethods.GlobalMemoryStatusEx(ref memStatusBefore))
                {
                    ramAvailableBefore = memStatusBefore.ullAvailPhys / (1024f * 1024f);
                }

                // 2. The 'Action' (Background Task)
                await Task.Delay(1000); // UX Tip: Add a small artificial delay
                await RamOptimizer.OptimizeMemoryAsync();

                // Final memory check
                float ramAvailableAfter = 0;
                NativeMethods.MEMORYSTATUSEX memStatusAfter = new NativeMethods.MEMORYSTATUSEX();
                memStatusAfter.dwLength = (uint)Marshal.SizeOf(typeof(NativeMethods.MEMORYSTATUSEX));
                if (NativeMethods.GlobalMemoryStatusEx(ref memStatusAfter))
                {
                    ramAvailableAfter = memStatusAfter.ullAvailPhys / (1024f * 1024f);
                }

                // Calculate difference
                float freedMb = ramAvailableAfter - ramAvailableBefore;
                if (freedMb < 0) freedMb = 0;

                // 3. The 'After' State (Success Result)
                BoostButtonText = $"Freed {freedMb:F0} MB!";
                BoostButtonColor = "#28a745"; // Success Green
                StatusMessage = $"Success! Freed {freedMb:F0} MB of RAM.";
                
                // Force UI refresh
                RefreshData();

                // 4. The Reset (Back to Normal)
                await Task.Delay(3000);
            }
            catch (Exception ex)
            {
                StatusMessage = $"Boost failed: {ex.Message}";
                BoostButtonText = "Error";
                BoostButtonColor = "#dc3545"; // Error Red
                await Task.Delay(2000);
            }
            finally
            {
                // Reset to original state
                BoostButtonText = "BOOST";
                BoostButtonColor = "#00ADEF";
                IsBoosting = false;
                IsBoostEnabled = true;
            }
        }

        public void Close()
        {
            _computer.Close();
            if (_cpuPerfCounter != null) _cpuPerfCounter.Dispose();
        }

        private class SystemSpecs
        {
            public string? OsName { get; set; }
            public string? BiosInfo { get; set; }
            public string? MotherboardModel { get; set; }
            public string? BiosVersion { get; set; }
            public string? BiosDate { get; set; }
            public string? CpuName { get; set; }
            public string? GpuName { get; set; }
            public string? RamInfo { get; set; }
            public string? RamTotal { get; set; }
            public string? RamType { get; set; }
            public double TotalRamBytes { get; set; }
        }
    }
}
