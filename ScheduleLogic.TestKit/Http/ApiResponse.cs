using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ScheduleLogic.TestKit.Http;

public sealed class ApiResponse
{
    public required string Method { get; init; }
    public required string Path { get; init; }
    public string? RequestBody { get; init; }

    public int StatusCode { get; init; }
    public string ContentType { get; init; } = "";
    public byte[] Bytes { get; init; } = Array.Empty<byte>();
    public IReadOnlyDictionary<string, string[]> Headers { get; init; } = new Dictionary<string, string[]>();
    public double ElapsedMs { get; init; }
    public string? Error { get; init; }

    private string? _body;

        public string Body => _body ??= Bytes.Length == 0 ? "" : Encoding.UTF8.GetString(Bytes);

    public bool IsSuccess => StatusCode is >= 200 and < 300;

    public bool IsJson =>
        ContentType.Contains("json", StringComparison.OrdinalIgnoreCase) ||
        (Body.Length > 0 && (Body.TrimStart().StartsWith('{') || Body.TrimStart().StartsWith('[')));

        public JsonNode? Json()
    {
        if (Bytes.Length == 0) return null;
        try { return JsonNode.Parse(Bytes); }
        catch (JsonException) { return null; }
    }

        public IEnumerable<string> HeaderValues(string name) =>
        Headers.Where(h => string.Equals(h.Key, name, StringComparison.OrdinalIgnoreCase))
               .SelectMany(h => h.Value);

        public string Summary =>
        Error != null
            ? $"{Method} {Path} -> ERROR: {Error}"
            : $"{Method} {Path} -> {StatusCode} ({ElapsedMs:0} ms)";

        public string BodyPreview(int max = 600)
    {
        if (ContentType.Contains("spreadsheet", StringComparison.OrdinalIgnoreCase) ||
            ContentType.Contains("octet-stream", StringComparison.OrdinalIgnoreCase))
            return $"<binary, {Bytes.Length} bytes>";
        var text = Body.Replace("\r", "").Replace("\n", " ");
        return text.Length <= max ? text : text[..max] + "…";
    }
}
