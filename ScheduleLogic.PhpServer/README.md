# ScheduleLogic.PhpServer

The PHP version of the ScheduleLogic backend. An exact counterpart of **ScheduleLogic.Server** (ASP.NET Core 10):

- it serves **the same endpoints** with the same JSON, so **nothing has to change** in the Angular client,
- it works with **the same PostgreSQL database** and reads its settings from the `appsettings*.json` files of the .NET project,
- it runs on **the same port** (`https://localhost:7098` in development), so only one of the two runs at a time,
- it **talks to the Python solver the same way**; the solver code does not change either,
- users registered with .NET can log in **with the same password**, because the password hash format is byte-for-byte identical to ASP.NET Core Identity's.

Native PHP 8 (≥ 8.1), **no external packages**: Composer is not needed. Only the built-in `pdo_pgsql`, `openssl` and `zip` extensions are required.

---

## Quick start (Windows, development)

### 1. Install PHP

1. Download PHP: <https://windows.php.net/download/>, **PHP 8.3 (or 8.4) – VS16 x64 Non Thread Safe – Zip**.
2. Extract it, for example to `C:\php`.
3. Add `C:\php` to the **PATH**: Start → "Edit the system environment variables" → Environment Variables → Path → New.
4. In `C:\php`, copy `php.ini-development` to `php.ini` and open it. Remove the `;` from the start of these lines:
   ```ini
   extension_dir = "ext"
   extension=openssl
   extension=pdo_pgsql
   extension=zip
   ```
5. Open a **new** terminal and check: `php -v`.

> Alternative: `winget install PHP.PHP.8.3`. In that case run `php --ini` to find where `php.ini` should be, and do step 4 there.

### 2. Install Caddy (HTTPS on port 7098)

