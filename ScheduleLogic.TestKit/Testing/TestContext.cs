using System.Security.Cryptography;
using System.Text.Json.Nodes;
using ScheduleLogic.TestKit.Data;
using ScheduleLogic.TestKit.Http;

namespace ScheduleLogic.TestKit.Testing;

public sealed class TestContext : IAsyncDisposable
{
    public const string UserPrefix = "sltest_";
    public const string DefaultPassword = "Test-Password-2026!";

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

        public ApiClient NewClient()
    {
        var client = new ApiClient(Target, Record, TimeSpan.FromSeconds(30));
        lock (_lock) _clients.Add(client);
        return client;
    }

        public bool RecordExchanges { get; set; } = true;

    private void Record(ApiResponse r)
    {
        lock (_lock)
        {
            if (!_recording)
            {
                Log.Add("  [cleanup] " + r.Summary);
                return;
            }
            Log.Add("  " + r.Summary);
            if (!RecordExchanges) return;

            if (r.RequestBody != null)
                Log.Add("      request:  " + Truncate(r.RequestBody, 300));
            if (r.Bytes.Length > 0)
                Log.Add("      response: " + r.BodyPreview(300));
            Exchanges.Add(new ExchangeRecord(r.Method, JsonShape.NormalizePath(r.Path), r.StatusCode,
                                             JsonShape.Of(r), Math.Round(r.ElapsedMs, 1)));
        }
    }

        public static string NewUsername() =>
        UserPrefix + Convert.ToHexString(RandomNumberGenerator.GetBytes(5)).ToLowerInvariant();

        public async Task<TestUser> CreateUserAsync(bool login = true)
    {
        var client = NewClient();
        var name = NewUsername();
        var user = new TestUser(name, name + "@example.com", DefaultPassword, client);

        var reg = await client.RegisterAsync(user.Username, user.Password, user.Email, Ct);
        if (reg.StatusCode == 0)
            throw new TestFailedException($"The backend is not reachable ({Target.BaseUrl}): {reg.Error}");
        if (reg.StatusCode == 429)
            Skip("Registration hit the rate limit. Disable it in development: RateLimiting:Enabled = false.");
        Status(reg, 200, "Registration");
        lock (_lock) _users.Add(user);

        if (login)
        {
            var r = await client.LoginAsync(user.Username, user.Password, Ct);
            if (r.StatusCode == 401)
                Skip("The newly registered user cannot log in, probably waiting for e-mail confirmation. " +
                     "In development set: \"DevOptions\": { \"SkipEmailConfirmation\": true } " +
                     "(ScheduleLogic.Server/appsettings.Development.json, the PHP backend reads it from there too).");
            Status(r, 200, "Login");
            user.LoggedIn = true;
        }
        return user;
    }

        public async Task<long> CreateEventAsync(TestUser user)
    {
        var before = await ListEventIdsAsync(user);
        Status(await user.Client.NewEventAsync(Ct), 200, "New event");
        var after = await ListEventIdsAsync(user);
        var created = after.Except(before).ToList();
        Check(created.Count == 1, $"Exactly one new event should have appeared in the list, {created.Count} did.");
        return created[0];
    }

    public async Task<List<long>> ListEventIdsAsync(TestUser user)
    {
        var r = await user.Client.GetEventsAsync(Ct);
        Status(r, 200, "Event list");
        var arr = r.Json() as JsonArray ?? throw new TestFailedException("The get-events response is not a JSON array.");
        return arr.Select(e => e!["eventId"]!.GetValue<long>()).ToList();
    }

        public async Task<(long EventId, JsonObject Saved)> CreateSampleEventAsync(TestUser user)
    {
        long id = await CreateEventAsync(user);
        var payload = SampleData.SmallEvent(id);
        Status(await user.Client.SaveEventAsync(payload, Ct), 200, "Saving the sample event");
        var saved = await GetEventJsonAsync(user, id);
        return (id, saved);
    }

    public async Task<JsonObject> GetEventJsonAsync(TestUser user, long id)
    {
        var r = await user.Client.GetEventAsync(id, Ct);
        Status(r, 200, "Loading the event");
        return r.Json() as JsonObject ?? throw new TestFailedException("The get-event response is not a JSON object.");
    }

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
            throw new TestFailedException($"{what}: the backend did not respond ({r.Error}).");
        if (!expected.Contains(r.StatusCode))
        {
            var exp = string.Join(" or ", expected);
            var body = r.Bytes.Length > 0 ? $" Response: {r.BodyPreview(200)}" : "";
            throw new TestFailedException($"{what}: expected status {exp}, got {r.StatusCode}.{body}");
        }
    }

    public void Skip(string reason) => throw new TestSkippedException(reason);

    private static string Truncate(string s, int max) => s.Length <= max ? s : s[..max] + "…";

    public async ValueTask DisposeAsync()
    {
        _recording = false;
        List<TestUser> users;
        lock (_lock) users = _users.ToList();

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
                if (!u.Deleted) Info($"Could not delete test user {u.Username} ({del.StatusCode}).");
            }
            catch (Exception ex)
            {
                Info($"Cleanup error ({u.Username}): {ex.Message}");
            }
        }

        lock (_lock)
        {
            foreach (var c in _clients) c.Dispose();
            _clients.Clear();
        }
    }
}

public sealed class TestUser(string username, string email, string password, ApiClient client)
{
    public string Username { get; } = username;
    public string Email { get; } = email;
    public string Password { get; set; } = password;
    public ApiClient Client { get; } = client;
    public bool LoggedIn { get; set; }
    public bool Deleted { get; set; }
}
