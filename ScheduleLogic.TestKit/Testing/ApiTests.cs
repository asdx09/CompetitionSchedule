using System.Diagnostics;
using System.Text.Json.Nodes;
using ScheduleLogic.TestKit.Data;
using ScheduleLogic.TestKit.Http;
using ScheduleLogic.TestKit.Validation;

namespace ScheduleLogic.TestKit.Testing;

public static class ApiTests
{
    public const string GroupSmoke = "Smoke";
    public const string GroupAuth = "Authentication";
    public const string GroupEvent = "Events";
    public const string GroupAcl = "Access control";
    public const string GroupValidation = "Input validation";
    public const string GroupSchedule = "Scheduling (solver)";
    public const string GroupSecurity = "Security";

    public static IReadOnlyList<string> Groups { get; } = new[]
    {
        GroupSmoke, GroupAuth, GroupEvent, GroupAcl, GroupValidation, GroupSchedule, GroupSecurity,
    };

    public static IReadOnlyList<TestCase> All { get; } = Build();

    // Property, not field: the static initializer of All runs before the fields.
    private static TimeSpan SolverTimeout => TimeSpan.FromSeconds(150);

    private const string SampleEventName = "Test shooting competition";
    private const string Anna = "Anna Smith";
    private const string Ben = "Ben Taylor";
    private const string Cecil = "Cecil Brown";
    private const string RangeB = "Range B";

