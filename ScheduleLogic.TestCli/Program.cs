using System.Globalization;
using ScheduleLogic.TestKit.Http;
using ScheduleLogic.TestKit.Load;
using ScheduleLogic.TestKit.Reporting;
using ScheduleLogic.TestKit.Testing;

return await Cli.RunAsync(args);

internal static class Cli
{
    private const string Help = """
        ScheduleLogic test runner (command line)

          sltest test    --url <address> [--label <name>] [--group <group>] [--id <test id>] [--strict-tls]
                         Runs the API tests and saves the report.
                         Exit code: 0 = all passed, 1 = there is a failed test.

          sltest list    Lists the tests.

          sltest compare <report-A.json> <report-B.json>
                         Compares two runs (e.g. .NET and PHP).

          sltest load    --url <address> [--label <name>] [--scenario CheckToken|GetEvents|GetEvent|SaveEvent|Mixed]
                         [--mode closed|open] [--levels 1,5,10,25] [--duration 30] [--warmup 5] [--pause 5]
                         [--users 10] [--event-size 100] [--monitor ScheduleLogic.Server,dotnet,php-cgi] [--strict-tls]
                         Stepped load test; the results are saved as CSV.

        Default address: https://localhost:7098. Reports are saved to Documents\ScheduleLogic.
        """;

