using System.Collections.Immutable;
using System.Management;
using Microsoft.Win32;
using CortexDNA.Core.Health;
using CortexDNA.Models;
namespace CortexDNA.SystemHealth;

/// <summary>Local read-only health sources. No WUA searches, process launches, mutations or monitoring.</summary>
public static class WindowsHealthProviders
{
    public static IReadOnlyList<IHealthCheckProvider> Create() =>
    [
        SnapshotHealthProviders.One(HealthNode.Startup,HealthArea.Startup,20,c =>
        {
            var enabled = c.Startup.Where(i=>i.Enabled).ToArray();
            int high = enabled.Count(i=>i.Impact == StartupImpactLevel.High);
            bool measured = c.Startup.Length > 0 && enabled.All(i=>i.Impact != StartupImpactLevel.NotMeasured);
            int points = Math.Min(20,high * 4);
            if (!measured) return new(HealthNode.Startup,HealthArea.Startup,HealthState.Unavailable,
                $"{enabled.Length} known enabled startup entries","Startup impact coverage is incomplete. Open Startup to load the existing catalog; unknown impact is not treated as low impact.",20,
                Recommendation:"Review startup impact in Startup. No startup entries or delay tasks were changed.");
            return new(HealthNode.Startup,HealthArea.Startup,high > 0 ? HealthState.OptimizationAvailable : HealthState.Healthy,
                $"{high} high-impact startup app(s)",$"{high} enabled startup apps have measured high impact; policy deducts 4 points each, capped at 20.",
                20,points,high > 0 ? "Review high-impact entries in Startup; this scan changes nothing." : null);
        }),
        SnapshotHealthProviders.One(HealthNode.Cleanup,HealthArea.Maintenance,10,c =>
            new(HealthNode.Cleanup,HealthArea.Maintenance,HealthState.Unavailable,"Cleanup preview available",
                "Cleanup opportunity has not been measured. This health scan does not enumerate personal files or run cleanup; no maintenance points are assumed.",10,
                Recommendation:"Use the existing Cleanup preview to review opportunities. No cleanup estimate was invented.")),
        new DelegateHealthProvider([new(HealthNode.Updates,HealthArea.Updates,15)],(_,t)=>ReadAsync(ReadUpdates,t)),
        new DelegateHealthProvider([new(HealthNode.Security,HealthArea.Security,25)],(_,t)=>ReadAsync(ReadSecurity,t))
    ];
    private static async Task<ImmutableArray<HealthCheckItem>> ReadAsync(Func<HealthCheckItem> read, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        // Track native reads to completion, including after cancellation, so no worker owns abandoned resources.
        var result = await Task.Run(() => { token.ThrowIfCancellationRequested(); var item = read(); token.ThrowIfCancellationRequested(); return item; }, CancellationToken.None).ConfigureAwait(false);
        token.ThrowIfCancellationRequested(); return [result];
    }
    private static HealthCheckItem ReadUpdates()
    {
        using var reboot = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsUpdate\Auto Update\RebootRequired", false);
        using var results = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsUpdate\Auto Update\Results\Install", false);
        string cached = results?.GetValue("LastSuccessTime") is string time ? $" Cached last successful installation: {time}." : "";
        if (reboot != null) return new(HealthNode.Updates, HealthArea.Updates, HealthState.Attention, "Update restart pending",
            "Windows Update has a local pending-restart marker. This does not determine whether further updates are available." + cached, 15, 2,
            "Review Windows Update and choose when to restart; this scan does not restart or search online.");
        return SnapshotHealthProviders.Missing(HealthNode.Updates, HealthArea.Updates, 15,
            "No pending-restart marker found. Installed-update completeness cannot be verified from the local cache." + cached);
    }
    private static HealthCheckItem ReadSecurity()
    {
        var scope = new ManagementScope(@"\\.\root\Microsoft\Windows\Defender",
            new ConnectionOptions { Timeout = TimeSpan.FromSeconds(3) });
        using var searcher = new ManagementObjectSearcher(scope,
            new ObjectQuery("SELECT AMRunningMode, AntivirusEnabled, RealTimeProtectionEnabled, AntivirusSignatureAge FROM MSFT_MpComputerStatus"),
            new System.Management.EnumerationOptions { Timeout = TimeSpan.FromSeconds(3), ReturnImmediately = false });
        using var results = searcher.Get();
        foreach (ManagementObject result in results)
        {
            using (result)
            {
                if (!string.Equals(result["AMRunningMode"] as string, "Normal", StringComparison.OrdinalIgnoreCase))
                    return SnapshotHealthProviders.Missing(HealthNode.Security, HealthArea.Security, 25, "Defender is not in normal active mode. Third-party protection is not inferred.");
                if (result["AntivirusEnabled"] is not bool enabled || result["RealTimeProtectionEnabled"] is not bool realtime ||
                    result["AntivirusSignatureAge"] == null)
                    return SnapshotHealthProviders.Missing(HealthNode.Security, HealthArea.Security, 25, "Defender posture fields are incomplete.");
                uint age = Convert.ToUInt32(result["AntivirusSignatureAge"], System.Globalization.CultureInfo.InvariantCulture);
                bool disabled = !enabled || !realtime; int points = disabled ? 15 : age > 7 ? 5 : 0;
                return new(HealthNode.Security, HealthArea.Security, disabled ? HealthState.Error : age > 7 ? HealthState.Attention : HealthState.Healthy,
                    disabled ? "Active Defender protection disabled" : $"Defender signatures {age} day(s) old",
                    disabled ? "Defender reports normal active mode with antivirus or real-time protection disabled."
                    : age > 7 ? "Active Defender signatures are more than 7 days old." : "Active Defender antivirus and real-time protection are enabled; signatures are at most 7 days old. Firewall and third-party products are not assessed.",
                    25, points, points > 0 ? "Review protection status in Windows Security. No security setting was changed." : null);
            }
        }
        return SnapshotHealthProviders.Missing(HealthNode.Security, HealthArea.Security, 25, "Local Defender posture is unavailable. No security conclusion is made.");
    }
}