    private static List<TestCase> Build() => new()
    {
        new TestCase
        {
            Id = "SMK-01", Group = GroupSmoke, Name = "API is reachable",
            Description = "Without login, the check-token endpoint must return 401. " +
                          "If even this fails, the backend is not running or the address is wrong.",
            Body = async ctx =>
            {
                var r = await ctx.NewClient().CheckTokenAsync(ctx.Ct);
                if (r.StatusCode == 0)
                    throw new TestFailedException($"The backend is not reachable ({ctx.Target.BaseUrl}): {r.Error}");
                if (r.StatusCode == 404)
                    throw new TestFailedException("The address responds, but with 404: this is not the ScheduleLogic API, or the port is wrong.");
                ctx.Status(r, 401, "check-token without login");
            },
        },

        new TestCase
        {
            Id = "AUTH-01", Group = GroupAuth, Name = "Registration and login",
            Description = "A new user registers and logs in; check-token returns the username.",
            Body = async ctx =>
            {
                var u = await ctx.CreateUserAsync();
                var r = await u.Client.CheckTokenAsync(ctx.Ct);
                ctx.Status(r, 200, "check-token");
                ctx.Check(Str(r.Json(), "name") == u.Username,
                    $"check-token returned the name '{Str(r.Json(), "name")}' instead of the expected '{u.Username}'.");
            },
        },
        new TestCase
        {
            Id = "AUTH-02", Group = GroupAuth, Name = "Username already taken",
            Description = "The same username (case-insensitively) cannot be registered again.",
            Body = async ctx =>
            {
                var u = await ctx.CreateUserAsync(login: false);
                var c = ctx.NewClient();
                var r = await c.RegisterAsync(u.Username, TestContext.DefaultPassword, "other_" + u.Email, ctx.Ct);
                ctx.Status(r, 400, "Registration with a taken username");
                r = await c.RegisterAsync(u.Username.ToUpperInvariant(), TestContext.DefaultPassword, "third_" + u.Email, ctx.Ct);
                ctx.Status(r, 400, "Registration with a taken username in upper case");
            },
        },
        new TestCase
        {
            Id = "AUTH-03", Group = GroupAuth, Name = "E-mail address already taken",
            Description = "The same e-mail address cannot be registered again.",
            Body = async ctx =>
            {
                var u = await ctx.CreateUserAsync(login: false);
                var r = await ctx.NewClient().RegisterAsync(TestContext.NewUsername(), TestContext.DefaultPassword, u.Email, ctx.Ct);
                ctx.Status(r, 400, "Registration with a taken e-mail address");
            },
        },
        new TestCase
        {
            Id = "AUTH-04", Group = GroupAuth, Name = "Wrong credentials",
            Description = "Both a wrong password and a non-existent user give 401.",
            Body = async ctx =>
            {
                var u = await ctx.CreateUserAsync(login: false);
                var c = ctx.NewClient();
                ctx.Status(await c.LoginAsync(u.Username, "Wrong-Password-123", ctx.Ct), 401, "Login with a wrong password");
                ctx.Status(await c.LoginAsync(TestContext.NewUsername(), TestContext.DefaultPassword, ctx.Ct), 401,
                    "Login with a non-existent user");
                ctx.Status(await c.CheckTokenAsync(ctx.Ct), 401, "check-token after failed logins");
            },
        },
        new TestCase
        {
            Id = "AUTH-05", Group = GroupAuth, Name = "Login with e-mail address",
            Description = "The e-mail address can be used instead of the username to log in.",
            Body = async ctx =>
            {
                var u = await ctx.CreateUserAsync(login: false);
                var c = ctx.NewClient();
                ctx.Status(await c.LoginAsync(u.Email, u.Password, ctx.Ct), 200, "Login with e-mail address");
                var r = await c.CheckTokenAsync(ctx.Ct);
                ctx.Status(r, 200, "check-token");
                ctx.Check(Str(r.Json(), "name") == u.Username, "check-token did not return the username.");
            },
        },
        new TestCase
        {
            Id = "AUTH-06", Group = GroupAuth, Name = "Logout",
            Description = "After logout, check-token returns 401.",
            Body = async ctx =>
            {
                var u = await ctx.CreateUserAsync();
                ctx.Status(await u.Client.LogoutAsync(ctx.Ct), 200, "Logout");
                ctx.Status(await u.Client.CheckTokenAsync(ctx.Ct), 401, "check-token after logout");
            },
        },
        new TestCase
        {
            Id = "AUTH-07", Group = GroupAuth, Name = "Protected endpoints without login",
            Description = "Every event and schedule endpoint returns 401 without login (not a redirect).",
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
                    (HttpMethod.Get, "api/Schedule/status?id=1", null),
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
                ctx.Check(wrong.Count == 0, "These endpoints did not return 401 without login: " + string.Join("; ", wrong));
            },
        },
        new TestCase
        {
            Id = "AUTH-08", Group = GroupAuth, Name = "Protection against user enumeration",
            Description = "Forgot password and resend confirmation always return 200; the response does not reveal " +
                          "whether the account exists. (For an existing account only the already confirmed case is " +
                          "checked, so that no real e-mail is sent.)",
            Body = async ctx =>
            {
                var u = await ctx.CreateUserAsync(login: false);
                var c = ctx.NewClient();
                var missing = TestContext.NewUsername();

                var forgotMissing = await c.ForgotPasswordAsync(missing, ctx.Ct);
                ctx.Status(forgotMissing, 200, "forgot-password for a non-existent account");
                var resendMissing = await c.ResendConfirmationAsync(missing, ctx.Ct);
                ctx.Status(resendMissing, 200, "resend-confirmation for a non-existent account");
                var resendExisting = await c.ResendConfirmationAsync(u.Username, ctx.Ct);
                ctx.Status(resendExisting, 200, "resend-confirmation for an existing (confirmed) account");
                ctx.Check(resendMissing.Body == resendExisting.Body,
                    "The resend-confirmation response differs for existing and non-existent accounts, which reveals that the account exists.");
            },
        },
        new TestCase
        {
            Id = "AUTH-09", Group = GroupAuth, Name = "Invalid password reset",
            Description = "reset-password returns 400 for a fake token and for a too short new password.",
            Body = async ctx =>
            {
                var c = ctx.NewClient();
                ctx.Status(await c.ResetPasswordAsync("NO-SUCH-TOKEN-" + Guid.NewGuid().ToString("N"), "New-Password-12345", ctx.Ct),
                    400, "reset-password with a fake token");
                ctx.Status(await c.ResetPasswordAsync("NO-SUCH-TOKEN", "short", ctx.Ct), 400, "reset-password with a short password");
            },
        },
        new TestCase
        {
            Id = "AUTH-10", Group = GroupAuth, Name = "Account deletion",
            Description = "After deletion the session ends and the account can no longer log in.",
            Body = async ctx =>
            {
                var u = await ctx.CreateUserAsync();
                await ctx.CreateSampleEventAsync(u);
                ctx.Status(await u.Client.DeleteAccountAsync(ctx.Ct), 200, "Account deletion");
                u.Deleted = true;
                ctx.Status(await u.Client.CheckTokenAsync(ctx.Ct), 401, "check-token after deletion");
                ctx.Status(await ctx.NewClient().LoginAsync(u.Username, u.Password, ctx.Ct), 401, "Login with the deleted account");
            },
        },

