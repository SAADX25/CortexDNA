using CortexDNA.Models;

namespace CortexDNA.Core.Startup
{
    /// <summary>
    /// Defers a user startup app by 30 seconds using Task Scheduler,
    /// then restores the original entry when delay is removed.
    /// </summary>
    public sealed class StartupDelayService
    {
        public void ApplyState(IEnumerable<StartupItem> items)
        {
            HashSet<string> delayed = ListDelayedTaskNames();
            foreach (var item in items)
                item.IsDelayed = delayed.Contains(StartupPaths.DelayTaskName(item.Id));
        }

        public void Delay(StartupItem item)
        {
            if (item.Location is not (StartupLocationKind.CurrentUserRun or StartupLocationKind.UserStartupFolder))
                throw new InvalidOperationException("Delay is only available for your user startup items.");

            var (path, args) = StartupPaths.SplitCommand(item.Command);
            if (string.IsNullOrWhiteSpace(path))
                throw new InvalidOperationException("Could not read this program's path.");

            RegisterLogonTask(StartupPaths.DelayTaskName(item.Id), path, args, item.Name);
            item.IsDelayed = true;
        }

        public void RemoveDelay(StartupItem item)
        {
            DeleteTask(StartupPaths.DelayTaskName(item.Id));
            item.IsDelayed = false;
        }

        private static HashSet<string> ListDelayedTaskNames()
        {
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            object? service = null;
            object? folder = null;
            try
            {
                service = Connect();
                folder = GetFolder((dynamic)service, StartupPaths.DelayTaskFolder, create: false);
                if (folder == null) return names;

                dynamic tasks = ((dynamic)folder).GetTasks(0);
                int count = (int)tasks.Count;
                for (int i = 1; i <= count; i++)
                {
                    dynamic task = tasks[i];
                    string? name = task.Name as string;
                    if (!string.IsNullOrWhiteSpace(name))
                        names.Add(name);
                    StartupCom.Release(task);
                }
                StartupCom.Release(tasks);
            }
            catch (Exception ex)
            {
                Logger.Log($"Startup delay list failed: {ex.Message}");
            }
            finally
            {
                StartupCom.Release(folder);
                StartupCom.Release(service);
            }

            return names;
        }

        private static void RegisterLogonTask(string taskName, string exe, string args, string displayName)
        {
            object? service = null;
            object? folder = null;
            object? definition = null;
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
                def.Principal.RunLevel = 0;  // LUA

                dynamic trigger = def.Triggers.Create(9); // TASK_TRIGGER_LOGON
                trigger.Delay = $"PT{StartupPaths.DelaySeconds}S";
                trigger.UserId = Environment.UserDomainName + "\\" + Environment.UserName;
                trigger.Enabled = true;

                dynamic action = def.Actions.Create(0); // TASK_ACTION_EXEC
                action.Path = exe;
                if (!string.IsNullOrWhiteSpace(args))
                    action.Arguments = args;

                ((dynamic)folder).RegisterTaskDefinition(
                    taskName,
                    definition,
                    6,    // TASK_CREATE_OR_UPDATE
                    null,
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
                StartupCom.Release(definition);
                StartupCom.Release(folder);
                StartupCom.Release(service);
            }
        }

        private static void DeleteTask(string taskName)
        {
            object? service = null;
            object? folder = null;
            try
            {
                service = Connect();
                folder = GetFolder((dynamic)service, StartupPaths.DelayTaskFolder, create: false);
                if (folder == null) return;
                ((dynamic)folder).DeleteTask(taskName, 0);
            }
            catch (Exception ex)
            {
                Logger.Log($"Startup delay delete skipped: {ex.Message}");
            }
            finally
            {
                StartupCom.Release(folder);
                StartupCom.Release(service);
            }
        }

        private static object Connect()
        {
            Type type = Type.GetTypeFromProgID("Schedule.Service")
                ?? throw new InvalidOperationException("Task Scheduler is not available.");
            dynamic service = Activator.CreateInstance(type)
                ?? throw new InvalidOperationException("Could not open Task Scheduler.");
            service.Connect();
            return service;
        }

        private static object? GetFolder(dynamic service, string path, bool create)
        {
            try
            {
                return service.GetFolder(path);
            }
            catch
            {
                if (!create) return null;
            }

            dynamic root = service.GetFolder("\\");
            try
            {
                try { root.CreateFolder("CortexDNA"); } catch { }
                dynamic cortex = service.GetFolder("\\CortexDNA");
                try { cortex.CreateFolder("StartupDelay"); } catch { }
                return service.GetFolder(path);
            }
            finally
            {
                StartupCom.Release(root);
            }
        }
    }
}
