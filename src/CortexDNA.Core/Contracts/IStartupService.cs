using CortexDNA.Models;

namespace CortexDNA.Core.Startup;

public interface IStartupService
{
    Task<StartupSnapshot> LoadAsync(bool migrateLegacy = true);
    StartupSnapshot Load(bool migrateLegacy = true);
    void SetEnabled(StartupItem item, bool enabled);
    void Delay(StartupItem item);
    void RemoveDelay(StartupItem item);
}