        new TestCase
        {
            Id = "EVT-01", Group = GroupEvent, Name = "New, empty event",
            Description = "A new user has no events; after new-event an empty event named 'New Event' appears, " +
                          "and get-event returns all 10 data lists.",
            Body = async ctx =>
            {
                var u = await ctx.CreateUserAsync();
                ctx.Check((await ctx.ListEventIdsAsync(u)).Count == 0, "A new user cannot have events.");
                long id = await ctx.CreateEventAsync(u);
                var ev = await ctx.GetEventJsonAsync(u, id);

                var expectedKeys = new[] { "eventData", "eventTypes", "groups", "locations", "participants",
                                           "registrations", "pauseTable", "locationTable", "constraints", "timeZones" };
                var missing = expectedKeys.Where(k => !ev.ContainsKey(k)).ToList();
                ctx.Check(missing.Count == 0, "Missing fields in the get-event response: " + string.Join(", ", missing));
                ctx.Check(Str(ev["eventData"], "eventId") == id.ToString(), "eventData.eventId does not match the event id.");
                ctx.Check(Str(ev["eventData"], "eventName") == "New Event",
                    $"The new event is named '{Str(ev["eventData"], "eventName")}' instead of the expected 'New Event'.");
                foreach (var list in expectedKeys.Skip(1))
                    ctx.Check(Arr(ev, list).Count == 0, $"The '{list}' list of the new event is not empty.");
            },
        },
        new TestCase
        {
            Id = "EVT-02", Group = GroupEvent, Name = "Save and load back",
            Description = "Saves a full event with temporary ids and loads it back: every item is there, the ids were " +
                          "replaced with final values, and the references point to the new ids.",
            Body = async ctx =>
            {
                var u = await ctx.CreateUserAsync();
                var (_, ev) = await ctx.CreateSampleEventAsync(u);

                ctx.Check(Str(ev["eventData"], "eventName") == SampleEventName, "The event name was not saved.");
                var counts = new Dictionary<string, int>
                {
                    ["eventTypes"] = 2, ["groups"] = 1, ["locations"] = 2, ["participants"] = 3,
                    ["registrations"] = 4, ["pauseTable"] = 1, ["locationTable"] = 3, ["constraints"] = 1,
                };
                foreach (var (list, n) in counts)
                    ctx.Check(Arr(ev, list).Count == n, $"The '{list}' list has {Arr(ev, list).Count} items instead of {n}.");

                ctx.Check(!ev.ToJsonString().Contains("tmp-"), "A temporary (tmp-) id remained in the saved data.");

                var times = Arr(ev, "eventTypes").Select(t => Str(t, "timeRange")).OrderBy(x => x).ToList();
                ctx.Check(times.SequenceEqual(new[] { "00:20:00", "00:30:00" }),
                    "The event type durations did not come back in 'hh:mm:ss' format: " + string.Join(", ", times));

                string groupId = Str(Arr(ev, "groups")[0], "groupId");
                var anna = Arr(ev, "participants").First(p => Str(p, "participantName") == Anna);
                var ben = Arr(ev, "participants").First(p => Str(p, "participantName") == Ben);
                ctx.Check(Str(anna, "groupId") == groupId, "The competitor's group reference does not point to the new group id.");
                ctx.Check(ben!["groupId"] == null, "groupId is not null for a competitor without a group.");

                var participantIds = Arr(ev, "participants").Select(p => Str(p, "participantId")).ToHashSet();
                var typeIds = Arr(ev, "eventTypes").Select(t => Str(t, "eventTypeId")).ToHashSet();
                ctx.Check(Arr(ev, "registrations").All(r => participantIds.Contains(Str(r, "participantId")) &&
                                                             typeIds.Contains(Str(r, "eventTypeId"))),
                    "An entry references a non-existent competitor or event type.");

                string locB = Str(Arr(ev, "locations").First(l => Str(l, "locationName") == RangeB), "locationId");
                var constraint = Arr(ev, "constraints")[0];
                ctx.Check(Str(constraint, "objectId") == locB, "The blocked period does not point to the new location id.");
                ctx.Check(SameInstant(Str(constraint, "startTime"), SampleData.DefaultStart.AddHours(4)),
                    $"The start of the blocked period shifted: {Str(constraint, "startTime")} (expected: 2026-03-01T11:00:00Z).");
            },
        },
        new TestCase
        {
            Id = "EVT-03", Group = GroupEvent, Name = "Edit (save again)",
            Description = "Modifies a loaded event: renames it, removes one competitor and adds a new one.",
            Body = async ctx =>
            {
                var u = await ctx.CreateUserAsync();
                var (id, ev) = await ctx.CreateSampleEventAsync(u);

                var edit = SampleData.FromLoaded(ev);
                edit["eventData"]!["eventName"] = "Renamed event";
                var cecil = Arr(edit, "participants").First(p => Str(p, "participantName") == Cecil)!;
                string cecilId = Str(cecil, "participantId");
                Arr(edit, "participants").Remove(cecil);
                foreach (var reg in Arr(edit, "registrations").Where(r => Str(r, "participantId") == cecilId).ToList())
                    Arr(edit, "registrations").Remove(reg);

                string newId = "tmp-" + Guid.NewGuid().ToString("N");
                Arr(edit, "participants").Add(SampleData.Participant(newId, 4, "New Competitor", null));
                Arr(edit, "registrations").Add(SampleData.Registration(newId, Str(Arr(edit, "eventTypes")[0], "eventTypeId")));

                ctx.Status(await u.Client.SaveEventAsync(edit, ctx.Ct), 200, "Saving the modified event");
                var after = await ctx.GetEventJsonAsync(u, id);

                ctx.Check(Str(after["eventData"], "eventName") == "Renamed event", "The new name was not saved.");
                var names = Arr(after, "participants").Select(p => Str(p, "participantName")).OrderBy(x => x).ToList();
                ctx.Check(names.SequenceEqual(new[] { Anna, Ben, "New Competitor" }),
                    "The competitor list is not the expected one: " + string.Join(", ", names));
                ctx.Check(Arr(after, "registrations").Count == 4, $"There are {Arr(after, "registrations").Count} entries instead of 4.");
            },
        },
        new TestCase
        {
            Id = "EVT-04", Group = GroupEvent, Name = "Invalid reference: rejected and rolled back",
            Description = "Saving with an entry that references a non-existent competitor returns 400, and the previous " +
                          "data stays unchanged (the save is one transaction).",
            Body = async ctx =>
            {
                var u = await ctx.CreateUserAsync();
                var (id, ev) = await ctx.CreateSampleEventAsync(u);

                var bad = SampleData.FromLoaded(ev);
                bad["eventData"]!["eventName"] = "MUST NOT BE SAVED";
                Arr(bad, "registrations").Add(SampleData.Registration("no-such-competitor", Str(Arr(bad, "eventTypes")[0], "eventTypeId")));
                ctx.Status(await u.Client.SaveEventAsync(bad, ctx.Ct), 400, "Saving with an invalid reference");

                var after = await ctx.GetEventJsonAsync(u, id);
                ctx.Check(Str(after["eventData"], "eventName") == SampleEventName,
                    "The event name changed after the failed save: no rollback.");
                ctx.Check(Arr(after, "registrations").Count == 4 && Arr(after, "participants").Count == 3,
                    "The event data changed after the failed save: no rollback.");
            },
        },
        new TestCase
        {
            Id = "EVT-05", Group = GroupEvent, Name = "Wizard (new event from import)",
            Description = "new-wizard creates the complete event in one step.",
            Body = async ctx =>
            {
                var u = await ctx.CreateUserAsync();
                var payload = SampleData.SmallEvent(0, "Wizard event");
                ctx.Status(await u.Client.NewWizardAsync(payload, ctx.Ct), 200, "new-wizard");
                var ids = await ctx.ListEventIdsAsync(u);
                ctx.Check(ids.Count == 1, $"There are {ids.Count} events after the wizard instead of 1.");
                var ev = await ctx.GetEventJsonAsync(u, ids[0]);
                ctx.Check(Str(ev["eventData"], "eventName") == "Wizard event", "The name of the event created by the wizard does not match.");
                ctx.Check(Arr(ev, "registrations").Count == 4 && Arr(ev, "participants").Count == 3,
                    "The data of the event created by the wizard is incomplete.");
            },
        },
        new TestCase
        {
            Id = "EVT-06", Group = GroupEvent, Name = "Event deletion",
            Description = "After deletion the event cannot be loaded and is not in the list; deleting it again gives 404.",
            Body = async ctx =>
            {
                var u = await ctx.CreateUserAsync();
                var (id, _) = await ctx.CreateSampleEventAsync(u);
                ctx.Status(await u.Client.DeleteEventAsync(id, ctx.Ct), 200, "Deletion");
                ctx.Status(await u.Client.GetEventAsync(id, ctx.Ct), 404, "get-event after deletion");
                ctx.Check(!(await ctx.ListEventIdsAsync(u)).Contains(id), "The deleted event is still in the list.");
                ctx.Status(await u.Client.DeleteEventAsync(id, ctx.Ct), 404, "Deleting again");
            },
        },
        new TestCase
        {
            Id = "EVT-07", Group = GroupEvent, Name = "Non-existent event",
            Description = "For a non-existent id both get-event and delete-event return 404.",
            Body = async ctx =>
            {
                var u = await ctx.CreateUserAsync();
                ctx.Status(await u.Client.GetEventAsync(987_654_321, ctx.Ct), 404, "get-event with a non-existent id");
                ctx.Status(await u.Client.DeleteEventAsync(987_654_321, ctx.Ct), 404, "delete-event with a non-existent id");
            },
        },

