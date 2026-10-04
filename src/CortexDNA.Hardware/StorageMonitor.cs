using System.Collections.Immutable;
using CortexDNA.Models;
namespace CortexDNA.Hardware;
/// <summary>Capacity metadata only. Never enumerates files or directories.</summary>
public sealed class StorageMonitor
{
    public ImmutableArray<StorageSnapshot> Read(CancellationToken token)
    {
        var result = ImmutableArray.CreateBuilder<StorageSnapshot>();
        try { foreach (var drive in DriveInfo.GetDrives()) { token.ThrowIfCancellationRequested(); try { if (drive.IsReady) result.Add(new(drive.Name, drive.VolumeLabel, drive.TotalSize, drive.AvailableFreeSpace)); } catch { result.Add(new(drive.Name, "Unavailable", null, null)); } } }
        catch (OperationCanceledException) { throw; }
        catch { }
        return result.ToImmutable();
    }
}
