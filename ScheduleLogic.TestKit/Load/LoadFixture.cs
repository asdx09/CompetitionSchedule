using System.Text.Json.Nodes;
using ScheduleLogic.TestKit.Data;
using ScheduleLogic.TestKit.Http;
using ScheduleLogic.TestKit.Testing;

namespace ScheduleLogic.TestKit.Load;

/// <summary>
/// A méréshez előkészített adatok: N bejelentkezett tesztfelhasználó,
/// mindegyiknek egy nagy versennyel. Egy sorozat összes szintje ugyanezt
/// használja; a végén a felhasználók (és velük a versenyek) törlődnek.
/// </summary>
public sealed class LoadFixture : IAsyncDisposable
{
    public sealed class FixtureUser
    {
        public required string Username { get; init; }
        public required ApiClient Client { get; init; }
        public required long EventId { get; init; }
        /// <summary>
        /// A mentési kérés törzse előre sorosítva: így a párhuzamos kérések nem
        /// osztoznak egy (nem szálbiztos) JsonNode példányon, és a sorosítás
        /// ideje sem kerül bele a mérésbe.
        /// </summary>
        public required string SaveBody { get; init; }

        /// <summary>Egy felhasználó egyszerre csak egy mentést küld (ahogy egy valódi felhasználó is).</summary>
        public SemaphoreSlim SaveLock { get; } = new(1, 1);
    }

    public BackendTarget Target { get; }
    public int EventParticipants { get; }
    public List<FixtureUser> Users { get; } = new();

    /// <summary>Az előkészítés során elküldött mentés mérete (bájt), tájékoztatásul.</summary>
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
                    log?.Report($"Előkészítés: {Interlocked.Increment(ref done)}/{users} felhasználó kész.");
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
            if (reg.StatusCode == 0) throw new InvalidOperationException($"A backend nem érhető el ({target.BaseUrl}): {reg.Error}");
            if (reg.StatusCode == 429)
                throw new InvalidOperationException("A regisztráció rate limitbe ütközött. Méréshez kapcsold ki: RateLimiting:Enabled = false.");
            if (reg.StatusCode != 200) throw new InvalidOperationException($"Sikertelen regisztráció ({reg.StatusCode}): {reg.BodyPreview(200)}");

            var login = await client.LoginAsync(name, TestContext.DefaultPassword, ct);
            if (login.StatusCode == 401)
                throw new InvalidOperationException("A tesztfelhasználó nem tud belépni (e-mail-megerősítés?). " +
                                                    "Fejlesztői módban: DevOptions:SkipEmailConfirmation = true.");
            if (login.StatusCode != 200) throw new InvalidOperationException($"Sikertelen belépés ({login.StatusCode}).");

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
                // Mentéskor a frontend a betöltött (végleges azonosítós) adatot küldi vissza.
                SaveBody = ApiClient.Serialize(SampleData.FromLoaded((JsonObject)loaded.Json()!)),
            };
        }
        catch
        {
            try { await client.DeleteAccountAsync(CancellationToken.None); } catch { /* takarítás */ }
            client.Dispose();
            throw;
        }
    }

    private static void Ensure(ApiResponse r, string what)
    {
        if (r.StatusCode != 200)
            throw new InvalidOperationException($"Előkészítés: a(z) {what} hívás {r.StatusCode} kódot adott. {r.BodyPreview(200)}");
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
            catch { /* a takarítás hibája ne takarja el a mérés eredményét */ }
            u.Client.Dispose();
        }
        Users.Clear();
    }
}
