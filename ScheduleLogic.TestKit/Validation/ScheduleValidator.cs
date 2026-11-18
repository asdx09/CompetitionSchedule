using System.Text.Json.Nodes;

namespace ScheduleLogic.TestKit.Validation;

/// <summary>
/// Független ellenőrző: a backendtől lekért kész beosztásról (GET
/// api/Schedule/{id}) megnézi, hogy betartja-e a verseny szabályait. Nem a
/// solver kódját használja, hanem csak a bemenetet és az eredményt, így a
/// solver és a backend helyességét is igazolja (tesztorákulum).
///
/// Ellenőrzött szabályok:
///  1. minden nevezés pontosan egyszer szerepel a beosztásban,
///  2. az indulás hossza egyezik a versenyszám időtartamával,
///  3. az indulás olyan helyszínen van, ahol a versenyszám rendezhető,
///  4. a lőállás sorszáma 1 és a helyszín lőállásszáma között van,
///  5. egy lőálláson nincs két egymást fedő indulás,
///  6. egy helyszínen egyszerre legfeljebb annyian lőnek, ahány lőállás van,
///  7. egy versenyző indulásai nem fedik egymást,
///  8. egy versenyszám összes indulása ugyanazon a helyszínen van.
/// </summary>
public static class ScheduleValidator
{
    public sealed record Entry(string Participant, string EventType, string Location, int Start, int End, int Slot);

