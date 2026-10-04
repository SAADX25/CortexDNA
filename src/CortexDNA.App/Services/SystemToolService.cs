using System.Diagnostics;
using System.IO;
using CortexDNA.Core;
using CortexDNA.UI;
namespace CortexDNA.Services;

public sealed record ToolItem(string Name, string Executable, string Description, string Keywords, bool RequiresElevation);
public sealed class SystemToolService(NotificationCenter notifications)
{
    public IReadOnlyList<ToolItem> Tools { get; } = [
        new("System information", "msinfo32.exe", "Windows system specifications", "msinfo diagnostics", false),
        new("Task Manager", "taskmgr.exe", "Processes and performance", "taskmgr applications", true),
        new("Resource Monitor", "resmon.exe", "CPU, memory, disk and network activity", "resmon diagnostics", true),
        new("Device Manager", "devmgmt.msc", "Installed hardware and drivers", "hardware drivers", true),
        new("Registry Editor", "regedit.exe", "Windows registry", "registry regedit", true),
        new("Services", "services.msc", "Windows service management", "windows services", true),
        new("Event Viewer", "eventvwr.msc", "Windows event logs", "events logs diagnostics", true),
        new("Control Panel", "control.exe", "Classic Windows settings", "settings control", false),
        new("Network connections", "ncpa.cpl", "Network adapters and connections", "network adapter", true),
        new("Command Prompt", "cmd.exe", "Windows command shell", "cmd terminal shell", true),
        new("PowerShell", "powershell.exe", "Windows PowerShell", "powershell pwsh terminal shell", true) ];
    private RelayCommand<ToolItem>? _launchCommand;
    public RelayCommand<ToolItem> LaunchCommand => _launchCommand ??= new(Launch);
    private void Launch(ToolItem? tool)
    {
        if (tool == null || !Tools.Contains(tool)) return;
        try { using var process = Process.Start(SystemToolLauncher.CreateStartInfo(tool.Executable)); }
        catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 1223) { }
        catch (Exception ex) { Logger.Log(ex); notifications.Publish("Windows could not open this tool. See the local log for details.", UiState.Error); }
    }
    public void OpenLogs()
    {
        string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CortexDNA", "Logs");
        if (!Directory.Exists(folder)) { notifications.Publish("No log folder has been created yet.", UiState.Idle); return; }
        try { using var process = Process.Start(new ProcessStartInfo(folder) { UseShellExecute = true }); }
        catch (Exception ex) { Logger.Log(ex); notifications.Publish("The log folder could not be opened.", UiState.Error); }
    }
}
