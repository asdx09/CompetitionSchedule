namespace ScheduleLogic.TestKit.Load;

public enum LoadScenario
{
        CheckToken,
        GetEvents,
        GetEvent,
        SaveEvent,
        Mixed,
}

public enum LoadMode
{
        Closed,
        Open,
}

public enum LoadOp { CheckToken, GetEvents, GetEvent, SaveEvent }

public sealed class LoadOptions
{
    public LoadScenario Scenario { get; set; } = LoadScenario.GetEvent;
    public LoadMode Mode { get; set; } = LoadMode.Closed;

        public int Level { get; set; } = 10;

    public TimeSpan Duration { get; set; } = TimeSpan.FromSeconds(30);

        public TimeSpan WarmUp { get; set; } = TimeSpan.FromSeconds(5);

    public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(30);

        public int MaxInFlight { get; set; } = 2000;

    public string Describe() =>
        $"{Scenario}, {(Mode == LoadMode.Closed ? $"{Level} concurrent users" : $"{Level} req/s")}, " +
        $"{Duration.TotalSeconds:0} s (+{WarmUp.TotalSeconds:0} s warm-up)";
}

public readonly record struct Sample(
    LoadOp Op,
    double ScheduledMs,
    double StartMs,
    double EndMs,
    int Status,
    bool Ok,
    bool WarmUp)
{
        // Measured from the scheduled start, so client-side queueing counts too (coordinated omission).
        public double LatencyMs => EndMs - ScheduledMs;

        public double ServiceMs => EndMs - StartMs;
}

public readonly record struct ResourceSample(double CpuPercent, double MemoryMb, int ProcessCount);

public sealed class TimelinePoint
{
    public int Second { get; init; }
    public bool WarmUp { get; init; }
    public int Requests { get; init; }
    public int Errors { get; init; }
    public double P50Ms { get; init; }
    public double P95Ms { get; init; }
    public double MaxMs { get; init; }
    public int InFlight { get; init; }
    public double? CpuPercent { get; init; }
    public double? MemoryMb { get; init; }
}

public sealed class LoadSummary
{
    public string Label { get; init; } = "";
    public string BaseUrl { get; init; } = "";
    public DateTime StartedAt { get; init; }
    public LoadScenario Scenario { get; init; }
    public LoadMode Mode { get; init; }
    public int Level { get; init; }
    public double DurationS { get; init; }
    public int FixtureUsers { get; init; }
    public int EventParticipants { get; init; }

    public int Total { get; init; }
    public int Ok { get; init; }
    public int Errors { get; init; }
    public int ClientDropped { get; init; }
    public double ErrorRatePercent => Total == 0 ? 0 : 100.0 * Errors / Total;
    public double Rps { get; init; }

    public double MeanMs { get; init; }
    public double P50Ms { get; init; }
    public double P75Ms { get; init; }
    public double P90Ms { get; init; }
    public double P95Ms { get; init; }
    public double P99Ms { get; init; }
    public double MaxMs { get; init; }
    public double ServiceP95Ms { get; init; }

    public double? IdleMemoryMb { get; init; }
    public double? CpuAvgPercent { get; init; }
    public double? CpuMaxPercent { get; init; }
    public double? MemoryAvgMb { get; init; }
    public double? MemoryMaxMb { get; init; }

        public string StatusCounts { get; init; } = "";
}

public sealed class LoadResult
{
    public required LoadSummary Summary { get; init; }
    public required List<TimelinePoint> Timeline { get; init; }
    public required List<Sample> Samples { get; init; }
}
