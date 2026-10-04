using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using Xunit;
using Xunit.Abstractions;

namespace CortexDNA.AutomatedTests;

public sealed class RegressionHarnessTests(ITestOutputHelper output)
{
    [Fact]
    public async Task ExistingRegressionAndWpfSmokeSuite()
    {
        var info = new ProcessStartInfo(Path.Combine(AppContext.BaseDirectory, "RegressionHarness", "CortexDNA.Tests.exe")) {
            UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
        using var process = Process.Start(info)!;
        Task<string> stdout = process.StandardOutput.ReadToEndAsync();
        Task<string> stderr = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(3));
        try { await process.WaitForExitAsync(timeout.Token); }
        catch (OperationCanceledException) { process.Kill(entireProcessTree: true); throw new TimeoutException("Owned regression harness timed out."); }
        output.WriteLine(await stdout);
        output.WriteLine(await stderr);
        Assert.Equal(0, process.ExitCode);
        var counts = Regex.Match(await stdout, @"TOTAL (\d+); PASSED (\d+); FAILED (\d+)");
        Assert.True(counts.Success, "Harness did not report its counts.");
        int total = int.Parse(counts.Groups[1].Value);
        Assert.True(total >= 31, "Existing regression coverage must not shrink.");
        Assert.Equal(total, int.Parse(counts.Groups[2].Value));
        Assert.Equal(0, int.Parse(counts.Groups[3].Value));
    }
}
