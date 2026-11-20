using System.Text.Json.Nodes;

namespace ScheduleLogic.TestKit.Validation;

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

        var expected = Arr(data, "registrations")
            .Select(r => (Participant: Str(r, "participantId"), EventType: Str(r, "eventTypeId")))
            .ToList();
        var actual = entries.GroupBy(e => (e.Participant, e.EventType)).ToDictionary(g => g.Key, g => g.Count());
        foreach (var key in expected)
        {
            int n = actual.GetValueOrDefault(key);
            if (n != 1)
                errors.Add($"Entry {names.GetValueOrDefault(key.Participant, key.Participant)} / {typeNames.GetValueOrDefault(key.EventType, key.EventType)} appears {n} times (should be exactly 1).");
        }
        foreach (var key in actual.Keys.Except(expected))
            errors.Add($"The schedule contains a start without an entry: competitor {key.Participant}, event type {key.EventType}.");

        foreach (var e in entries)
        {
            if (typeMinutes.TryGetValue(e.EventType, out var minutes) && e.End - e.Start != minutes)
                errors.Add($"{Who(e)}: the start is {e.End - e.Start} minutes long, the event type takes {minutes} minutes.");
            if (e.End <= e.Start)
                errors.Add($"{Who(e)}: the end is not after the start ({e.Start}–{e.End}).");

            if (!allowed.TryGetValue(e.EventType, out var locs) || !locs.Contains(e.Location))
                errors.Add($"{Who(e)}: placed at a location ({e.Location}) where the event type cannot be held.");

            if (!slots.TryGetValue(e.Location, out var max))
                errors.Add($"{Who(e)}: unknown location ({e.Location}).");
            else if (e.Slot < 1 || e.Slot > max)
                errors.Add($"{Who(e)}: lane number is {e.Slot}, but the location only has {max} lanes.");
        }

        foreach (var g in entries.GroupBy(e => (e.Location, e.Slot)))
            foreach (var (a, b) in Overlapping(g))
                errors.Add($"Overlap at location {g.Key.Location}, lane {g.Key.Slot}: {Who(a)} ({a.Start}–{a.End}) and {Who(b)} ({b.Start}–{b.End}).");

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
                errors.Add($"Location {g.Key} has {peak} shooters at the same time, but only {cap} lanes.");
        }

        foreach (var g in entries.GroupBy(e => e.Participant))
            foreach (var (a, b) in Overlapping(g))
                errors.Add($"Two starts of {names.GetValueOrDefault(g.Key, g.Key)} overlap: {a.Start}–{a.End} and {b.Start}–{b.End}.");

        foreach (var g in entries.GroupBy(e => e.EventType))
        {
            var distinct = g.Select(e => e.Location).Distinct().ToList();
            if (distinct.Count > 1)
                errors.Add($"The starts of event type {typeNames.GetValueOrDefault(g.Key, g.Key)} are at several locations: {string.Join(", ", distinct)}.");
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