        new TestCase
        {
            Id = "ACL-01", Group = GroupAcl, Name = "Another user's event is not accessible",
            Description = "User B cannot read, save or delete user A's event, cannot start a schedule for it and " +
                          "cannot export it. The response is 'not found', so the existence of the event is not revealed either.",
            Body = async ctx =>
            {
                var a = await ctx.CreateUserAsync();
                var (id, ev) = await ctx.CreateSampleEventAsync(a);
                var b = await ctx.CreateUserAsync();

                ctx.Status(await b.Client.GetEventAsync(id, ctx.Ct), 404, "B: get-event");
                ctx.Status(await b.Client.GetScheduleAsync(id, ctx.Ct), 404, "B: get schedule");
                ctx.Status(await b.Client.IsRunningAsync(id, ctx.Ct), 404, "B: isRunning");
                ctx.Status(await b.Client.StopScheduleAsync(id, ctx.Ct), 404, "B: stop solver");
                ctx.Status(await b.Client.SolverStatusAsync(id, ctx.Ct), 404, "B: solver status");
                ctx.Status(await b.Client.ExportAsync(id, ctx.Ct), 404, "B: export");
                ctx.Status(await b.Client.StartScheduleAsync(id, ctx.Ct), 404, "B: start schedule");

                var hijack = SampleData.FromLoaded(ev);
                hijack["eventData"]!["eventName"] = "MODIFIED BY B";
                ctx.StatusIn(await b.Client.SaveEventAsync(hijack, ctx.Ct), "B: saving A's event", 400, 404);
                ctx.Status(await b.Client.DeleteEventAsync(id, ctx.Ct), 404, "B: deletion");

                var after = await ctx.GetEventJsonAsync(a, id);
                ctx.Check(Str(after["eventData"], "eventName") == SampleEventName, "User B was able to modify A's event!");
            },
        },
        new TestCase
        {
            Id = "ACL-02", Group = GroupAcl, Name = "The event list only shows own events",
            Description = "A's event does not appear in B's list.",
            Body = async ctx =>
            {
                var a = await ctx.CreateUserAsync();
                long id = await ctx.CreateEventAsync(a);
                var b = await ctx.CreateUserAsync();
                ctx.Check(!(await ctx.ListEventIdsAsync(b)).Contains(id), "B sees A's event in its own list!");
            },
        },

