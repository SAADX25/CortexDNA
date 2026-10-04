using System.Security.Principal;
using System.IO;
using System.Xml.Linq;
using CortexDNA.Core.Startup;
using CortexDNA.Models;
using Xunit;

namespace CortexDNA.AutomatedTests;

public sealed class LegacyMigrationTests
{
    private static readonly XNamespace Ns = "http://schemas.microsoft.com/windows/2004/02/mit/task";
    private static string Sid { get { using var identity = WindowsIdentity.GetCurrent(); return identity.User!.Value; } }
    private static string? Resolve(string user) => user == @"DOMAIN\tester" ? Sid : user;
    private static StartupItem Item(string id = "currentuserrun:demo") => new() {
        Id = id, Name = "Demo", Command = @"""C:\Apps\demo.exe"" --quiet", LocationLabel = "Current user", IsEnabled = false };
    private static DelayTaskSnapshot Legacy(StartupItem item, string suffix = "1234ABCD")
    {
        string safe = new(item.Id.Where(char.IsLetterOrDigit).Take(24).ToArray());
        string name = $"Delay_{safe}_{suffix}";
        var xml = new XDocument(new XElement(Ns + "Task", new XAttribute("version", "1.2"),
            new XElement(Ns + "RegistrationInfo", new XElement(Ns + "Description", "Cortex DNA delayed start for Demo"), new XElement(Ns + "URI", StartupPaths.DelayTaskFolder + "\\" + name)),
            new XElement(Ns + "Triggers", new XElement(Ns + "LogonTrigger", new XElement(Ns + "Enabled", true), new XElement(Ns + "UserId", @"DOMAIN\tester"), new XElement(Ns + "Delay", "PT30S"))),
            new XElement(Ns + "Principals", new XElement(Ns + "Principal", new XAttribute("id", "Author"), new XElement(Ns + "UserId", Sid), new XElement(Ns + "LogonType", "InteractiveToken"), new XElement(Ns + "RunLevel", "LeastPrivilege"))),
            new XElement(Ns + "Settings", new XElement(Ns + "Enabled", true), new XElement(Ns + "StartWhenAvailable", true)),
            new XElement(Ns + "Actions", new XAttribute("Context", "Author"), new XElement(Ns + "Exec", new XElement(Ns + "Command", @"C:\Apps\demo.exe"), new XElement(Ns + "Arguments", "--quiet")))))
            .ToString(SaveOptions.DisableFormatting);
        return new(name, StartupPaths.DelayTaskFolder + "\\" + name, xml);
    }
    private static DelayTaskSnapshot Edit(DelayTaskSnapshot task, string element, string value)
    {
        var doc = XDocument.Parse(task.Xml);
        foreach (var node in doc.Descendants(Ns + element)) node.Value = value;
        return task with { Xml = doc.ToString(SaveOptions.DisableFormatting) };
    }

    [Fact]
    public void VerifiedLegacyTaskMigratesAndIsKnownOnNextLoad()
    {
        var item = Item(); var legacy = Legacy(item); var store = new Store(legacy);
        Assert.Empty(LegacyStartupDelayMigration.Apply([item], store, Sid, Resolve, migrate: true));
        Assert.True(item.IsDelayed); Assert.False(item.IsEnabled);
        Assert.Null(store.Read(legacy.Name));
        string modern = StartupPaths.DelayTaskName(item.Id);
        Assert.NotNull(store.Read(modern));
        Assert.Equal(new[] { "create:" + modern, "activate:" + modern, "delete:" + legacy.Name }, store.Mutations);
        var nextSession = Item(); store.Mutations.Clear();
        Assert.Empty(LegacyStartupDelayMigration.Apply([nextSession], store, Sid, Resolve, migrate: true));
        Assert.True(nextSession.IsDelayed); Assert.Empty(store.Mutations);
    }

    [Theory]
    [InlineData("description")]
    [InlineData("executable")]
    [InlineData("arguments")]
    [InlineData("identity")]
    [InlineData("elevation")]
    [InlineData("delay")]
    [InlineData("working-directory")]
    [InlineData("extra-action")]
    [InlineData("disabled")]
    [InlineData("unknown-name")]
    [InlineData("malformed-xml")]
    public void UncertainTaskIsRetainedForReview(string mismatch)
    {
        var item = Item(); var task = Legacy(item);
        task = mismatch switch {
            "description" => Edit(task, "Description", "Someone else's task"),
            "executable" => Edit(task, "Command", @"C:\Other\demo.exe"),
            "arguments" => Edit(task, "Arguments", "--different"),
            "identity" => Edit(task, "UserId", "S-1-5-18"),
            "elevation" => Edit(task, "RunLevel", "HighestAvailable"),
            "delay" => Edit(task, "Delay", "PT5S"),
            "disabled" => Edit(task, "Enabled", "false"),
            "unknown-name" => task with { Name = "Unrelated", Path = StartupPaths.DelayTaskFolder + "\\Unrelated" },
            "malformed-xml" => task with { Xml = "<!DOCTYPE Task [<!ENTITY external SYSTEM 'file:///C:/private'>]><Task>&external;</Task>" },
            _ => task
        };
        if (mismatch is "working-directory" or "extra-action")
        {
            var doc = XDocument.Parse(task.Xml);
            if (mismatch == "working-directory") doc.Descendants(Ns + "Exec").Single().Add(new XElement(Ns + "WorkingDirectory", @"C:\Different"));
            else doc.Descendants(Ns + "Actions").Single().Add(new XElement(Ns + "Exec", new XElement(Ns + "Command", "other.exe")));
            task = task with { Xml = doc.ToString(SaveOptions.DisableFormatting) };
        }
        var store = new Store(task);
        Assert.NotEmpty(LegacyStartupDelayMigration.Apply([item], store, Sid, Resolve, migrate: true));
        Assert.False(item.IsDelayed); Assert.Equal(task, store.Read(task.Name)); Assert.Empty(store.Mutations);
    }

    [Theory]
    [InlineData(@"\Other\StartupDelay")]
    [InlineData(@"\CortexDNA\StartupDelay\Nested")]
    public void TasksOutsideExactFolderAreNeverTouched(string folder)
    {
        var item = Item(); var task = Legacy(item); task = task with { Path = folder + "\\" + task.Name };
        var store = new Store(task);
        LegacyStartupDelayMigration.Apply([item], store, Sid, Resolve, migrate: true);
        Assert.Empty(store.Mutations); Assert.False(item.IsDelayed);
    }

    [Fact]
    public void ReadOnlySmokeRecognizesLegacyWithoutChangingStartup()
    {
        var item = Item(); var store = new Store(Legacy(item));
        Assert.Empty(LegacyStartupDelayMigration.Apply([item], store, Sid, Resolve, migrate: false));
        Assert.True(item.IsDelayed); Assert.Empty(store.Mutations);
    }

    [Fact]
    public void SchedulerIdentityNormalizationPreservesVerifiedMigration()
    {
        var item = Item(); var task = Legacy(item); var store = new Store(task) { Failure = "identity-normalization" };
        Assert.Empty(LegacyStartupDelayMigration.Apply([item], store, Sid, Resolve, migrate: true));
        Assert.Null(store.Read(task.Name)); Assert.NotNull(store.Read(StartupPaths.DelayTaskName(item.Id)));
    }

    [Theory]
    [InlineData("duplicate-legacy")]
    [InlineData("ambiguous-item")]
    [InlineData("target-collision")]
    [InlineData("running")]
    public void ConflictsAreRetainedWithoutOverwriteOrDelete(string conflict)
    {
        var item = conflict == "ambiguous-item" ? Item("currentuserrun:abcdefghijklmnopqrstuvwxyA") : Item();
        var task = Legacy(item); var store = new Store(task);
        var items = new List<StartupItem> { item };
        if (conflict == "duplicate-legacy") store.Tasks.Add(Legacy(item, "ABCD1234").Name, Legacy(item, "ABCD1234"));
        if (conflict == "ambiguous-item") items.Add(Item(item.Id[..^1] + "B"));
        if (conflict == "target-collision") {
            string target = StartupPaths.DelayTaskName(item.Id);
            store.Tasks.Add(target, task with { Name = target, Path = StartupPaths.DelayTaskFolder + "\\" + target, Xml = "<unrelated/>" });
        }
        if (conflict == "running") store.Tasks[task.Name] = task with { IsRunning = true };
        Assert.NotEmpty(LegacyStartupDelayMigration.Apply(items, store, Sid, Resolve, migrate: true));
        Assert.Empty(store.Mutations); Assert.NotNull(store.Read(task.Name));
    }

    [Theory]
    [InlineData("create")]
    [InlineData("activate")]
    [InlineData("delete")]
    [InlineData("verify")]
    [InlineData("changed-source")]
    public void MigrationFailureKeepsOriginal(string failure)
    {
        var item = Item(); var legacy = Legacy(item); var store = new Store(legacy) { Failure = failure };
        Assert.NotEmpty(LegacyStartupDelayMigration.Apply([item], store, Sid, Resolve, migrate: true));
        Assert.NotNull(store.Read(legacy.Name)); Assert.True(item.IsDelayed);
        if (failure is "create" or "activate" or "delete" or "changed-source") Assert.Null(store.Read(StartupPaths.DelayTaskName(item.Id)));
    }

    private sealed class Store(params DelayTaskSnapshot[] tasks) : IStartupDelayTaskStore
    {
        public Dictionary<string, DelayTaskSnapshot> Tasks { get; } = tasks.ToDictionary(task => task.Name, StringComparer.OrdinalIgnoreCase);
        public List<string> Mutations { get; } = new();
        public string? Failure;
        public IReadOnlyList<DelayTaskSnapshot> List() => Tasks.Values.ToArray();
        public DelayTaskSnapshot? Read(string name)
        {
            var result = Tasks.GetValueOrDefault(name);
            if (Failure == "changed-source" && result != null && Mutations.Count == 0) return result with { Xml = result.Xml + " " };
            return result;
        }
        public void CreateOnly(string name, string xml)
        {
            if (Failure == "create") throw new IOException("create failed");
            Mutations.Add("create:" + name);
            if (Failure == "identity-normalization") xml = xml.Replace(@"DOMAIN\tester", Sid, StringComparison.Ordinal);
            Tasks.Add(name, new(name, StartupPaths.DelayTaskFolder + "\\" + name, Failure == "verify" ? "<changed/>" : xml));
        }
        public void DeleteIfUnchanged(DelayTaskSnapshot expected)
        {
            if (Failure == "delete" && expected.Name.EndsWith("1234ABCD", StringComparison.Ordinal)) throw new IOException("delete failed");
            if (Tasks[expected.Name] != expected) throw new IOException("changed");
            Mutations.Add("delete:" + expected.Name); Tasks.Remove(expected.Name);
        }
        public DelayTaskSnapshot ReplaceIfUnchanged(DelayTaskSnapshot expected, string xml)
        {
            if (Tasks[expected.Name] != expected) throw new IOException("changed");
            if (Failure == "activate") throw new IOException("activation failed");
            Mutations.Add("activate:" + expected.Name);
            if (Failure == "identity-normalization") xml = xml.Replace(@"DOMAIN\tester", Sid, StringComparison.Ordinal);
            return Tasks[expected.Name] = expected with { Xml = xml };
        }
    }
}
