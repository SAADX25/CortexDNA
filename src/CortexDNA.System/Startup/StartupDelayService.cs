using CortexDNA.Models;

namespace CortexDNA.Core.Startup
{
    /// <summary>
    /// Defers a user startup app by 30 seconds using Task Scheduler,
    /// then restores the original entry when delay is removed.
    /// </summary>
    public sealed class StartupDelayService
    {
        public IReadOnlyList<string> ReviewNotes { get; private set; } = Array.Empty<string>();
        private readonly IStartupDelayTaskStore _tasks = new SchedulerTaskStore();

        public void ApplyState(IEnumerable<StartupItem> items, bool migrateLegacy = true)
        {
            using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
            try
            {
                ReviewNotes = LegacyStartupDelayMigration.Apply(items.ToArray(), _tasks,
                    identity.User!.Value, ResolveIdentity, migrateLegacy);
                foreach (string note in ReviewNotes) Logger.Log(note);
            }
            catch (Exception ex)
            {
                ReviewNotes = new[] { "Startup delay tasks could not be verified. No migration was performed." };
                Logger.Log(ex);
            }
        }

        private static string? ResolveIdentity(string user)
        {
            if (string.IsNullOrWhiteSpace(user)) return null;
            try
            {
                if (user.StartsWith("S-1-", StringComparison.OrdinalIgnoreCase))
                    return new System.Security.Principal.SecurityIdentifier(user).Value;
                return ((System.Security.Principal.SecurityIdentifier)new System.Security.Principal.NTAccount(user)
                    .Translate(typeof(System.Security.Principal.SecurityIdentifier))).Value;
            }
            catch (Exception ex) when (ex is System.Security.Principal.IdentityNotMappedException or ArgumentException or System.Security.SecurityException)
            { return null; }
        }
        public void Delay(StartupItem item)
        {
            if (item.Location is not (StartupLocationKind.CurrentUserRun or StartupLocationKind.UserStartupFolder))
                throw new InvalidOperationException("Delay is only available for your user startup items.");

            var (path, args) = StartupPaths.SplitCommand(item.Command);
            path = Environment.ExpandEnvironmentVariables(path);
            if (!System.IO.Path.IsPathFullyQualified(path) || !System.IO.File.Exists(path) ||
                !path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Delay requires a quoted, absolute executable path. Ambiguous commands cannot be delayed.");

            string directory = Environment.ExpandEnvironmentVariables(item.WorkingDirectory);
            if (!string.IsNullOrEmpty(directory) &&
                (!System.IO.Path.IsPathFullyQualified(directory) || !System.IO.Directory.Exists(directory)))
                throw new InvalidOperationException("The shortcut's working directory is unavailable.");
            var existing = _tasks.Read(StartupPaths.DelayTaskName(item.Id));
            using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
            if (existing != null && !LegacyStartupDelayMigration.Matches(existing, item, identity.User!.Value, ResolveIdentity))
                throw new InvalidOperationException("A different or stale delay task uses this name. Review Task Scheduler first.");
            RegisterLogonTask(StartupPaths.DelayTaskName(item.Id), path, args, item.Name, directory);
            item.IsDelayed = true;
        }

        public void RemoveDelay(StartupItem item)
        {
            var task = _tasks.Read(StartupPaths.DelayTaskName(item.Id));
            if (task != null)
            {
                using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
                if (!LegacyStartupDelayMigration.Matches(task, item, identity.User!.Value, ResolveIdentity))
                    throw new InvalidOperationException("The delay task identity changed. Nothing was deleted.");
                _tasks.DeleteIfUnchanged(task);
            }
            else if (item.IsDelayed)
                throw new InvalidOperationException("Legacy delay is still pending review or migration. Nothing was deleted.");
            item.IsDelayed = false;
        }

        private static void RegisterLogonTask(string taskName, string exe, string args, string displayName, string workingDirectory)
        {
            object? service = null;
            object? folder = null;
            object? definition = null;
            object? triggerObject = null;
            object? actionObject = null;
            object? registered = null;
            try
            {
                service = Connect();
                folder = GetFolder((dynamic)service, StartupPaths.DelayTaskFolder, create: true)
                    ?? throw new InvalidOperationException("Could not create the delay task folder.");

                definition = ((dynamic)service).NewTask(0);
                dynamic def = definition;
                def.RegistrationInfo.Description = $"Cortex DNA delayed start for {displayName}";
                def.Settings.Enabled = true;
                def.Settings.StartWhenAvailable = true;
                def.Settings.DisallowStartIfOnBatteries = false;
                def.Settings.StopIfGoingOnBatteries = false;
                def.Settings.AllowDemandStart = true;
                def.Principal.LogonType = 3; // TASK_LOGON_INTERACTIVE_TOKEN
                using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
                def.Principal.UserId = identity.User!.Value;
                def.Principal.RunLevel = 0;  // LUA

                dynamic trigger = def.Triggers.Create(9); // TASK_TRIGGER_LOGON
                triggerObject = trigger;
                trigger.Delay = $"PT{StartupPaths.DelaySeconds}S";
                trigger.UserId = identity.User!.Value;
                trigger.Enabled = true;

                dynamic action = def.Actions.Create(0); // TASK_ACTION_EXEC
                actionObject = action;
                action.Path = exe;
                if (!string.IsNullOrEmpty(workingDirectory)) action.WorkingDirectory = workingDirectory;
                if (!string.IsNullOrWhiteSpace(args))
                    action.Arguments = args;

                registered = ((dynamic)folder).RegisterTaskDefinition(
                    taskName,
                    definition,
                    6,    // TASK_CREATE_OR_UPDATE
                    identity.User!.Value,
                    null,
                    3);   // TASK_LOGON_INTERACTIVE_TOKEN
            }
            catch (Exception ex)
            {
                Logger.Log($"Startup delay register failed: {ex.Message}");
                throw new InvalidOperationException("Could not create the 30s delay task.");
            }
            finally
            {
                StartupCom.Release(registered);
                StartupCom.Release(actionObject);
                StartupCom.Release(triggerObject);
                StartupCom.Release(definition);
                StartupCom.Release(folder);
                StartupCom.Release(service);
            }
        }

        private sealed class SchedulerTaskStore : IStartupDelayTaskStore
        {
            public IReadOnlyList<DelayTaskSnapshot> List()
            {
                var snapshots = new List<DelayTaskSnapshot>();
                WithFolder(folder =>
                {
                    object? collection = null;
                    try
                    {
                        collection = folder.GetTasks(1); // Include hidden tasks; all still require identity verification.
                        dynamic tasks = collection;
                        for (int i = 1; i <= (int)tasks.Count; i++)
                        {
                            object? task = null;
                            try { task = tasks[i]; snapshots.Add(Snapshot((dynamic)task)); }
                            finally { StartupCom.Release(task); }
                        }
                    }
                    finally { StartupCom.Release(collection); }
                });
                return snapshots;
            }
            public DelayTaskSnapshot? Read(string name)
            {
                CheckName(name);
                DelayTaskSnapshot? result = null;
                WithFolder(folder =>
                {
                    object? task = null;
                    try { task = folder.GetTask(name); result = Snapshot((dynamic)task); }
                    catch (System.Runtime.InteropServices.COMException ex) when (ex.HResult == unchecked((int)0x80070002)) { }
                    finally { StartupCom.Release(task); }
                });
                return result;
            }
            public void CreateOnly(string name, string xml)
            {
                CheckName(name);
                WithFolder(folder =>
                {
                    object? task = null;
                    try
                    {
                        using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
                        task = folder.RegisterTask(name, xml, 2, identity.User!.Value, null, 3);
                    }
                    finally { StartupCom.Release(task); }
                }, requireFolder: true);
            }
            public void DeleteIfUnchanged(DelayTaskSnapshot expected)
            {
                CheckName(expected.Name);
                if (!expected.Path.Equals(StartupPaths.DelayTaskFolder + "\\" + expected.Name, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Refusing to delete a task outside the delay folder.");
                WithFolder(folder =>
                {
                    object? task = null;
                    try
                    {
                        task = folder.GetTask(expected.Name);
                        var current = Snapshot((dynamic)task);
                        if (current.Xml != expected.Xml || current.IsRunning)
                            throw new InvalidOperationException("Task changed or started running. Nothing was deleted.");
                        folder.DeleteTask(expected.Name, 0);
                    }
                    finally { StartupCom.Release(task); }
                }, requireFolder: true);
            }
            public DelayTaskSnapshot ReplaceIfUnchanged(DelayTaskSnapshot expected, string xml)
            {
                CheckName(expected.Name);
                DelayTaskSnapshot? result = null;
                WithFolder(folder =>
                {
                    object? task = null, updated = null;
                    try
                    {
                        task = folder.GetTask(expected.Name);
                        var current = Snapshot((dynamic)task);
                        if (current.Xml != expected.Xml || current.IsRunning)
                            throw new InvalidOperationException("New task changed; activation refused.");
                        using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
                        updated = folder.RegisterTask(expected.Name, xml, 4, identity.User!.Value, null, 3);
                        result = Snapshot((dynamic)updated);
                    }
                    finally { StartupCom.Release(updated); StartupCom.Release(task); }
                }, requireFolder: true);
                return result!;
            }
            private static DelayTaskSnapshot Snapshot(dynamic task) =>
                new((string)task.Name, (string)task.Path, (string)task.Xml, (int)task.State is 2 or 4);
            private static void CheckName(string name)
            {
                if (string.IsNullOrEmpty(name) || name.IndexOfAny(new[] { '\\', '/' }) >= 0)
                    throw new ArgumentException("Invalid delay task name.");
            }
            private static void WithFolder(Action<dynamic> action, bool requireFolder = false)
            {
                object? service = null, folder = null;
                try
                {
                    service = Connect();
                    folder = GetFolder((dynamic)service, StartupPaths.DelayTaskFolder, create: false);
                    if (folder == null)
                    {
                        if (requireFolder) throw new InvalidOperationException("Delay folder no longer exists.");
                        return;
                    }
                    action((dynamic)folder);
                }
                finally { StartupCom.Release(folder); StartupCom.Release(service); }
            }
        }
        private static object Connect()
        {
            Type type = Type.GetTypeFromProgID("Schedule.Service")
                ?? throw new InvalidOperationException("Task Scheduler is not available.");
            dynamic service = Activator.CreateInstance(type)
                ?? throw new InvalidOperationException("Could not open Task Scheduler.");
            try { service.Connect(); return service; }
            catch { StartupCom.Release(service); throw; }
        }

        private static object? GetFolder(dynamic service, string path, bool create)
        {
            try
            {
                return service.GetFolder(path);
            }
            catch (System.Runtime.InteropServices.COMException ex) when (ex.HResult == unchecked((int)0x80070002))
            {
                if (!create) return null;
            }

            dynamic root = service.GetFolder("\\");
            try
            {
                try { root.CreateFolder("CortexDNA"); } catch { }
                dynamic cortex = service.GetFolder("\\CortexDNA");
                try { try { cortex.CreateFolder("StartupDelay"); } catch { } }
                finally { StartupCom.Release(cortex); }
                return service.GetFolder(path);
            }
            finally
            {
                StartupCom.Release(root);
            }
        }
    }
}