        new TestCase
        {
            Id = "VAL-01", Group = GroupValidation, Name = "Empty credentials",
            Description = "Login with an empty username and password returns 400 or 401, not 500.",
            Body = async ctx =>
            {
                ctx.StatusIn(await ctx.NewClient().LoginAsync("", "", ctx.Ct), "Login with empty credentials", 400, 401);
            },
        },
        new TestCase
        {
            Id = "VAL-02", Group = GroupValidation, Name = "Non-numeric id",
            Description = "get-event?id=abc -> 400.",
            Body = async ctx =>
            {
                var u = await ctx.CreateUserAsync();
                ctx.Status(await u.Client.GetAsync("api/Event/get-event?id=abc", ctx.Ct), 400, "get-event?id=abc");
            },
        },
        new TestCase
        {
            Id = "VAL-03", Group = GroupValidation, Name = "Malformed JSON",
            Description = "Saving with a syntactically invalid JSON body returns 400, not 500.",
            Body = async ctx =>
            {
                var u = await ctx.CreateUserAsync();
                var r = await u.Client.SendAsync(HttpMethod.Post, "api/Event/save-event", rawBody: "{\"eventData\": {not json", ct: ctx.Ct);
                ctx.Status(r, 400, "Saving malformed JSON");
            },
        },
        new TestCase
        {
            Id = "VAL-04", Group = GroupValidation, Name = "Password too short at registration",
            Description = "Registration is not possible with a password shorter than 8 characters.",
            Body = async ctx =>
            {
                var name = TestContext.NewUsername();
                var c = ctx.NewClient();
                var r = await c.RegisterAsync(name, "abc", name + "@example.com", ctx.Ct);
                if (r.StatusCode == 200) await CleanupStrayUser(ctx, name, "abc");
                ctx.Status(r, 400, "Registration with a 3-character password");
            },
        },
        new TestCase
        {
            Id = "VAL-05", Group = GroupValidation, Name = "Invalid e-mail address",
            Description = "Registration with an invalid e-mail address returns 400, not 500.",
            Body = async ctx =>
            {
                var name = TestContext.NewUsername();
                var r = await ctx.NewClient().RegisterAsync(name, TestContext.DefaultPassword, "not-an-email", ctx.Ct);
                if (r.StatusCode == 200) await CleanupStrayUser(ctx, name, TestContext.DefaultPassword);
                ctx.Status(r, 400, "Registration with an invalid e-mail address");
            },
        },
        new TestCase
        {
            Id = "VAL-06", Group = GroupValidation, Name = "Empty location name",
            Description = "Saving a location with an empty name returns 400.",
            Body = async ctx => await SaveShouldFail(ctx, ev => Arr(ev, "locations")[0]!["locationName"] = "", "with an empty location name"),
        },
        new TestCase
        {
            Id = "VAL-07", Group = GroupValidation, Name = "Negative break time",
            Description = "Saving with a negative break time returns 400 (database CHECK constraint).",
            Body = async ctx => await SaveShouldFail(ctx, ev => ev["eventData"]!["basePauseTime"] = -5, "with a negative break time"),
        },
        new TestCase
        {
            Id = "VAL-08", Group = GroupValidation, Name = "Weight above 500",
            Description = "An objective weight can be at most 500.",
            Body = async ctx => await SaveShouldFail(ctx, ev => ev["eventData"]!["locWeight"] = 501, "with a weight of 501"),
        },
        new TestCase
        {
            Id = "VAL-09", Group = GroupValidation, Name = "End before start",
            Description = "If the end of the event is earlier than its start, saving returns 400.",
            Body = async ctx => await SaveShouldFail(ctx, ev =>
            {
                ev["eventData"]!["startDate"] = "2026-03-02T07:00:00Z";
                ev["eventData"]!["endDate"] = "2026-03-01T07:00:00Z";
            }, "with reversed dates"),
        },

