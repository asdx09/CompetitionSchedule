using System.Security.Cryptography;
using System.Text.Json.Nodes;
using ScheduleLogic.TestKit.Data;
using ScheduleLogic.TestKit.Http;

namespace ScheduleLogic.TestKit.Testing;

/// <summary>
/// Egy teszt futási környezete: ezen keresztül hoz létre klienseket és
/// tesztfelhasználókat, naplóz és ellenőriz. A teszt végén (sikertől
/// függetlenül) törli az összes általa létrehozott felhasználót, a fióktörlés
/// pedig kaszkádoltan viszi a versenyeket és a beosztásokat is.
/// </summary>
public sealed class TestContext : IAsyncDisposable
{
    public const string UserPrefix = "sltest_";
    public const string DefaultPassword = "Teszt-Jelszo-2026!";

    private readonly List<ApiClient> _clients = new();
    private readonly List<TestUser> _users = new();
    private readonly object _lock = new();
    private bool _recording = true;

    public BackendTarget Target { get; }
    public CancellationToken Ct { get; }
    public List<string> Log { get; } = new();
    public List<ExchangeRecord> Exchanges { get; } = new();

    public TestContext(BackendTarget target, CancellationToken ct)
    {
        Target = target;
        Ct = ct;
    }

    // ------------------------------------------------------------------
    // Kliensek, felhasználók
    // ------------------------------------------------------------------

    /// <summary>Új "böngésző" (saját sütikkel), minden kérése a naplóba kerül.</summary>
    public ApiClient NewClient()
    {
        var client = new ApiClient(Target, Record, TimeSpan.FromSeconds(30));
        lock (_lock) _clients.Add(client);
        return client;
    }

    /// <summary>
    /// Ha hamis, a kérések csak egy sorral kerülnek a naplóba, és nem
    /// számítanak bele a backendek összehasonlításába (pl. várakozás közbeni
    /// ismételt lekérdezések, amelyek száma futásonként változik).
    /// </summary>
    public bool RecordExchanges { get; set; } = true;

    private void Record(ApiResponse r)
    {
        lock (_lock)
        {
            if (!_recording)
            {
                Log.Add("  [takarítás] " + r.Summary);
                return;
            }
            Log.Add("  " + r.Summary);
            if (!RecordExchanges) return;

            if (r.RequestBody != null)
                Log.Add("      kérés:  " + Truncate(r.RequestBody, 300));
            if (r.Bytes.Length > 0)
                Log.Add("      válasz: " + r.BodyPreview(300));
            Exchanges.Add(new ExchangeRecord(r.Method, JsonShape.NormalizePath(r.Path), r.StatusCode,
                                             JsonShape.Of(r), Math.Round(r.ElapsedMs, 1)));
        }
    }

    /// <summary>Véletlen, ütközésmentes tesztfelhasználó-név.</summary>
    public static string NewUsername() =>
        UserPrefix + Convert.ToHexString(RandomNumberGenerator.GetBytes(5)).ToLowerInvariant();

    /// <summary>
    /// Regisztrál egy új felhasználót, és (alapból) be is lépteti. A teszt
    /// végén automatikusan törlődik.
    /// </summary>
    public async Task<TestUser> CreateUserAsync(bool login = true)
    {
        var client = NewClient();
        var name = NewUsername();
        var user = new TestUser(name, name + "@example.com", DefaultPassword, client);

        var reg = await client.RegisterAsync(user.Username, user.Password, user.Email, Ct);
        if (reg.StatusCode == 0)
            throw new TestFailedException($"A backend nem érhető el ({Target.BaseUrl}): {reg.Error}");
        if (reg.StatusCode == 429)
            Skip("A regisztráció rate limitbe ütközött. Fejlesztői módban kapcsold ki: RateLimiting:Enabled = false.");
        Status(reg, 200, "Regisztráció");
        lock (_lock) _users.Add(user);

        if (login)
        {
            var r = await client.LoginAsync(user.Username, user.Password, Ct);
            if (r.StatusCode == 401)
                Skip("A frissen regisztrált felhasználó nem tud belépni, valószínűleg e-mail-megerősítésre vár. " +
                     "Fejlesztői módban állítsd be: \"DevOptions\": { \"SkipEmailConfirmation\": true } " +
                     "(ScheduleLogic.Server/appsettings.Development.json, a PHP is innen olvassa).");
            Status(r, 200, "Belépés");
            user.LoggedIn = true;
        }
        return user;
    }

