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

    public IEnumerable<IHardware> Hardware => _computer.Hardware;
    public void Open() => _computer.Open();
    public void Refresh() => _computer.Accept(new UpdateVisitor());
    public void Close() => _computer.Close();
}