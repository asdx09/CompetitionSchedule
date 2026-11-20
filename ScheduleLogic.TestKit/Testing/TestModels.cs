using System.Text.Json.Serialization;

namespace ScheduleLogic.TestKit.Testing;

public enum TestStatus
{
    NotRun,
    Running,
    Passed,
    Failed,
    Skipped,
}

public sealed class TestCase
{
    public required string Id { get; init; }
    public required string Group { get; init; }
    public required string Name { get; init; }
    public string Description { get; init; } = "";

        public bool RequiresSolver { get; init; }

    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(60);

    [JsonIgnore]
    public required Func<TestContext, Task> Body { get; init; }

    public override string ToString() => $"{Id} {Name}";
}

public sealed record ExchangeRecord(string Method, string Path, int Status, string Shape, double ElapsedMs)
{
        [JsonIgnore]
    public string Signature => $"{Method} {Path} -> {Status} {Shape}";
}

public sealed class TestResult
{
    public required string TestId { get; init; }
    public required string Group { get; init; }
    public required string Name { get; init; }
    public TestStatus Status { get; set; }
    public double DurationMs { get; set; }
    public string Message { get; set; } = "";
    public DateTime StartedAt { get; set; }
    public List<string> Log { get; set; } = new();
    public List<ExchangeRecord> Exchanges { get; set; } = new();
}

public sealed class TestFailedException(string message) : Exception(message);

public sealed class TestSkippedException(string message) : Exception(message);
