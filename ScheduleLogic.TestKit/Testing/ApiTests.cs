using System.Diagnostics;
using System.Text.Json.Nodes;
using ScheduleLogic.TestKit.Data;
using ScheduleLogic.TestKit.Http;
using ScheduleLogic.TestKit.Validation;

namespace ScheduleLogic.TestKit.Testing;

/// <summary>
/// A ScheduleLogic API fekete dobozos tesztjei. Mindkét backend (.NET és PHP)
/// ellen ugyanazok futnak, csak a HTTP felületet használják. Minden teszt
/// saját, véletlen nevű felhasználót hoz létre, és a végén törli, így
/// tetszőleges sorrendben és egyenként is futtathatók.
/// </summary>
public static class ApiTests
{
    public const string GroupSmoke = "Alap";
    public const string GroupAuth = "Hitelesítés";
    public const string GroupEvent = "Versenyek";
    public const string GroupAcl = "Jogosultság";
    public const string GroupValidation = "Bemenet-ellenőrzés";
    public const string GroupSchedule = "Beosztás (solver)";
    public const string GroupSecurity = "Biztonság";

    public static IReadOnlyList<string> Groups { get; } = new[]
    {
        GroupSmoke, GroupAuth, GroupEvent, GroupAcl, GroupValidation, GroupSchedule, GroupSecurity,
    };

    public static IReadOnlyList<TestCase> All { get; } = Build();

    // Property (nem mező): az All statikus inicializálása előbb fut, mint a mezőké.
    private static TimeSpan SolverTimeout => TimeSpan.FromSeconds(150);

