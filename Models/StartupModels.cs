namespace CortexDNA.Models
{
    public enum AppSection
    {
        Overview,
        Startup
    }

    public enum StartupLocationKind
    {
        CurrentUserRun,
        LocalMachineRun,
        LocalMachineRun32,
        UserStartupFolder,
        CommonStartupFolder,
        PackagedApp
    }

    public enum StartupImpactLevel
    {
        NotMeasured,
        Low,
        Medium,
        High
    }

    public sealed class StartupItem
    {
        public required string Id { get; init; }
        public required string Name { get; init; }
        public required string Command { get; init; }
        public string ExecutablePath { get; init; } = string.Empty;
        public string IconPath { get; init; } = string.Empty;
        public bool IsRunning { get; set; }
        public StartupLocationKind Location { get; init; }
        public required string LocationLabel { get; init; }
        public string ApprovalValueName { get; init; } = string.Empty;
        public bool IsEnabled { get; set; } = true;
        public bool IsDelayed { get; set; }
        public StartupImpactLevel Impact { get; set; } = StartupImpactLevel.NotMeasured;
        public double? DurationMs { get; set; }
        public string Publisher { get; set; } = string.Empty;
        public bool CanModify { get; set; } = true;
        public string PackageFamilyName { get; init; } = string.Empty;
        public string StateRegistryPath { get; set; } = string.Empty;
    }

    public sealed class StartupSnapshot
    {
        public IReadOnlyList<StartupItem> Items { get; init; } = Array.Empty<StartupItem>();
        public TimeSpan? LastBootDuration { get; init; }
        public TimeSpan? LastBiosDuration { get; init; }
        public DateTime? LastBootTime { get; init; }
        public string? DiagnosticsNote { get; init; }
    }
}
