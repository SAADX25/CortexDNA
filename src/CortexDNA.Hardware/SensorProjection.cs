using System.Collections.Immutable;
using LibreHardwareMonitor.Hardware;
namespace CortexDNA.Hardware;

public sealed record SensorReading(string Name, string Kind, double? Value);
public sealed record DeviceReading(string Id, string Name, string Kind, ImmutableArray<SensorReading> Sensors);
internal static class SensorProjection
{
    public static ImmutableArray<DeviceReading> Read(IHardwareSession session, CancellationToken token)
    {
        var result = ImmutableArray.CreateBuilder<DeviceReading>();
        foreach (var hardware in session.Hardware)
        {
            token.ThrowIfCancellationRequested();
            try
            {
                string kind = hardware.HardwareType == HardwareType.Cpu ? "CPU" : hardware.HardwareType is HardwareType.GpuAmd or HardwareType.GpuNvidia or HardwareType.GpuIntel ? "GPU" : "Other";
                if (kind == "Other") continue;
                if (!session.IsDeviceAvailable(hardware.Identifier.ToString()))
                { result.Add(new(hardware.Identifier.ToString(), hardware.Name, kind, [])); continue; }
                var sensors = ImmutableArray.CreateBuilder<SensorReading>();
                foreach (var sensor in hardware.Sensors)
                { token.ThrowIfCancellationRequested(); try { double? value = sensor.Value; sensors.Add(new(sensor.Name, sensor.SensorType.ToString(), value.HasValue && double.IsFinite(value.Value) ? value : null)); } catch (OperationCanceledException) { throw; } catch { } }
                result.Add(new(hardware.Identifier.ToString(), hardware.Name, kind, sensors.ToImmutable()));
            }
            catch (OperationCanceledException) { throw; }
            catch { }
        }
        return result.ToImmutable();
    }
    internal static double? Value(DeviceReading device, string kind, string? preferred = null)
    {
        var sensor = preferred == null ? device.Sensors.FirstOrDefault(s => s.Kind == kind) : device.Sensors.FirstOrDefault(s => s.Kind == kind && s.Name == preferred);
        var value = sensor?.Value; return value.HasValue && double.IsFinite(value.Value) ? value : null;
    }
}