    private static List<TestCase> Build() => new()
    {
        // ==============================================================
        // Alap
        // ==============================================================
        new TestCase
        {
            Id = "SMK-01", Group = GroupSmoke, Name = "Az API elérhető",
            Description = "Bejelentkezés nélkül a check-token végpontnak 401-et kell adnia. " +
                          "Ha ez sem megy, a backend nem fut, vagy rossz a cím.",
            Body = async ctx =>
            {
                var r = await ctx.NewClient().CheckTokenAsync(ctx.Ct);
                if (r.StatusCode == 0)
                    throw new TestFailedException($"A backend nem érhető el ({ctx.Target.BaseUrl}): {r.Error}");
                if (r.StatusCode == 404)
                    throw new TestFailedException("A cím válaszol, de 404-et ad: ez nem a ScheduleLogic API, vagy rossz a port.");
                ctx.Status(r, 401, "check-token bejelentkezés nélkül");
            },
        },

        // ==============================================================
        // Hitelesítés
        // ==============================================================
        new TestCase
        {
            Id = "AUTH-01", Group = GroupAuth, Name = "Regisztráció és belépés",
            Description = "Új felhasználó regisztrál és belép; a check-token a felhasználónevét adja vissza.",
            Body = async ctx =>
            {
                var u = await ctx.CreateUserAsync();
                var r = await u.Client.CheckTokenAsync(ctx.Ct);
                ctx.Status(r, 200, "check-token");
                ctx.Check(Str(r.Json(), "name") == u.Username,
                    $"A check-token a(z) '{Str(r.Json(), "name")}' nevet adta, a várt '{u.Username}' helyett.");
            },
        },
        new TestCase
        {
            Id = "AUTH-02", Group = GroupAuth, Name = "Foglalt felhasználónév",
            Description = "Ugyanazzal a névvel (kis- és nagybetűtől függetlenül) nem lehet még egyszer regisztrálni.",
            Body = async ctx =>
            {
                var u = await ctx.CreateUserAsync(login: false);
                var c = ctx.NewClient();
                var r = await c.RegisterAsync(u.Username, TestContext.DefaultPassword, "masik_" + u.Email, ctx.Ct);
                ctx.Status(r, 400, "Regisztráció foglalt névvel");
                r = await c.RegisterAsync(u.Username.ToUpperInvariant(), TestContext.DefaultPassword, "harmadik_" + u.Email, ctx.Ct);
                ctx.Status(r, 400, "Regisztráció foglalt névvel, nagybetűkkel");
            },
        },
        new TestCase
        {
            Id = "AUTH-03", Group = GroupAuth, Name = "Foglalt e-mail-cím",
            Description = "Ugyanazzal az e-mail-címmel nem lehet még egyszer regisztrálni.",
            Body = async ctx =>
            {
                var u = await ctx.CreateUserAsync(login: false);
                var r = await ctx.NewClient().RegisterAsync(TestContext.NewUsername(), TestContext.DefaultPassword, u.Email, ctx.Ct);
                ctx.Status(r, 400, "Regisztráció foglalt e-mail-címmel");
            },
        },
        new TestCase
        {
            Id = "AUTH-04", Group = GroupAuth, Name = "Hibás belépési adatok",
            Description = "Rossz jelszóval és nem létező felhasználóval is 401 a válasz.",
            Body = async ctx =>
            {
                var u = await ctx.CreateUserAsync(login: false);
                var c = ctx.NewClient();
                ctx.Status(await c.LoginAsync(u.Username, "Rossz-Jelszo-123", ctx.Ct), 401, "Belépés rossz jelszóval");
                ctx.Status(await c.LoginAsync(TestContext.NewUsername(), TestContext.DefaultPassword, ctx.Ct), 401,
                    "Belépés nem létező felhasználóval");
                ctx.Status(await c.CheckTokenAsync(ctx.Ct), 401, "check-token sikertelen belépések után");
            },
        },
        new TestCase
        {
            Id = "AUTH-05", Group = GroupAuth, Name = "Belépés e-mail-címmel",
            Description = "A felhasználónév helyett az e-mail-címmel is be lehet lépni.",
            Body = async ctx =>
            {
                var u = await ctx.CreateUserAsync(login: false);
                var c = ctx.NewClient();
                ctx.Status(await c.LoginAsync(u.Email, u.Password, ctx.Ct), 200, "Belépés e-mail-címmel");
                var r = await c.CheckTokenAsync(ctx.Ct);
                ctx.Status(r, 200, "check-token");
                ctx.Check(Str(r.Json(), "name") == u.Username, "A check-token nem a felhasználónevet adta vissza.");
            },
        },
        new TestCase
        {
            Id = "AUTH-06", Group = GroupAuth, Name = "Kijelentkezés",
            Description = "Kijelentkezés után a check-token 401-et ad.",
            Body = async ctx =>
            {
                var u = await ctx.CreateUserAsync();
                ctx.Status(await u.Client.LogoutAsync(ctx.Ct), 200, "Kijelentkezés");
                ctx.Status(await u.Client.CheckTokenAsync(ctx.Ct), 401, "check-token kijelentkezés után");
            },
        },
        new TestCase
        {
            Id = "AUTH-07", Group = GroupAuth, Name = "Védett végpontok bejelentkezés nélkül",
            Description = "Minden verseny- és beosztásvégpont 401-et ad bejelentkezés nélkül (nem átirányítást).",
            Body = async ctx =>
            {
                var c = ctx.NewClient();
                var calls = new (HttpMethod Method, string Path, object? Body)[]
                {
                    (HttpMethod.Get, "api/Event/get-events", null),
                    (HttpMethod.Get, "api/Event/get-event?id=1", null),
                    (HttpMethod.Post, "api/Event/new-event", null),
                    (HttpMethod.Post, "api/Event/save-event", SampleData.SmallEvent(1)),
                    (HttpMethod.Post, "api/Event/new-wizard", SampleData.SmallEvent(0)),
                    (HttpMethod.Delete, "api/Event/delete-event?id=1", null),
                    (HttpMethod.Post, "api/Schedule/1", null),
                    (HttpMethod.Get, "api/Schedule/1", null),
                    (HttpMethod.Get, "api/Schedule/isRunning?id=1", null),
                    (HttpMethod.Post, "api/Schedule/stop?id=1", null),
                    (HttpMethod.Get, "api/Schedule/export?id=1", null),
                    (HttpMethod.Post, "api/Authentication/logout", null),
                    (HttpMethod.Delete, "api/Authentication/delete", null),
                };
                var wrong = new List<string>();
                foreach (var (method, path, body) in calls)
                {
                    var r = await c.SendAsync(method, path, body, ct: ctx.Ct);
                    if (r.StatusCode != 401) wrong.Add($"{method} {path} -> {r.StatusCode}");
                }
                ctx.Check(wrong.Count == 0, "Ezek a végpontok nem 401-et adtak bejelentkezés nélkül: " + string.Join("; ", wrong));
            },
        },
        new TestCase
        {
            Id = "AUTH-08", Group = GroupAuth, Name = "Felhasználó-felderítés elleni védelem",
            Description = "Az elfelejtett jelszó és a megerősítés újraküldése mindig 200-at ad, a válaszból nem derül ki, " +
                          "létezik-e a fiók. (Létező fióknál csak a már megerősített esetet nézi, hogy ne menjen ki valódi levél.)",
            Body = async ctx =>
            {
                var u = await ctx.CreateUserAsync(login: false);
                var c = ctx.NewClient();
                var missing = TestContext.NewUsername();

                var forgotMissing = await c.ForgotPasswordAsync(missing, ctx.Ct);
                ctx.Status(forgotMissing, 200, "forgot-password nem létező fiókra");
                var resendMissing = await c.ResendConfirmationAsync(missing, ctx.Ct);
                ctx.Status(resendMissing, 200, "resend-confirmation nem létező fiókra");
                var resendExisting = await c.ResendConfirmationAsync(u.Username, ctx.Ct);
                ctx.Status(resendExisting, 200, "resend-confirmation létező (megerősített) fiókra");
                ctx.Check(resendMissing.Body == resendExisting.Body,
                    "A resend-confirmation válasza eltér létező és nem létező fióknál, ebből kiderül, hogy a fiók létezik.");
            },
        },
        new TestCase
        {
            Id = "AUTH-09", Group = GroupAuth, Name = "Érvénytelen jelszó-visszaállítás",
            Description = "Hamis tokennel és túl rövid új jelszóval a reset-password 400-at ad.",
            Body = async ctx =>
            {
                var c = ctx.NewClient();
                ctx.Status(await c.ResetPasswordAsync("NINCS-ILYEN-TOKEN-" + Guid.NewGuid().ToString("N"), "Uj-Jelszo-12345", ctx.Ct),
                    400, "reset-password hamis tokennel");
                ctx.Status(await c.ResetPasswordAsync("NINCS-ILYEN-TOKEN", "rovid", ctx.Ct), 400, "reset-password rövid jelszóval");
            },
        },
        new TestCase
        {
            Id = "AUTH-10", Group = GroupAuth, Name = "Fiók törlése",
            Description = "Törlés után a munkamenet megszűnik, és a fiókkal már nem lehet belépni.",
            Body = async ctx =>
            {
                var u = await ctx.CreateUserAsync();
                await ctx.CreateSampleEventAsync(u);
                ctx.Status(await u.Client.DeleteAccountAsync(ctx.Ct), 200, "Fiók törlése");
                u.Deleted = true;
                ctx.Status(await u.Client.CheckTokenAsync(ctx.Ct), 401, "check-token törlés után");
                ctx.Status(await ctx.NewClient().LoginAsync(u.Username, u.Password, ctx.Ct), 401, "Belépés a törölt fiókkal");
            },
        },

        // ==============================================================
        // Versenyek
        // ==============================================================
        new TestCase
        {
            Id = "EVT-01", Group = GroupEvent, Name = "Új, üres verseny",
            Description = "Új felhasználónak nincs versenye; a new-event után egy 'New Event' nevű, üres verseny jelenik meg, " +
                          "és a get-event mind a 10 adatlistát visszaadja.",
            Body = async ctx =>
            {
                var u = await ctx.CreateUserAsync();
                ctx.Check((await ctx.ListEventIdsAsync(u)).Count == 0, "Új felhasználónak nem lehet versenye.");
                long id = await ctx.CreateEventAsync(u);
                var ev = await ctx.GetEventJsonAsync(u, id);

                var expectedKeys = new[] { "eventData", "eventTypes", "groups", "locations", "participants",
                                           "registrations", "pauseTable", "locationTable", "constraints", "timeZones" };
                var missing = expectedKeys.Where(k => !ev.ContainsKey(k)).ToList();
                ctx.Check(missing.Count == 0, "Hiányzó mezők a get-event válaszában: " + string.Join(", ", missing));
                ctx.Check(Str(ev["eventData"], "eventId") == id.ToString(), "Az eventData.eventId nem egyezik a verseny azonosítójával.");
                ctx.Check(Str(ev["eventData"], "eventName") == "New Event",
                    $"Az új verseny neve '{Str(ev["eventData"], "eventName")}', a várt 'New Event' helyett.");
                foreach (var list in expectedKeys.Skip(1))
                    ctx.Check(Arr(ev, list).Count == 0, $"Az új verseny '{list}' listája nem üres.");
            },
        },
        new TestCase
        {
            Id = "EVT-02", Group = GroupEvent, Name = "Mentés és visszaolvasás",
            Description = "A teljes versenyt elmenti ideiglenes azonosítókkal, majd visszaolvassa: minden elem megvan, " +
                          "az azonosítók végleges értékre cserélődtek, és a hivatkozások az új azonosítókra mutatnak.",
            Body = async ctx =>
            {
                var u = await ctx.CreateUserAsync();
                var (_, ev) = await ctx.CreateSampleEventAsync(u);

                ctx.Check(Str(ev["eventData"], "eventName") == "Teszt lőverseny", "A verseny neve nem mentődött.");
                var counts = new Dictionary<string, int>
                {
                    ["eventTypes"] = 2, ["groups"] = 1, ["locations"] = 2, ["participants"] = 3,
                    ["registrations"] = 4, ["pauseTable"] = 1, ["locationTable"] = 3, ["constraints"] = 1,
                };
                foreach (var (list, n) in counts)
                    ctx.Check(Arr(ev, list).Count == n, $"A(z) '{list}' listában {Arr(ev, list).Count} elem van, {n} helyett.");

                ctx.Check(!ev.ToJsonString().Contains("tmp-"), "A mentett adatokban ideiglenes (tmp-) azonosító maradt.");

                var times = Arr(ev, "eventTypes").Select(t => Str(t, "timeRange")).OrderBy(x => x).ToList();
                ctx.Check(times.SequenceEqual(new[] { "00:20:00", "00:30:00" }),
                    "A versenyszámok időtartama nem 'óó:pp:mm' formában jött vissza: " + string.Join(", ", times));

                string groupId = Str(Arr(ev, "groups")[0], "groupId");
                var anna = Arr(ev, "participants").First(p => Str(p, "participantName") == "Kiss Anna");
                var bela = Arr(ev, "participants").First(p => Str(p, "participantName") == "Nagy Béla");
                ctx.Check(Str(anna, "groupId") == groupId, "A versenyző csoport-hivatkozása nem az új csoportazonosítóra mutat.");
                ctx.Check(bela!["groupId"] == null, "Csoport nélküli versenyzőnél a groupId nem null.");

                var participantIds = Arr(ev, "participants").Select(p => Str(p, "participantId")).ToHashSet();
                var typeIds = Arr(ev, "eventTypes").Select(t => Str(t, "eventTypeId")).ToHashSet();
                ctx.Check(Arr(ev, "registrations").All(r => participantIds.Contains(Str(r, "participantId")) &&
                                                             typeIds.Contains(Str(r, "eventTypeId"))),
                    "Van olyan nevezés, amely nem létező versenyzőre vagy versenyszámra hivatkozik.");

                string locB = Str(Arr(ev, "locations").First(l => Str(l, "locationName") == "Lőtér B"), "locationId");
                var constraint = Arr(ev, "constraints")[0];
                ctx.Check(Str(constraint, "objectId") == locB, "A tiltott időszak nem az új helyszínazonosítóra mutat.");
                ctx.Check(SameInstant(Str(constraint, "startTime"), SampleData.DefaultStart.AddHours(4)),
                    $"A tiltott időszak kezdete elcsúszott: {Str(constraint, "startTime")} (várt: 2026-03-01T11:00:00Z).");
            },
        },
        new TestCase
        {
            Id = "EVT-03", Group = GroupEvent, Name = "Szerkesztés (ismételt mentés)",
            Description = "Betöltött verseny módosítása: átnevezés, egy versenyző törlése, egy új hozzáadása.",
            Body = async ctx =>
            {
                var u = await ctx.CreateUserAsync();
                var (id, ev) = await ctx.CreateSampleEventAsync(u);

                var edit = SampleData.FromLoaded(ev);
                edit["eventData"]!["eventName"] = "Átnevezett verseny";
                var cili = Arr(edit, "participants").First(p => Str(p, "participantName") == "Tóth Cili")!;
                string ciliId = Str(cili, "participantId");
                Arr(edit, "participants").Remove(cili);
                foreach (var reg in Arr(edit, "registrations").Where(r => Str(r, "participantId") == ciliId).ToList())
                    Arr(edit, "registrations").Remove(reg);

                string newId = "tmp-" + Guid.NewGuid().ToString("N");
                Arr(edit, "participants").Add(SampleData.Participant(newId, 4, "Új Versenyző", null));
                Arr(edit, "registrations").Add(SampleData.Registration(newId, Str(Arr(edit, "eventTypes")[0], "eventTypeId")));

                ctx.Status(await u.Client.SaveEventAsync(edit, ctx.Ct), 200, "Módosított verseny mentése");
                var after = await ctx.GetEventJsonAsync(u, id);

                ctx.Check(Str(after["eventData"], "eventName") == "Átnevezett verseny", "Az új név nem mentődött.");
                var names = Arr(after, "participants").Select(p => Str(p, "participantName")).OrderBy(x => x).ToList();
                ctx.Check(names.SequenceEqual(new[] { "Kiss Anna", "Nagy Béla", "Új Versenyző" }),
                    "A versenyzők listája nem a várt: " + string.Join(", ", names));
                ctx.Check(Arr(after, "registrations").Count == 4, $"{Arr(after, "registrations").Count} nevezés van 4 helyett.");
            },
        },
        new TestCase
        {
            Id = "EVT-04", Group = GroupEvent, Name = "Hibás hivatkozás: elutasítás és visszagörgetés",
            Description = "Nem létező versenyzőre mutató nevezéssel a mentés 400-at ad, és a korábbi adatok változatlanok " +
                          "maradnak (a mentés egy tranzakció).",
            Body = async ctx =>
            {
                var u = await ctx.CreateUserAsync();
                var (id, ev) = await ctx.CreateSampleEventAsync(u);

                var bad = SampleData.FromLoaded(ev);
                bad["eventData"]!["eventName"] = "NEM SZABAD ELMENTENI";
                Arr(bad, "registrations").Add(SampleData.Registration("nincs-ilyen-versenyzo", Str(Arr(bad, "eventTypes")[0], "eventTypeId")));
                ctx.Status(await u.Client.SaveEventAsync(bad, ctx.Ct), 400, "Mentés hibás hivatkozással");

                var after = await ctx.GetEventJsonAsync(u, id);
                ctx.Check(Str(after["eventData"], "eventName") == "Teszt lőverseny",
                    "A sikertelen mentés után megváltozott a verseny neve: nincs visszagörgetés.");
                ctx.Check(Arr(after, "registrations").Count == 4 && Arr(after, "participants").Count == 3,
                    "A sikertelen mentés után megváltoztak a verseny adatai: nincs visszagörgetés.");
            },
        },
        new TestCase
        {
            Id = "EVT-05", Group = GroupEvent, Name = "Varázsló (új verseny importból)",
            Description = "A new-wizard egy lépésben hozza létre a teljes versenyt.",
            Body = async ctx =>
            {
                var u = await ctx.CreateUserAsync();
                var payload = SampleData.SmallEvent(0, "Varázsló verseny");
                ctx.Status(await u.Client.NewWizardAsync(payload, ctx.Ct), 200, "new-wizard");
                var ids = await ctx.ListEventIdsAsync(u);
                ctx.Check(ids.Count == 1, $"A varázsló után {ids.Count} verseny van 1 helyett.");
                var ev = await ctx.GetEventJsonAsync(u, ids[0]);
                ctx.Check(Str(ev["eventData"], "eventName") == "Varázsló verseny", "A varázslóval létrehozott verseny neve nem egyezik.");
                ctx.Check(Arr(ev, "registrations").Count == 4 && Arr(ev, "participants").Count == 3,
                    "A varázslóval létrehozott verseny adatai hiányosak.");
            },
        },
        new TestCase
        {
            Id = "EVT-06", Group = GroupEvent, Name = "Verseny törlése",
            Description = "Törlés után a verseny nem kérhető le és nincs a listában; másodszor törölve 404.",
            Body = async ctx =>
            {
                var u = await ctx.CreateUserAsync();
                var (id, _) = await ctx.CreateSampleEventAsync(u);
                ctx.Status(await u.Client.DeleteEventAsync(id, ctx.Ct), 200, "Törlés");
                ctx.Status(await u.Client.GetEventAsync(id, ctx.Ct), 404, "get-event törlés után");
                ctx.Check(!(await ctx.ListEventIdsAsync(u)).Contains(id), "A törölt verseny még a listában van.");
                ctx.Status(await u.Client.DeleteEventAsync(id, ctx.Ct), 404, "Ismételt törlés");
            },
        },
        new TestCase
        {
            Id = "EVT-07", Group = GroupEvent, Name = "Nem létező verseny",
            Description = "Nem létező azonosítóra a get-event és a delete-event is 404-et ad.",
            Body = async ctx =>
            {
                var u = await ctx.CreateUserAsync();
                ctx.Status(await u.Client.GetEventAsync(987_654_321, ctx.Ct), 404, "get-event nem létező azonosítóval");
                ctx.Status(await u.Client.DeleteEventAsync(987_654_321, ctx.Ct), 404, "delete-event nem létező azonosítóval");
            },
        },

        // ==============================================================
        // Jogosultság
        // ==============================================================
        new TestCase
        {
            Id = "ACL-01", Group = GroupAcl, Name = "Más felhasználó versenye nem érhető el",
            Description = "B felhasználó A versenyét nem olvashatja, mentheti, törölheti, nem indíthat rá beosztást és nem " +
                          "exportálhatja. A válasz 'nem található', így a verseny létezése sem derül ki.",
            Body = async ctx =>
            {
                var a = await ctx.CreateUserAsync();
                var (id, ev) = await ctx.CreateSampleEventAsync(a);
                var b = await ctx.CreateUserAsync();

                ctx.Status(await b.Client.GetEventAsync(id, ctx.Ct), 404, "B: get-event");
                ctx.Status(await b.Client.GetScheduleAsync(id, ctx.Ct), 404, "B: beosztás lekérése");
                ctx.Status(await b.Client.IsRunningAsync(id, ctx.Ct), 404, "B: isRunning");
                ctx.Status(await b.Client.StopScheduleAsync(id, ctx.Ct), 404, "B: solver leállítása");
                ctx.Status(await b.Client.ExportAsync(id, ctx.Ct), 404, "B: export");
                ctx.Status(await b.Client.StartScheduleAsync(id, ctx.Ct), 404, "B: beosztás indítása");

                var hijack = SampleData.FromLoaded(ev);
                hijack["eventData"]!["eventName"] = "B ÁTÍRTA";
                ctx.StatusIn(await b.Client.SaveEventAsync(hijack, ctx.Ct), "B: mentés A versenyére", 400, 404);
                ctx.Status(await b.Client.DeleteEventAsync(id, ctx.Ct), 404, "B: törlés");

                var after = await ctx.GetEventJsonAsync(a, id);
                ctx.Check(Str(after["eventData"], "eventName") == "Teszt lőverseny", "B felhasználó módosítani tudta A versenyét!");
            },
        },
        new TestCase
        {
            Id = "ACL-02", Group = GroupAcl, Name = "A versenylista csak a saját versenyeket mutatja",
            Description = "B listájában nem jelenik meg A versenye.",
            Body = async ctx =>
            {
                var a = await ctx.CreateUserAsync();
                long id = await ctx.CreateEventAsync(a);
                var b = await ctx.CreateUserAsync();
                ctx.Check(!(await ctx.ListEventIdsAsync(b)).Contains(id), "B látja A versenyét a saját listájában!");
            },
        },

        // ==============================================================
        // Bemenet-ellenőrzés
        // ==============================================================
        new TestCase
        {
            Id = "VAL-01", Group = GroupValidation, Name = "Üres belépési adatok",
            Description = "Üres felhasználónévvel és jelszóval a belépés 400-at vagy 401-et ad, nem 500-at.",
            Body = async ctx =>
            {
                // 400 (validációs hiba) vagy 401 is elfogadható, csak összeomlás (500) ne legyen.
                ctx.StatusIn(await ctx.NewClient().LoginAsync("", "", ctx.Ct), "Belépés üres adatokkal", 400, 401);
            },
        },
        new TestCase
        {
            Id = "VAL-02", Group = GroupValidation, Name = "Nem szám azonosító",
            Description = "get-event?id=abc -> 400.",
            Body = async ctx =>
            {
                var u = await ctx.CreateUserAsync();
                ctx.Status(await u.Client.GetAsync("api/Event/get-event?id=abc", ctx.Ct), 400, "get-event?id=abc");
            },
        },
        new TestCase
        {
            Id = "VAL-03", Group = GroupValidation, Name = "Hibás JSON",
            Description = "Szintaktikailag hibás JSON törzzsel a mentés 400-at ad, nem 500-at.",
            Body = async ctx =>
            {
                var u = await ctx.CreateUserAsync();
                var r = await u.Client.SendAsync(HttpMethod.Post, "api/Event/save-event", rawBody: "{\"eventData\": {nem json", ct: ctx.Ct);
                ctx.Status(r, 400, "Mentés hibás JSON-nal");
            },
        },
        new TestCase
        {
            Id = "VAL-04", Group = GroupValidation, Name = "Túl rövid jelszó regisztrációnál",
            Description = "8 karakternél rövidebb jelszóval nem lehet regisztrálni.",
            Body = async ctx =>
            {
                var name = TestContext.NewUsername();
                var c = ctx.NewClient();
                var r = await c.RegisterAsync(name, "abc", name + "@example.com", ctx.Ct);
                if (r.StatusCode == 200) await CleanupStrayUser(ctx, name, "abc");
                ctx.Status(r, 400, "Regisztráció 3 karakteres jelszóval");
            },
        },
        new TestCase
        {
            Id = "VAL-05", Group = GroupValidation, Name = "Érvénytelen e-mail-cím",
            Description = "Érvénytelen e-mail-címmel a regisztráció 400-at ad, nem 500-at.",
            Body = async ctx =>
            {
                var name = TestContext.NewUsername();
                var r = await ctx.NewClient().RegisterAsync(name, TestContext.DefaultPassword, "ez-nem-email", ctx.Ct);
                if (r.StatusCode == 200) await CleanupStrayUser(ctx, name, TestContext.DefaultPassword);
                ctx.Status(r, 400, "Regisztráció érvénytelen e-mail-címmel");
            },
        },
        new TestCase
        {
            Id = "VAL-06", Group = GroupValidation, Name = "Üres helyszínnév",
            Description = "Üres nevű helyszínnel a mentés 400-at ad.",
            Body = async ctx => await SaveShouldFail(ctx, ev => Arr(ev, "locations")[0]!["locationName"] = "", "üres helyszínnévvel"),
        },
        new TestCase
        {
            Id = "VAL-07", Group = GroupValidation, Name = "Negatív szünet",
            Description = "Negatív átállási idővel a mentés 400-at ad (adatbázis CHECK feltétel).",
            Body = async ctx => await SaveShouldFail(ctx, ev => ev["eventData"]!["basePauseTime"] = -5, "negatív szünettel"),
        },
        new TestCase
        {
            Id = "VAL-08", Group = GroupValidation, Name = "500-nál nagyobb súly",
            Description = "A célfüggvény súlya legfeljebb 500 lehet.",
            Body = async ctx => await SaveShouldFail(ctx, ev => ev["eventData"]!["locWeight"] = 501, "501-es súllyal"),
        },
        new TestCase
        {
            Id = "VAL-09", Group = GroupValidation, Name = "A befejezés a kezdés előtt van",
            Description = "Ha a verseny vége korábbi, mint a kezdete, a mentés 400-at ad.",
            Body = async ctx => await SaveShouldFail(ctx, ev =>
            {
                ev["eventData"]!["startDate"] = "2026-03-02T07:00:00Z";
                ev["eventData"]!["endDate"] = "2026-03-01T07:00:00Z";
            }, "fordított dátumokkal"),
        },

        // ==============================================================
        // Beosztás (solver)
        // ==============================================================
        new TestCase
        {
            Id = "SCH-01", Group = GroupSchedule, Name = "Solver állapot lekérdezése",
            Description = "Olyan versenyre, amire nem indult beosztás, az isRunning false-t ad.",
            Body = async ctx =>
            {
                var u = await ctx.CreateUserAsync();
                long id = await ctx.CreateEventAsync(u);
                var r = await u.Client.IsRunningAsync(id, ctx.Ct);
                ctx.Status(r, 200, "isRunning");
                ctx.Check(r.Body.Trim() == "false", $"Az isRunning válasza '{r.Body.Trim()}', a várt 'false' helyett.");
            },
        },
        new TestCase
        {
            Id = "SCH-02", Group = GroupSchedule, Name = "Beosztás készítése és ellenőrzése",
            Description = "Elindítja a solvert a minta versenyre, megvárja az eredményt, majd a független validátorral " +
                          "ellenőrzi: minden nevezés pontosan egyszer szerepel, a hosszak, helyszínek, lőállások rendben " +
                          "vannak, és nincs átfedés.",
            RequiresSolver = true,
            Timeout = SolverTimeout,
            Body = async ctx =>
            {
                var u = await ctx.CreateUserAsync();
                var (id, _) = await ctx.CreateSampleEventAsync(u);
                var schedule = await SolveAsync(ctx, u, id, expected: 4);
                var errors = ScheduleValidator.Validate(schedule);
                foreach (var e in errors) ctx.Info("Szabálysértés: " + e);
                ctx.Check(errors.Count == 0, $"A beosztás {errors.Count} szabályt sért, pl.: {errors.FirstOrDefault()}");
            },
        },
        new TestCase
        {
            Id = "SCH-03", Group = GroupSchedule, Name = "Excel export",
            Description = "Kész beosztásnál az export érvényes .xlsx fájlt ad (a ZIP fejléc 'PK').",
            RequiresSolver = true,
            Timeout = SolverTimeout,
            Body = async ctx =>
            {
                var u = await ctx.CreateUserAsync();
                var (id, _) = await ctx.CreateSampleEventAsync(u);
                await SolveAsync(ctx, u, id, expected: 4);
                var r = await u.Client.ExportAsync(id, ctx.Ct);
                ctx.Status(r, 200, "Export");
                ctx.Check(r.ContentType.Contains("spreadsheetml", StringComparison.OrdinalIgnoreCase),
                    $"Az export Content-Type fejléce '{r.ContentType}', nem Excel.");
                ctx.Check(r.Bytes.Length > 100 && r.Bytes[0] == (byte)'P' && r.Bytes[1] == (byte)'K',
                    "Az exportált fájl nem érvényes .xlsx (nem ZIP).");
            },
        },
        new TestCase
        {
            Id = "SCH-04", Group = GroupSchedule, Name = "Mentés után a régi beosztás törlődik",
            Description = "A verseny újramentése érvényteleníti a korábbi beosztást, mert az már nem felelne meg az adatoknak.",
            RequiresSolver = true,
            Timeout = SolverTimeout,
            Body = async ctx =>
            {
                var u = await ctx.CreateUserAsync();
                var (id, ev) = await ctx.CreateSampleEventAsync(u);
                await SolveAsync(ctx, u, id, expected: 4);
                ctx.Status(await u.Client.SaveEventAsync(SampleData.FromLoaded(ev), ctx.Ct), 200, "Újramentés");
                var r = await u.Client.GetScheduleAsync(id, ctx.Ct);
                ctx.Status(r, 200, "Beosztás lekérése újramentés után");
                ctx.Check(Arr(r.Json() as JsonObject, "timeZones").Count == 0, "Újramentés után megmaradt a régi beosztás.");
            },
        },
        new TestCase
        {
            Id = "SCH-05", Group = GroupSchedule, Name = "Futó keresés leállítása",
            Description = "Egy nagyobb versenyre indított keresést leállít, utána az isRunning false.",
            RequiresSolver = true,
            Timeout = SolverTimeout,
            Body = async ctx =>
            {
                var u = await ctx.CreateUserAsync();
                long id = await ctx.CreateEventAsync(u);
                ctx.Status(await u.Client.SaveEventAsync(SampleData.LargeEvent(id, 120), ctx.Ct), 200, "Nagy verseny mentése");
                try
                {
                    ctx.Status(await u.Client.StartScheduleAsync(id, ctx.Ct), 200, "Beosztás indítása");
                    await Task.Delay(1500, ctx.Ct);
                    var stop = await u.Client.StopScheduleAsync(id, ctx.Ct);
                    ctx.Status(stop, 200, "Leállítás");
                    ctx.Check(stop.Body.Trim() is "true" or "false", $"A leállítás válasza nem logikai érték: '{stop.Body}'.");
                    await WaitNotRunningAsync(ctx, u, id, TimeSpan.FromSeconds(20));
                }
                finally
                {
                    // Ha a teszt elbukott, akkor se maradjon futó keresés a solverben.
                    await u.Client.StopScheduleAsync(id, CancellationToken.None);
                }
            },
        },

        // ==============================================================
        // Biztonság
        // ==============================================================
        new TestCase
        {
            Id = "SEC-01", Group = GroupSecurity, Name = "Hamis solver-válasz elutasítása",
            Description = "Az api/Schedule/answer végpontot csak a solver hívhatja. A teszt egy hitelesítés nélküli, " +
                          "kitalált beosztást küld egy létező versenyre: ezt el kell utasítani, és nem kerülhet be az " +
                          "adatbázisba. (Reverse proxy mögött minden kérés localhostról érkezik, ezért az IP-alapú " +
                          "ellenőrzés nem elég.)",
            Body = async ctx =>
            {
                var u = await ctx.CreateUserAsync();
                var (id, ev) = await ctx.CreateSampleEventAsync(u);
                var forged = new JsonObject
                {
                    ["Status"] = "OPTIMAL",
                    ["EventId"] = id,
                    ["SolutionNumber"] = "1",
                    ["Schedule"] = new JsonArray(Arr(ev, "registrations").Select((r, i) => (JsonNode)new JsonObject
                    {
                        ["ParticipantId"] = long.Parse(Str(r, "participantId")),
                        ["EventTypeId"] = long.Parse(Str(r, "eventTypeId")),
                        ["LocationId"] = long.Parse(Str(Arr(ev, "locations")[0], "locationId")),
                        ["Start"] = 600 + i * 60,
                        ["End"] = 630 + i * 60,
                        ["Slot"] = 0,
                    }).ToArray()),
                };
                var r = await ctx.NewClient().PostAsync("api/Schedule/answer", forged, ctx.Ct);
                var stored = await u.Client.GetScheduleAsync(id, ctx.Ct);
                int count = Arr(stored.Json() as JsonObject, "timeZones").Count;
                ctx.Check(!r.IsSuccess && count == 0,
                    $"A backend elfogadta a hitelesítés nélküli solver-választ ({r.StatusCode}), és {count} kitalált indulás " +
                    "került a beosztásba. Az answer végpontnak aláírt / titkos kulcsos visszahívást kellene várnia.");
            },
        },
        new TestCase
        {
            Id = "SEC-02", Group = GroupSecurity, Name = "Kijelentkezés után a régi süti érvénytelen",
            Description = "Egy elmentett munkamenet-sütit kijelentkezés után visszajátszva a backendnek 401-et kell adnia " +
                          "(pl. ha a sütit korábban ellopták).",
            Body = async ctx =>
            {
                var u = await ctx.CreateUserAsync();
                var thief = ctx.NewClient();
                thief.CopyCookiesFrom(u.Client);
                ctx.Status(await thief.CheckTokenAsync(ctx.Ct), 200, "A másolt süti kijelentkezés előtt");
                ctx.Status(await u.Client.LogoutAsync(ctx.Ct), 200, "Kijelentkezés");
                var r = await thief.CheckTokenAsync(ctx.Ct);
                ctx.Check(r.StatusCode == 401,
                    $"Kijelentkezés után a régi süti még érvényes ({r.StatusCode}). A munkamenetet szerveroldalon is " +
                    "érvényteleníteni kellene (pl. security stamp vagy szerveroldali session).");
            },
        },
        new TestCase
        {
            Id = "SEC-03", Group = GroupSecurity, Name = "A munkamenet-süti beállításai",
            Description = "A belépési süti HttpOnly (JavaScript nem olvashatja), és a SameSite értéke nem None.",
            Body = async ctx =>
            {
                var u = await ctx.CreateUserAsync(login: false);
                var r = await u.Client.LoginAsync(u.Username, u.Password, ctx.Ct);
                ctx.Status(r, 200, "Belépés");
                u.LoggedIn = true;
                var cookie = r.HeaderValues("Set-Cookie").FirstOrDefault(c => c.StartsWith("sl_auth=", StringComparison.Ordinal));
                ctx.Check(cookie != null, "A belépés nem állított be 'sl_auth' sütit.");
                ctx.Info("Set-Cookie: " + cookie);
                var lower = cookie!.ToLowerInvariant();
                ctx.Check(lower.Contains("httponly"), "A munkamenet-süti nem HttpOnly.");
                ctx.Check(!lower.Contains("samesite=none"), "A munkamenet-süti SameSite=None, így más oldalról is elküldi a böngésző (CSRF).");
            },
        },
        new TestCase
        {
            Id = "SEC-04", Group = GroupSecurity, Name = "CORS: idegen oldal nem kap hozzáférést",
            Description = "Ismeretlen Origin-ről érkező preflight kérésre nincs Access-Control-Allow-Origin fejléc.",
            Body = async ctx =>
            {
                var r = await ctx.NewClient().SendAsync(HttpMethod.Options, "api/Event/get-events",
                    headers: new Dictionary<string, string>
                    {
                        ["Origin"] = "https://gonosz-oldal.example",
                        ["Access-Control-Request-Method"] = "GET",
                    }, ct: ctx.Ct);
                ctx.Check(r.StatusCode != 0, "A backend nem válaszolt.");
                var allowed = r.HeaderValues("Access-Control-Allow-Origin").ToList();
                ctx.Check(allowed.Count == 0,
                    "Idegen Origin-re is jött Access-Control-Allow-Origin fejléc: " + string.Join(", ", allowed));
            },
        },
        new TestCase
        {
            Id = "SEC-05", Group = GroupSecurity, Name = "A hibaüzenet nem szivárogtat belső részleteket",
            Description = "Hibás kérésekre a válasz nem tartalmaz kivételnevet, stack trace-t vagy SQL hibát.",
            Body = async ctx =>
            {
                var u = await ctx.CreateUserAsync();
                var responses = new List<ApiResponse>
                {
                    await ctx.NewClient().RegisterAsync(TestContext.NewUsername(), TestContext.DefaultPassword, "ez-nem-email", ctx.Ct),
                    await u.Client.SendAsync(HttpMethod.Post, "api/Event/save-event", rawBody: "{\"eventData\": {nem json", ct: ctx.Ct),
                    await u.Client.SaveEventAsync(new JsonObject { ["eventData"] = new JsonObject { ["eventId"] = "nem-szam" } }, ctx.Ct),
                };
                string[] leaks = { "Exception", "   at ", "Npgsql", "SQLSTATE", "PDOException", "Stack trace", ".cs:line", ".php" };
                foreach (var r in responses)
                {
                    var found = leaks.Where(l => r.Body.Contains(l, StringComparison.OrdinalIgnoreCase)).ToList();
                    ctx.Check(found.Count == 0, $"{r.Method} {r.Path} ({r.StatusCode}) válaszában belső részlet látszik: " +
                                                string.Join(", ", found));
                }
            },
        },
    };

