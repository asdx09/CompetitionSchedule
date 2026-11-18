using System.Text.Json.Nodes;

namespace ScheduleLogic.TestKit.Data;

/// <summary>
/// Teszt- és mérőadatok a save-event / new-wizard végpontokhoz, pontosan
/// abban a formában, ahogy az Angular kliens küldi: az új elemek ideiglenes
/// (GUID) azonosítót kapnak, a végleges azonosítót az adatbázis osztja ki.
/// </summary>
public static class SampleData
{
    public static readonly DateTime DefaultStart = new(2026, 3, 1, 7, 0, 0, DateTimeKind.Utc);

    private static string Tmp() => "tmp-" + Guid.NewGuid().ToString("N");
    private static string Iso(DateTime utc) => utc.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'");
    private static string Hms(int minutes) => TimeSpan.FromMinutes(minutes).ToString(@"hh\:mm\:ss");

    /// <summary>
    /// Kis, biztosan megoldható verseny: 2 helyszín, 2 versenyszám,
    /// 3 versenyző (egy csoportban egy), 4 nevezés, 1 tiltott időszak.
    /// </summary>
    public static JsonObject SmallEvent(long eventId, string name = "Teszt lőverseny")
    {
        string l1 = Tmp(), l2 = Tmp(), t1 = Tmp(), t2 = Tmp(), g1 = Tmp(), p1 = Tmp(), p2 = Tmp(), p3 = Tmp();
        return new JsonObject
        {
            ["eventData"] = EventData(eventId.ToString(), name, DefaultStart, DefaultStart.AddHours(9)),
            ["eventTypes"] = new JsonArray(
                EventType(t1, "Légpuska 40 lövés", 30),
                EventType(t2, "Légpisztoly 40 lövés", 20)),
            ["groups"] = new JsonArray(Group(g1, "Junior")),
            ["locations"] = new JsonArray(
                Location(l1, "Lőtér A", 2),
                Location(l2, "Lőtér B", 1)),
            ["participants"] = new JsonArray(
                Participant(p1, 1, "Kiss Anna", g1),
                Participant(p2, 2, "Nagy Béla", null),
                Participant(p3, 3, "Tóth Cili", null)),
            ["registrations"] = new JsonArray(
                Registration(p1, t1),
                Registration(p1, t2),
                Registration(p2, t1),
                Registration(p3, t2)),
            ["pauseTable"] = new JsonArray(Pause(l1, l2, 10)),
            ["locationTable"] = new JsonArray(
                LocationTable(t1, l1),
                LocationTable(t2, l1),
                LocationTable(t2, l2)),
            ["constraints"] = new JsonArray(
                Constraint(l2, "L", DefaultStart.AddHours(4), DefaultStart.AddHours(5.5))),
            ["timeZones"] = new JsonArray(),
        };
    }

    /// <summary>
    /// Nagyobb, valószerű verseny a terheléses méréshez (determinisztikus:
    /// ugyanazzal a seeddel mindig ugyanazt adja). Kb. 2 nevezés/versenyző.
    /// </summary>
    public static JsonObject LargeEvent(long eventId, int participants, int seed = 42, string name = "Mérési verseny")
    {
        var rnd = new Random(seed);
        participants = Math.Max(1, participants);
        int locationCount = Math.Clamp(participants / 40 + 2, 2, 12);
        int typeCount = 6;
        int groupCount = Math.Max(1, participants / 10);
        int days = 1 + participants / 150;

        var locations = Enumerable.Range(1, locationCount).Select(i => (Id: Tmp(), Name: $"Lőtér {i}", Slots: 6 + i % 5)).ToList();
        var types = Enumerable.Range(1, typeCount).Select(i => (Id: Tmp(), Name: $"Versenyszám {i}", Minutes: 20 + 10 * (i % 5))).ToList();
        var groups = Enumerable.Range(1, groupCount).Select(i => (Id: Tmp(), Name: $"Egyesület {i}")).ToList();

        var locationTable = new JsonArray();
        foreach (var (t, i) in types.Select((t, i) => (t, i)))
        {
            locationTable.Add(LocationTable(t.Id, locations[i % locationCount].Id));
            if (locationCount > 1 && i % 2 == 0)
                locationTable.Add(LocationTable(t.Id, locations[(i + 1) % locationCount].Id));
        }

        var participantArr = new JsonArray();
        var registrations = new JsonArray();
        for (int i = 1; i <= participants; i++)
        {
            string pid = Tmp();
            string? gid = rnd.NextDouble() < 0.5 ? groups[rnd.Next(groupCount)].Id : null;
            participantArr.Add(Participant(pid, i, $"Versenyző {i:000}", gid));
            foreach (var t in types.OrderBy(_ => rnd.Next()).Take(1 + rnd.Next(3)))
                registrations.Add(Registration(pid, t.Id));
        }

        var pauses = new JsonArray();
        for (int i = 0; i + 1 < locationCount; i++)
            pauses.Add(Pause(locations[i].Id, locations[i + 1].Id, 10));

        return new JsonObject
        {
            ["eventData"] = EventData(eventId.ToString(), name, DefaultStart, DefaultStart.AddDays(days - 1).AddHours(10)),
            ["eventTypes"] = new JsonArray(types.Select(t => (JsonNode)EventType(t.Id, t.Name, t.Minutes)).ToArray()),
            ["groups"] = new JsonArray(groups.Select(g => (JsonNode)Group(g.Id, g.Name)).ToArray()),
            ["locations"] = new JsonArray(locations.Select(l => (JsonNode)Location(l.Id, l.Name, l.Slots)).ToArray()),
            ["participants"] = participantArr,
            ["registrations"] = registrations,
            ["pauseTable"] = pauses,
            ["locationTable"] = locationTable,
            ["constraints"] = new JsonArray(
                Constraint(locations[0].Id, "L", DefaultStart.AddHours(5), DefaultStart.AddHours(6))),
            ["timeZones"] = new JsonArray(),
        };
    }

