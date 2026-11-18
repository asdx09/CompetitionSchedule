# Konfiguráció és titkok kezelése

**Szabály:** jelszó, kulcs, token soha nem kerülhet verziókezelt fájlba (`appsettings*.json`, forráskód). Az `appsettings.json` és az `appsettings.Development.json` csak nem titkos beállításokat tartalmaz, ezért verziókezelt.

A beállítások betöltési sorrendje (a későbbi felülírja a korábbit), mindkét backendnél ugyanaz:

1. `ScheduleLogic.Server/appsettings.json`
2. `ScheduleLogic.Server/appsettings.{Környezet}.json`
3. **User Secrets** (csak `Development` környezetben)
4. **környezeti változók**, `__` elválasztóval (pl. `Database__Password`)

A PHP backend ugyanezeket a fájlokat és ugyanezt a User Secrets tárolót olvassa (`ScheduleLogic.PhpServer/src/Core/Config.php`).

## Titkok listája

| Kulcs | Mire való | Fejlesztés | Éles |
|---|---|---|---|
| `Database:Password` | PostgreSQL jelszó (`schedulelogic_app`) | User Secrets | `Database__Password` |
| `MailSend:User`, `MailSend:Password` | SMTP (MailerSend) belépés | User Secrets | `MailSend__User`, `MailSend__Password` |
| `AdminAuth:Username`, `AdminAuth:PasswordHash` | Admin felület belépés (a jelszó csak hash-ként) | User Secrets | `AdminAuth__Username`, `AdminAuth__PasswordHash` |
| `Solver:CallbackSecret` | A solver visszahívásának HMAC-kulcsa (min. 32 karakter) | User Secrets | `Solver__CallbackSecret`, vagy ha nincs megadva, generált fájl: `App_Data/solver-callback.key` |

Nem titkos, de fontos beállítások (appsettings): `Database:Host/Port/Name/User`, `URLS:*`, `MailSend:Host/Port/From`, `RateLimiting:*`, `ForwardedHeaders:KnownProxies`, `DevOptions:SkipEmailConfirmation` (csak Developmentben hat).

## Fejlesztés: User Secrets

A titkok a felhasználói profilban vannak, a projekten kívül:
`%APPDATA%\Microsoft\UserSecrets\3ed9a405-dd29-4812-85cc-89a441e52809\secrets.json`

Visual Studióban: jobb klikk a `ScheduleLogic.Server` projekten → **Manage User Secrets**. Parancssorból:

```powershell
cd ScheduleLogic.Server
dotnet user-secrets list
dotnet user-secrets set "Database:Password" "<jelszó>"
dotnet user-secrets set "MailSend:User" "<smtp felhasználó>"
dotnet user-secrets set "MailSend:Password" "<smtp jelszó>"
dotnet user-secrets set "Solver:CallbackSecret" "<legalább 32 véletlen karakter>"
```

Admin jelszó-hash: `php ScheduleLogic.PhpServer/dev/hash-password.php "<jelszó>"`, az eredmény a `.NET`-tel is működik.

Ha a `Database:Password` hiányzik, a backend induláskor érthető hibaüzenettel leáll.

## Éles üzemeltetés: környezeti változók

Például systemd alatt (`/etc/systemd/system/schedulelogic.service`):

```ini
[Service]
Environment=ASPNETCORE_ENVIRONMENT=Production
EnvironmentFile=/etc/schedulelogic/secrets.env
```

`/etc/schedulelogic/secrets.env` (jogosultság: `chmod 600`, tulajdonos a szolgáltatás felhasználója):

```
Database__Password=...
MailSend__User=...
MailSend__Password=...
AdminAuth__Username=...
AdminAuth__PasswordHash=...
Solver__CallbackSecret=...
```

A PHP-FPM-nél ugyanezek a nevek a pool konfigurációjába kerülnek, pl. `env[Database__Password] = ...`.

## Adatbázis-modell újragenerálása (scaffold)

A `--no-onconfiguring` kapcsolóval, hogy az EF ne írjon kapcsolati stringet (jelszót) a `ScheduleLogicContext.cs`-be:

```powershell
dotnet user-secrets set "ConnectionStrings:Scaffold" "Host=localhost;Database=scheduleLogic;Username=<user>;Password=<jelszó>"
dotnet ef dbcontext scaffold "Name=ConnectionStrings:Scaffold" Npgsql.EntityFrameworkCore.PostgreSQL -o Models -f --no-onconfiguring
```

## Ha egy titok mégis bekerült a gitbe

A törlés a legújabb commitból nem elég: a git előzményben és minden klónban megmarad. Ezért:

1. **Cseréld le a titkot** (új jelszó / kulcs), ez az egyetlen biztos megoldás.
2. Opcionálisan töröld az előzményből (`git filter-repo --replace-text`), majd force push. Ez minden klónt érint.
