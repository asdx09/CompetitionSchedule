namespace ScheduleLogic.TestKit.Load;

/// <summary>Milyen kérésekkel terheljük a backendet.</summary>
public enum LoadScenario
{
    /// <summary>GET check-token: a legkisebb kérés, csak a munkamenet ellenőrzése (a keretrendszer "alapköltsége").</summary>
    CheckToken,
    /// <summary>GET get-events: egy egyszerű lekérdezés.</summary>
    GetEvents,
    /// <summary>GET get-event: egy nagy verseny összes adata (9 tábla).</summary>
    GetEvent,
    /// <summary>POST save-event: egy nagy verseny mentése egy tranzakcióban (törlés + beszúrás).</summary>
    SaveEvent,
    /// <summary>Valós használatot közelítő keverék: 60% get-event, 25% get-events, 10% check-token, 5% save-event.</summary>
    Mixed,
}

public enum LoadMode
{
    /// <summary>Zárt modell: N virtuális felhasználó, mindegyik a válasz után azonnal újra kér.</summary>
    Closed,
    /// <summary>Nyitott modell: rögzített kérés/másodperc, a válaszoktól függetlenül.</summary>
    Open,
}

public enum LoadOp { CheckToken, GetEvents, GetEvent, SaveEvent }

public sealed class LoadOptions
{
    public LoadScenario Scenario { get; set; } = LoadScenario.GetEvent;
    public LoadMode Mode { get; set; } = LoadMode.Closed;

    /// <summary>Zárt módban a párhuzamos felhasználók, nyitott módban a kérés/másodperc.</summary>
    public int Level { get; set; } = 10;

    public TimeSpan Duration { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>Bemelegítés: ennyi ideig fut a terhelés, de nem számít bele az eredménybe (JIT, OPcache, kapcsolatkészlet).</summary>
    public TimeSpan WarmUp { get; set; } = TimeSpan.FromSeconds(5);

    public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>Nyitott módban ennyi lehet egyszerre úton; afölött a kliens eldobja a kérést (és ezt jelzi).</summary>
    public int MaxInFlight { get; set; } = 2000;

    public string Describe() =>
        $"{Scenario}, {(Mode == LoadMode.Closed ? $"{Level} párhuzamos felhasználó" : $"{Level} kérés/s")}, " +
        $"{Duration.TotalSeconds:0} s (+{WarmUp.TotalSeconds:0} s bemelegítés)";
}

/// <summary>Egy kérés mérési adata. Az időpontok a mérés kezdetétől számított ezredmásodpercek.</summary>
public readonly record struct Sample(
    LoadOp Op,
    double ScheduledMs,
    double StartMs,
    double EndMs,
    int Status,
    bool Ok,
    bool WarmUp)
{
    /// <summary>
    /// Késleltetés a felhasználó szemszögéből: a tervezett indítástól a válaszig.
    /// Nyitott módban így a kliensoldali várakozás is beleszámít, ezért nem
    /// szépíti az eredményt, ha a szerver lelassul (coordinated omission).
    /// </summary>
    public double LatencyMs => EndMs - ScheduledMs;

    /// <summary>Tiszta kiszolgálási idő: a kérés elküldésétől a válaszig.</summary>
    public double ServiceMs => EndMs - StartMs;
}

public readonly record struct ResourceSample(double CpuPercent, double MemoryMb, int ProcessCount);

/// <summary>Egy másodperc összesítése (élő grafikonhoz és a timeline CSV-hez).</summary>
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

    /// <summary>Státuszkódok gyakorisága, pl. "200:15230 500:3".</summary>
    public string StatusCounts { get; init; } = "";
}

public sealed class LoadResult
{
    public required LoadSummary Summary { get; init; }
    public required List<TimelinePoint> Timeline { get; init; }
    public required List<Sample> Samples { get; init; }
}