    // ------------------------------------------------------------------
    // Segédfüggvények
    // ------------------------------------------------------------------

    /// <summary>Mentés egy módosított minta versennyel, aminek 400-at kell adnia, és az adatok nem változhatnak.</summary>
    private static async Task SaveShouldFail(TestContext ctx, Action<JsonObject> modify, string what)
    {
        var u = await ctx.CreateUserAsync();
        var (id, ev) = await ctx.CreateSampleEventAsync(u);
        var bad = SampleData.FromLoaded(ev);
        modify(bad);
        ctx.Status(await u.Client.SaveEventAsync(bad, ctx.Ct), 400, "Mentés " + what);
        var after = await ctx.GetEventJsonAsync(u, id);
        ctx.Check(JsonNode.DeepEquals(after["eventData"], ev["eventData"]) && Arr(after, "locations").Count == 2,
            "A sikertelen mentés után megváltoztak a verseny adatai.");
    }

    /// <summary>Ha egy elutasítandó regisztráció mégis sikerült, a felhasználót töröljük.</summary>
    private static async Task CleanupStrayUser(TestContext ctx, string username, string password)
    {
        var c = ctx.NewClient();
        if ((await c.LoginAsync(username, password, CancellationToken.None)).StatusCode == 200)
            await c.DeleteAccountAsync(CancellationToken.None);
        else
            ctx.Info($"A(z) {username} felhasználó létrejött, de nem lehet vele belépni, így nem törölhető automatikusan.");
    }

