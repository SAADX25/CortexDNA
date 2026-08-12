using System.IO;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.Win32;
using CortexDNA.Models;

namespace CortexDNA.Core.Startup
{
    /// <summary>
    /// Store / packaged apps that Task Manager lists via windows.startupTask
    /// (Copilot, Xbox, Terminal, Telegram, WhatsApp, …).
    /// </summary>
    internal static class StartupPackagedCatalog
    {
        private const string SystemAppData =
            @"Software\Classes\Local Settings\Software\Microsoft\Windows\CurrentVersion\AppModel\SystemAppData";

        private static readonly Dictionary<string, string> KnownNames = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Microsoft.Copilot"] = "Microsoft 365 Copilot",
            ["Microsoft.MicrosoftOfficeHub"] = "Microsoft 365 Copilot",
            ["Microsoft.XboxApp"] = "Xbox",
            ["Microsoft.XboxGamingOverlay"] = "Xbox",
            ["Microsoft.GamingApp"] = "Xbox",
            ["Microsoft.WindowsTerminal"] = "Terminal",
            ["Microsoft.WindowsTerminalPreview"] = "Terminal",
            ["microsoft.windowscommunicationsapps"] = "Calendar",
            ["Microsoft.People"] = "People",
            ["Microsoft.Windows.Photos"] = "Photos",
            ["Microsoft.Todos"] = "Microsoft To Do",
            ["Microsoft.OutlookForWindows"] = "Outlook",
            ["MSTeams"] = "Microsoft Teams",
            ["MicrosoftTeams"] = "Microsoft Teams",
            ["5319275A.WhatsAppDesktop"] = "WhatsApp",
            ["TelegramMessengerLLP.TelegramDesktop"] = "Telegram Desktop",
            ["AppUp.IntelGraphicsExperience"] = "Intel Graphics Command Center",
            ["Microsoft.WindowsNotepad"] = "Notepad",
            ["Microsoft.Paint"] = "Paint",
            ["Microsoft.ScreenSketch"] = "Snipping Tool",
            ["Microsoft.WindowsCalculator"] = "Calculator",
            ["Microsoft.WindowsStore"] = "Microsoft Store",
            ["Microsoft.Windows.DevHome"] = "Dev Home",
            ["Microsoft.YourPhone"] = "Phone Link",
            ["MicrosoftWindows.CrossDevice"] = "Mobile devices",
            ["Microsoft.Windows.CrossDevice"] = "Mobile devices",
            ["MicrosoftWindows.Client.CBS"] = "Microsoft 365 Copilot"
        };

        public static void AddTo(Dictionary<string, StartupItem> items)
        {
            var states = ReadStates();
            AddFromRegistry(items, states);
            AddFromManifests(items, states);
        }

        public static void ApplyState(IEnumerable<StartupItem> items)
        {
            var states = ReadStates();
            foreach (var item in items)
            {
                if (item.Location != StartupLocationKind.PackagedApp)
                    continue;

                if (TryMatchState(states, item, out var state))
                {
                    item.IsEnabled = state.Enabled;
                    item.CanModify = state.CanModify;
                    if (!string.IsNullOrWhiteSpace(state.RegistryPath))
                        item.StateRegistryPath = state.RegistryPath;
                }
                else
                {
                    item.IsEnabled = false;
                }

                bool? approved = StartupApprovalService.ReadApprovedEnabled(
                    item.Name,
                    item.ApprovalValueName,
                    item.PackageFamilyName);
                if (approved.HasValue)
                    item.IsEnabled = approved.Value;
            }
        }

        public static void SetEnabled(StartupItem item, bool enabled)
        {
            if (item.Location != StartupLocationKind.PackagedApp)
                return;
            if (!item.CanModify)
                throw new InvalidOperationException("This startup item cannot be changed.");
            if (string.IsNullOrWhiteSpace(item.PackageFamilyName))
                throw new InvalidOperationException("Could not find this Store app startup task.");

            var paths = ResolveWritePaths(item);
            if (paths.Count == 0)
                throw new InvalidOperationException("Could not update this Store app startup task.");

            int value = enabled ? 2 : 1;
            foreach (string path in paths)
                WriteTaskState(path, value);

            item.StateRegistryPath = paths[0];

            if (ReadPathEnabled(paths[0]) != enabled)
                throw new InvalidOperationException("Windows did not accept this startup change.");

            item.IsEnabled = enabled;
        }