        new TestCase
        {
            Id = "SCH-01", Group = GroupSchedule, Name = "Solver status query",
            Description = "For an event without a started schedule, isRunning returns false and the stored solver status is empty.",
            Body = async ctx =>
            {
                var u = await ctx.CreateUserAsync();
                long id = await ctx.CreateEventAsync(u);
                var r = await u.Client.IsRunningAsync(id, ctx.Ct);
                ctx.Status(r, 200, "isRunning");
                ctx.Check(r.Body.Trim() == "false", $"isRunning returned '{r.Body.Trim()}' instead of the expected 'false'.");
                ctx.Check(await GetSolverStatusAsync(ctx, u, id) == null, "A new event already has a solver status.");
            },
        },
        new TestCase
        {
            Id = "SCH-02", Group = GroupSchedule, Name = "Generate and validate a schedule",
            Description = "Starts the solver for the sample event, waits for the result and checks it with the " +
                          "independent validator: every entry appears exactly once, durations, locations and lanes are " +
                          "correct, and there are no overlaps.",
            RequiresSolver = true,
            Timeout = SolverTimeout,
            Body = async ctx =>
            {
                var u = await ctx.CreateUserAsync();
                var (id, _) = await ctx.CreateSampleEventAsync(u);
                var schedule = await SolveAsync(ctx, u, id, expected: 4);
                var errors = ScheduleValidator.Validate(schedule);
                foreach (var e in errors) ctx.Info("Rule violation: " + e);
                ctx.Check(errors.Count == 0, $"The schedule violates {errors.Count} rules, e.g.: {errors.FirstOrDefault()}");
                var status = await WaitFinalStatusAsync(ctx, u, id, TimeSpan.FromSeconds(90));
                ctx.Check(status is "OPTIMAL" or "FEASIBLE", $"The stored solver status is '{status}' instead of OPTIMAL or FEASIBLE.");
            },
        },
        new TestCase
        {
            Id = "SCH-03", Group = GroupSchedule, Name = "Excel export",
            Description = "For a finished schedule the export returns a valid .xlsx file (ZIP header 'PK').",
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
                    $"The Content-Type of the export is '{r.ContentType}', not Excel.");
                ctx.Check(r.Bytes.Length > 100 && r.Bytes[0] == (byte)'P' && r.Bytes[1] == (byte)'K',
                    "The exported file is not a valid .xlsx (not a ZIP).");
            },
        },
        new TestCase
        {
            Id = "SCH-04", Group = GroupSchedule, Name = "Saving deletes the old schedule",
            Description = "Saving the event again invalidates the previous schedule, since it would no longer match the data.",
            RequiresSolver = true,
            Timeout = SolverTimeout,
            Body = async ctx =>
            {
                var u = await ctx.CreateUserAsync();
                var (id, ev) = await ctx.CreateSampleEventAsync(u);
                await SolveAsync(ctx, u, id, expected: 4);
                ctx.Status(await u.Client.SaveEventAsync(SampleData.FromLoaded(ev), ctx.Ct), 200, "Saving again");
                var r = await u.Client.GetScheduleAsync(id, ctx.Ct);
                ctx.Status(r, 200, "Getting the schedule after saving again");
                ctx.Check(Arr(r.Json() as JsonObject, "timeZones").Count == 0, "The old schedule remained after saving again.");
                ctx.Check(await GetSolverStatusAsync(ctx, u, id) == null, "The solver status of the old schedule remained after saving again.");
            },
        },
        new TestCase
        {
            Id = "SCH-05", Group = GroupSchedule, Name = "Stopping a running search",
            Description = "Stops a search started for a larger event; afterwards isRunning is false.",
            RequiresSolver = true,
            Timeout = SolverTimeout,
            Body = async ctx =>
            {
                var u = await ctx.CreateUserAsync();
                long id = await ctx.CreateEventAsync(u);
                ctx.Status(await u.Client.SaveEventAsync(SampleData.LargeEvent(id, 120), ctx.Ct), 200, "Saving a large event");
                try
                {
                    ctx.Status(await u.Client.StartScheduleAsync(id, ctx.Ct), 200, "Starting the schedule");
                    await Task.Delay(1500, ctx.Ct);
                    var stop = await u.Client.StopScheduleAsync(id, ctx.Ct);
                    ctx.Status(stop, 200, "Stop");
                    ctx.Check(stop.Body.Trim() is "true" or "false", $"The stop response is not a boolean: '{stop.Body}'.");
                    await WaitNotRunningAsync(ctx, u, id, TimeSpan.FromSeconds(20));
                    var status = await GetSolverStatusAsync(ctx, u, id);
                    if (status == "MEMORY_LIMIT") ctx.Info("The solver hit its memory limit before the stop request arrived.");
                    ctx.Check(status is "STOPPED" or "MEMORY_LIMIT", $"The stored solver status is '{status}' instead of STOPPED.");
                }
                finally
                {
                    await u.Client.StopScheduleAsync(id, CancellationToken.None);
                }
            },
        },

        new TestCase
        {
            Id = "SCH-06", Group = GroupSchedule, Name = "Impossible event is reported",
            Description = "An event whose events do not fit into the competition time cannot be scheduled. The solver must " +
                          "report it (INFEASIBLE or MODEL_INVALID) instead of staying silent, and no schedule may be stored.",
            RequiresSolver = true,
            Timeout = SolverTimeout,
            Body = async ctx =>
            {
                var u = await ctx.CreateUserAsync();
                var (id, ev) = await ctx.CreateSampleEventAsync(u);
                var tooShort = SampleData.FromLoaded(ev);
                var start = DateTimeOffset.Parse(Str(ev["eventData"], "startDate"));
                tooShort["eventData"]!["endDate"] = start.AddMinutes(10).UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ");
                ctx.Status(await u.Client.SaveEventAsync(tooShort, ctx.Ct), 200, "Saving a 10-minute competition");
                ctx.Status(await u.Client.StartScheduleAsync(id, ctx.Ct), 200, "Starting the schedule");
                var status = await WaitFinalStatusAsync(ctx, u, id, TimeSpan.FromSeconds(90));
                ctx.Check(status is "INFEASIBLE" or "MODEL_INVALID", $"The stored solver status is '{status}' instead of INFEASIBLE or MODEL_INVALID.");
                var r = await u.Client.GetScheduleAsync(id, ctx.Ct);
                ctx.Check(Arr(r.Json() as JsonObject, "timeZones").Count == 0, "A schedule was stored for an impossible event.");
            },
        },

        new TestCase
        {
            Id = "SEC-01", Group = GroupSecurity, Name = "Forged solver answer is rejected",
            Description = "Only the solver may call the api/Schedule/answer endpoint. The test sends an unauthenticated, " +
                          "made-up schedule for an existing event: it must be rejected and must not get into the " +
                          "database. (Behind a reverse proxy every request comes from localhost, so an IP-based check " +
                          "is not enough.)",
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
                    $"The backend accepted the unauthenticated solver answer ({r.StatusCode}), and {count} made-up starts " +
                    "got into the schedule. The answer endpoint should require a signed / secret-keyed callback.");
            },
        },
        new TestCase
        {
            Id = "SEC-02", Group = GroupSecurity, Name = "The old cookie is invalid after logout",
            Description = "Replaying a saved session cookie after logout must give 401 (e.g. if the cookie was stolen earlier).",
            Body = async ctx =>
            {
                var u = await ctx.CreateUserAsync();
                var thief = ctx.NewClient();
                thief.CopyCookiesFrom(u.Client);
                ctx.Status(await thief.CheckTokenAsync(ctx.Ct), 200, "The copied cookie before logout");
                ctx.Status(await u.Client.LogoutAsync(ctx.Ct), 200, "Logout");
                var r = await thief.CheckTokenAsync(ctx.Ct);
                ctx.Check(r.StatusCode == 401,
                    $"The old cookie is still valid after logout ({r.StatusCode}). The session should also be " +
                    "invalidated on the server side (e.g. security stamp or server-side session).");
            },
        },
        new TestCase
        {
            Id = "SEC-03", Group = GroupSecurity, Name = "Session cookie attributes",
            Description = "The login cookie is HttpOnly (JavaScript cannot read it) and its SameSite value is not None.",
            Body = async ctx =>
            {
                var u = await ctx.CreateUserAsync(login: false);
                var r = await u.Client.LoginAsync(u.Username, u.Password, ctx.Ct);
                ctx.Status(r, 200, "Login");
                u.LoggedIn = true;
                var cookie = r.HeaderValues("Set-Cookie").FirstOrDefault(c => c.StartsWith("sl_auth=", StringComparison.Ordinal));
                ctx.Check(cookie != null, "Login did not set an 'sl_auth' cookie.");
                ctx.Info("Set-Cookie: " + cookie);
                var lower = cookie!.ToLowerInvariant();
                ctx.Check(lower.Contains("httponly"), "The session cookie is not HttpOnly.");
                ctx.Check(!lower.Contains("samesite=none"), "The session cookie is SameSite=None, so browsers also send it from other sites (CSRF).");
            },
        },
        new TestCase
        {
            Id = "SEC-04", Group = GroupSecurity, Name = "CORS: foreign sites get no access",
            Description = "A preflight request from an unknown Origin gets no Access-Control-Allow-Origin header.",
            Body = async ctx =>
            {
                var r = await ctx.NewClient().SendAsync(HttpMethod.Options, "api/Event/get-events",
                    headers: new Dictionary<string, string>
                    {
                        ["Origin"] = "https://evil-site.example",
                        ["Access-Control-Request-Method"] = "GET",
                    }, ct: ctx.Ct);
                ctx.Check(r.StatusCode != 0, "The backend did not respond.");
                var allowed = r.HeaderValues("Access-Control-Allow-Origin").ToList();
                ctx.Check(allowed.Count == 0,
                    "An Access-Control-Allow-Origin header was returned for a foreign Origin: " + string.Join(", ", allowed));
            },
        },
        new TestCase
        {
            Id = "SEC-05", Group = GroupSecurity, Name = "Error responses do not leak internals",
            Description = "Responses to invalid requests contain no exception names, stack traces or SQL errors.",
            Body = async ctx =>
            {
                var u = await ctx.CreateUserAsync();
                var responses = new List<ApiResponse>
                {
                    await ctx.NewClient().RegisterAsync(TestContext.NewUsername(), TestContext.DefaultPassword, "not-an-email", ctx.Ct),
                    await u.Client.SendAsync(HttpMethod.Post, "api/Event/save-event", rawBody: "{\"eventData\": {not json", ct: ctx.Ct),
                    await u.Client.SaveEventAsync(new JsonObject { ["eventData"] = new JsonObject { ["eventId"] = "not-a-number" } }, ctx.Ct),
                };
                string[] leaks = { "Exception", "   at ", "Npgsql", "SQLSTATE", "PDOException", "Stack trace", ".cs:line", ".php" };
                foreach (var r in responses)
                {
                    var found = leaks.Where(l => r.Body.Contains(l, StringComparison.OrdinalIgnoreCase)).ToList();
                    ctx.Check(found.Count == 0, $"The response of {r.Method} {r.Path} ({r.StatusCode}) exposes internal details: " +
                                                string.Join(", ", found));
                }
            },
        },
    };

        private static async Task SaveShouldFail(TestContext ctx, Action<JsonObject> modify, string what)
    {
        var u = await ctx.CreateUserAsync();
        var (id, ev) = await ctx.CreateSampleEventAsync(u);
        var bad = SampleData.FromLoaded(ev);
        modify(bad);
        ctx.Status(await u.Client.SaveEventAsync(bad, ctx.Ct), 400, "Saving " + what);
        var after = await ctx.GetEventJsonAsync(u, id);
        ctx.Check(JsonNode.DeepEquals(after["eventData"], ev["eventData"]) && Arr(after, "locations").Count == 2,
            "The event data changed after the failed save.");
    }

        private static async Task CleanupStrayUser(TestContext ctx, string username, string password)
    {
        var c = ctx.NewClient();
        if ((await c.LoginAsync(username, password, CancellationToken.None)).StatusCode == 200)
            await c.DeleteAccountAsync(CancellationToken.None);
        else
            ctx.Info($"User {username} was created but cannot log in, so it cannot be deleted automatically.");
    }

        private static async Task<JsonObject> SolveAsync(TestContext ctx, TestUser u, long id, int expected)
    {
        var sw = Stopwatch.StartNew();
        ctx.Status(await u.Client.StartScheduleAsync(id, ctx.Ct), 200, "Starting the schedule");
        ctx.RecordExchanges = false;
        try
        {
            while (sw.Elapsed < TimeSpan.FromSeconds(120))
            {
                var r = await u.Client.GetScheduleAsync(id, ctx.Ct);
                ctx.Status(r, 200, "Getting the schedule");
                if (Arr(r.Json() as JsonObject, "timeZones").Count >= expected) break;
                await Task.Delay(1000, ctx.Ct);
            }
        }
        finally
        {
            ctx.RecordExchanges = true;
        }

        var final = await u.Client.GetScheduleAsync(id, ctx.Ct);
        ctx.Status(final, 200, "Getting the schedule");
        var json = final.Json() as JsonObject ?? throw new TestFailedException("The schedule response is not a JSON object.");
        int n = Arr(json, "timeZones").Count;
        if (n < expected)
        {
            var running = await u.Client.IsRunningAsync(id, ctx.Ct);
            throw new TestFailedException(
                $"The schedule was not finished in {sw.Elapsed.TotalSeconds:0} s ({n}/{expected} starts, isRunning: {running.Body.Trim()}). " +
                "Is the Python solver (ScheduleSolver/Main.py) running, and can it reach the backend callback address (URLS:Backend)?");
        }
        ctx.Info($"The schedule was ready in {sw.Elapsed.TotalSeconds:0.0} s ({n} starts).");
        return json;
    }

    private static async Task<string?> GetSolverStatusAsync(TestContext ctx, TestUser u, long id)
    {
        var r = await u.Client.SolverStatusAsync(id, ctx.Ct);
        ctx.Status(r, 200, "Solver status");
        var node = (r.Json() as JsonObject)?["status"];
        return node == null ? null : Str(r.Json(), "status");
    }

    private static async Task<string?> WaitFinalStatusAsync(TestContext ctx, TestUser u, long id, TimeSpan timeout)
    {
        var sw = Stopwatch.StartNew();
        string? status = null;
        ctx.RecordExchanges = false;
        try
        {
            while (sw.Elapsed < timeout)
            {
                status = await GetSolverStatusAsync(ctx, u, id);
                if (status is not ("QUEUED" or "RUNNING")) break;
                await Task.Delay(1000, ctx.Ct);
            }
        }
        finally
        {
            ctx.RecordExchanges = true;
        }
        ctx.Info($"Solver status: {status ?? "(none)"} after {sw.Elapsed.TotalSeconds:0.0} s.");
        return status;
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
        throw new TestFailedException($"The search is still running {timeout.TotalSeconds:0} s after stopping.");
    }

    private static JsonArray Arr(JsonNode? o, string name) => o?[name] as JsonArray ?? new JsonArray();

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
