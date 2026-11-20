using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Security;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ScheduleLogic.TestKit.Http;

public sealed class ApiClient : IDisposable
{
    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;
    private readonly Action<ApiResponse>? _onResponse;

    public BackendTarget Target { get; }
    public CookieContainer Cookies { get; } = new();

        public ApiClient(BackendTarget target, Action<ApiResponse>? onResponse = null, TimeSpan? timeout = null)
    {
        Target = target;
        _onResponse = onResponse;

        var handler = new SocketsHttpHandler
        {
            CookieContainer = Cookies,
            UseCookies = true,
            AllowAutoRedirect = false,
            AutomaticDecompression = DecompressionMethods.None,
            PooledConnectionLifetime = TimeSpan.FromMinutes(5),
            MaxConnectionsPerServer = int.MaxValue,
        };
        if (target.IgnoreCertificateErrors)
        {
            handler.SslOptions = new SslClientAuthenticationOptions
            {
                RemoteCertificateValidationCallback = (_, _, _, _) => true
            };
        }

        _http = new HttpClient(handler)
        {
            BaseAddress = target.BaseUri,
            Timeout = timeout ?? TimeSpan.FromSeconds(60),
        };
        _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

        public async Task<ApiResponse> SendAsync(
        HttpMethod method,
        string path,
        object? json = null,
        string? rawBody = null,
        string rawContentType = "application/json",
        IDictionary<string, string>? headers = null,
        CancellationToken ct = default)
    {
        string? bodyText = rawBody ?? (json == null ? null : Serialize(json));
        using var request = new HttpRequestMessage(method, path.TrimStart('/'));
        if (bodyText != null)
        {
            request.Content = new StringContent(bodyText, Encoding.UTF8);
            request.Content.Headers.ContentType = MediaTypeHeaderValue.Parse(
                rawBody != null ? rawContentType : "application/json");
        }
        if (headers != null)
        {
            foreach (var (name, value) in headers)
                request.Headers.TryAddWithoutValidation(name, value);
        }

        var sw = Stopwatch.StartNew();
        ApiResponse result;
        try
        {
            using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseContentRead, ct)
                                            .ConfigureAwait(false);
            var bytes = await response.Content.ReadAsByteArrayAsync(ct).ConfigureAwait(false);
            sw.Stop();

            var allHeaders = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
            foreach (var h in response.Headers) allHeaders[h.Key] = h.Value.ToArray();
            foreach (var h in response.Content.Headers) allHeaders[h.Key] = h.Value.ToArray();

            result = new ApiResponse
            {
                Method = method.Method,
                Path = "/" + path.TrimStart('/'),
                RequestBody = bodyText,
                StatusCode = (int)response.StatusCode,
                ContentType = response.Content.Headers.ContentType?.ToString() ?? "",
                Bytes = bytes,
                Headers = allHeaders,
                ElapsedMs = sw.Elapsed.TotalMilliseconds,
            };
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException or IOException)
        {
            sw.Stop();
            if (ct.IsCancellationRequested) throw;
            result = new ApiResponse
            {
                Method = method.Method,
                Path = "/" + path.TrimStart('/'),
                RequestBody = bodyText,
                StatusCode = 0,
                ElapsedMs = sw.Elapsed.TotalMilliseconds,
                Error = ex is TaskCanceledException ? "timeout" : ex.GetBaseException().Message,
            };
        }

        _onResponse?.Invoke(result);
        return result;
    }

    public static string Serialize(object value) =>
        value is JsonNode node ? node.ToJsonString(JsonOptions) : JsonSerializer.Serialize(value, JsonOptions);

    public Task<ApiResponse> GetAsync(string path, CancellationToken ct = default) =>
        SendAsync(HttpMethod.Get, path, ct: ct);

    public Task<ApiResponse> PostAsync(string path, object? json = null, CancellationToken ct = default) =>
        SendAsync(HttpMethod.Post, path, json, ct: ct);

