using System.Diagnostics.Eventing.Reader;
using System.IO;
using System.Xml.Linq;
using Microsoft.Win32;
using CortexDNA.Models;

namespace CortexDNA.Core.Startup
{
    /// <summary>
    /// Reads boot duration and per-app startup cost from Windows event logs
    /// (Diagnostics-Performance and Shell-Core), matching Task Manager's idea
    /// of High / Medium / Low / Not measured.
    /// </summary>
    public sealed class StartupImpactService
    {
        private const string PerfLog = "Microsoft-Windows-Diagnostics-Performance/Operational";
        private const string ShellLog = "Microsoft-Windows-Shell-Core/Operational";

        public void Apply(StartupSnapshotBuilder builder, IReadOnlyList<StartupItem> items)
        {
            ReadBiosTime(builder);

            try
            {
                ReadBootSummary(builder);
            }
            catch (Exception ex)
            {
                Logger.Log($"Startup boot log read failed: {ex.Message}");
                builder.DiagnosticsNote = "Windows has not recorded boot diagnostics yet.";
            }

            try
            {
                var durations = ReadAppDurations();
                ApplyDurations(items, durations);
            }
            catch (Exception ex)
            {
                Logger.Log($"Startup app impact read failed: {ex.Message}");
            }
        }

        private static void ReadBiosTime(StartupSnapshotBuilder builder)
        {
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Session Manager\Power");
                object? raw = key?.GetValue("FwPOSTTime");
                int ms = raw switch
                {
                    int i => i,
                    uint u => unchecked((int)u),
                    long l => (int)l,
                    _ => 0
                };
                if (ms > 0)
                    builder.LastBiosDuration = TimeSpan.FromMilliseconds(ms);
            }
            catch (Exception ex)
            {
                Logger.Log($"BIOS time read failed: {ex.Message}");
            }
        }

        private static void ReadBootSummary(StartupSnapshotBuilder builder)
        {
            if (!EventLogExists(PerfLog))
            {
                builder.DiagnosticsNote = "Boot diagnostics log is not available on this PC.";
                return;
            }

            string query = "*[System[(EventID=100)]]";
            using var reader = CreateReader(PerfLog, query);
            using EventRecord? rec = reader.ReadEvent();
            if (rec == null)
            {
                builder.DiagnosticsNote = "No boot measurements yet. Windows fills this after a few restarts.";
                return;
            }

            string xml = rec.ToXml();
            builder.LastBootTime = rec.TimeCreated;

            double? bootMs = ReadNamedNumber(xml, "BootTime") ?? ReadNamedNumber(xml, "MainPathBootTime");
            if (bootMs.HasValue && bootMs.Value > 0)
                builder.LastBootDuration = TimeSpan.FromMilliseconds(bootMs.Value);
        }

        private static Dictionary<string, double> ReadAppDurations()
        {
            var map = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
            if (!EventLogExists(ShellLog))
                return map;

            // Recent auto-start related events. IDs vary by Windows build, so we
            // scan a bounded window and keep the longest duration per executable.
            string query = "*[System[TimeCreated[timediff(@SystemTime) <= 2592000000]]]";
            using var reader = CreateReader(ShellLog, query);

            int scanned = 0;
            EventRecord? rec;
            while ((rec = reader.ReadEvent()) != null && scanned < 400)
            {
                scanned++;
                using (rec)
                {
                    string xml = rec.ToXml();
                    double? ms = ReadNamedNumber(xml, "Duration")
                        ?? ReadNamedNumber(xml, "ElapsedTime")
                        ?? ReadNamedNumber(xml, "TimeTaken")
                        ?? ReadNamedNumber(xml, "TotalTime");
                    if (ms == null || ms <= 0 || ms > 180_000)
                        continue;

                    string? exe = ReadNamedString(xml, "Name")
                        ?? ReadNamedString(xml, "ProcessName")
                        ?? ReadNamedString(xml, "FileName")
                        ?? ReadNamedString(xml, "ImageName")
                        ?? ReadNamedString(xml, "CommandLine");

                    string? key = NormalizeExe(exe);
                    if (key == null) continue;

                    if (!map.TryGetValue(key, out double existing) || ms.Value > existing)
                        map[key] = ms.Value;
                }
            }

            return map;
        }

        private static void ApplyDurations(IReadOnlyList<StartupItem> items, Dictionary<string, double> durations)
        {
            if (durations.Count == 0) return;

            foreach (var item in items)
            {
                string? key = NormalizeExe(item.ExecutablePath) ?? NormalizeExe(item.Name);
                if (key == null) continue;
                if (!durations.TryGetValue(key, out double ms)) continue;

                item.DurationMs = ms;
                item.Impact = Classify(ms);
            }
        }

        internal static StartupImpactLevel Classify(double durationMs)
        {
            if (durationMs >= 1000) return StartupImpactLevel.High;
            if (durationMs >= 300) return StartupImpactLevel.Medium;
            return StartupImpactLevel.Low;
        }

        private static EventLogReader CreateReader(string log, string xpath)
        {
            var q = new EventLogQuery(log, PathType.LogName, xpath) { ReverseDirection = true };
            return new EventLogReader(q);
        }

        private static bool EventLogExists(string log)
        {
            try
            {
                EventLogSession.GlobalSession.GetLogInformation(log, PathType.LogName);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static double? ReadNamedNumber(string xml, string name)
        {
            string? text = ReadNamedString(xml, name);
            if (string.IsNullOrWhiteSpace(text)) return null;
            return double.TryParse(text, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out double value)
                ? value
                : null;
        }

        private static string? ReadNamedString(string xml, string name)
        {
            try
            {
                var doc = XDocument.Parse(xml);
                XNamespace ns = "http://schemas.microsoft.com/win/2004/08/events/event";
                var node = doc.Descendants(ns + "Data")
                    .FirstOrDefault(e => string.Equals((string?)e.Attribute("Name"), name, StringComparison.OrdinalIgnoreCase));
                string? value = node?.Value?.Trim();
                return string.IsNullOrWhiteSpace(value) ? null : value;
            }
            catch
            {
                return null;
            }
        }

        internal static string? NormalizeExe(string? pathOrName)
        {
            if (string.IsNullOrWhiteSpace(pathOrName))
                return null;

            string trimmed = pathOrName.Trim().Trim('"');
            string file = trimmed;
            try { file = Path.GetFileName(trimmed); } catch { }

            if (string.IsNullOrWhiteSpace(file))
                return null;

            return Path.GetFileNameWithoutExtension(file).ToLowerInvariant();
        }
    }

    public sealed class StartupSnapshotBuilder
    {
        public TimeSpan? LastBootDuration { get; set; }
        public TimeSpan? LastBiosDuration { get; set; }
        public DateTime? LastBootTime { get; set; }
        public string? DiagnosticsNote { get; set; }
    }
}
