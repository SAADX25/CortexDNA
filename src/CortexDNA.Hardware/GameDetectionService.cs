using System.Diagnostics;
namespace CortexDNA.Hardware;
/// <summary>Preserves the existing game allowlist and this application's own priority reduction.</summary>
public sealed class GameDetectionService(TimeProvider? time = null) : IDisposable
{
    private static readonly HashSet<string> Games = new(["cs2", "valorant-win64-shipping", "vgc", "r5apex", "fortnite-win64-shipping", "cod", "gta5", "overwatch"], StringComparer.OrdinalIgnoreCase);
    private readonly TimeProvider _time = time ?? TimeProvider.System;
    private long _last; private bool _sampled, _game; private ProcessPriorityClass? _original;
    public bool Read(CancellationToken token)
    {
        token.ThrowIfCancellationRequested(); long now = _time.GetTimestamp();
        if (_sampled && _time.GetElapsedTime(_last, now) < TimeSpan.FromSeconds(5)) return _game;
        _sampled = true; _last = now; bool detected = false;
        Process[] processes = [];
        try { processes = Process.GetProcesses(); foreach (var process in processes) { token.ThrowIfCancellationRequested(); try { if (Games.Contains(process.ProcessName)) { detected = true; break; } } catch { } } }
        catch (OperationCanceledException) { throw; }
        catch { }
        finally { foreach (var process in processes) process.Dispose(); }
        if (detected != _game) { _game = detected; try { using var current = Process.GetCurrentProcess(); if (_game) { _original = current.PriorityClass; current.PriorityClass = ProcessPriorityClass.BelowNormal; } else if (_original.HasValue) { current.PriorityClass = _original.Value; _original = null; } } catch { } }
        return _game;
    }
    public void Dispose() { if (_original.HasValue) try { using var current = Process.GetCurrentProcess(); current.PriorityClass = _original.Value; } catch { } finally { _original = null; } }
}
