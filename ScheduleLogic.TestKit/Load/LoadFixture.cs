using System.Text.Json.Nodes;
using ScheduleLogic.TestKit.Data;
using ScheduleLogic.TestKit.Http;
using ScheduleLogic.TestKit.Testing;

namespace ScheduleLogic.TestKit.Load;

public sealed class LoadFixture : IAsyncDisposable
{
    public sealed class FixtureUser
    {
        public required string Username { get; init; }
        public required ApiClient Client { get; init; }
        public required long EventId { get; init; }
                public required string SaveBody { get; init; }

                public SemaphoreSlim SaveLock { get; } = new(1, 1);
    }

    public BackendTarget Target { get; }
    public int EventParticipants { get; }
    public List<FixtureUser> Users { get; } = new();

        public int SavePayloadBytes { get; private set; }

    private LoadFixture(BackendTarget target, int participants)
    {
        Target = target;
        EventParticipants = participants;
    }

    public static async Task<LoadFixture> CreateAsync(
        BackendTarget target, int users, int participants, TimeSpan requestTimeout,
        IProgress<string>? log = null, CancellationToken ct = default)
    {
        var fx = new LoadFixture(target, participants);
        try
        {
            using var gate = new SemaphoreSlim(4);
            int done = 0;
            var tasks = Enumerable.Range(0, Math.Max(1, users)).Select(async i =>
            {
                await gate.WaitAsync(ct);
                try
                {
                    var u = await CreateUserAsync(target, i, participants, requestTimeout, ct);
                    lock (fx.Users) fx.Users.Add(u);
                    log?.Report($"Preparation: {Interlocked.Increment(ref done)}/{users} users ready.");
                }
                finally { gate.Release(); }
            }).ToList();
            await Task.WhenAll(tasks);
            fx.SavePayloadBytes = System.Text.Encoding.UTF8.GetByteCount(fx.Users[0].SaveBody);
            return fx;
        }
        catch
        {
            await fx.DisposeAsync();
            throw;
        }
    }

    private static async Task<FixtureUser> CreateUserAsync(
        BackendTarget target, int index, int participants, TimeSpan timeout, CancellationToken ct)
    {
        var client = new ApiClient(target, null, timeout);
        var name = TestContext.NewUsername();
        try
        {
            var reg = await client.RegisterAsync(name, TestContext.DefaultPassword, name + "@example.com", ct);
            if (reg.StatusCode == 0) throw new InvalidOperationException($"The backend is not reachable ({target.BaseUrl}): {reg.Error}");
            if (reg.StatusCode == 429)
                throw new InvalidOperationException("Registration hit the rate limit. Turn it off for measurements: RateLimiting:Enabled = false.");
            if (reg.StatusCode != 200) throw new InvalidOperationException($"Registration failed ({reg.StatusCode}): {reg.BodyPreview(200)}");

            var login = await client.LoginAsync(name, TestContext.DefaultPassword, ct);
            if (login.StatusCode == 401)
                throw new InvalidOperationException("The test user cannot log in (e-mail confirmation?). " +
                                                    "In development mode: DevOptions:SkipEmailConfirmation = true.");
            if (login.StatusCode != 200) throw new InvalidOperationException($"Login failed ({login.StatusCode}).");

            Ensure(await client.NewEventAsync(ct), "new-event");
            var list = await client.GetEventsAsync(ct);
            Ensure(list, "get-events");
            long id = (list.Json() as JsonArray)!.Max(e => e!["eventId"]!.GetValue<long>());

            Ensure(await client.SaveEventAsync(SampleData.LargeEvent(id, participants, seed: 1000 + index), ct), "save-event");
            var loaded = await client.GetEventAsync(id, ct);
            Ensure(loaded, "get-event");

            return new FixtureUser
            {
                Username = name,
                Client = client,
                EventId = id,
                SaveBody = ApiClient.Serialize(SampleData.FromLoaded((JsonObject)loaded.Json()!)),
            };
        }
        catch
        {
            try { await client.DeleteAccountAsync(CancellationToken.None); } catch {  }
            client.Dispose();
            throw;
        }
    }

    private static void Ensure(ApiResponse r, string what)
    {
        if (r.StatusCode != 200)
            throw new InvalidOperationException($"Preparation: the {what} call returned {r.StatusCode}. {r.BodyPreview(200)}");
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var u in Users)
        {
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                await u.Client.DeleteAccountAsync(cts.Token);
            }
            catch {  }
            u.Client.Dispose();
        }
        Users.Clear();
    }
}