        private static void AddFromRegistry(Dictionary<string, StartupItem> items, Dictionary<string, PackagedState> states)
        {
            foreach (var state in states.Values)
            {
                if (string.IsNullOrWhiteSpace(state.TaskId) || string.IsNullOrWhiteSpace(state.PackageFamilyName))
                    continue;

                AddPackaged(
                    items,
                    state.PackageFamilyName,
                    state.TaskId,
                    NameFromPfn(state.PackageFamilyName),
                    "Microsoft Store",
                    string.Empty,
                    state.RegistryPath ?? string.Empty,
                    state.Enabled,
                    state.CanModify);
            }
        }

        private static void AddFromManifests(Dictionary<string, StartupItem> items, Dictionary<string, PackagedState> states)
        {
            string windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            ScanManifestRoot(Path.Combine(programFiles, "WindowsApps"), items, states);
            ScanManifestRoot(Path.Combine(windows, "SystemApps"), items, states);
        }

        private static void ScanManifestRoot(
            string root,
            Dictionary<string, StartupItem> items,
            Dictionary<string, PackagedState> states)
        {
            if (!Directory.Exists(root))
                return;

            IEnumerable<string> dirs;
            try
            {
                dirs = Directory.EnumerateDirectories(root);
            }
            catch (Exception ex)
            {
                Logger.Log($"Packaged startup scan skipped ({root}): {ex.Message}");
                return;
            }

            foreach (string dir in dirs)
            {
                string manifest = Path.Combine(dir, "AppxManifest.xml");
                if (!File.Exists(manifest))
                    continue;

                try
                {
                    ParseManifest(dir, manifest, items, states);
                }
                catch (Exception ex)
                {
                    Logger.Log($"Packaged manifest failed ({Path.GetFileName(dir)}): {ex.Message}");
                }
            }
        }

        private static void ParseManifest(
            string packageDir,
            string manifestPath,
            Dictionary<string, StartupItem> items,
            Dictionary<string, PackagedState> states)
        {
            var doc = XDocument.Load(manifestPath);
            var tasks = doc.Descendants().Where(e => e.Name.LocalName == "StartupTask").ToList();
            if (tasks.Count == 0)
                return;

            var identity = doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "Identity");
            string identityName = (string?)identity?.Attribute("Name") ?? Path.GetFileName(packageDir);
            string pfn = PackageFamilyName(Path.GetFileName(packageDir), identityName);

