using System.IO;
using System.Xml;
using System.Xml.Linq;
using CortexDNA.Models;

namespace CortexDNA.Core.Startup;

internal sealed record DelayTaskSnapshot(string Name, string Path, string Xml, bool IsRunning = false);

internal interface IStartupDelayTaskStore
{
    IReadOnlyList<DelayTaskSnapshot> List();
    DelayTaskSnapshot? Read(string name);
    void CreateOnly(string name, string xml);
    DelayTaskSnapshot ReplaceIfUnchanged(DelayTaskSnapshot expected, string xml);
    void DeleteIfUnchanged(DelayTaskSnapshot expected);
}

internal static class LegacyStartupDelayMigration
{
    private static readonly object Sync = new();
    private static readonly XNamespace Ns = "http://schemas.microsoft.com/windows/2004/02/mit/task";

    internal static IReadOnlyList<string> Apply(IReadOnlyList<StartupItem> items,
        IStartupDelayTaskStore store, string sid, Func<string, string?> resolveIdentity, bool migrate)
    {
        lock (Sync)
        {
            var notes = new List<string>();
            var tasks = store.List();
            foreach (var item in items) item.IsDelayed = false;
            foreach (var task in tasks)
            {
                // No mutation, even inspection for migration, outside our exact task folder.
                if (!InFolder(task)) continue;
                var matches = items.Where(item => Matches(task, item, sid, resolveIdentity)).ToArray();
                if (matches.Length != 1)
                {
                    notes.Add($"Legacy/stale delay task '{task.Name}' retained: no unique verified startup match.");
                    continue;
                }
                var item = matches[0];
                item.IsDelayed = true; // Recognize old names even in read-only mode or on migration failure.
                string target = StartupPaths.DelayTaskName(item.Id);
                if (task.Name.Equals(target, StringComparison.OrdinalIgnoreCase)) continue;
                if (task.IsRunning || tasks.Count(t => Matches(t, item, sid, resolveIdentity)) != 1)
                {
                    notes.Add($"Legacy delay task '{task.Name}' retained: running or duplicate tasks require review.");
                    continue;
                }
                if (!migrate) continue;
                if (store.Read(target) != null)
                {
                    notes.Add($"Legacy delay task '{task.Name}' retained: target name already exists; nothing overwritten.");
                    continue;
                }
                DelayTaskSnapshot? created = null;
                try
                {
                    var current = store.Read(task.Name);
                    if (current == null || current.Xml != task.Xml || current.IsRunning)
                        throw new InvalidOperationException("Legacy task changed before migration.");
                    string activeXml = RenamedXml(task.Xml, target);
                    string xml = DisabledXml(activeXml);
                    // TASK_CREATE only. Preserve the complete original definition, not just its action.
                    store.CreateOnly(target, xml);
                    created = store.Read(target);
                    if (created == null || !Matches(created, item, sid, resolveIdentity, allowDisabled: true) ||
                        !EquivalentDefinition(xml, created.Xml, resolveIdentity))
                        throw new InvalidOperationException("New delay task could not be verified; legacy task retained.");
                    // New task stays disabled until its complete definition has been verified.
                    created = store.ReplaceIfUnchanged(created, activeXml);
                    if (!Matches(created, item, sid, resolveIdentity) || !EquivalentDefinition(activeXml, created.Xml, resolveIdentity))
                        throw new InvalidOperationException("Activated delay task could not be verified; legacy task retained.");
                    store.DeleteIfUnchanged(task);
                }
                catch (Exception ex)
                {
                    // A failed migration must retain the original. Remove only the exact new definition we own.
                    string expectedXml = RenamedXml(task.Xml, target);
                    if (created != null && (EquivalentDefinition(expectedXml, created.Xml, resolveIdentity) ||
                        EquivalentDefinition(DisabledXml(expectedXml), created.Xml, resolveIdentity)))
                    {
                        try { store.DeleteIfUnchanged(created); }
                        catch (Exception rollback) { notes.Add($"Migration rollback for '{target}' needs review: {rollback.Message}"); }
                    }
                    notes.Add($"Legacy delay task '{task.Name}' retained; migration failed: {ex.Message}");
                }
            }
            return notes;
        }
    }

