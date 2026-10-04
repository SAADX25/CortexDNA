using CortexDNA.Core;
using LibreHardwareMonitor.Hardware;

namespace CortexDNA.Hardware;

/// <summary>
/// Phase 1 vendor adapter. Caller retains the existing lock and shutdown ordering.
/// Sensor snapshots and polling separation belong to Phase 3.
/// </summary>
public interface IHardwareSession
{
    IEnumerable<IHardware> Hardware { get; }
    void Open();
    void Refresh();
    void Refresh(CancellationToken token) { token.ThrowIfCancellationRequested(); Refresh(); token.ThrowIfCancellationRequested(); }
    bool IsDeviceAvailable(string id) => true;
    void Close();
}

public sealed class HardwareSession : IHardwareSession
{
    private readonly Computer _computer = new()
    {
        IsCpuEnabled = true,
        IsGpuEnabled = true,
        IsMemoryEnabled = false,
        IsMotherboardEnabled = false,
        IsControllerEnabled = false,
        IsNetworkEnabled = false,
        IsStorageEnabled = false
    };

    private readonly HashSet<string> _failedDevices = new(StringComparer.Ordinal);
    public IEnumerable<IHardware> Hardware => _computer.Hardware;
    public bool IsDeviceAvailable(string id) => !_failedDevices.Contains(id);
    public void Open() => _computer.Open();
    public void Refresh() => Refresh(CancellationToken.None);
    public void Refresh(CancellationToken token)
    {
        foreach (var hardware in _computer.Hardware) RefreshDevice(hardware, token);
    }
    private void RefreshDevice(IHardware hardware, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        try { hardware.Update(); _failedDevices.Remove(hardware.Identifier.ToString()); } catch { _failedDevices.Add(hardware.Identifier.ToString()); }
        token.ThrowIfCancellationRequested();
        foreach (var child in hardware.SubHardware) RefreshDevice(child, token);
    }
    public void Close() => _computer.Close();
}