    /// <summary>Beosztás indítása és megvárása, amíg minden nevezés bekerül.</summary>
    private static async Task<JsonObject> SolveAsync(TestContext ctx, TestUser u, long id, int expected)
    {
        var sw = Stopwatch.StartNew();
        ctx.Status(await u.Client.StartScheduleAsync(id, ctx.Ct), 200, "Beosztás indítása");
        ctx.RecordExchanges = false;
        try
        {
            while (sw.Elapsed < TimeSpan.FromSeconds(120))
            {
                var r = await u.Client.GetScheduleAsync(id, ctx.Ct);
                ctx.Status(r, 200, "Beosztás lekérése");
                if (Arr(r.Json() as JsonObject, "timeZones").Count >= expected) break;
                await Task.Delay(1000, ctx.Ct);
            }
        }
        finally
        {
            ctx.RecordExchanges = true;
        }

        var final = await u.Client.GetScheduleAsync(id, ctx.Ct);
        ctx.Status(final, 200, "Beosztás lekérése");
        var json = final.Json() as JsonObject ?? throw new TestFailedException("A beosztás válasza nem JSON objektum.");
        int n = Arr(json, "timeZones").Count;
        if (n < expected)
        {
            var running = await u.Client.IsRunningAsync(id, ctx.Ct);
            throw new TestFailedException(
                $"{sw.Elapsed.TotalSeconds:0} s alatt nem készült el a beosztás ({n}/{expected} indulás, isRunning: {running.Body.Trim()}). " +
                "Fut a Python solver (ScheduleSolver/Main.py), és eléri a backend visszahívási címét (URLS:Backend)?");
        }
        ctx.Info($"A beosztás {sw.Elapsed.TotalSeconds:0.0} s alatt elkészült ({n} indulás).");
        return json;
    }