            string publisher = ((string?)doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "PublisherDisplayName")?.Value)
                ?? "Microsoft Store";
            string packageDisplay = ((string?)doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "DisplayName")?.Value) ?? string.Empty;
            string logo = ((string?)doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "Logo")?.Value) ?? string.Empty;
            string? exe = doc.Descendants()
                .Where(e => e.Name.LocalName == "Application")
                .Select(e => (string?)e.Attribute("Executable"))
                .FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));

            string iconPath = ResolveLogo(packageDir, logo)
                ?? ResolveLogo(packageDir, @"Assets\StoreLogo.png")
                ?? (exe != null ? Path.Combine(packageDir, exe) : string.Empty);
            string exePath = exe != null ? Path.Combine(packageDir, exe) : string.Empty;

            foreach (var task in tasks)
            {
                string taskId = (string?)task.Attribute("TaskId") ?? "StartupTask";
                string display = (string?)task.Attribute("DisplayName") ?? string.Empty;
                string name = PickName(display, packageDisplay, pfn, identityName);

                string mapKey = StateKey(pfn, taskId);
                states.TryGetValue(mapKey, out var state);
                if (state.TaskId == null)
                    states.TryGetValue(taskId, out state);

                bool enabled = state.TaskId != null && state.Enabled;

                AddPackaged(
                    items,
                    pfn,
                    taskId,
                    name,
                    CleanPublisher(publisher),
                    File.Exists(exePath) ? exePath : string.Empty,
                    state.RegistryPath ?? string.Empty,
                    enabled,
                    state.TaskId == null || state.CanModify,
                    iconPath);
            }
        }

        private static void AddPackaged(
            Dictionary<string, StartupItem> items,
            string pfn,
            string taskId,
            string name,
            string publisher,
            string exePath,
            string registryPath,
            bool enabled,
            bool canModify,
            string? iconPath = null)
        {
            string id = StartupPaths.MakeId(StartupLocationKind.PackagedApp, pfn);
            var existing = items.Values.FirstOrDefault(i =>
                i.Location == StartupLocationKind.PackagedApp
                && string.Equals(i.PackageFamilyName, pfn, StringComparison.OrdinalIgnoreCase));
            if (existing != null || items.ContainsKey(id))
            {
                existing ??= items[id];
                if (string.IsNullOrWhiteSpace(existing.StateRegistryPath) && !string.IsNullOrWhiteSpace(registryPath))
                    existing.StateRegistryPath = registryPath;
                if (string.IsNullOrWhiteSpace(existing.IconPath) && !string.IsNullOrWhiteSpace(iconPath))
                    items[existing.Id] = CloneWithIcon(existing, iconPath!, publisher, name, exePath);
                return;
            }

            items[id] = new StartupItem
            {
                Id = id,
                Name = name,
                Command = $"shell:AppsFolder\\{pfn}!App",
                ExecutablePath = exePath,
                IconPath = iconPath ?? string.Empty,
                Location = StartupLocationKind.PackagedApp,
                LocationLabel = StartupPaths.LocationLabel(StartupLocationKind.PackagedApp),
                ApprovalValueName = taskId,
                Publisher = publisher,
                CanModify = canModify,
                IsEnabled = enabled,
                PackageFamilyName = pfn,
                StateRegistryPath = registryPath
            };
        }

        private static StartupItem CloneWithIcon(StartupItem item, string iconPath, string publisher, string name, string exePath)
        {
            return new StartupItem
            {
                Id = item.Id,
                Name = IsResource(item.Name) ? name : item.Name,
                Command = item.Command,
                ExecutablePath = string.IsNullOrWhiteSpace(item.ExecutablePath) ? exePath : item.ExecutablePath,
                IconPath = iconPath,
                Location = item.Location,
                LocationLabel = item.LocationLabel,
                ApprovalValueName = item.ApprovalValueName,
                Publisher = string.Equals(item.Publisher, "Microsoft Store", StringComparison.Ordinal) ? publisher : item.Publisher,
                CanModify = item.CanModify,
                IsEnabled = item.IsEnabled,
                PackageFamilyName = item.PackageFamilyName,
                StateRegistryPath = item.StateRegistryPath
            };
        }

        private static Dictionary<string, PackagedState> ReadStates()
        {
            var map = new Dictionary<string, PackagedState>(StringComparer.OrdinalIgnoreCase);
            try
            {
                using var root = Registry.CurrentUser.OpenSubKey(SystemAppData);
                if (root == null) return map;
                foreach (string pfn in root.GetSubKeyNames())
                {
                    using var pkg = root.OpenSubKey(pfn);
                    if (pkg == null) continue;
                    Walk(pkg, $@"{SystemAppData}\{pfn}", pfn, 0, map);
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"Packaged startup registry failed: {ex.Message}");
            }

            return map;
        }

        private static void Walk(
            RegistryKey key,
            string relativePath,
            string pfn,
            int depth,
            Dictionary<string, PackagedState> map)
        {
            if (depth > 6) return;

            object? raw = key.GetValue("State") ?? key.GetValue("UserEnabledState");
            string taskId = relativePath.Split('\\')[^1];
            bool looksLikeTask = taskId.Contains("Startup", StringComparison.OrdinalIgnoreCase)
                || relativePath.Contains("StartupTasks", StringComparison.OrdinalIgnoreCase);

            if (raw != null && looksLikeTask)
            {
                int value = Convert.ToInt32(raw);
                var state = new PackagedState
                {
                    PackageFamilyName = pfn,
                    TaskId = taskId,
                    RegistryPath = relativePath,
                    Enabled = value is 2 or 4,
                    CanModify = value is not 3 and not 4
                };
                map[StateKey(pfn, taskId)] = state;
                map[taskId] = state;
            }
            else if (looksLikeTask && relativePath.Contains(@"\StartupTasks\", StringComparison.OrdinalIgnoreCase)
                     && !relativePath.EndsWith(@"\StartupTasks", StringComparison.OrdinalIgnoreCase))
            {
                var state = new PackagedState
                {
                    PackageFamilyName = pfn,
                    TaskId = taskId,
                    RegistryPath = relativePath,
                    Enabled = false,
                    CanModify = true
                };
                map.TryAdd(StateKey(pfn, taskId), state);
                map.TryAdd(taskId, state);
            }

            foreach (string name in key.GetSubKeyNames())
            {
                using var sub = key.OpenSubKey(name);
                if (sub == null) continue;
                Walk(sub, $@"{relativePath}\{name}", pfn, depth + 1, map);
            }
        }

        private static bool TryMatchState(
            Dictionary<string, PackagedState> states,
            StartupItem item,
            out PackagedState state)
        {
            var matches = states.Values
                .Where(s => string.Equals(s.PackageFamilyName, item.PackageFamilyName, StringComparison.OrdinalIgnoreCase)
                            && !string.IsNullOrWhiteSpace(s.RegistryPath))
                .GroupBy(s => s.RegistryPath, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .OrderByDescending(s => string.Equals(s.TaskId, item.ApprovalValueName, StringComparison.OrdinalIgnoreCase))
                .ThenByDescending(s => s.RegistryPath!.Contains(@"\StartupTasks\", StringComparison.OrdinalIgnoreCase))
                .ThenByDescending(s => s.RegistryPath!.Length)
                .ToList();

            if (matches.Count == 0)
            {
                state = default;
                return false;
            }

            state = matches[0];
            return true;
        }

        private static List<string> ResolveWritePaths(StartupItem item)
        {
            var paths = new List<string>();
            AddPath(paths, item.StateRegistryPath);

            foreach (string path in FindExistingTaskPaths(item.PackageFamilyName, item.ApprovalValueName))
                AddPath(paths, path);

            if (paths.Count == 0)
                AddPath(paths, FindOrCreateTaskPath(item.PackageFamilyName, item.ApprovalValueName));

            return paths;
        }

        private static void AddPath(List<string> paths, string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return;
            if (!paths.Exists(p => string.Equals(p, path, StringComparison.OrdinalIgnoreCase)))
                paths.Add(path);
        }

        private static List<string> FindExistingTaskPaths(string pfn, string taskId)
        {
            var paths = new List<string>();
            try
            {
                using var pkg = Registry.CurrentUser.OpenSubKey($@"{SystemAppData}\{pfn}");
                if (pkg == null)
                    return paths;
                CollectTaskPaths(pkg, $@"{SystemAppData}\{pfn}", taskId, paths);
            }
            catch (Exception ex)
            {
                Logger.Log($"Packaged startup path search failed ({pfn}): {ex.Message}");
            }

            return paths;
        }

        private static void CollectTaskPaths(RegistryKey key, string relativePath, string taskId, List<string> paths)
        {
            string name = relativePath.Split('\\')[^1];
            bool underTasks = relativePath.Contains(@"\StartupTasks\", StringComparison.OrdinalIgnoreCase);
            bool isTasksFolder = name.Equals("StartupTasks", StringComparison.OrdinalIgnoreCase);
            bool nameMatches = name.Equals(taskId, StringComparison.OrdinalIgnoreCase)
                || name.Contains("Startup", StringComparison.OrdinalIgnoreCase);

            if (underTasks && !isTasksFolder)
                paths.Add(relativePath);
            else if (nameMatches && (key.GetValue("State") != null || key.GetValue("UserEnabledState") != null))
                paths.Add(relativePath);

            if (isTasksFolder)
                paths.Add($@"{relativePath}\{taskId}");

            foreach (string child in key.GetSubKeyNames())
            {
                using var sub = key.OpenSubKey(child);
                if (sub == null) continue;
                CollectTaskPaths(sub, $@"{relativePath}\{child}", taskId, paths);
            }
        }

        private static string FindOrCreateTaskPath(string pfn, string taskId)
        {
            try
            {
                using var pkg = Registry.CurrentUser.OpenSubKey($@"{SystemAppData}\{pfn}", writable: true)
                    ?? Registry.CurrentUser.CreateSubKey($@"{SystemAppData}\{pfn}");
                if (pkg == null)
                    return $@"{SystemAppData}\{pfn}\StartupTasks\{taskId}";

                string? nested = FindStartupTasksFolder(pkg, $@"{SystemAppData}\{pfn}");
                return string.IsNullOrWhiteSpace(nested)
                    ? $@"{SystemAppData}\{pfn}\StartupTasks\{taskId}"
                    : $@"{nested}\{taskId}";
            }
            catch
            {
                return $@"{SystemAppData}\{pfn}\StartupTasks\{taskId}";
            }
        }

        private static string? FindStartupTasksFolder(RegistryKey key, string relativePath)
        {
            if (relativePath.Split('\\')[^1].Equals("StartupTasks", StringComparison.OrdinalIgnoreCase))
                return relativePath;

            foreach (string child in key.GetSubKeyNames())
            {
                using var sub = key.OpenSubKey(child);
                if (sub == null) continue;
                string? found = FindStartupTasksFolder(sub, $@"{relativePath}\{child}");
                if (found != null)
                    return found;
            }

            return null;
        }

        private static void WriteTaskState(string path, int value)
        {
            using var key = Registry.CurrentUser.CreateSubKey(path, true)
                ?? throw new InvalidOperationException("Could not update this Store app startup task.");
            key.SetValue("State", value, RegistryValueKind.DWord);
            key.SetValue("UserEnabledState", value, RegistryValueKind.DWord);
        }

        private static bool ReadPathEnabled(string path)
        {
            using var key = Registry.CurrentUser.OpenSubKey(path);
            object? raw = key?.GetValue("State") ?? key?.GetValue("UserEnabledState");
            if (raw == null)
                return true;
            int value = Convert.ToInt32(raw);
            return value is 2 or 4;
        }

        private static string? ResolveLogo(string packageDir, string logo)
        {
            if (string.IsNullOrWhiteSpace(logo))
                return null;

            string relative = logo.Replace('/', '\\').TrimStart('\\');
            string full = Path.Combine(packageDir, relative);
            if (File.Exists(full))
                return full;

            string? dir = Path.GetDirectoryName(full);
            string name = Path.GetFileNameWithoutExtension(full);
            string ext = Path.GetExtension(full);
            if (string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir) || string.IsNullOrWhiteSpace(name))
                return FindStoreLogo(packageDir);

            try
            {
                return Directory.EnumerateFiles(dir, name + "*" + ext)
                    .OrderByDescending(f => f.Contains("scale-200", StringComparison.OrdinalIgnoreCase))
                    .ThenByDescending(f => f.Length)
                    .FirstOrDefault()
                    ?? FindStoreLogo(packageDir);
            }
            catch
            {
                return FindStoreLogo(packageDir);
            }
        }

        private static string? FindStoreLogo(string packageDir)
        {
            string assets = Path.Combine(packageDir, "Assets");
            if (!Directory.Exists(assets))
                return null;

            try
            {
                return Directory.EnumerateFiles(assets, "StoreLogo*.png")
                    .Concat(Directory.EnumerateFiles(assets, "Square44x44Logo*.png"))
                    .OrderByDescending(f => f.Contains("scale-200", StringComparison.OrdinalIgnoreCase))
                    .FirstOrDefault();
            }
            catch
            {
                return null;
            }
        }

        private static string PickName(string taskDisplay, string packageDisplay, string pfn, string identityName)
        {
            if (!IsResource(taskDisplay)) return taskDisplay.Trim();
            if (!IsResource(packageDisplay)) return packageDisplay.Trim();
            return NameFromPfn(pfn.Length > 0 ? pfn : identityName);
        }

        private static bool IsResource(string? value) =>
            string.IsNullOrWhiteSpace(value) || value.StartsWith("ms-resource:", StringComparison.OrdinalIgnoreCase);

        private static string NameFromPfn(string pfn)
        {
            int us = pfn.LastIndexOf('_');
            string identity = us > 0 ? pfn[..us] : pfn;
            if (KnownNames.TryGetValue(identity, out string? known))
                return known;

            int dot = identity.LastIndexOf('.');
            string tail = dot >= 0 ? identity[(dot + 1)..] : identity;
            return Regex.Replace(tail, "([a-z])([A-Z])", "$1 $2");
        }

        private static string PackageFamilyName(string folderName, string identityName)
        {
            int sep = folderName.LastIndexOf("__", StringComparison.Ordinal);
            if (sep >= 0)
                return $"{identityName}_{folderName[(sep + 2)..]}";

            int us = folderName.LastIndexOf('_');
            return us > 0 ? $"{identityName}_{folderName[(us + 1)..]}" : identityName;
        }

        private static string CleanPublisher(string publisher)
        {
            if (IsResource(publisher))
                return "Microsoft Store";
            return publisher.Trim();
        }

        private static string StateKey(string pfn, string taskId) => $"{pfn}|{taskId}";

        private struct PackagedState
        {
            public string PackageFamilyName;
            public string? TaskId;
            public string? RegistryPath;
            public bool Enabled;
            public bool CanModify;
        }
    }
}
