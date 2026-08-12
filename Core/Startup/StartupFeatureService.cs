using CortexDNA.Models;

namespace CortexDNA.Core.Startup
{
    /// <summary>
    /// Facade for the Startup Impact feature. Keeps catalog, approval,
    /// delay, and Windows log impact behind one entry point.
    /// </summary>
    public sealed class StartupFeatureService
    {
        private readonly StartupCatalogService _catalog = new();
        private readonly StartupApprovalService _approval = new();
        private readonly StartupDelayService _delay = new();
        private readonly StartupImpactService _impact = new();

        public Task<StartupSnapshot> LoadAsync()
        {
            return Task.Run(Load);
        }

        public StartupSnapshot Load()
        {
            var items = _catalog.Enumerate().ToList();
            _approval.ApplyState(items);
            StartupPackagedCatalog.ApplyState(items);
            _delay.ApplyState(items);
            StartupProcessProbe.Apply(items);

            var builder = new StartupSnapshotBuilder();
            _impact.Apply(builder, items);

            return new StartupSnapshot
            {
                Items = items
                    .OrderByDescending(i => i.IsEnabled)
                    .ThenBy(i => i.Name, StringComparer.OrdinalIgnoreCase)
                    .ToList(),
                LastBootDuration = builder.LastBootDuration,
                LastBiosDuration = builder.LastBiosDuration,
                LastBootTime = builder.LastBootTime,
                DiagnosticsNote = builder.DiagnosticsNote
            };
        }

        public void SetEnabled(StartupItem item, bool enabled)
        {
            if (enabled && item.IsDelayed)
                _delay.RemoveDelay(item);

            _approval.SetEnabled(item, enabled);
        }

        public void Delay(StartupItem item)
        {
            _delay.Delay(item);
            _approval.SetEnabled(item, false);
        }

        public void RemoveDelay(StartupItem item)
        {
            _delay.RemoveDelay(item);
            _approval.SetEnabled(item, true);
        }
    }
}