    internal static bool Matches(DelayTaskSnapshot task, StartupItem item, string sid,
        Func<string, string?> resolveIdentity, bool allowDisabled = false)
    {
        try
        {
            if (!InFolder(task) || item.Location is not (StartupLocationKind.CurrentUserRun or StartupLocationKind.UserStartupFolder)) return false;
            string modern = StartupPaths.DelayTaskName(item.Id);
            string safe = new string(item.Id.Where(char.IsLetterOrDigit).Take(24).ToArray());
            if (safe.Length == 0) safe = "Item";
            string prefix = $"Delay_{safe}_";
            bool legacy = task.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) &&
                task.Name.Length == prefix.Length + 8 && task.Name[prefix.Length..].All(Uri.IsHexDigit);
            if (!legacy && !task.Name.Equals(modern, StringComparison.OrdinalIgnoreCase)) return false;
            var root = Parse(task.Xml).Root;
            if (root?.Name != Ns + "Task") return false;
            if ((string?)root.Element(Ns + "RegistrationInfo")?.Element(Ns + "Description") != $"Cortex DNA delayed start for {item.Name}") return false;
            var principals = root.Element(Ns + "Principals")?.Elements().ToArray();
            var triggers = root.Element(Ns + "Triggers")?.Elements().ToArray();
            var actions = root.Element(Ns + "Actions")?.Elements().ToArray();
            if (principals is not { Length: 1 } || principals[0].Name != Ns + "Principal" ||
                triggers is not { Length: 1 } || triggers[0].Name != Ns + "LogonTrigger" ||
                actions is not { Length: 1 } || actions[0].Name != Ns + "Exec") return false;
            string principalUser = (string?)principals[0].Element(Ns + "UserId") ?? "";
            string triggerUser = (string?)triggers[0].Element(Ns + "UserId") ?? "";
            if (resolveIdentity(principalUser) != sid || resolveIdentity(triggerUser) != sid ||
                (string?)principals[0].Element(Ns + "LogonType") != "InteractiveToken" ||
                (string?)principals[0].Element(Ns + "RunLevel") != "LeastPrivilege") return false;
            if (XmlConvert.ToTimeSpan((string?)triggers[0].Element(Ns + "Delay") ?? "") != TimeSpan.FromSeconds(30)) return false;
            if (!Enabled(triggers[0].Element(Ns + "Enabled")) ||
                (!allowDisabled && !Enabled(root.Element(Ns + "Settings")?.Element(Ns + "Enabled")))) return false;
            var (exe, args) = StartupPaths.SplitCommand(item.Command);
            string command = (string?)actions[0].Element(Ns + "Command") ?? "";
            string working = (string?)actions[0].Element(Ns + "WorkingDirectory") ?? "";
            return SamePath(exe, command) &&
                ((string?)actions[0].Element(Ns + "Arguments") ?? "") == args &&
                SamePath(item.WorkingDirectory, working, allowEmpty: true);
        }
        catch (Exception ex) when (ex is XmlException or ArgumentException or FormatException or IOException) { return false; }
    }

    private static bool InFolder(DelayTaskSnapshot task) =>
        task.Name.Length > 0 && task.Name.IndexOfAny(['\\', '/']) < 0 &&
        task.Path.Equals(StartupPaths.DelayTaskFolder + "\\" + task.Name, StringComparison.OrdinalIgnoreCase);

    private static bool Enabled(XElement? element) => element == null || XmlConvert.ToBoolean(element.Value);

    private static bool SamePath(string left, string right, bool allowEmpty = false)
    {
        if (left.Length == 0 || right.Length == 0) return allowEmpty && left.Length == 0 && right.Length == 0;
        left = Environment.ExpandEnvironmentVariables(left);
        right = Environment.ExpandEnvironmentVariables(right);
        return Path.IsPathFullyQualified(left) && Path.IsPathFullyQualified(right) &&
            Path.GetFullPath(left).Equals(Path.GetFullPath(right), StringComparison.OrdinalIgnoreCase);
    }
    private static XDocument Parse(string xml)
    {
        using var reader = XmlReader.Create(new StringReader(xml), new XmlReaderSettings {
            DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 1024 * 1024 });
        return XDocument.Load(reader);
    }
    private static string RenamedXml(string xml, string name)
    {
        var document = Parse(xml);
        var registration = document.Root!.Element(Ns + "RegistrationInfo")!;
        registration.SetElementValue(Ns + "URI", StartupPaths.DelayTaskFolder + "\\" + name);
        return document.ToString(SaveOptions.DisableFormatting);
    }
    private static bool EquivalentDefinition(string expected, string actual, Func<string, string?> resolveIdentity)
    {
        try
        {
            var left = Parse(expected); var right = Parse(actual);
            // Scheduler may serialize DOMAIN\name as a SID. Compare that known semantic alias only.
            foreach (var doc in new[] { left, right })
                foreach (var user in doc.Descendants(Ns + "UserId"))
                    if (resolveIdentity(user.Value) is string resolved) user.Value = resolved;
            return XNode.DeepEquals(left.Root, right.Root);
        }
        catch (XmlException) { return false; }
    }
    private static string DisabledXml(string xml)
    {
        var document = Parse(xml);
        var settings = document.Root!.Element(Ns + "Settings");
        if (settings == null) { settings = new XElement(Ns + "Settings"); document.Root.Add(settings); }
        settings.SetElementValue(Ns + "Enabled", false);
        return document.ToString(SaveOptions.DisableFormatting);
    }
}
