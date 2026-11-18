namespace ScheduleLogic.TestKit.Load;

/// <summary>
/// Lépcsős mérés: ugyanazon az előkészített adaton egymás után több
/// terhelési szint (pl. 1, 5, 10, 25, 50 felhasználó). A szakdolgozat
/// "válaszidő a terhelés függvényében" grafikonjához ez kell.
/// </summary>
public static class LoadSeries
{
    public static List<int> ParseLevels(string text) =>
        text.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(s => int.TryParse(s.Trim(), out var n) ? n : 0)
            .Where(n => n > 0)
            .ToList();

    public static async Task<List<LoadResult>> RunAsync(
        LoadFixture fx,
        LoadOptions template,
        IReadOnlyList<int> levels,
        TimeSpan pauseBetween,
        ProcessMonitor? monitor,
        Action<int, LoadOptions>? onLevelStarted = null,
        IProgress<TimelinePoint>? progress = null,
        Action<LoadResult>? onLevelFinished = null,
        CancellationToken ct = default)
    {
        var results = new List<LoadResult>();
        for (int i = 0; i < levels.Count && !ct.IsCancellationRequested; i++)
        {
            var o = new LoadOptions
            {
                Scenario = template.Scenario,
                Mode = template.Mode,
                Level = levels[i],
                Duration = template.Duration,
                WarmUp = template.WarmUp,
                RequestTimeout = template.RequestTimeout,
                MaxInFlight = template.MaxInFlight,
            };
            onLevelStarted?.Invoke(i, o);
            var r = await LoadEngine.RunAsync(fx, o, monitor, progress, ct);
            results.Add(r);
            onLevelFinished?.Invoke(r);

            // Szünet a szintek között, hogy az előző szint utóhatása (pl. a
            // PHP-FPM munkafolyamatok, GC) ne csússzon át a következőbe.
            if (i + 1 < levels.Count && pauseBetween > TimeSpan.Zero)
            {
                try { await Task.Delay(pauseBetween, ct); }
                catch (OperationCanceledException) { break; }
            }
        }
        return results;
    }
}
