<div align="center">

# Cortex DNA

**A modern Windows desktop utility for real-time hardware monitoring, system cleanup, and privacy management.**

Built with C# and WPF · Runs locally on Windows · Open Source

[![C#](https://img.shields.io/badge/C%23-.NET%2010-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![WPF](https://img.shields.io/badge/WPF-Desktop_App-0078D4?logo=windows&logoColor=white)](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/)
[![Platform](https://img.shields.io/badge/Platform-Windows_x64-0078D4?logo=windows&logoColor=white)](https://github.com/SAADX25/CortexDNA/releases)
[![GitHub Release](https://img.shields.io/github/v/release/SAADX25/CortexDNA?label=Latest%20Release&color=28a745)](https://github.com/SAADX25/CortexDNA/releases)
[![Last Commit](https://img.shields.io/github/last-commit/SAADX25/CortexDNA?color=blue)](https://github.com/SAADX25/CortexDNA/commits)

</div>

---

## What is Cortex DNA?

Cortex DNA is a Windows desktop application that gives you a clean, unified dashboard for monitoring your PC hardware and managing common system maintenance tasks — all from a single window.

Instead of opening Task Manager, System Information, Device Manager, and various Settings panels separately, Cortex DNA brings the most useful information into one modern interface.

**Who is it for?**
- Windows users who want a quick overview of their system health
- Users looking for a simple way to clean temporary files
- Anyone who wants quick access to common Windows admin tools
- Enthusiasts who want to monitor CPU, GPU, RAM, network, and storage at a glance

## Features

All features listed below are verified from the actual source code:

### 🖥️ Hardware Monitoring Dashboard
- **CPU Monitoring** — Real-time CPU load percentage and current clock speed (GHz), powered by [LibreHardwareMonitor](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor) and Windows Performance Counters
- **GPU Monitoring** — GPU temperature and core load for NVIDIA, AMD, and Intel GPUs
- **Memory (RAM)** — Live usage bar showing used/total GB with percentage, plus RAM speed and capacity info
- **Network Activity** — Real-time download and upload speed (KB/s or MB/s)
- **Storage Drives** — All connected drives with usage bars, free space, and total capacity
- **BIOS & Motherboard** — Motherboard model, BIOS version, and BIOS date with copy-to-clipboard buttons

### 🧹 System Cleanup
- **RAM Boost** — Frees unused working set memory across processes using Windows API (`EmptyWorkingSet`), then reports how much RAM was freed
- **Disk Cleanup** — Scans and cleans temporary files from:
  - User Temp folder
  - Windows System Temp folder
  - Windows Prefetch
  - Recent Items
  - Windows Update Cache
  - Error Reporting Logs
- **Cleanup Confirmation Window** — Shows a breakdown of what will be cleaned and how much space each location uses before you confirm
- **Cleanup Results Window** — Displays total space freed and number of files removed after cleanup

### 🔒 Windows Privacy Settings
- Toggle **Diagnostic Data** collection (Windows telemetry) on or off
- Toggle **Settings Suggestions** (Start menu app suggestions) on or off
- Toggle **Start Menu Web Search** (Bing search results in Start) on or off
- Each toggle reads and writes the actual Windows Registry values
- Quick-access buttons to open the relevant registry keys in Registry Editor for manual verification

### 🛠️ Quick-Access Utility Sidebar
One-click launchers for common Windows tools:
- System Info (`msinfo32`)
- Task Manager
- Device Manager
- Registry Editor
- Windows Services
- Network Connections
- Command Prompt
- PowerShell
- Event Viewer
- Control Panel
- Resource Monitor

### 🎮 Automatic Game Mode
- Detects running games (CS2, Valorant, Apex Legends, Fortnite, GTA V, Overwatch, and more)
- Automatically throttles sensor polling from 1-second to 10-second intervals
- Lowers Cortex DNA's own process priority to `BelowNormal`
- Resumes normal monitoring when the game exits

### 🎨 Theming & Customization
- **Dark Theme** — Default dark mode with blue accent colors
- **Light Theme** — Soft slate theme with warm blue-gray tones
- Theme preference is saved to `%AppData%\CortexDNA\theme_settings.json`
- **Background Opacity Slider** — Adjust window transparency from the settings menu

### 📌 System Tray Integration
- Minimizing the app hides it to the system tray (pauses monitoring to reduce resource usage)
- Double-click the tray icon to restore
- Right-click tray menu with Open, Manage/Uninstall, and Exit options
- Single-instance enforcement — launching a second instance brings the existing window to focus

### 📋 Copy System Specs
- Copy individual specs (CPU name, GPU name, motherboard model, BIOS version/date) with one click
- **Copy All Specs** — Copies a formatted summary of your device name, processor, RAM, GPU, motherboard, BIOS, and OS edition to clipboard

## Screenshots

> **Screenshots are not yet included in the repository.**
>
> To improve the repository presentation, add screenshots to an `assets/screenshots/` folder and update the paths below:

```
assets/screenshots/dashboard.png          — Main dashboard with CPU, Memory, and Network cards
assets/screenshots/cleanup-confirmation.png — Disk cleanup scan results dialog
assets/screenshots/cleanup-results.png    — Cleanup completion summary
assets/screenshots/privacy-settings.png   — Windows privacy toggles section
assets/screenshots/about.png              — About window
```


## Installation

### Download the Installer

1. Go to the [**Releases**](https://github.com/SAADX25/CortexDNA/releases) page
2. Download `CortexDNA_Installer_v1.5.0.exe` (or the latest version)
3. Run the installer — it will guide you through setup
4. Launch Cortex DNA from the Start Menu or Desktop shortcut

### Requirements

- **OS:** Windows 10 or later (64-bit)
- **Runtime:** [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0) (framework-dependent build — the installer does not bundle the runtime)
- **Recommended:** Run as Administrator for full hardware sensor access and privacy settings changes

## Build from Source

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- Windows 10 or later (x64)

### Build Steps

```bash
git clone https://github.com/SAADX25/CortexDNA.git
cd CortexDNA

dotnet restore
dotnet build
dotnet run
```

### Publish a Release Build

```bash
dotnet publish -c Release -r win-x64 --self-contained false /p:PublishReadyToRun=true
```

The published output will be in `bin/Release/net10.0-windows/win-x64/publish/`.

### Build the Installer

The repository includes a `Build_Release.bat` script that automates publishing and compiling the Inno Setup installer. Requires [Inno Setup 6](https://jrsoftware.org/isinfo.php) to be installed.

## Usage

1. **Launch Cortex DNA** from the Start Menu, Desktop shortcut, or by running the executable
2. **View the Dashboard** — The main window shows real-time CPU, Memory, Network, GPU, BIOS, and Storage cards
3. **Use the Sidebar** — Click any utility button (e.g., Task Manager, Device Manager) to launch it directly
4. **Boost RAM** — Click the **BOOST** button in the Memory card to free unused working set memory
5. **Clean Disk** — Click **CLEAN DISK** to scan for temporary files, review the confirmation dialog, then clean
6. **Manage Privacy** — Scroll down to the Windows Privacy Settings section and use the toggles
7. **Switch Themes** — Click the theme toggle button (sun icon) in the title bar
8. **Minimize to Tray** — Minimize the window to hide it to the system tray; double-click the tray icon to restore

## Project Structure

```
CortexDNA/
├── Controls/
│   ├── HardwareDashboardControl.xaml(.cs)  — Main hardware monitoring dashboard UI
│   └── PrivacySettingsControl.xaml(.cs)    — Windows privacy toggles UI
├── Converters/
│   └── SemanticColorConverters.cs          — Usage/temperature → color converters
├── Core/
│   ├── Logger.cs                           — File-based logging
│   ├── NativeMethods.cs                    — P/Invoke declarations (GlobalMemoryStatusEx)
│   ├── ObservableObject.cs                 — INotifyPropertyChanged base class
│   ├── RamOptimizer.cs                     — Working set memory optimization via Windows API
│   ├── RelayCommand.cs                     — ICommand implementation for MVVM
│   └── UpdateVisitor.cs                    — LibreHardwareMonitor visitor
├── Models/
│   └── HardwareModels.cs                   — Data models (SystemInfo, Sensors, Storage, etc.)
├── Themes/
│   ├── DarkTheme.xaml                      — Dark color scheme
│   └── LightTheme.xaml                     — Light color scheme
├── ViewModels/
│   ├── HardwareViewModel.cs                — Hardware monitoring, cleanup, and boost logic
│   ├── MainViewModel.cs                    — Root ViewModel
│   ├── PrivacyViewModel.cs                 — Registry-based privacy toggle logic
│   └── ViewModelBase.cs                    — Base ViewModel class
├── App.xaml(.cs)                           — App entry, single-instance logic, exception handling
├── MainWindow.xaml(.cs)                    — Main window, sidebar, theming, tray integration
├── AboutWindow.xaml(.cs)                   — About dialog with version info
├── CleanConfirmationWindow.xaml(.cs)       — Pre-cleanup confirmation dialog
├── CleanupResultsWindow.xaml(.cs)          — Post-cleanup results dialog
├── CortexDNA.csproj                        — Project configuration (.NET 10, WPF)
├── CortexDNA_Installer.iss                 — Inno Setup installer script
├── Build_Release.bat                       — Automated build + installer script
└── app.ico                                 — Application icon
```

## Tech Stack

| Technology | Purpose |
|---|---|
| **C# / .NET 10** | Application language and runtime |
| **WPF (Windows Presentation Foundation)** | Desktop UI framework |
| **XAML** | Declarative UI layout and styling |
| **LibreHardwareMonitor** (`0.9.5`) | CPU/GPU/motherboard sensor data |
| **System.Management** (`10.0.2`) | WMI queries for system information |
| **Windows API (P/Invoke)** | Memory status, working set optimization |
| **Windows Performance Counters** | CPU performance percentage |
| **Inno Setup** | Windows installer packaging |

## Roadmap

Future improvements under consideration:

- [ ] Add screenshots and a GIF demo to the README
- [ ] More detailed cleanup reports with per-category breakdowns
- [ ] Configurable cleanup paths and options
- [ ] Startup with Windows option (currently registry key is prepared but not user-configurable)
- [ ] Expanded privacy controls (more registry-based toggles)
- [ ] System notification alerts for high CPU/GPU temperature
- [ ] Export system specs to a file
- [ ] Portable version (no installer required)
- [ ] Improved .gitignore and cleaner repository structure
- [ ] Unit and integration tests
- [ ] Changelog / version history documentation

## Contributing

Contributions are welcome.

1. Fork this repository
2. Create a new branch (`git checkout -b feature/your-feature`)
3. Make your changes
4. Test the application locally
5. Submit a pull request with a clear description of your changes

## License

This project does not currently include a LICENSE file. If you plan to use, modify, or distribute this code, please contact the author. Adding an [MIT License](https://choosealicense.com/licenses/mit/) is recommended for open-source-friendly distribution.

## Disclaimer

Cortex DNA is designed to help users review and manage selected local Windows data. It interacts with Windows system files, registry settings, and temporary file locations.

- **Always review cleanup actions before confirming.** The disk cleanup feature permanently deletes files from temp folders, prefetch, and update caches.
- **Privacy toggles modify Windows Registry values.** Changes to diagnostic data and telemetry settings may require administrator privileges and could affect Windows Update behavior.
- **The RAM boost feature** uses `EmptyWorkingSet` to trim process memory — this is a standard Windows API call and does not terminate processes.
- Cortex DNA runs entirely locally on your machine. It does not collect, transmit, or store any data externally.

## Support

If you find Cortex DNA useful, consider giving the repository a ⭐ — it helps others discover the project.

---