    public Task<ApiResponse> DeleteAsync(string path, CancellationToken ct = default) =>
        SendAsync(HttpMethod.Delete, path, ct: ct);

    public Task<ApiResponse> RegisterAsync(string username, string password, string email, CancellationToken ct = default) =>
        PostAsync("api/Authentication/register", new { username, password, email }, ct);

    public Task<ApiResponse> LoginAsync(string usernameOrEmail, string password, CancellationToken ct = default) =>
        PostAsync("api/Authentication/login", new { username = usernameOrEmail, password }, ct);

    public Task<ApiResponse> LogoutAsync(CancellationToken ct = default) =>
        PostAsync("api/Authentication/logout", ct: ct);

    public Task<ApiResponse> CheckTokenAsync(CancellationToken ct = default) =>
        GetAsync("api/Authentication/check-token", ct);

    public Task<ApiResponse> DeleteAccountAsync(CancellationToken ct = default) =>
        DeleteAsync("api/Authentication/delete", ct);

    public Task<ApiResponse> ForgotPasswordAsync(string usernameOrEmail, CancellationToken ct = default) =>
        PostAsync("api/Authentication/forgot-password", new { usernameOrEmail }, ct);

    public Task<ApiResponse> ResendConfirmationAsync(string usernameOrEmail, CancellationToken ct = default) =>
        PostAsync("api/Authentication/resend-confirmation", new { usernameOrEmail }, ct);

    public Task<ApiResponse> ResetPasswordAsync(string token, string newPassword, CancellationToken ct = default) =>
        PostAsync("api/Authentication/reset-password", new { token, newPassword }, ct);

    public Task<ApiResponse> NewEventAsync(CancellationToken ct = default) =>
        PostAsync("api/Event/new-event", ct: ct);

    public Task<ApiResponse> GetEventsAsync(CancellationToken ct = default) =>
        GetAsync("api/Event/get-events", ct);

    public Task<ApiResponse> GetEventAsync(long id, CancellationToken ct = default) =>
        GetAsync($"api/Event/get-event?id={id}", ct);

    public Task<ApiResponse> SaveEventAsync(JsonNode data, CancellationToken ct = default) =>
        PostAsync("api/Event/save-event", data, ct);

    public Task<ApiResponse> NewWizardAsync(JsonNode data, CancellationToken ct = default) =>
        PostAsync("api/Event/new-wizard", data, ct);

    public Task<ApiResponse> DeleteEventAsync(long id, CancellationToken ct = default) =>
        DeleteAsync($"api/Event/delete-event?id={id}", ct);

    public Task<ApiResponse> StartScheduleAsync(long id, CancellationToken ct = default) =>
        PostAsync($"api/Schedule/{id}", ct: ct);

    public Task<ApiResponse> GetScheduleAsync(long id, CancellationToken ct = default) =>
        GetAsync($"api/Schedule/{id}", ct);

    public Task<ApiResponse> IsRunningAsync(long id, CancellationToken ct = default) =>
        GetAsync($"api/Schedule/isRunning?id={id}", ct);

    public Task<ApiResponse> SolverStatusAsync(long id, CancellationToken ct = default) =>
        GetAsync($"api/Schedule/status?id={id}", ct);

    public Task<ApiResponse> StopScheduleAsync(long id, CancellationToken ct = default) =>
        PostAsync($"api/Schedule/stop?id={id}", ct: ct);

    public Task<ApiResponse> ExportAsync(long id, CancellationToken ct = default) =>
        GetAsync($"api/Schedule/export?id={id}", ct);

        public void CopyCookiesFrom(ApiClient other)
    {
        foreach (Cookie c in other.Cookies.GetAllCookies())
            Cookies.Add(Target.BaseUri, new Cookie(c.Name, c.Value, string.IsNullOrEmpty(c.Path) ? "/" : c.Path));
    }

    public void Dispose() => _http.Dispose();
}
