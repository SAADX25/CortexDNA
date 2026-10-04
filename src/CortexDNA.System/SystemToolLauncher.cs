using System.Diagnostics;
using System.IO;

namespace CortexDNA.Core;

internal static class SystemToolLauncher
{
    internal static ProcessStartInfo CreateStartInfo(string tool)
    {
        string system = Environment.SystemDirectory;
        string windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        var info = new ProcessStartInfo { UseShellExecute = true, WorkingDirectory = system };
        switch (tool.ToLowerInvariant())
        {
            case "regedit.exe":
                info.FileName = Path.Combine(windows, "regedit.exe");
                info.Verb = "runas";
                break;
            case "devmgmt.msc":
            case "services.msc":
            case "eventvwr.msc":
                info.FileName = Path.Combine(system, "mmc.exe");
                info.Arguments = $"\"{Path.Combine(system, tool)}\"";
                info.Verb = "runas";
                break;
            case "ncpa.cpl":
                info.FileName = Path.Combine(system, "control.exe");
                info.Arguments = $"\"{Path.Combine(system, "ncpa.cpl")}\"";
                info.Verb = "runas";
                break;
            case "powershell.exe":
                info.FileName = Path.Combine(system, @"WindowsPowerShell\v1.0\powershell.exe");
                info.Verb = "runas";
                break;
            case "cmd.exe":
            case "taskmgr.exe":
            case "resmon.exe":
                info.FileName = Path.Combine(system, tool);
                info.Verb = "runas";
                break;
            case "msinfo32.exe":
            case "control.exe":
                info.FileName = Path.Combine(system, tool);
                break;
            default:
                throw new ArgumentException("Unknown system tool.", nameof(tool));
        }
        return info;
    }
}
