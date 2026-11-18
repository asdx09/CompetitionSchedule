using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ScheduleLogic.TestKit.Http;

/// <summary>
/// Egy HTTP kérés-válasz pár minden adata, amit a tesztek és a riport használ.
/// Hálózati hiba (nem fut a backend, időtúllépés) esetén a StatusCode 0,
/// és az Error mező tartalmazza az okot.
/// </summary>
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

    /// <summary>A válasz törzse szövegként (UTF-8).</summary>
    public string Body => _body ??= Bytes.Length == 0 ? "" : Encoding.UTF8.GetString(Bytes);

    public bool IsSuccess => StatusCode is >= 200 and < 300;

    public bool IsJson =>
        ContentType.Contains("json", StringComparison.OrdinalIgnoreCase) ||
        (Body.Length > 0 && (Body.TrimStart().StartsWith('{') || Body.TrimStart().StartsWith('[')));

    /// <summary>A válasz JSON-ként, vagy null, ha nem értelmezhető.</summary>
    public JsonNode? Json()
    {
        if (Bytes.Length == 0) return null;
        try { return JsonNode.Parse(Bytes); }
        catch (JsonException) { return null; }
    }

    /// <summary>Egy fejléc összes értéke (kis/nagybetű-érzéketlenül).</summary>
    public IEnumerable<string> HeaderValues(string name) =>
        Headers.Where(h => string.Equals(h.Key, name, StringComparison.OrdinalIgnoreCase))
               .SelectMany(h => h.Value);

    /// <summary>Rövid, egysoros leírás a naplóba.</summary>
    public string Summary =>
        Error != null
            ? $"{Method} {Path} -> HIBA: {Error}"
            : $"{Method} {Path} -> {StatusCode} ({ElapsedMs:0} ms)";

    /// <summary>A törzs rövidített változata a részletes naplóhoz.</summary>
    public string BodyPreview(int max = 600)
    {
        if (ContentType.Contains("spreadsheet", StringComparison.OrdinalIgnoreCase) ||
            ContentType.Contains("octet-stream", StringComparison.OrdinalIgnoreCase))
            return $"<bináris, {Bytes.Length} bájt>";
        var text = Body.Replace("\r", "").Replace("\n", " ");
        return text.Length <= max ? text : text[..max] + "…";
    }
}
