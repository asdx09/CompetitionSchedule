using ScheduleLogic.TestKit.Testing;

namespace ScheduleLogic.TestKit.Reporting;

/// <summary>Egy teszt összevetése két futás (pl. .NET és PHP backend) között.</summary>
public sealed class ComparisonRow
{
    public required string TestId { get; init; }
    public required string Name { get; init; }
    public TestStatus? StatusA { get; init; }
    public TestStatus? StatusB { get; init; }
    public string MessageA { get; init; } = "";
    public string MessageB { get; init; } = "";

    /// <summary>A kérések eltérései (csak A-ban, csak B-ben szereplő végpont/státusz/szerkezet).</summary>
    public List<string> Differences { get; init; } = new();

    public bool StatusDiffers => StatusA != StatusB;
    public bool HasDifference => StatusDiffers || Differences.Count > 0;

    public string Summary =>
        !HasDifference ? "azonos"
        : StatusDiffers ? "eltérő eredmény"
        : $"{Differences.Count} eltérő válasz";
}

/// <summary>
/// Két riport összehasonlítása. A tesztek eredményén túl a kérések
/// "aláírását" (metódus, útvonal azonosítók nélkül, státuszkód, a válasz
/// JSON-szerkezete) is összeveti, így az is kiderül, ha egy backend más
/// formátumban vagy más hibakóddal válaszol, mint a másik, még ha a teszt
/// mindkettőn sikeres is.
/// </summary>
public static class ReportComparer
{
    public static List<ComparisonRow> Compare(TestRunReport a, TestRunReport b)
    {
        var mapA = a.Results.ToDictionary(r => r.TestId);
        var mapB = b.Results.ToDictionary(r => r.TestId);
        var order = a.Results.Select(r => r.TestId)
                    .Concat(b.Results.Select(r => r.TestId).Where(id => !mapA.ContainsKey(id)))
                    .ToList();

        var rows = new List<ComparisonRow>();
        foreach (var id in order)
        {
            mapA.TryGetValue(id, out var ra);
            mapB.TryGetValue(id, out var rb);
            var diffs = new List<string>();
            if (ra != null && rb != null && ra.Status != TestStatus.Skipped && rb.Status != TestStatus.Skipped)
            {
                // Halmazként hasonlítunk: az ismételt (várakozó) lekérdezések száma
                // futásonként változhat, a sorrend pedig nem lényeges.
                var sigA = ra.Exchanges.Select(e => e.Signature).ToHashSet();
                var sigB = rb.Exchanges.Select(e => e.Signature).ToHashSet();
                diffs.AddRange(sigA.Except(sigB).Select(s => $"csak {a.Label}: {s}"));
                diffs.AddRange(sigB.Except(sigA).Select(s => $"csak {b.Label}: {s}"));
            }
            rows.Add(new ComparisonRow
            {
                TestId = id,
                Name = (ra ?? rb)!.Name,
                StatusA = ra?.Status,
                StatusB = rb?.Status,
                MessageA = ra?.Message ?? "(nem futott)",
                MessageB = rb?.Message ?? "(nem futott)",
                Differences = diffs,
            });
        }
        return rows;
    }

    /// <summary>Átlagos válaszidő tesztenként és backendenként (tájékoztató jellegű).</summary>
    public static double AverageMs(TestResult? r) =>
        r == null || r.Exchanges.Count == 0 ? 0 : r.Exchanges.Average(e => e.ElapsedMs);
}
