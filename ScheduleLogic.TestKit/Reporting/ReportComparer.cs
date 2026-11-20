using ScheduleLogic.TestKit.Testing;

namespace ScheduleLogic.TestKit.Reporting;

public sealed class ComparisonRow
{
    public required string TestId { get; init; }
    public required string Name { get; init; }
    public TestStatus? StatusA { get; init; }
    public TestStatus? StatusB { get; init; }
    public string MessageA { get; init; } = "";
    public string MessageB { get; init; } = "";

        public List<string> Differences { get; init; } = new();

    public bool StatusDiffers => StatusA != StatusB;
    public bool HasDifference => StatusDiffers || Differences.Count > 0;

    public string Summary =>
        !HasDifference ? "identical"
        : StatusDiffers ? "different result"
        : $"{Differences.Count} different responses";
}

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
                var sigA = ra.Exchanges.Select(e => e.Signature).ToHashSet();
                var sigB = rb.Exchanges.Select(e => e.Signature).ToHashSet();
                diffs.AddRange(sigA.Except(sigB).Select(s => $"only {a.Label}: {s}"));
                diffs.AddRange(sigB.Except(sigA).Select(s => $"only {b.Label}: {s}"));
            }
            rows.Add(new ComparisonRow
            {
                TestId = id,
                Name = (ra ?? rb)!.Name,
                StatusA = ra?.Status,
                StatusB = rb?.Status,
                MessageA = ra?.Message ?? "(not run)",
                MessageB = rb?.Message ?? "(not run)",
                Differences = diffs,
            });
        }
        return rows;
    }

        public static double AverageMs(TestResult? r) =>
        r == null || r.Exchanges.Count == 0 ? 0 : r.Exchanges.Average(e => e.ElapsedMs);
}