    /// <summary>
    /// A frontend egy már betöltött versenyt (a végleges azonosítókkal) küld
    /// vissza mentéskor; ez a get-event válaszából ilyen mentési kérést készít.
    /// </summary>
    public static JsonObject FromLoaded(JsonObject loaded)
    {
        var copy = (JsonObject)loaded.DeepClone();
        copy["timeZones"] = new JsonArray();
        return copy;
    }

    // ------------------------------------------------------------------

    public static JsonObject EventData(string eventId, string name, DateTime startUtc, DateTime endUtc) => new()
    {
        ["eventId"] = eventId,
        ["eventName"] = name,
        ["startDate"] = Iso(startUtc),
        ["endDate"] = Iso(endUtc),
        ["isPrivate"] = true,
        ["basePauseTime"] = 5,
        ["locationPauseTime"] = 5,
        ["locWeight"] = 1,
        ["groupWeight"] = 1,
        ["typeWeight"] = 1,
        ["compWeight"] = 1,
    };

    public static JsonObject EventType(string id, string name, int minutes) => new()
    {
        ["eventTypeId"] = id, ["eventId"] = "0", ["typeName"] = name, ["timeRange"] = Hms(minutes),
    };

    public static JsonObject Group(string id, string name) => new()
    {
        ["groupId"] = id, ["eventId"] = "0", ["groupName"] = name,
    };

    public static JsonObject Location(string id, string name, int slots) => new()
    {
        ["locationId"] = id, ["eventId"] = "0", ["locationName"] = name, ["slots"] = slots,
    };

    public static JsonObject Participant(string id, int number, string name, string? groupId) => new()
    {
        ["participantId"] = id, ["eventId"] = "0", ["competitorNumber"] = number,
        ["participantName"] = name, ["groupId"] = groupId,
    };

    public static JsonObject Registration(string participantId, string eventTypeId) => new()
    {
        ["registrationId"] = Tmp(), ["eventId"] = "0", ["participantId"] = participantId, ["eventTypeId"] = eventTypeId,
    };

    public static JsonObject Pause(string loc1, string loc2, int minutes) => new()
    {
        ["pauseId"] = Tmp(), ["eventId"] = "0", ["locationId1"] = loc1, ["locationId2"] = loc2, ["pause"] = Hms(minutes),
    };

    public static JsonObject LocationTable(string eventTypeId, string locationId) => new()
    {
        ["locationTableId"] = Tmp(), ["eventId"] = "0", ["eventTypeId"] = eventTypeId, ["locationId"] = locationId,
    };

    public static JsonObject Constraint(string objectId, string type, DateTime startUtc, DateTime endUtc) => new()
    {
        ["constraintId"] = Tmp(), ["eventId"] = "0", ["objectId"] = objectId, ["constraintType"] = type,
        ["startTime"] = Iso(startUtc), ["endTime"] = Iso(endUtc),
    };
}
