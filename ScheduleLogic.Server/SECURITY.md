# Security fixes in the .NET backend

The issues found while writing the PHP backend are fixed in the .NET version too, so that both backends have the same security level. Each one is checked by the `ScheduleLogic.TestRunner` tests (test id in parentheses).

| # | Before | Now | Where |
|---|---|---|---|
| 1 | `api/Schedule/answer` only checked whether the request came from localhost. Behind a reverse proxy (nginx) every request comes from there, so anyone could submit a forged schedule. | The callback URL carries a per-event HMAC-SHA256 key (`?key=...`). Missing or wrong key: 403. The result may only reference the competitors, event types and locations of that event, otherwise 400. Saving is one transaction. The Python solver did not need any change. (SEC-01) | `Services/Security/SolverCallbackSigner.cs`, `ScheduleController`, `ScheduleService`, `ScheduleRepository` |
| 2 | The authentication ticket was stored in the cookie, so after logout the old cookie stayed valid until it expired. | Server-side sessions (`ITicketStore`): the cookie only holds a random id. Logout ends the session immediately; account deletion (own or by admin) and password reset end all sessions of the user. (SEC-02) | `Services/Security/ServerSideTicketStore.cs`, `Program.cs` |
| 3 | In production `SameSite=None` and `Domain=schedulelogic.hu`: a foreign site could send requests on behalf of the user (CSRF). | `SameSite=Lax`, no Domain, always `Secure` in production. (SEC-03) | `Program.cs` |
| 4 | No rate limiting: unlimited password guessing, mass registration and e-mail sending. | Fixed window per IP address: login 20/5 min, registration 5/15 min, e-mail sending 5/15 min, password reset 10/15 min, admin login 5/15 min. Over the limit: 429 + `Retry-After`. Configurable: `RateLimiting:*`. | `Services/Security/RateLimitingSetup.cs` |
| 5 | Behind a proxy, every request had the proxy's IP. | `UseForwardedHeaders`: the real client IP is taken only from headers sent by a trusted proxy (`ForwardedHeaders:KnownProxies`, localhost by default). | `Program.cs` |
| 6 | No validation at registration: short passwords accepted, an invalid e-mail address caused a 500 error. | Username 1–255 characters without control characters; valid e-mail; password 8–1024 characters. Two concurrent registrations hitting the unique index give a clear error instead of 500. (VAL-04, VAL-05) | `AuthenticationService.cs`, `PasswordPolicy.cs` |
| 7 | Empty names (e.g. location) were saved. | `[Required]` on every mandatory text field (empty and whitespace-only are rejected). (VAL-06) | `Class/EventModels.cs`, `AuthenticationModels.cs` |
| 8 | Login answered faster for a non-existent user, revealing whether the account exists. | A dummy password check also runs for non-existent accounts. | `AuthenticationService.LoginUser` |
| 9 | `SuccessRehashNeeded` was rejected, so after an Identity update (e.g. .NET 10) users with old hashes could not have logged in. | Accepted, and the password is rehashed with the current parameters at login. | `AuthenticationService.LoginUser` |
| 10 | The database password was hardcoded in `ScheduleLogicContext.cs` (and got into the public git repository); `Program.cs` also registered a second, hardcoded connection string. | The connection string comes only from configuration (User Secrets / environment variable), built with `NpgsqlConnectionStringBuilder` (escaping). A missing password gives a clear startup error. | `Program.cs`, `Models/ScheduleLogicContext.cs`, `CONFIGURATION.md` |
| 11 | No `X-Content-Type-Options` / `X-Frame-Options`. | `nosniff` and `DENY` on every response. | `Program.cs` |

Other fixes:
- `api/Schedule/stop`: always returned `false` because of the solver's lowercase `status` key; it now reports the stop correctly.
- The `returnURL` in the response of `POST api/Schedule/{id}` is empty, because the callback key does not belong in the browser.
- The wizard creates and fills the event in one transaction; a failed import does not leave an empty "New Event".
- Saving with a non-numeric event id returns 400 instead of 500.
- Removed unused packages: `Microsoft.AspNet.Identity.Core` (old, .NET Framework), `Authentication.Google`, `Authentication.JwtBearer`, `EntityFrameworkCore.SqlServer`, `Swashbuckle`.

**Note:** sessions are kept in memory, so everyone has to log in again after a backend restart. With several server instances, `ServerSideTicketStore` has to be replaced with a distributed cache (e.g. Redis).