    /// <summary>Új, üres verseny a felhasználónak; visszaadja az azonosítóját.</summary>
    public async Task<long> CreateEventAsync(TestUser user)
    {
        var before = await ListEventIdsAsync(user);
        Status(await user.Client.NewEventAsync(Ct), 200, "Új verseny");
        var after = await ListEventIdsAsync(user);
        var created = after.Except(before).ToList();
        Check(created.Count == 1, $"Pontosan egy új versenynek kellett megjelennie a listában, {created.Count} jelent meg.");
        return created[0];
    }

    public async Task<List<long>> ListEventIdsAsync(TestUser user)
    {
        var r = await user.Client.GetEventsAsync(Ct);
        Status(r, 200, "Versenyek listája");
        var arr = r.Json() as JsonArray ?? throw new TestFailedException("A get-events válasza nem JSON tömb.");
        return arr.Select(e => e!["eventId"]!.GetValue<long>()).ToList();
    }

    /// <summary>Új verseny a minta adatokkal feltöltve (2 helyszín, 2 versenyszám, 3 versenyző).</summary>
    public async Task<(long EventId, JsonObject Saved)> CreateSampleEventAsync(TestUser user)
    {
        long id = await CreateEventAsync(user);
        var payload = SampleData.SmallEvent(id);
        Status(await user.Client.SaveEventAsync(payload, Ct), 200, "Minta verseny mentése");
        var saved = await GetEventJsonAsync(user, id);
        return (id, saved);
    }

    public async Task<JsonObject> GetEventJsonAsync(TestUser user, long id)
    {
        var r = await user.Client.GetEventAsync(id, Ct);
        Status(r, 200, "Verseny lekérése");
        return r.Json() as JsonObject ?? throw new TestFailedException("A get-event válasza nem JSON objektum.");
    }

    // ------------------------------------------------------------------
    // Ellenőrzések
    // ------------------------------------------------------------------

    public void Info(string message)
    {
        lock (_lock) Log.Add("• " + message);
    }

    public void Check(bool condition, string message)
    {
        if (!condition) throw new TestFailedException(message);
    }

    public void Status(ApiResponse r, int expected, string what) => StatusIn(r, what, expected);

    public void StatusIn(ApiResponse r, string what, params int[] expected)
    {
        if (r.StatusCode == 0)
            throw new TestFailedException($"{what}: a backend nem válaszolt ({r.Error}).");
        if (!expected.Contains(r.StatusCode))
        {
            var exp = string.Join(" vagy ", expected);
            var body = r.Bytes.Length > 0 ? $" Válasz: {r.BodyPreview(200)}" : "";
            throw new TestFailedException($"{what}: várt státusz {exp}, kapott {r.StatusCode}.{body}");
        }
    }

    public void Skip(string reason) => throw new TestSkippedException(reason);

    private static string Truncate(string s, int max) => s.Length <= max ? s : s[..max] + "…";

    // ------------------------------------------------------------------
    // Takarítás
    // ------------------------------------------------------------------

    public async ValueTask DisposeAsync()
    {
        _recording = false;
        List<TestUser> users;
        lock (_lock) users = _users.ToList();

        // A takarítás akkor is lefut, ha a tesztet megszakították.
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        foreach (var u in users.Where(u => !u.Deleted))
        {
            try
            {
                var check = await u.Client.CheckTokenAsync(cts.Token);
                if (check.StatusCode != 200)
                    await u.Client.LoginAsync(u.Username, u.Password, cts.Token);
                var del = await u.Client.DeleteAccountAsync(cts.Token);
                u.Deleted = del.StatusCode == 200;
                if (!u.Deleted) Info($"A(z) {u.Username} tesztfelhasználót nem sikerült törölni ({del.StatusCode}).");
            }
            catch (Exception ex)
            {
                Info($"Takarítási hiba ({u.Username}): {ex.Message}");
            }
        }

        lock (_lock)
        {
            foreach (var c in _clients) c.Dispose();
            _clients.Clear();
        }
    }
}

/// <summary>Egy tesztfelhasználó és a hozzá tartozó (bejelentkezett) kliens.</summary>
public sealed class TestUser(string username, string email, string password, ApiClient client)
{
    public string Username { get; } = username;
    public string Email { get; } = email;
    public string Password { get; set; } = password;
    public ApiClient Client { get; } = client;
    public bool LoggedIn { get; set; }
    public bool Deleted { get; set; }
}