    private static async Task WaitNotRunningAsync(TestContext ctx, TestUser u, long id, TimeSpan timeout)
    {
        var sw = Stopwatch.StartNew();
        ctx.RecordExchanges = false;
        try
        {
            while (sw.Elapsed < timeout)
            {
                var r = await u.Client.IsRunningAsync(id, ctx.Ct);
                ctx.Status(r, 200, "isRunning");
                if (r.Body.Trim() == "false") return;
                await Task.Delay(500, ctx.Ct);
            }
        }
        finally
        {
            ctx.RecordExchanges = true;
        }
        throw new TestFailedException($"Leállítás után {timeout.TotalSeconds:0} s-mal is fut a keresés.");
    }

    private static JsonArray Arr(JsonNode? o, string name) => o?[name] as JsonArray ?? new JsonArray();

    /// <summary>Egy mező szöveges értéke (szám esetén is), vagy üres szöveg.</summary>
    private static string Str(JsonNode? o, string name) =>
        o?[name] switch
        {
            null => "",
            JsonValue v when v.TryGetValue<string>(out var s) => s,
            JsonNode n => n.ToJsonString(),
        };

    private static bool SameInstant(string value, DateTime expectedUtc) =>
        DateTimeOffset.TryParse(value, out var dto) && dto.UtcDateTime == expectedUtc;
}
