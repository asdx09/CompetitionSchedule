using System.Diagnostics;

namespace ScheduleLogic.TestKit.Load;

/// <summary>
/// A szerver folyamatainak CPU- és memóriahasználata (ugyanazon a gépen).
/// Név szerint figyel, és összeadja az azonos nevű folyamatokat, mert a PHP
/// több munkafolyamattal fut (php-fpm / php-cgi), a .NET pedig eggyel
/// (ScheduleLogic.Server vagy dotnet).
///
/// A CPU% a teljes gépre vetített érték (100% = minden mag teljesen
/// terhelve); a magok számával szorozva kapjuk, hány magnyi munkát végzett.
/// </summary>
public sealed class ProcessMonitor
{
    private readonly string[] _names;
    private readonly Dictionary<int, TimeSpan> _lastCpu = new();
    private readonly Stopwatch _wall = Stopwatch.StartNew();
    private TimeSpan _lastWall;

    public ProcessMonitor(IEnumerable<string> processNames)
    {
        _names = processNames
            .Select(n => n.Trim())
            .Where(n => n.Length > 0)
            .Select(n => n.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? n[..^4] : n)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public IReadOnlyList<string> Names => _names;

    public static ProcessMonitor? FromList(string? commaSeparated)
    {
        var names = (commaSeparated ?? "").Split(',', ';').Where(s => s.Trim().Length > 0).ToList();
        return names.Count == 0 ? null : new ProcessMonitor(names);
    }

    /// <summary>
    /// Pillanatnyi memória és az előző hívás óta eltelt idő átlagos CPU-ja.
    /// Az első hívás CPU-értéke 0 (nincs mihez viszonyítani).
    /// </summary>
    public ResourceSample Sample()
    {
        var now = _wall.Elapsed;
        double wallMs = (now - _lastWall).TotalMilliseconds;
        _lastWall = now;

        double cpuMs = 0, memBytes = 0;
        int count = 0;
        var seen = new HashSet<int>();

        foreach (var name in _names)
        {
            Process[] procs;
            try { procs = Process.GetProcessesByName(name); }
            catch { continue; }

            foreach (var p in procs)
            {
                using (p)
                {
                    // A mérőprogram maga is lehet "dotnet" nevű folyamat: azt nem számoljuk.
                    if (p.Id == Environment.ProcessId) continue;
                    try
                    {
                        var cpu = p.TotalProcessorTime;
                        memBytes += p.WorkingSet64;
                        count++;
                        seen.Add(p.Id);
                        if (_lastCpu.TryGetValue(p.Id, out var prev))
                            cpuMs += (cpu - prev).TotalMilliseconds;
                        _lastCpu[p.Id] = cpu;
                    }
                    catch
                    {
                        // Közben kilépett, vagy nincs jogunk olvasni (pl. más felhasználó szolgáltatása).
                    }
                }
            }
        }

        foreach (var gone in _lastCpu.Keys.Where(id => !seen.Contains(id)).ToList())
            _lastCpu.Remove(gone);

        double cpuPercent = wallMs <= 0 ? 0 : 100.0 * cpuMs / (wallMs * Environment.ProcessorCount);
        return new ResourceSample(Math.Round(cpuPercent, 1), Math.Round(memBytes / 1024 / 1024, 1), count);
    }
}
