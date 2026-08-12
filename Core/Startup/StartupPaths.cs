using System.IO;
using Microsoft.Win32;
using CortexDNA.Models;

namespace CortexDNA.Core.Startup
{
    internal static class StartupPaths
    {
        public const string UserRun = @"Software\Microsoft\Windows\CurrentVersion\Run";
        public const string MachineRun = @"Software\Microsoft\Windows\CurrentVersion\Run";
        public const string MachineRun32 = @"Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Run";
        public const string ApprovedRun = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";
        public const string ApprovedRun32 = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run32";
        public const string ApprovedFolder = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\StartupFolder";
        public const string DelayTaskFolder = @"\CortexDNA\StartupDelay";
        public const int DelaySeconds = 30;

        public static string UserStartupFolder =>
            Environment.GetFolderPath(Environment.SpecialFolder.Startup);

        public static string CommonStartupFolder =>
            Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup);

        public static string ApprovalSubKey(StartupLocationKind location) => location switch
        {
            StartupLocationKind.CurrentUserRun => ApprovedRun,
            StartupLocationKind.LocalMachineRun => ApprovedRun,
            StartupLocationKind.LocalMachineRun32 => ApprovedRun32,
            _ => ApprovedFolder
        };

        public static RegistryHive Hive(StartupLocationKind location) => location switch
        {
            StartupLocationKind.CurrentUserRun => RegistryHive.CurrentUser,
            StartupLocationKind.UserStartupFolder => RegistryHive.CurrentUser,
            StartupLocationKind.PackagedApp => RegistryHive.CurrentUser,
            _ => RegistryHive.LocalMachine
        };

        public static string MakeId(StartupLocationKind location, string name) =>
            $"{location}:{name}".ToLowerInvariant();

        public static string DelayTaskName(string id)
        {
            string hash = Math.Abs(id.GetHashCode(StringComparison.OrdinalIgnoreCase)).ToString("X8");
            string safe = new string(id.Where(char.IsLetterOrDigit).Take(24).ToArray());
            if (string.IsNullOrEmpty(safe)) safe = "Item";
            return $"Delay_{safe}_{hash}";
        }

        public static string? ExtractExecutable(string command)
        {
            if (string.IsNullOrWhiteSpace(command))
                return null;

            command = command.Trim();
            if (command.StartsWith('"'))
            {
                int end = command.IndexOf('"', 1);
                if (end > 1)
                    return command[1..end];
            }

            int exe = command.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
            if (exe >= 0)
                return command[..(exe + 4)].Trim('"');

            string first = command.Split(' ', 2)[0].Trim('"');
            return string.IsNullOrWhiteSpace(first) ? null : first;
        }

        public static (string Path, string Arguments) SplitCommand(string command)
        {
            command = (command ?? string.Empty).Trim();
            if (command.StartsWith('"'))
            {
                int end = command.IndexOf('"', 1);
                if (end > 1)
                {
                    string path = command[1..end];
                    string args = command[(end + 1)..].Trim();
                    return (path, args);
                }
            }

            int space = command.IndexOf(' ');
            if (space < 0)
                return (command.Trim('"'), string.Empty);

            return (command[..space].Trim('"'), command[(space + 1)..].Trim());
        }

        public static string LocationLabel(StartupLocationKind location) => location switch
        {
            StartupLocationKind.CurrentUserRun => "Current user",
            StartupLocationKind.LocalMachineRun => "All users",
            StartupLocationKind.LocalMachineRun32 => "All users (32-bit)",
            StartupLocationKind.UserStartupFolder => "Startup folder",
            StartupLocationKind.CommonStartupFolder => "Startup folder (all users)",
            StartupLocationKind.PackagedApp => "Microsoft Store",
            _ => "Startup"
        };
    }
}