    public static List<string> Validate(JsonObject data)
    {
        var errors = new List<string>();

        var typeMinutes = Arr(data, "eventTypes").ToDictionary(
            t => Str(t, "eventTypeId"),
            t => (int)TimeSpan.Parse(Str(t, "timeRange")).TotalMinutes);
        var typeNames = Arr(data, "eventTypes").ToDictionary(t => Str(t, "eventTypeId"), t => Str(t, "typeName"));
        var slots = Arr(data, "locations").ToDictionary(l => Str(l, "locationId"), l => Int(l, "slots"));
        var allowed = Arr(data, "locationTable")
            .GroupBy(x => Str(x, "eventTypeId"))
            .ToDictionary(g => g.Key, g => g.Select(x => Str(x, "locationId")).ToHashSet());
        var names = Arr(data, "participants").ToDictionary(p => Str(p, "participantId"), p => Str(p, "participantName"));

        var entries = Arr(data, "timeZones").Select(z => new Entry(
            Str(z, "participantId"), Str(z, "eventTypeId"), Str(z, "locationId"),
            Int(z, "startTime"), Int(z, "endTime"), Int(z, "slot"))).ToList();

        string Who(Entry e) =>
            $"{names.GetValueOrDefault(e.Participant, "#" + e.Participant)} / {typeNames.GetValueOrDefault(e.EventType, "#" + e.EventType)}";

        // 1. minden nevezés pontosan egyszer
        var expected = Arr(data, "registrations")
            .Select(r => (Participant: Str(r, "participantId"), EventType: Str(r, "eventTypeId")))
            .ToList();
        var actual = entries.GroupBy(e => (e.Participant, e.EventType)).ToDictionary(g => g.Key, g => g.Count());
        foreach (var key in expected)
        {
            int n = actual.GetValueOrDefault(key);
            if (n != 1)
                errors.Add($"A(z) {names.GetValueOrDefault(key.Participant, key.Participant)} / {typeNames.GetValueOrDefault(key.EventType, key.EventType)} nevezés {n}× szerepel (pontosan 1 kellene).");
        }
        foreach (var key in actual.Keys.Except(expected))
            errors.Add($"A beosztásban olyan indulás van, amihez nincs nevezés: versenyző {key.Participant}, versenyszám {key.EventType}.");

        foreach (var e in entries)
        {
            // 2. időtartam
            if (typeMinutes.TryGetValue(e.EventType, out var minutes) && e.End - e.Start != minutes)
                errors.Add($"{Who(e)}: az indulás {e.End - e.Start} perces, a versenyszám {minutes} perces.");
            if (e.End <= e.Start)
                errors.Add($"{Who(e)}: a befejezés nem a kezdés után van ({e.Start}–{e.End}).");

            // 3. megengedett helyszín
            if (!allowed.TryGetValue(e.EventType, out var locs) || !locs.Contains(e.Location))
                errors.Add($"{Who(e)}: olyan helyszínre került ({e.Location}), ahol a versenyszám nem rendezhető.");

            // 4. lőállás sorszáma
            if (!slots.TryGetValue(e.Location, out var max))
                errors.Add($"{Who(e)}: ismeretlen helyszín ({e.Location}).");
            else if (e.Slot < 1 || e.Slot > max)
                errors.Add($"{Who(e)}: a lőállás sorszáma {e.Slot}, de a helyszínen csak {max} lőállás van.");
        }

        // 5. egy lőálláson nincs átfedés
        foreach (var g in entries.GroupBy(e => (e.Location, e.Slot)))
            foreach (var (a, b) in Overlapping(g))
                errors.Add($"Átfedés a(z) {g.Key.Location} helyszín {g.Key.Slot}. lőállásán: {Who(a)} ({a.Start}–{a.End}) és {Who(b)} ({b.Start}–{b.End}).");

        // 6. kapacitás (söpréssel: +1 kezdéskor, -1 befejezéskor)
        foreach (var g in entries.GroupBy(e => e.Location))
        {
            if (!slots.TryGetValue(g.Key, out var cap)) continue;
            int current = 0, peak = 0;
            foreach (var (_, delta) in g.SelectMany(e => new[] { (e.Start, +1), (e.End, -1) })
                                        .OrderBy(x => x.Item1).ThenBy(x => x.Item2))
            {
                current += delta;
                peak = Math.Max(peak, current);
            }
            if (peak > cap)
                errors.Add($"A(z) {g.Key} helyszínen egyszerre {peak} induló van, de csak {cap} lőállás.");
        }

        // 7. versenyző nem lehet két helyen egyszerre
        foreach (var g in entries.GroupBy(e => e.Participant))
            foreach (var (a, b) in Overlapping(g))
                errors.Add($"{names.GetValueOrDefault(g.Key, g.Key)} két indulása fedi egymást: {a.Start}–{a.End} és {b.Start}–{b.End}.");

        // 8. egy versenyszám egy helyszínen
        foreach (var g in entries.GroupBy(e => e.EventType))
        {
            var distinct = g.Select(e => e.Location).Distinct().ToList();
            if (distinct.Count > 1)
                errors.Add($"A(z) {typeNames.GetValueOrDefault(g.Key, g.Key)} versenyszám indulásai több helyszínen vannak: {string.Join(", ", distinct)}.");
        }

        return errors;
    }

    private static IEnumerable<(Entry, Entry)> Overlapping(IEnumerable<Entry> items)
    {
        var sorted = items.OrderBy(e => e.Start).ToList();
        for (int i = 0; i < sorted.Count; i++)
            for (int j = i + 1; j < sorted.Count && sorted[j].Start < sorted[i].End; j++)
                yield return (sorted[i], sorted[j]);
    }

    private static IEnumerable<JsonObject> Arr(JsonObject data, string name) =>
        (data[name] as JsonArray ?? new JsonArray()).OfType<JsonObject>();

    private static string Str(JsonObject o, string name) =>
        o[name] switch
        {
            null => "",
            JsonValue v when v.TryGetValue<string>(out var s) => s,
            JsonNode n => n.ToJsonString(),
        };

    private static int Int(JsonObject o, string name) =>
        o[name] switch
        {
            JsonValue v when v.TryGetValue<int>(out var i) => i,
            JsonValue v when v.TryGetValue<long>(out var l) => (int)l,
            JsonValue v when v.TryGetValue<string>(out var s) && int.TryParse(s, out var p) => p,
            _ => 0,
        };
}
