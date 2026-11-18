using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using ScheduleLogic.TestKit.Http;

namespace ScheduleLogic.TestKit.Testing;

/// <summary>
/// A válaszok "szerkezeti ujjlenyomata" a két backend összehasonlításához:
/// a JSON kulcsai és típusai, értékek nélkül. Két backend akkor ad azonos
/// szerkezetű választ, ha ez az ujjlenyomat egyezik, függetlenül attól, hogy
/// az adatbázis milyen azonosítókat osztott ki.
/// </summary>
public static partial class JsonShape
{
    public static string Of(ApiResponse r)
    {
        if (r.StatusCode == 0) return "network-error";
        if (r.Bytes.Length == 0) return "empty";
        if (r.ContentType.Contains("spreadsheet", StringComparison.OrdinalIgnoreCase)) return "xlsx";

        var node = r.IsJson ? r.Json() : null;
        // Egy JSON szöveg ("Username already taken!") ugyanaz a válasz, mint a
        // sima szöveges: a .NET a kliens Accept fejléce miatt JSON-ként küldi.
        if (node is JsonValue sv && sv.GetValueKind() == JsonValueKind.String)
            return Text(sv.GetValue<string>());
        if (node != null) return Describe(node);
        if (r.Body.TrimStart().StartsWith("<", StringComparison.Ordinal)) return "html";

        // Rövid szöveges válasz (pl. "Username already taken!"): ez is a
        // szerződés része, a frontend megjeleníti.
        return Text(r.Body);
    }

    private static string Text(string body)
    {
        var text = body.Trim();
        return "text:" + (text.Length > 120 ? text[..120] : text);
    }

    /// <summary>
    /// Ezeket a mezőket a szerkezet-összehasonlítás figyelmen kívül hagyja:
    /// a traceId az ASP.NET ProblemDetails diagnosztikai mezője, nem a
    /// szerződés része.
    /// </summary>
    private static readonly HashSet<string> IgnoredKeys = new(StringComparer.OrdinalIgnoreCase) { "traceId" };

    public static string Describe(JsonNode? node) => node switch
    {
        null => "null",
        JsonObject obj => "{" + string.Join(",", obj
            .Where(p => !IgnoredKeys.Contains(p.Key))
            .OrderBy(p => p.Key, StringComparer.OrdinalIgnoreCase)
            // A validációs hibák ("errors") kulcsai a hibaüzenet részletei
            // (pl. "$.eventData" vagy "Locations[0].LocationName"), ezek
            // backendenként eltérhetnek: csak azt nézzük, hogy van-e ilyen mező.
            .Select(p => p.Key + ":" + (string.Equals(p.Key, "errors", StringComparison.OrdinalIgnoreCase) ? "{…}" : Describe(p.Value)))) + "}",
        JsonArray arr => arr.Count == 0 ? "[]" : "[" + Describe(arr[0]) + "]",
        JsonValue v => v.GetValueKind() switch
        {
            JsonValueKind.String => "string",
            JsonValueKind.Number => "number",
            JsonValueKind.True or JsonValueKind.False => "bool",
            _ => "null",
        },
        _ => "?",
    };

    /// <summary>
    /// Útvonal azonosítók nélkül: api/Schedule/123 -> api/Schedule/{n},
    /// get-event?id=42 -> get-event?id={n}.
    /// </summary>
    public static string NormalizePath(string path)
    {
        var p = NumberSegment().Replace(path, "/{n}");
        p = NumberQuery().Replace(p, "=${1}{n}");
        return p;
    }

    [GeneratedRegex(@"/\d+(?=/|\?|$)")]
    private static partial Regex NumberSegment();

    [GeneratedRegex(@"=(-?)\d+")]
    private static partial Regex NumberQuery();
}
