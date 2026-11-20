# ScheduleLogic test runner and load tester

It consists of three projects:

| Project | What it is |
|---|---|
| `ScheduleLogic.TestKit` | Class library: API client, the tests, the schedule validator, the load engine, reports. No external NuGet packages. |
| `ScheduleLogic.TestRunner` | WinForms UI: tests run with buttons, comparison of two runs, load testing with a live chart. |
| `ScheduleLogic.TestCli` | The same from the command line (`sltest`), for scripts or CI. |

The tests are **black-box**: they only use the HTTP API, so the same tests run against both the .NET and the PHP backend.

## Prerequisites (on a development machine)

1. **Disable e-mail confirmation in development mode.** In `ScheduleLogic.Server/appsettings.Development.json`:
   ```json
   "DevOptions": { "SkipEmailConfirmation": true }
   ```
   Both the .NET and the PHP backend read it from there. It only takes effect in the `Development` environment; in production confirmation is always required, whatever the setting says.
2. **Disable the rate limit during development** (otherwise the many registrations get 429): `"RateLimiting": { "Enabled": false }`, also in `ScheduleLogic.Server/appsettings.Development.json` (this is the default there). It is enabled in production.
3. The **Scheduling (solver)** tests need the Python solver running (`ScheduleSolver/Main.py`).

## Usage (GUI)

In Visual Studio: right-click the `ScheduleLogic.TestRunner` project and choose *Set as Startup Project* (or *Debug → Start new instance*) while the backend is running.

- **Top bar:** the backend label (`.NET` / `PHP`) and address (default `https://localhost:7098`). The two backends run on the same port, so the label tells which one answered.
- **Tests tab:** buttons per group, *All tests*, or double-click a row. The description and full log (requests, responses) of the selected test are shown below. After every run a JSON report is saved to `Documents\ScheduleLogic\TestResults`.
- **Compare tab:** run all tests against .NET, start PHP instead, run again with the `PHP` label, then select the two reports here. Besides the test result, the comparison also checks the status code and JSON structure of every response (ignoring ids and timestamps).
- **Load tab:** stepped measurement (e.g. `1,5,10,25,50` users), a live chart, and CSV files at the end in `Documents\ScheduleLogic\LoadResults` (they open directly in Excel).

Every test creates its own randomly named (`sltest_...`) user and deletes it at the end; deleting the account also deletes its events (cascade).

## Test groups

| Group | What it checks |
|---|---|
| Smoke | Whether the API is reachable |
| Authentication | Registration, login (with username and e-mail), logout, taken username/e-mail, protected endpoints, protection against user enumeration, password reset, account deletion |
| Events | New event, full save and load back (id replacement, references), editing, invalid reference → 400 + rollback, wizard, deletion |
| Access control | Another user's event is not accessible through any endpoint |
| Input validation | Malformed JSON, non-numeric id, short password, invalid e-mail, empty name, negative break, weight too large, reversed dates |
| Scheduling (solver) | Generating a schedule, checking it with an **independent validator**, Excel export, saving again deletes the old schedule, stopping a running search |
| Security | Forged solver answer, session replay after logout, cookie attributes, CORS, leaking internals in error messages |

The validator (`ScheduleValidator`) does not use the solver's code, only the input and the result: every entry exactly once, correct duration, allowed location, existing lane, no overlap on a lane or for a competitor, capacity not exceeded, one event type at one location.

## Load testing

- **Closed model:** N virtual users, each sends the next request right after the response. Shows how much the server can handle.
- **Open model:** a fixed request rate per second. Latency is measured from the *scheduled* start, so if the server slows down it shows (no "coordinated omission").
- Scenarios: `CheckToken` (framework base cost), `GetEvents`, `GetEvent` (large event, 9 tables), `SaveEvent` (transactional save), `Mixed`.
- CPU and memory usage of the **monitored processes** is sampled every second (.NET: `ScheduleLogic.Server`, PHP: `php-cgi` / `php-fpm` / `php`). The idle memory before the measurement is also included in the summary.

To keep the measurement fair:
- .NET: Release build, not running under the debugger (`dotnet run -c Release` or the compiled exe).
- PHP: OPcache enabled, and **not** the built-in server (`php -S`), because it is single-threaded; for measurements use `php-cgi` / `php-fpm` with several workers behind nginx or Caddy.
- Either both behind the same kind of proxy, or neither.
- Use a warm-up (5 s by default), and repeat every measurement 3–5 times.

Output files: `summary.csv` (one row per level), `timeline_level{N}.csv` (per-second values), `requests_level{N}.csv` (every request).

## Command line

```
sltest test --url https://localhost:7098 --label NET
sltest test --url https://localhost:7098 --label PHP --group Security
sltest compare <A.json> <B.json>
sltest load --url https://localhost:7098 --label NET --scenario GetEvent --mode closed --levels 1,5,10,25,50 --duration 30 --warmup 5 --users 10 --event-size 100 --monitor ScheduleLogic.Server
```

The exit code of `test` is 1 if any test failed, so it can be used in CI.
