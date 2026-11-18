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

/// <summary>Egy teszt definíciója. A törzs egy TestContext-et kap, abból dolgozik.</summary>
public sealed class TestCase
{
    public required string Id { get; init; }
    public required string Group { get; init; }
    public required string Name { get; init; }
    public string Description { get; init; } = "";

    /// <summary>A teszthez futó Python solver is kell (beosztás készítése).</summary>
    public bool RequiresSolver { get; init; }

    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(60);

    [JsonIgnore]
    public required Func<TestContext, Task> Body { get; init; }

    public override string ToString() => $"{Id} {Name}";
}

/// <summary>
/// Egy kérés összefoglalója a két backend összehasonlításához. Azonosítók és
/// időpontok nélkül: csak a végpont, a státuszkód és a válasz szerkezete.
/// </summary>
public sealed record ExchangeRecord(string Method, string Path, int Status, string Shape, double ElapsedMs)
{
    /// <summary>Ezt hasonlítja össze a két futás között (az időt nem).</summary>
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

/// <summary>A teszt elbukott: egy ellenőrzés nem teljesült.</summary>
public sealed class TestFailedException(string message) : Exception(message);

/// <summary>A teszt nem futtatható ebben a környezetben (pl. nem fut a solver).</summary>
public sealed class TestSkippedException(string message) : Exception(message);
