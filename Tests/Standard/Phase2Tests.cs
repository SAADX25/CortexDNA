using System.Diagnostics;
using CortexDNA.Services;
using CortexDNA.UI;
using Xunit;
using Xunit.Abstractions;
namespace CortexDNA.AutomatedTests;

[CollectionDefinition("WPF process isolation", DisableParallelization = true)]
public sealed class WpfProcessIsolation { }
[Collection("WPF process isolation")]
public sealed class Phase2Tests(ITestOutputHelper output)
{
    [Fact]
    public void NotificationsAreBoundedAndDismissible()
    {
        var center = new NotificationCenter();
        for (int i = 0; i < 25; i++) center.Publish($"Notice {i}", UiState.Warning);
        Assert.Equal(20, center.Items.Count); Assert.Equal("Notice 5", center.Items[0].Message);
        center.DismissCommand.Execute(center.Items[0]); Assert.Equal(19, center.Items.Count);
    }
    [Fact]
    public void EmptyAndConsecutiveDuplicateNotificationsAreIgnored()
    { var center = new NotificationCenter(); center.Publish(" "); center.Publish("Ready"); center.Publish("Ready"); Assert.Single(center.Items); }
    [Fact]
    public async Task AllPhase2PagesAndLifecyclesLoadInWpf()
    {
        using var process = Process.Start(new ProcessStartInfo(System.IO.Path.Combine(AppContext.BaseDirectory, "UiSmoke", "CortexDNA.UiSmoke.exe"))
        { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true })!;
        var stdout = process.StandardOutput.ReadToEndAsync(); var stderr = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(3));
        try { await process.WaitForExitAsync(timeout.Token); } catch (OperationCanceledException) { process.Kill(true); throw new TimeoutException("Owned UI smoke process timed out."); }
        output.WriteLine(await stdout); output.WriteLine(await stderr); Assert.Equal(0, process.ExitCode);
        Assert.Contains("UI SMOKE PASSED", await stdout);
    }
}
