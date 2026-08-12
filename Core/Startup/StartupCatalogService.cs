using System.Diagnostics;
using System.IO;
using Microsoft.Win32;
using CortexDNA.Models;

namespace CortexDNA.Core.Startup
{
    /// <summary>
    /// Enumerates Windows startup entries from Run keys and Startup folders
    /// (the same places Task Manager's Startup tab uses).
    /// </summary>
    public sealed class StartupCatalogService
    {
        public IReadOnlyList<StartupItem> Enumerate()
        {
            var items = new Dictionary<string, StartupItem>(StringComparer.OrdinalIgnoreCase);

            AddRunKey(items, RegistryHive.CurrentUser, StartupPaths.UserRun, StartupLocationKind.CurrentUserRun);
            AddRunKey(items, RegistryHive.LocalMachine, StartupPaths.MachineRun, StartupLocationKind.LocalMachineRun);
            AddRunKey(items, RegistryHive.LocalMachine, StartupPaths.MachineRun32, StartupLocationKind.LocalMachineRun32);
            AddRunKey(items, RegistryHive.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Policies\Explorer\Run", StartupLocationKind.CurrentUserRun);
            AddRunKey(items, RegistryHive.LocalMachine, @"Software\Microsoft\Windows\CurrentVersion\Policies\Explorer\Run", StartupLocationKind.LocalMachineRun);
            AddStartupFolder(items, StartupPaths.UserStartupFolder, StartupLocationKind.UserStartupFolder);
            AddStartupFolder(items, StartupPaths.CommonStartupFolder, StartupLocationKind.CommonStartupFolder);
            StartupPackagedCatalog.AddTo(items);

            return items.Values
                .OrderByDescending(i => i.Impact)
                .ThenBy(i => i.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static void AddRunKey(
            Dictionary<string, StartupItem> items,
            RegistryHive hive,
            string subKey,
            StartupLocationKind location)
        {
            try
            {
                using var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Registry64);
                using var key = baseKey.OpenSubKey(subKey);
                if (key == null) return;

                foreach (string name in key.GetValueNames())
                {
                    if (string.IsNullOrWhiteSpace(name)) continue;

                    string command = key.GetValue(name)?.ToString() ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(command)) continue;

                    AddItem(items, name, command, location, name, iconFallback: null);
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"Startup catalog Run key failed ({location}): {ex.Message}");
            }
        }

        private static void AddStartupFolder(
            Dictionary<string, StartupItem> items,
            string folder,
            StartupLocationKind location)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
                    return;

                foreach (string file in Directory.EnumerateFiles(folder, "*.lnk"))
                {
                    string name = Path.GetFileNameWithoutExtension(file);
                    string command = ResolveShortcut(file) ?? file;
                    AddItem(items, name, command, location, Path.GetFileName(file), iconFallback: file);
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"Startup catalog folder failed ({location}): {ex.Message}");
            }
        }

        private static void AddItem(
            Dictionary<string, StartupItem> items,
            string name,
            string command,
            StartupLocationKind location,
            string approvalName,
            string? iconFallback)
        {
            string id = StartupPaths.MakeId(location, approvalName);
            if (items.ContainsKey(id))
                return;

            string? exe = StartupPaths.ExtractExecutable(command);
            string iconPath = ResolveIconPath(exe, iconFallback);
            items[id] = new StartupItem
            {
                Id = id,
                Name = name,
                Command = command,
                ExecutablePath = exe ?? string.Empty,
                IconPath = iconPath,
                Location = location,
                LocationLabel = StartupPaths.LocationLabel(location),
                ApprovalValueName = approvalName,
                Publisher = ReadPublisher(exe),
                CanModify = true
            };
        }

        private static string ResolveIconPath(string? exe, string? fallback)
        {
            if (!string.IsNullOrWhiteSpace(exe) && File.Exists(exe))
                return exe;
            if (!string.IsNullOrWhiteSpace(fallback) && File.Exists(fallback))
                return fallback;
            return exe ?? string.Empty;
        }

        private static string ResolveShortcut(string shortcutPath)
        {
            try
            {
                Type? type = Type.GetTypeFromProgID("WScript.Shell");
                if (type == null) return shortcutPath;

                dynamic shell = Activator.CreateInstance(type)!;
                try
                {
                    dynamic link = shell.CreateShortcut(shortcutPath);
                    string target = (link.TargetPath as string) ?? string.Empty;
                    string args = (link.Arguments as string) ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(target))
                        return shortcutPath;
                    return string.IsNullOrWhiteSpace(args) ? $"\"{target}\"" : $"\"{target}\" {args}";
                }
                finally
                {
                    StartupCom.Release(shell);
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"Shortcut resolve failed: {ex.Message}");
                return shortcutPath;
            }
        }

        private static string ReadPublisher(string? exe)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(exe) || !File.Exists(exe))
                    return string.Empty;

                var info = FileVersionInfo.GetVersionInfo(exe);
                return info.CompanyName?.Trim() ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }
    }
}