    public static async Task<int> RunAsync(string[] args)
    {
        if (args.Length == 0 || args[0] is "help" or "-h" or "--help")
        {
            Console.WriteLine(Help);
            return 0;
        }

        var opts = ParseOptions(args.Skip(1).ToArray(), out var positional);
        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

        try
        {
            return args[0] switch
            {
                "test" => await RunTests(opts, cts.Token),
                "list" => ListTests(),
                "compare" => Compare(positional),
                "load" => await RunLoad(opts, cts.Token),
                _ => Unknown(args[0]),
            };
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException or IOException)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Error.WriteLine("Error: " + ex.Message);
            Console.ResetColor();
            return 2;
        }
    }

    private static int Unknown(string cmd)
    {
        Console.Error.WriteLine($"Unknown command: {cmd}\n\n{Help}");
        return 2;
    }

    private static BackendTarget Target(Dictionary<string, string> o) =>
        new(o.GetValueOrDefault("label", "backend"), o.GetValueOrDefault("url", "https://localhost:7098"),
            IgnoreCertificateErrors: !o.ContainsKey("strict-tls"));

    private static int ListTests()
    {
        foreach (var g in ApiTests.All.GroupBy(t => t.Group))
        {
            Console.WriteLine($"\n{g.Key}");
            foreach (var t in g) Console.WriteLine($"  {t.Id,-8} {t.Name}");
        }
        return 0;
    }

    private static async Task<int> RunTests(Dictionary<string, string> o, CancellationToken ct)
    {
        var target = Target(o);
        IEnumerable<TestCase> tests = ApiTests.All;
        if (o.TryGetValue("group", out var group))
            tests = tests.Where(t => t.Group.StartsWith(group, StringComparison.OrdinalIgnoreCase));
        if (o.TryGetValue("id", out var ids))
        {
            var set = ids.Split(',').Select(s => s.Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase);
            tests = tests.Where(t => set.Contains(t.Id));
        }
        var list = tests.ToList();
        if (list.Count == 0) throw new ArgumentException("No test matches the filter.");

        Console.WriteLine($"Running {list.Count} tests: {target}\n");
        var report = new TestRunReport { Label = target.Label, BaseUrl = target.BaseUrl, StartedAt = DateTime.Now };
        report.Results = await TestRunner.RunManyAsync(list, target,
            onStarted: t => Console.Write($"  {t.Id,-8} {t.Name,-55} "),
            onFinished: r =>
            {
                Console.ForegroundColor = r.Status switch
                {
                    TestStatus.Passed => ConsoleColor.Green,
                    TestStatus.Failed => ConsoleColor.Red,
                    _ => ConsoleColor.Yellow,
                };
                Console.Write(r.Status switch { TestStatus.Passed => "OK  ", TestStatus.Failed => "FAIL", _ => "SKIP" });
                Console.ResetColor();
                Console.WriteLine($" {r.DurationMs,6:0} ms");
                if (r.Status != TestStatus.Passed) Console.WriteLine($"           {r.Message}");
            }, ct);
        report.FinishedAt = DateTime.Now;
        var file = ReportStore.Save(report);

        Console.WriteLine($"\nPassed: {report.Passed}, failed: {report.Failed}, skipped: {report.Skipped}");
        Console.WriteLine($"Report: {file}");
        return report.Failed > 0 ? 1 : 0;
    }

    private static int Compare(List<string> files)
    {
        if (files.Count != 2) throw new ArgumentException("Two report files are needed: sltest compare a.json b.json");
        var a = ReportStore.Load(files[0]);
        var b = ReportStore.Load(files[1]);
        var rows = ReportComparer.Compare(a, b);
        Console.WriteLine($"A: {a}\nB: {b}\n");
        foreach (var r in rows)
        {
            Console.ForegroundColor = r.HasDifference ? ConsoleColor.Yellow : ConsoleColor.Gray;
            Console.WriteLine($"{r.TestId,-8} {r.Name,-55} A: {r.StatusA,-8} B: {r.StatusB,-8} {r.Summary}");
            Console.ResetColor();
            foreach (var d in r.Differences) Console.WriteLine("           " + d);
        }
        Console.WriteLine($"\n{rows.Count(r => r.HasDifference)} tests differ, {rows.Count(r => !r.HasDifference)} are identical.");
        return 0;
    }

    private static async Task<int> RunLoad(Dictionary<string, string> o, CancellationToken ct)
    {
        var target = Target(o);
        var template = new LoadOptions
        {
            Scenario = Enum.Parse<LoadScenario>(o.GetValueOrDefault("scenario", "GetEvent"), ignoreCase: true),
            Mode = o.GetValueOrDefault("mode", "closed").StartsWith("o", StringComparison.OrdinalIgnoreCase) ? LoadMode.Open : LoadMode.Closed,
            Duration = TimeSpan.FromSeconds(int.Parse(o.GetValueOrDefault("duration", "30"))),
            WarmUp = TimeSpan.FromSeconds(int.Parse(o.GetValueOrDefault("warmup", "5"))),
        };
        var levels = LoadSeries.ParseLevels(o.GetValueOrDefault("levels", "1,5,10,25"));
        if (levels.Count == 0) throw new ArgumentException("Specify at least one level: --levels 1,5,10");
        int users = int.Parse(o.GetValueOrDefault("users", "10"));
        int eventSize = int.Parse(o.GetValueOrDefault("event-size", "100"));
        var monitor = ProcessMonitor.FromList(o.GetValueOrDefault("monitor", ""));
        var pause = TimeSpan.FromSeconds(int.Parse(o.GetValueOrDefault("pause", "5")));

        Console.WriteLine($"Preparing: {users} users, an event with {eventSize} competitors ({target})");
        await using var fx = await LoadFixture.CreateAsync(target, users, eventSize, template.RequestTimeout,
            new Progress<string>(_ => { }), ct);
        Console.WriteLine($"Ready. Size of one save: {fx.SavePayloadBytes / 1024.0:0.0} KB" +
                          (monitor != null ? $", monitored processes: {string.Join(", ", monitor.Names)}" : ""));

        var results = await LoadSeries.RunAsync(fx, template, levels, pause, monitor,
            onLevelStarted: (i, lo) => Console.WriteLine($"\n[{i + 1}/{levels.Count}] {lo.Describe()}"),
            progress: new Progress<TimelinePoint>(p =>
                Console.WriteLine($"  {p.Second,4}s {(p.WarmUp ? "(warm-up)" : "         ")} {p.Requests,6} req  " +
                                  $"p50 {p.P50Ms,7:0.0} ms  p95 {p.P95Ms,7:0.0} ms  errors {p.Errors,4}" +
                                  (p.CpuPercent != null ? $"  CPU {p.CpuPercent,5:0.0}%  mem {p.MemoryMb,7:0} MB" : ""))),
            onLevelFinished: r =>
            {
                var s = r.Summary;
                Console.WriteLine($"  => {s.Rps:0.0} req/s, mean {s.MeanMs:0.0} ms, p50 {s.P50Ms:0.0}, p95 {s.P95Ms:0.0}, " +
                                  $"p99 {s.P99Ms:0.0} ms, errors {s.Errors}/{s.Total} ({s.StatusCounts})");
            },
            ct: ct);

        if (results.Count > 0)
        {
            var dir = LoadCsv.SaveRun(results);
            Console.WriteLine($"\nResults: {dir}");
        }
        Console.WriteLine("Cleaning up (deleting test users)...");
        return 0;
    }

    private static Dictionary<string, string> ParseOptions(string[] args, out List<string> positional)
    {
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        positional = new List<string>();
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i].StartsWith("--", StringComparison.Ordinal))
            {
                var key = args[i][2..];
                if (i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal))
                    dict[key] = args[++i];
                else
                    dict[key] = "true";
            }
            else positional.Add(args[i]);
        }
        return dict;
    }
}
