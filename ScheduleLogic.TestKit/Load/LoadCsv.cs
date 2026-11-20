using System.Globalization;
using System.Text;
using ScheduleLogic.TestKit.Reporting;

namespace ScheduleLogic.TestKit.Load;

public static class LoadCsv
{
    public static CultureInfo Culture { get; set; } = CultureInfo.CurrentCulture;

    // Local list separator and decimal mark, so the CSV opens directly in Excel.
    private static string Sep => Culture.TextInfo.ListSeparator;

        public static string SaveRun(IReadOnlyList<LoadResult> results, string? folder = null)
    {
        if (results.Count == 0) throw new ArgumentException("There is no result to save.");
        var first = results[0].Summary;
        folder ??= ReportStore.LoadResultsFolder;
        var dir = Path.Combine(folder, $"{first.StartedAt:yyyyMMdd-HHmmss}_{ReportStore.SafeName(first.Label)}_{first.Scenario}_{first.Mode}");
        Directory.CreateDirectory(dir);

        WriteSummaries(Path.Combine(dir, "summary.csv"), results.Select(r => r.Summary));
        foreach (var r in results)
        {
            WriteTimeline(Path.Combine(dir, $"timeline_level{r.Summary.Level}.csv"), r);
            WriteSamples(Path.Combine(dir, $"requests_level{r.Summary.Level}.csv"), r);
        }
        return dir;
    }

    public static void WriteSummaries(string path, IEnumerable<LoadSummary> summaries)
    {
        var sb = new StringBuilder();
        sb.AppendLine(Row("Backend", "URL", "Started", "Scenario", "Mode", "Level", "Duration (s)",
            "Users", "Event size", "Requests", "Succeeded", "Failed", "Error rate (%)", "Dropped by client",
            "Requests/s", "Mean (ms)", "p50 (ms)", "p75 (ms)", "p90 (ms)", "p95 (ms)", "p99 (ms)", "Max (ms)",
            "Service p95 (ms)", "Idle memory (MB)", "CPU avg (%)", "CPU max (%)",
            "Memory avg (MB)", "Memory max (MB)", "Status codes"));
        foreach (var s in summaries)
        {
            sb.AppendLine(Row(s.Label, s.BaseUrl, s.StartedAt.ToString("yyyy-MM-dd HH:mm:ss"), s.Scenario.ToString(),
                s.Mode == LoadMode.Closed ? "closed" : "open", N(s.Level), N(s.DurationS), N(s.FixtureUsers),
                N(s.EventParticipants), N(s.Total), N(s.Ok), N(s.Errors), N(Math.Round(s.ErrorRatePercent, 2)),
                N(s.ClientDropped), N(s.Rps), N(s.MeanMs), N(s.P50Ms), N(s.P75Ms), N(s.P90Ms), N(s.P95Ms), N(s.P99Ms),
                N(s.MaxMs), N(s.ServiceP95Ms), N(s.IdleMemoryMb), N(s.CpuAvgPercent), N(s.CpuMaxPercent),
                N(s.MemoryAvgMb), N(s.MemoryMaxMb), s.StatusCounts));
        }
        File.WriteAllText(path, sb.ToString(), new UTF8Encoding(true));
    }

    public static void WriteTimeline(string path, LoadResult r)
    {
        var sb = new StringBuilder();
        sb.AppendLine(Row("Second", "Warm-up", "Requests", "Errors", "p50 (ms)", "p95 (ms)", "Max (ms)",
            "In flight", "CPU (%)", "Memory (MB)"));
        foreach (var p in r.Timeline)
            sb.AppendLine(Row(N(p.Second), p.WarmUp ? "yes" : "no", N(p.Requests), N(p.Errors), N(p.P50Ms),
                N(p.P95Ms), N(p.MaxMs), N(p.InFlight), N(p.CpuPercent), N(p.MemoryMb)));
        File.WriteAllText(path, sb.ToString(), new UTF8Encoding(true));
    }

    public static void WriteSamples(string path, LoadResult r)
    {
        var sb = new StringBuilder();
        sb.AppendLine(Row("Operation", "Scheduled (ms)", "Sent (ms)", "Response (ms)", "Latency (ms)",
            "Service (ms)", "Status", "Succeeded", "Warm-up"));
        foreach (var s in r.Samples)
            sb.AppendLine(Row(s.Op.ToString(), N(Math.Round(s.ScheduledMs, 2)), N(Math.Round(s.StartMs, 2)),
                N(Math.Round(s.EndMs, 2)), N(Math.Round(s.LatencyMs, 2)), N(Math.Round(s.ServiceMs, 2)),
                N(s.Status), s.Ok ? "yes" : "no", s.WarmUp ? "yes" : "no"));
        File.WriteAllText(path, sb.ToString(), new UTF8Encoding(true));
    }

    private static string N(double? v) => v?.ToString(Culture) ?? "";
    private static string N(int v) => v.ToString(Culture);

    private static string Row(params string[] cells) =>
        string.Join(Sep, cells.Select(c =>
            c.Contains(Sep) || c.Contains('"') || c.Contains('\n') ? "\"" + c.Replace("\"", "\"\"") + "\"" : c));
}
