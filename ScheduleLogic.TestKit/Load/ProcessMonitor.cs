using System.Diagnostics;

namespace ScheduleLogic.TestKit.Load;

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
