# Configuration and secrets

**Rule:** passwords, keys and tokens never go into a version-controlled file (`appsettings*.json`, source code). `appsettings.json` and `appsettings.Development.json` contain only non-secret settings, so they are version-controlled.

Load order of the settings (later sources override earlier ones), the same for both backends:

1. `ScheduleLogic.Server/appsettings.json`
2. `ScheduleLogic.Server/appsettings.{Environment}.json`
3. **User Secrets** (only in the `Development` environment)
4. **Environment variables**, with `__` as separator (e.g. `Database__Password`)

The PHP backend reads the same files and the same User Secrets store (`ScheduleLogic.PhpServer/src/Core/Config.php`).

## Secrets

| Key | Purpose | Development | Production |
|---|---|---|---|
| `Database:Password` | PostgreSQL password (`schedulelogic_app`) | User Secrets | `Database__Password` |
| `MailSend:User`, `MailSend:Password` | SMTP (MailerSend) login | User Secrets | `MailSend__User`, `MailSend__Password` |
| `AdminAuth:Username`, `AdminAuth:PasswordHash` | Admin UI login (password stored only as a hash) | User Secrets | `AdminAuth__Username`, `AdminAuth__PasswordHash` |
| `Solver:CallbackSecret` | HMAC key of the solver callback (min. 32 characters) | User Secrets | `Solver__CallbackSecret`, or if not set, a generated file: `App_Data/solver-callback.key` |

Non-secret but important settings (appsettings): `Database:Host/Port/Name/User`, `URLS:*`, `MailSend:Host/Port/From`, `RateLimiting:*`, `ForwardedHeaders:KnownProxies`, `DevOptions:SkipEmailConfirmation` (only effective in Development).

## Development: User Secrets

The secrets are stored in the user profile, outside the project:
`%APPDATA%\Microsoft\UserSecrets\3ed9a405-dd29-4812-85cc-89a441e52809\secrets.json`

In Visual Studio: right-click the `ScheduleLogic.Server` project → **Manage User Secrets**. From the command line:

```powershell
cd ScheduleLogic.Server
dotnet user-secrets list
dotnet user-secrets set "Database:Password" "<password>"
dotnet user-secrets set "MailSend:User" "<smtp user>"
dotnet user-secrets set "MailSend:Password" "<smtp password>"
dotnet user-secrets set "Solver:CallbackSecret" "<at least 32 random characters>"
```

Admin password hash: `php ScheduleLogic.PhpServer/dev/hash-password.php "<password>"`; the result also works with .NET.

If `Database:Password` is missing, the backend stops at startup with a clear error message.

## Production: environment variables

For example with systemd (`/etc/systemd/system/schedulelogic.service`):

```ini
[Service]
Environment=ASPNETCORE_ENVIRONMENT=Production
EnvironmentFile=/etc/schedulelogic/secrets.env
```

`/etc/schedulelogic/secrets.env` (permissions: `chmod 600`, owned by the service user):

```
Database__Password=...
MailSend__User=...
MailSend__Password=...
AdminAuth__Username=...
AdminAuth__PasswordHash=...
Solver__CallbackSecret=...
```

For PHP-FPM the same names go into the pool configuration, e.g. `env[Database__Password] = ...`.

## Regenerating the database model (scaffold)

Use `--no-onconfiguring` so that EF does not write a connection string (password) into `ScheduleLogicContext.cs`:

```powershell
dotnet user-secrets set "ConnectionStrings:Scaffold" "Host=localhost;Database=scheduleLogic;Username=<user>;Password=<password>"
dotnet ef dbcontext scaffold "Name=ConnectionStrings:Scaffold" Npgsql.EntityFrameworkCore.PostgreSQL -o Models -f --no-onconfiguring
```

## If a secret was committed anyway

Deleting it in the latest commit is not enough: it stays in the git history and in every clone. Therefore:

1. **Rotate the secret** (new password / key); this is the only reliable fix.
2. Optionally remove it from the history (`git filter-repo --replace-text`), then force push. This affects every clone.
