using System.Globalization;
using System.Text;
using ScheduleLogic.TestKit.Reporting;

namespace ScheduleLogic.TestKit.Load;

/// <summary>
/// Mérési eredmények CSV-be, Excelben közvetlenül megnyitható formában: a
/// gép területi beállításai szerinti elválasztóval és tizedesjellel (magyar
/// Windowson ';' és ','), UTF-8 BOM-mal, hogy az ékezetek is jók legyenek.
/// </summary>
public static class LoadCsv
{
    public static CultureInfo Culture { get; set; } = CultureInfo.CurrentCulture;

    private static string Sep => Culture.TextInfo.ListSeparator;

    /// <summary>A sorozat összes szintjének összesítése egy fájlba; visszaadja a mappát.</summary>
    public static string SaveRun(IReadOnlyList<LoadResult> results, string? folder = null)
    {
        if (results.Count == 0) throw new ArgumentException("Nincs menthető eredmény.");
        var first = results[0].Summary;
        folder ??= ReportStore.LoadResultsFolder;
        var dir = Path.Combine(folder, $"{first.StartedAt:yyyyMMdd-HHmmss}_{ReportStore.SafeName(first.Label)}_{first.Scenario}_{first.Mode}");
        Directory.CreateDirectory(dir);

        WriteSummaries(Path.Combine(dir, "osszesites.csv"), results.Select(r => r.Summary));
        foreach (var r in results)
        {
            WriteTimeline(Path.Combine(dir, $"idosor_szint{r.Summary.Level}.csv"), r);
            WriteSamples(Path.Combine(dir, $"keresek_szint{r.Summary.Level}.csv"), r);
        }
        return dir;
    }

    public static void WriteSummaries(string path, IEnumerable<LoadSummary> summaries)
    {
        var sb = new StringBuilder();
        sb.AppendLine(Row("Backend", "URL", "Kezdés", "Forgatókönyv", "Mód", "Szint", "Időtartam (s)",
            "Felhasználók", "Verseny mérete", "Kérések", "Sikeres", "Hibás", "Hibaarány (%)", "Kliens által eldobott",
            "Kérés/s", "Átlag (ms)", "p50 (ms)", "p75 (ms)", "p90 (ms)", "p95 (ms)", "p99 (ms)", "Max (ms)",
            "Kiszolgálási p95 (ms)", "Tétlen memória (MB)", "CPU átlag (%)", "CPU max (%)",
            "Memória átlag (MB)", "Memória max (MB)", "Státuszkódok"));
        foreach (var s in summaries)
        {
            sb.AppendLine(Row(s.Label, s.BaseUrl, s.StartedAt.ToString("yyyy-MM-dd HH:mm:ss"), s.Scenario.ToString(),
                s.Mode == LoadMode.Closed ? "zárt" : "nyitott", N(s.Level), N(s.DurationS), N(s.FixtureUsers),
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
        sb.AppendLine(Row("Másodperc", "Bemelegítés", "Kérések", "Hibák", "p50 (ms)", "p95 (ms)", "Max (ms)",
            "Úton lévő kérések", "CPU (%)", "Memória (MB)"));
        foreach (var p in r.Timeline)
            sb.AppendLine(Row(N(p.Second), p.WarmUp ? "igen" : "nem", N(p.Requests), N(p.Errors), N(p.P50Ms),
                N(p.P95Ms), N(p.MaxMs), N(p.InFlight), N(p.CpuPercent), N(p.MemoryMb)));
        File.WriteAllText(path, sb.ToString(), new UTF8Encoding(true));
    }

    public static void WriteSamples(string path, LoadResult r)
    {
        var sb = new StringBuilder();
        sb.AppendLine(Row("Művelet", "Tervezett (ms)", "Elküldve (ms)", "Válasz (ms)", "Késleltetés (ms)",
            "Kiszolgálás (ms)", "Státusz", "Sikeres", "Bemelegítés"));
        foreach (var s in r.Samples)
            sb.AppendLine(Row(s.Op.ToString(), N(Math.Round(s.ScheduledMs, 2)), N(Math.Round(s.StartMs, 2)),
                N(Math.Round(s.EndMs, 2)), N(Math.Round(s.LatencyMs, 2)), N(Math.Round(s.ServiceMs, 2)),
                N(s.Status), s.Ok ? "igen" : "nem", s.WarmUp ? "igen" : "nem"));
        File.WriteAllText(path, sb.ToString(), new UTF8Encoding(true));
    }

    private static string N(double? v) => v?.ToString(Culture) ?? "";
    private static string N(int v) => v.ToString(Culture);

    private static string Row(params string[] cells) =>
        string.Join(Sep, cells.Select(c =>
            c.Contains(Sep) || c.Contains('"') || c.Contains('\n') ? "\"" + c.Replace("\"", "\"\"") + "\"" : c));
}