The built-in PHP server only speaks HTTP, but the frontend calls `https://localhost:7098`. [Caddy](https://caddyserver.com) is a single exe that puts HTTPS in front of it:

```powershell
winget install CaddyServer.Caddy
```

(Or download it from <https://caddyserver.com/download> and put it on the PATH.)

On the first start Windows asks whether to trust Caddy's local certificate authority. Answer **Yes**; it only asks once.

### 3. Check and start

```powershell
cd "D:\VS .net web api\Szakdolgozat\ScheduleLogic.PhpServer"
$env:APP_ENV = "Development"
php dev\check-env.php        # reports anything missing (extension, DB connection, ...)
```

If everything is `[OK]`: **double-click `dev\start-dev.cmd`**. It starts PHP (internally on port 7099) and Caddy (`https://localhost:7098`). Stop: `Ctrl+C`.

The other components start as usual:

```powershell
cd ..\schedulelogic.client;  npm start          # https://localhost:58921
cd ..\ScheduleSolver;        python Main.py      # http://127.0.0.1:8000
```

> **Important:** the .NET and the PHP backend use the same port 7098, so only one of them can run at a time. If you start .NET from Visual Studio, stop PHP first, and vice versa.

### Admin UI

`https://localhost:7098/admin`. The credentials are in the config (`AdminAuth:Username`, `AdminAuth:PasswordHash`). If you put them into the Visual Studio **User Secrets** for .NET, PHP reads them from there too in Development mode. A new password hash:

```powershell
php dev\hash-password.php "NewAdminPassword"
```

The result also works with .NET.

---

## Structure: C# → PHP mapping

```
ScheduleLogic.PhpServer/
├── public/index.php            ← entry point (Program.cs: app.Run)
├── src/
│   ├── App.php                 ← Program.cs: DI, routes, CORS, cookies, middleware order
│   ├── Controllers/            ← Controllers/*.cs  (1:1, the same endpoints)
│   ├── Services/               ← Services/*.cs
│   │   ├── PasswordHasher.php  ← ASP.NET Core Identity PasswordHasher<T> (V2 + V3 format)
│   │   ├── SmtpEmailSender.php ← System.Net.Mail.SmtpClient (STARTTLS)
│   │   ├── XlsxWriter.php      ← ClosedXML (Excel export)
│   │   └── SolverCallbackSigner.php  ← NEW: authentication of the solver callback
│   ├── Repositories/           ← Services/Repositories/*.cs  (PDO + SQL instead of EF Core)
│   ├── Models/                 ← Class/*Models.cs  (input validation, like [ApiController])
│   └── Core/                   ← small classes replacing the framework (router, request/response,
│                                  config, session-based auth, CORS, rate limit, log)
├── views/admin/                ← Views/Admin/*.cshtml
├── dev/                        ← start scripts, Caddyfile, environment checker, hash generator
├── deploy/nginx.conf.example   ← production deployment
├── tests/                      ← API, solver and frontend end-to-end tests
└── storage/                    ← sessions, logs, rate limit (gitignored)
```

| Endpoint | C# | PHP |
|---|---|---|
| `api/Authentication/*` (login, register, confirm, resend-confirmation, forgot-password, reset-password, check-token, delete, logout) | `AuthenticationController` | `Controllers/AuthenticationController.php` |
| `api/Event/*` (new-event, delete-event, get-events, get-event, save-event, new-wizard) | `EventController` | `Controllers/EventController.php` |
| `api/Schedule/*` (`{id}` POST/GET, isRunning, stop, answer, export) | `ScheduleController` | `Controllers/ScheduleController.php` |
| `admin`, `admin/login`, `admin/logout`, `admin/api/*` | `AdminController` + Razor views | `Controllers/AdminController.php` + `views/admin` |

What the framework did in .NET is a small, readable class here:

| ASP.NET Core | PHP |
|---|---|
| Attribute routing (case-insensitive, `{id:long}`) | `Core/Router.php` |
| `IConfiguration` (appsettings + environment + User Secrets + env variables) | `Core/Config.php` |
| Cookie authentication (sliding expiration, 2 schemes) | `Core/SessionAuth.php` (server-side session) |
| `[ApiController]` model validation → 400 ProblemDetails | `Models/InputReader.php`, `Models/EventInput.php` |
| `IExceptionHandler` | `App::handleException()` |
| System.Text.Json (camelCase, case-insensitive reading) | `Core/Json.php` |
| DateTime / TimeOnly JSON format, `ToLocalTime()` | `Core/DotNetTime.php` |
| `UseStaticFiles` + `MapFallbackToFile("index.html")` | `Core/StaticFiles.php` |
| Antiforgery token | `Core/Csrf.php` |

---

## Configuration

PHP reads **the same files** as .NET, in this order (later ones override earlier ones):

1. `../ScheduleLogic.Server/appsettings.json`
2. `../ScheduleLogic.Server/appsettings.{Environment}.json`
3. the .NET **User Secrets** (only in Development; based on the `UserSecretsId` in the `.csproj`)
4. `ScheduleLogic.PhpServer/appsettings.json` (optional, only for PHP-specific values; sample: `appsettings.sample.json`)
5. `ScheduleLogic.PhpServer/appsettings.{Environment}.json`
6. environment variables with `__` as separator, e.g. `Database__Password` (same as in .NET)

The environment comes from `APP_ENV` (or `ASPNETCORE_ENVIRONMENT`). **The default is `Production`**, as in .NET. The `start-dev` scripts set it to `Development`.

PHP-only settings (all optional):

| Key | Default | Purpose |
|---|---|---|
| `AppSettings:TimeZone` | `Europe/Budapest` | the "server local time" (the counterpart of .NET `ToLocalTime()`) |
| `AppSettings:WebRoot` | `../ScheduleLogic.Server/wwwroot` | location of the built Angular client |
| `AppSettings:ExportDateFormat` | `Y-m-d H:i:s` | date format in the Excel export |
| `Cors:AllowedOrigins` | dev: `https://localhost:58921`, production: `https://schedulelogic.hu` | CORS |
| `ForwardedHeaders:KnownProxies` | `127.0.0.1`, `::1` | which proxy's `X-Forwarded-For` header to trust |
| `Solver:CallbackSecret` | (generated, `storage/solver-callback.key`) | signing key of the solver callback |
| `RateLimiting:*` | see `appsettings.sample.json` | limits against overload / password guessing |
| `MailSend:VerifyCertificate` | `true` | only worth turning off for a local test SMTP server |

---

## Differences from the .NET backend

### Fixed security issues

| # | .NET backend | PHP backend |
|---|---|---|
| 1 | `api/Schedule/answer` only checked whether the request came from localhost. **Behind a reverse proxy (nginx) every request comes from there**, so anyone could submit a forged result. | The solver callback URL carries a per-event **HMAC-SHA256 key** (`?key=...`). The solver does not change, because it posts back to the `returnURL` it received. The key is only valid for that event, and the submitted ids must belong to the event too. |
| 2 | In production the cookie was `SameSite=None`, so a foreign site could send requests on behalf of the user (CSRF). | `SameSite=Lax`, no Domain. |
| 3 | No validation at registration (an empty password or a wrong e-mail got through). | Password of at least 8 characters, valid e-mail, username of 1–255 characters. |
| 4 | `SuccessRehashNeeded` was rejected, so after an Identity update old users could not have logged in. | Accepted, and the hash is updated automatically at login. |
| 5 | No rate limiting. | Login, registration, e-mail sending, password reset and admin login are limited (429 + `Retry-After`). |
| 6 | After logout the old cookie was in principle still valid (until expiry). | Server-side session: ends immediately on logout or account deletion. |
| 7 | — | A dummy hash also runs for a non-existent user, so the response time does not reveal whether the account exists. |

These have since been fixed in the .NET backend as well (see `ScheduleLogic.Server/SECURITY.md`).

### Other (minor) differences

- **`api/Schedule/stop`**: .NET always returned `false`, because it read the solver's lowercase `status` key case-sensitively. PHP correctly returns `true` when the solver stopped. (The frontend does not use this value.)
- The `returnURL` in the response of **`POST api/Schedule/{id}`** is empty, because the callback key does not belong in the browser. (The frontend does not use it either.)
- **Wizard**: if the import failed, .NET left an empty "New Event" behind. Here it is one transaction, so nothing is left on error.
- **Saving the solver result** happens in one transaction, so no partial state can remain.
- **Excel export**: dates use the `2026-03-01 09:00:00` format. .NET wrote them according to the server's locale, which differed between machines.
- Lists (events, participants etc.) come sorted by id; with .NET the order was not guaranteed.
- `DatabaseService.cs` is not registered anywhere in .NET (old, dead code), so it was not ported.

---

## Production deployment (Linux)

```bash
sudo apt install php8.3-fpm php8.3-pgsql php8.3-zip nginx
```

- Sample configuration: `deploy/nginx.conf.example`. nginx serves the Angular build; `/api` and `/admin` requests go to PHP-FPM.
- `APP_ENV` defaults to `Production`. Passwords can also be given as environment variables (`Database__Password`, `MailSend__Password`, …).
- The `storage/` folder must be writable by the PHP-FPM user (`www-data`).
- If the .NET project folder is not on the server, set `SL_SHARED_CONFIG_DIR` to the folder of the `appsettings` files, or put them into the `ScheduleLogic.PhpServer` folder.

---

## Testing

Details: `tests/README.md`. These passed during development, on a real PHP 8.3 + PostgreSQL 16 environment:

- **`test_api.py`**: 142 checks covering all 29 endpoints (formats, error codes, access control, e-mail with STARTTLS, hash compatibility with an independent Python implementation, upgrading an old hash, CORS, rate limit, admin + CSRF, solver key, Excel content),
- **`test_real_solver.py`**: with the real OR-Tools solver; the resulting schedule satisfies every constraint,
- **`e2e_frontend.js`**: the real Angular client clicked through in headless Chromium, also in the development setup (Angular on 58921, PHP behind Caddy on 7098).

---

## Troubleshooting

| Symptom | Fix |
|---|---|
| `could not find driver` | `extension=pdo_pgsql` is not enabled in `php.ini` (`php --ini` shows which ini is loaded). |
| `password authentication failed` | The credentials in the `Database` section (and `Database:Password` in User Secrets) are wrong. See `CONFIGURATION.md` in the repository root. |
| The browser does not trust the `https://localhost:7098` certificate | `caddy trust` (as administrator), then restart the browser. |
| `Port 7098 is in use` | The .NET backend (Visual Studio) is running; stop it first. |
| Admin login always fails | There is no `AdminAuth` in the config. Create a hash with `php dev\hash-password.php`. |
| Logs | `storage/logs/app-YYYY-MM-DD.log` (also written to the console in development). |
