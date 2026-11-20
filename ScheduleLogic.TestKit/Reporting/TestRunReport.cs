using System.Text.Json;
using System.Text.Json.Serialization;
using ScheduleLogic.TestKit.Testing;

namespace ScheduleLogic.TestKit.Reporting;

public sealed class TestRunReport
{
    public string Label { get; set; } = "";
    public string BaseUrl { get; set; } = "";
    public string MachineName { get; set; } = Environment.MachineName;
    public DateTime StartedAt { get; set; }
    public DateTime FinishedAt { get; set; }
    public List<TestResult> Results { get; set; } = new();

    public int Passed => Results.Count(r => r.Status == TestStatus.Passed);
    public int Failed => Results.Count(r => r.Status == TestStatus.Failed);
    public int Skipped => Results.Count(r => r.Status == TestStatus.Skipped);

    [JsonIgnore]
    public string? FilePath { get; set; }

    public override string ToString() =>
        $"{StartedAt:yyyy-MM-dd HH:mm} – {Label} ({Passed} passed, {Failed} failed, {Skipped} skipped)";
}

public static class ReportStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public static string BaseFolder =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "ScheduleLogic");

    public static string TestResultsFolder => Path.Combine(BaseFolder, "TestResults");
    public static string LoadResultsFolder => Path.Combine(BaseFolder, "LoadResults");

    public static string Save(TestRunReport report, string? folder = null)
    {
        folder ??= TestResultsFolder;
        Directory.CreateDirectory(folder);
        var file = Path.Combine(folder, $"{report.StartedAt:yyyyMMdd-HHmmss}_{SafeName(report.Label)}.json");
        File.WriteAllText(file, JsonSerializer.Serialize(report, Options));
        report.FilePath = file;
        return file;
    }

    public static TestRunReport Load(string file)
    {
        var report = JsonSerializer.Deserialize<TestRunReport>(File.ReadAllText(file), Options)
                     ?? throw new InvalidDataException("Empty report file: " + file);
        report.FilePath = file;
        return report;
    }

        public static List<TestRunReport> List(string? folder = null)
    {
        folder ??= TestResultsFolder;
        if (!Directory.Exists(folder)) return new();
        var list = new List<TestRunReport>();
        foreach (var f in Directory.GetFiles(folder, "*.json").OrderByDescending(f => f))
        {
            try { list.Add(Load(f)); }
            catch (Exception) {  }
        }
        return list;
    }

    public static string SafeName(string s)
    {
        var invalid = Path.GetInvalidFileNameChars().Concat(new[] { ' ', '.' }).ToHashSet();
        var name = new string(s.Select(c => invalid.Contains(c) ? '_' : c).ToArray()).Trim('_');
        return name.Length == 0 ? "backend" : name;
    }
}
