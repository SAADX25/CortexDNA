using System.Management;
using System.Security.Principal;
using System.Text.Json;
using CortexDNA.Models;
namespace CortexDNA.Hardware;
/// <summary>Static specifications only; the legacy specification cache contains no sensor history.</summary>
public sealed class HardwareInfoService
{
    private readonly string _path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CortexDNA", "specs.json");
    private static bool IsAdmin()
    { try { using var identity = WindowsIdentity.GetCurrent(); return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator); } catch { return false; } }
    public HardwareInfoSnapshot ReadCached()
    { try { return (File.Exists(_path) ? JsonSerializer.Deserialize<HardwareInfoSnapshot>(File.ReadAllText(_path)) ?? new HardwareInfoSnapshot() : new HardwareInfoSnapshot()) with { IsAdmin = IsAdmin() }; } catch { return new HardwareInfoSnapshot() with { IsAdmin = IsAdmin() }; } }
    private static string? Query(string type, string property, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        try { using var query = new ManagementObjectSearcher($"SELECT {property} FROM {type}"); query.Options.Timeout = TimeSpan.FromSeconds(5); using var results = query.Get(); foreach (ManagementObject obj in results) { using (obj) { token.ThrowIfCancellationRequested(); return obj[property]?.ToString()?.Trim(); } } }
        catch (OperationCanceledException) { throw; }
        catch { }
        return null;
    }
    private static string? PreferredGpuName(CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        try
        {
            var gpus = new List<(string Name, long Vram)>();
            using var query = new ManagementObjectSearcher("SELECT Name, AdapterRAM FROM Win32_VideoController");
            query.Options.Timeout = TimeSpan.FromSeconds(5);
            using var results = query.Get();
            foreach (ManagementObject obj in results)
            {
                using (obj)
                {
                    token.ThrowIfCancellationRequested();
                    var name = obj["Name"]?.ToString() ?? "Unknown GPU";
                    long.TryParse(obj["AdapterRAM"]?.ToString(), out var vram); gpus.Add((name, vram));
                }
            }
            return gpus.OrderByDescending(g => g.Vram)
                .ThenByDescending(g => g.Name.Contains("NVIDIA") || g.Name.Contains("AMD") || g.Name.Contains("Radeon"))
                .ThenByDescending(g => g.Name.Length).FirstOrDefault().Name;
        }
        catch (OperationCanceledException) { throw; }
        catch { return null; }
    }
    public HardwareInfoSnapshot Read(CancellationToken token)
    {
        var cached = ReadCached(); var os = Query("Win32_OperatingSystem", "Caption", token); var build = Query("Win32_OperatingSystem", "BuildNumber", token);
        var version = Query("Win32_BIOS", "SMBIOSBIOSVersion", token); var manufacturer = Query("Win32_BIOS", "Manufacturer", token); var date = Query("Win32_BIOS", "ReleaseDate", token);
        if (date?.Length >= 8) date = $"{date[..4]}-{date.Substring(4, 2)}-{date.Substring(6, 2)}";
        var board = Query("Win32_BaseBoard", "Product", token); var maker = Query("Win32_BaseBoard", "Manufacturer", token);
        var cpu = Query("Win32_Processor", "Name", token); var gpu = PreferredGpuName(token);
        double.TryParse(Query("Win32_ComputerSystem", "TotalPhysicalMemory", token), out var capacity); var speed = Query("Win32_PhysicalMemory", "Speed", token);
        bool admin = false; try { using var identity = WindowsIdentity.GetCurrent(); admin = new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator); } catch { }
        var info = new HardwareInfoSnapshot(os == null ? cached.OsName : $"{os} (Build {build ?? "N/A"})", version == null ? cached.BiosInfo : $"{manufacturer} (v{version})", board == null ? cached.MotherboardModel : $"{maker} {board}", version ?? cached.BiosVersion, date ?? cached.BiosDate, cpu ?? cached.CpuName, gpu ?? cached.GpuName, capacity > 0 ? $"{capacity / (1024d * 1024 * 1024):F1} GB" + (speed == null ? "" : $" @ {speed} MHz") : cached.RamInfo, capacity > 0 ? $"{capacity / (1024d * 1024 * 1024):F1} GB" : cached.RamTotal, speed == null ? cached.RamType : $"{speed} MHz", capacity > 0 ? capacity : cached.TotalRamBytes, admin);
        token.ThrowIfCancellationRequested();
        try { Directory.CreateDirectory(Path.GetDirectoryName(_path)!); File.WriteAllText(_path + ".tmp", JsonSerializer.Serialize(info)); token.ThrowIfCancellationRequested(); File.Move(_path + ".tmp", _path, true); } catch (OperationCanceledException) { throw; } catch { }
        return info;
    }
}
