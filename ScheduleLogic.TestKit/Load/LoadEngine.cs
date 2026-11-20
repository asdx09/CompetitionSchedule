using System.Diagnostics;
using ScheduleLogic.TestKit.Http;

namespace ScheduleLogic.TestKit.Load;

public static class LoadEngine
{
    public static async Task<LoadResult> RunAsync(
        LoadFixture fx,
        LoadOptions o,
        ProcessMonitor? monitor = null,
        IProgress<TimelinePoint>? progress = null,
        CancellationToken ct = default)
    {
        if (fx.Users.Count == 0) throw new InvalidOperationException("There are no prepared users.");

        var startedAt = DateTime.Now;
        var samples = new List<Sample>(capacity: 100_000);
        var timeline = new List<TimelinePoint>();
        var resources = new List<(int Second, bool WarmUp, ResourceSample Value)>();
        int inFlight = 0, dropped = 0;

        double warmMs = o.WarmUp.TotalMilliseconds;
        double totalMs = warmMs + o.Duration.TotalMilliseconds;

        ResourceSample? idle = null;
        if (monitor != null)
        {
            monitor.Sample();
            await Task.Delay(500, ct);
            idle = monitor.Sample();
        }

        using var runCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var sw = Stopwatch.StartNew();

        async Task Execute(int vu, double scheduledMs)
        {
            var user = fx.Users[vu % fx.Users.Count];
            var op = PickOp(o.Scenario);
            Interlocked.Increment(ref inFlight);
            double start = sw.Elapsed.TotalMilliseconds;
            ApiResponse r;
            try
            {
                r = await Send(user, op, runCts.Token);
            }
            catch (OperationCanceledException)
            {
                Interlocked.Decrement(ref inFlight);
                return;
            }
            double end = sw.Elapsed.TotalMilliseconds;
            Interlocked.Decrement(ref inFlight);

            var s = new Sample(op, scheduledMs, start, end, r.StatusCode, r.StatusCode == 200, scheduledMs < warmMs);
            lock (samples) samples.Add(s);
        }

        var ticker = Task.Run(async () =>
        {
            int lastIndex = 0, second = 0;
            while (!runCts.IsCancellationRequested)
            {
                try { await Task.Delay(1000, runCts.Token); }
                catch (OperationCanceledException) { break; }
                second++;
                List<Sample> window;
                lock (samples)
                {
                    window = samples.GetRange(lastIndex, samples.Count - lastIndex);
                    lastIndex = samples.Count;
                }
                var res = monitor?.Sample();
                bool warm = second * 1000.0 <= warmMs;
                if (res != null) lock (resources) resources.Add((second, warm, res.Value));
                var lat = window.Select(x => x.LatencyMs).OrderBy(x => x).ToArray();
                var point = new TimelinePoint
                {
                    Second = second,
                    WarmUp = warm,
                    Requests = window.Count,
                    Errors = window.Count(x => !x.Ok),
                    P50Ms = Percentile(lat, 50),
                    P95Ms = Percentile(lat, 95),
                    MaxMs = lat.Length == 0 ? 0 : Math.Round(lat[^1], 1),
                    InFlight = Volatile.Read(ref inFlight),
                    CpuPercent = res?.CpuPercent,
                    MemoryMb = res?.MemoryMb,
                };
                lock (timeline) timeline.Add(point);
                progress?.Report(point);
            }
        });

        var running = new List<Task>();
        try
        {
            if (o.Mode == LoadMode.Closed)
            {
                for (int vu = 0; vu < Math.Max(1, o.Level); vu++)
                {
                    int id = vu;
                    running.Add(Task.Run(async () =>
                    {
                        while (!runCts.IsCancellationRequested && sw.Elapsed.TotalMilliseconds < totalMs)
                            await Execute(id, sw.Elapsed.TotalMilliseconds);
                    }));
                }
                await Task.WhenAll(running);
            }
            else
            {
                double intervalMs = 1000.0 / Math.Max(1, o.Level);
                long i = 0;
                var pending = new HashSet<Task>();
                while (!runCts.IsCancellationRequested)
                {
                    double due = i * intervalMs;
                    if (due >= totalMs) break;
                    double now = sw.Elapsed.TotalMilliseconds;
                    if (now < due)
                    {
                        await Task.Delay(TimeSpan.FromMilliseconds(Math.Max(1, due - now)), runCts.Token);
                        continue;
                    }
                    if (Volatile.Read(ref inFlight) >= o.MaxInFlight)
                    {
                        Interlocked.Increment(ref dropped);
                        lock (samples) samples.Add(new Sample(PickOp(o.Scenario), due, due, due, -1, false, due < warmMs));
                    }
                    else
                    {
                        int vu = (int)(i % int.MaxValue);
                        double scheduled = due;
                        var t = Task.Run(() => Execute(vu, scheduled));
                        pending.Add(t);
                        if (pending.Count > 4 * o.MaxInFlight) pending.RemoveWhere(x => x.IsCompleted);
                    }
                    i++;
                }
                await Task.WhenAll(pending);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
        }
        finally
        {
            runCts.Cancel();
            await ticker;
        }

        return new LoadResult
        {
            Summary = Summarize(fx, o, startedAt, samples, resources, idle, dropped),
            Timeline = timeline,
            Samples = samples,
        };
    }

    private static LoadOp PickOp(LoadScenario scenario) => scenario switch
    {
        LoadScenario.CheckToken => LoadOp.CheckToken,
        LoadScenario.GetEvents => LoadOp.GetEvents,
        LoadScenario.GetEvent => LoadOp.GetEvent,
        LoadScenario.SaveEvent => LoadOp.SaveEvent,
        _ => Random.Shared.Next(100) switch
        {
            < 60 => LoadOp.GetEvent,
            < 85 => LoadOp.GetEvents,
            < 95 => LoadOp.CheckToken,
            _ => LoadOp.SaveEvent,
        },
    };

    private static Task<ApiResponse> Send(LoadFixture.FixtureUser u, LoadOp op, CancellationToken ct)
    {
        switch (op)
        {
            case LoadOp.CheckToken: return u.Client.CheckTokenAsync(ct);
            case LoadOp.GetEvents: return u.Client.GetEventsAsync(ct);
            case LoadOp.GetEvent: return u.Client.GetEventAsync(u.EventId, ct);
            default: return SaveSerialized(u, ct);
        }
    }

    private static async Task<ApiResponse> SaveSerialized(LoadFixture.FixtureUser u, CancellationToken ct)
    {
        await u.SaveLock.WaitAsync(ct);
        try
        {
            return await u.Client.SendAsync(HttpMethod.Post, "api/Event/save-event", rawBody: u.SaveBody, ct: ct);
        }
        finally
        {
            u.SaveLock.Release();
        }
    }

    private static LoadSummary Summarize(
        LoadFixture fx, LoadOptions o, DateTime startedAt, List<Sample> all,
        List<(int Second, bool WarmUp, ResourceSample Value)> resources, ResourceSample? idle, int dropped)
    {
        var measured = all.Where(s => !s.WarmUp).ToList();
        var lat = measured.Where(s => s.Status > 0).Select(s => s.LatencyMs).OrderBy(x => x).ToArray();
        var service = measured.Where(s => s.Status > 0).Select(s => s.ServiceMs).OrderBy(x => x).ToArray();
        var res = resources.Where(r => !r.WarmUp).Select(r => r.Value).ToList();

        return new LoadSummary
        {
            Label = fx.Target.Label,
            BaseUrl = fx.Target.BaseUrl,
            StartedAt = startedAt,
            Scenario = o.Scenario,
            Mode = o.Mode,
            Level = o.Level,
            DurationS = o.Duration.TotalSeconds,
            FixtureUsers = fx.Users.Count,
            EventParticipants = fx.EventParticipants,
            Total = measured.Count,
            Ok = measured.Count(s => s.Ok),
            Errors = measured.Count(s => !s.Ok),
            ClientDropped = dropped,
            Rps = Math.Round(measured.Count(s => s.Ok) / o.Duration.TotalSeconds, 1),
            MeanMs = lat.Length == 0 ? 0 : Math.Round(lat.Average(), 1),
            P50Ms = Percentile(lat, 50),
            P75Ms = Percentile(lat, 75),
            P90Ms = Percentile(lat, 90),
            P95Ms = Percentile(lat, 95),
            P99Ms = Percentile(lat, 99),
            MaxMs = lat.Length == 0 ? 0 : Math.Round(lat[^1], 1),
            ServiceP95Ms = Percentile(service, 95),
            IdleMemoryMb = idle?.MemoryMb,
            CpuAvgPercent = res.Count == 0 ? null : Math.Round(res.Average(r => r.CpuPercent), 1),
            CpuMaxPercent = res.Count == 0 ? null : res.Max(r => r.CpuPercent),
            MemoryAvgMb = res.Count == 0 ? null : Math.Round(res.Average(r => r.MemoryMb), 1),
            MemoryMaxMb = res.Count == 0 ? null : res.Max(r => r.MemoryMb),
            StatusCounts = string.Join(" ", measured.GroupBy(s => s.Status).OrderBy(g => g.Key)
                                                    .Select(g => $"{(g.Key == -1 ? "dropped" : g.Key == 0 ? "network_error" : g.Key.ToString())}:{g.Count()}")),
        };
    }

        public static double Percentile(double[] sorted, double p)
    {
        if (sorted.Length == 0) return 0;
        int rank = (int)Math.Ceiling(p / 100.0 * sorted.Length);
        return Math.Round(sorted[Math.Clamp(rank - 1, 0, sorted.Length - 1)], 1);
    }
}
