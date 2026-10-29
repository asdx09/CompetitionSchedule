# ScheduleLogic.PhpServer

A ScheduleLogic backend PHP-s változata. A **ScheduleLogic.Server** (ASP.NET Core 8) pontos megfelelője:

- **ugyanazokat a végpontokat** szolgálja ki ugyanazzal a JSON-nal, így az Angular kliensen **semmit nem kell módosítani**,
- **ugyanazzal a PostgreSQL adatbázissal** dolgozik, és a beállításait a .NET projekt `appsettings*.json` fájljaiból olvassa,
- **ugyanazon a porton** fut (fejlesztéskor `https://localhost:7098`), így a kettő közül mindig csak az egyik megy,
- **ugyanúgy kommunikál a Python solverrel**, a solver kódján sem kell változtatni,
- a .NET-ben regisztrált felhasználók **ugyanazzal a jelszóval** be tudnak lépni, mert a jelszó-hash formátuma bájtra azonos az ASP.NET Core Identity-ével.

Natív PHP 8 (≥ 8.1), **külső csomag nélkül**: nem kell Composer. Csak a PHP beépített `pdo_pgsql`, `openssl` és `zip` kiterjesztései kellenek.

---

## Gyors indítás (Windows, fejlesztés)

### 1. PHP telepítése

1. Töltsd le a PHP-t: <https://windows.php.net/download/>, **PHP 8.3 (vagy 8.4) – VS16 x64 Non Thread Safe – Zip**.
2. Csomagold ki például a `C:\php` mappába.
3. Add hozzá a `C:\php`-t a **PATH**-hoz: Start → „Rendszer környezeti változóinak szerkesztése” → Környezeti változók → Path → Új.
4. A `C:\php` mappában másold le a `php.ini-development` fájlt `php.ini` néven, és nyisd meg. Vedd ki a `;` jelet ezeknek a soroknak az elejéről:
   ```ini
   extension_dir = "ext"
   extension=openssl
   extension=pdo_pgsql
   extension=zip
   ```
5. Nyiss **új** terminált, és ellenőrizd: `php -v`.

> Alternatíva: `winget install PHP.PHP.8.3`. Ilyenkor a `php --ini` paranccsal derítsd ki, hol legyen a `php.ini`, és ugyanezt a 4. lépést csináld meg ott.

### 2. Caddy telepítése (HTTPS a 7098-as porton)

A beépített PHP szerver csak HTTP-t tud, a frontend viszont `https://localhost:7098`-at hív. A [Caddy](https://caddyserver.com) egyetlen exe, ami HTTPS-t tesz elé:

```powershell
winget install CaddyServer.Caddy
```

(Vagy töltsd le a <https://caddyserver.com/download> oldalról, és tedd a PATH-ba.)

Első indításkor a Windows megkérdezi, hogy megbízzon-e a Caddy helyi tanúsítványkiadójában. Erre **Igen**-t kell válaszolni, és csak egyszer kérdezi.

### 3. Ellenőrzés és indítás

```powershell
cd "D:\VS .net web api\Szakdolgozat\ScheduleLogic.PhpServer"
$env:APP_ENV = "Development"
php dev\check-env.php        # megmondja, ha valami hiányzik (kiterjesztés, DB kapcsolat, ...)
```

Ha minden `[OK]`: **dupla kattintás a `dev\start-dev.cmd`-n**. Elindul a PHP (belül a 7099-es porton) és a Caddy (`https://localhost:7098`). Leállítás: `Ctrl+C`.

A többi komponens a szokásos módon indul:

```powershell
cd ..\schedulelogic.client;  npm start          # https://localhost:58921
cd ..\ScheduleSolver;        python Main.py      # http://127.0.0.1:8000
```

> **Fontos:** a .NET backend és a PHP backend ugyanazt a 7098-as portot használja, ezért egyszerre csak az egyik futhat. Ha a Visual Studióból indítod a .NET-et, előbb a PHP-t állítsd le, és fordítva.

### Admin felület

`https://localhost:7098/admin`. A belépési adatok a configban vannak (`AdminAuth:Username`, `AdminAuth:PasswordHash`). Ha a .NET-hez a Visual Studio **User Secrets**-ébe tetted őket, a PHP Development módban onnan is beolvassa őket. Új jelszó-hash így készíthető:

```powershell
php dev\hash-password.php "UjAdminJelszo"
```

Az eredmény a .NET-tel is működik.

---

## Felépítés: C# → PHP megfeleltetés

```
ScheduleLogic.PhpServer/
├── public/index.php            ← belépési pont (Program.cs: app.Run)
├── src/
│   ├── App.php                 ← Program.cs: DI, útvonalak, CORS, cookie-k, middleware sorrend
│   ├── Controllers/            ← Controllers/*.cs  (1:1, ugyanazok a végpontok)
│   ├── Services/               ← Services/*.cs
│   │   ├── PasswordHasher.php  ← ASP.NET Core Identity PasswordHasher<T> (V2 + V3 formátum)
│   │   ├── SmtpEmailSender.php ← System.Net.Mail.SmtpClient (STARTTLS)
│   │   ├── XlsxWriter.php      ← ClosedXML (Excel export)
│   │   └── SolverCallbackSigner.php  ← ÚJ: a solver-visszahívás hitelesítése
│   ├── Repositories/           ← Services/Repositories/*.cs  (EF Core helyett PDO + SQL)
│   ├── Models/                 ← Class/*Models.cs  (bemenet-ellenőrzés, mint az [ApiController])
│   └── Core/                   ← keretrendszer-pótló kis osztályok (router, request/response,
│                                  config, session-alapú auth, CORS, rate limit, log)
├── views/admin/                ← Views/Admin/*.cshtml
├── dev/                        ← indító scriptek, Caddyfile, környezet-ellenőrző, hash-generáló
├── deploy/nginx.conf.example   ← éles telepítés
├── tests/                      ← API-, solver- és frontend end-to-end tesztek
└── storage/                    ← session-ök, naplók, rate limit (gitignore-olt)
```

| Végpont | C# | PHP |
|---|---|---|
| `api/Authentication/*` (login, register, confirm, resend-confirmation, forgot-password, reset-password, check-token, delete, logout) | `AuthenticationController` | `Controllers/AuthenticationController.php` |
| `api/Event/*` (new-event, delete-event, get-events, get-event, save-event, new-wizard) | `EventController` | `Controllers/EventController.php` |
| `api/Schedule/*` (`{id}` POST/GET, isRunning, stop, answer, export) | `ScheduleController` | `Controllers/ScheduleController.php` |
| `admin`, `admin/login`, `admin/logout`, `admin/api/*` | `AdminController` + Razor nézetek | `Controllers/AdminController.php` + `views/admin` |

Ami a .NET-ben a keretrendszer dolga volt, itt egy-egy kis, olvasható osztály:

| ASP.NET Core | PHP |
|---|---|
| Attribútum-routing (kis/nagybetű-érzéketlen, `{id:long}`) | `Core/Router.php` |
| `IConfiguration` (appsettings + környezet + User Secrets + env változók) | `Core/Config.php` |
| Cookie Authentication (sliding expiration, 2 séma) | `Core/SessionAuth.php` (szerveroldali session) |
| `[ApiController]` modell-validáció → 400 ProblemDetails | `Models/InputReader.php`, `Models/EventInput.php` |
| `IExceptionHandler` | `App::handleException()` |
| System.Text.Json (camelCase, kis/nagybetű-érzéketlen beolvasás) | `Core/Json.php` |
| DateTime / TimeOnly JSON formátum, `ToLocalTime()` | `Core/DotNetTime.php` |
| `UseStaticFiles` + `MapFallbackToFile("index.html")` | `Core/StaticFiles.php` |
| Antiforgery token | `Core/Csrf.php` |

---

## Konfiguráció

A PHP **ugyanazokat a fájlokat** olvassa, mint a .NET, ebben a sorrendben (a későbbi felülírja a korábbit):

1. `../ScheduleLogic.Server/appsettings.json`
2. `../ScheduleLogic.Server/appsettings.{Környezet}.json`
3. a .NET **User Secrets** (csak Development módban; a `.csproj` `UserSecretsId`-ja alapján)
4. `ScheduleLogic.PhpServer/appsettings.json` (opcionális, csak PHP-specifikus értékekhez, minta: `appsettings.sample.json`)
5. `ScheduleLogic.PhpServer/appsettings.{Környezet}.json`
6. környezeti változók `__` elválasztóval, pl. `Database__Password` (ugyanúgy, mint .NET-ben)

A környezetet az `APP_ENV` (vagy `ASPNETCORE_ENVIRONMENT`) változó adja meg. **Alapértéke `Production`**, akárcsak .NET-ben. A `start-dev` scriptek `Development`-re állítják.

Csak a PHP-ben létező beállítások (mind opcionális):

| Kulcs | Alapérték | Mire jó |
|---|---|---|
| `AppSettings:TimeZone` | `Europe/Budapest` | a „szerver helyi ideje” (a .NET `ToLocalTime()` megfelelője) |
| `AppSettings:WebRoot` | `../ScheduleLogic.Server/wwwroot` | a lebuildelt Angular kliens helye |
| `AppSettings:ExportDateFormat` | `Y-m-d H:i:s` | dátumformátum az Excel exportban |
| `Cors:AllowedOrigins` | dev: `https://localhost:58921`, éles: `https://schedulelogic.hu` | CORS |
| `ForwardedHeaders:KnownProxies` | `127.0.0.1`, `::1` | melyik proxy `X-Forwarded-For` fejlécében bízzunk |
| `Solver:CallbackSecret` | (generált, `storage/solver-callback.key`) | a solver-visszahívás aláírókulcsa |
| `RateLimiting:*` | lásd `appsettings.sample.json` | túlterhelés / jelszó-próbálgatás elleni korlát |
| `MailSend:VerifyCertificate` | `true` | csak helyi teszt SMTP-hez érdemes kikapcsolni |

---

## Eltérések a .NET backendhez képest

### Javított biztonsági hibák

| # | .NET backend | PHP backend |
|---|---|---|
| 1 | Az `api/Schedule/answer` csak azt ellenőrizte, hogy a kérés localhostról jön-e. **Reverse proxy (nginx) mögött minden kérés onnan jön**, így bárki beküldhetett hamis eredményt. | A solver-visszahívás URL-jébe eseményhez kötött **HMAC-SHA256 kulcs** kerül (`?key=...`). A solvernek nem kell változnia, mert a kapott `returnURL`-re küld vissza. A kulcs csak az adott eseményhez jó, és a beküldött azonosítóknak is az eseményhez kell tartozniuk. |
| 2 | Élesben a cookie `SameSite=None` volt, ezzel egy idegen oldal a felhasználó nevében küldhetett kéréseket (CSRF). | `SameSite=Lax`, Domain nélkül. |
| 3 | A regisztrációnál nem volt ellenőrzés (üres jelszó, rossz e-mail is átment). | Legalább 8 karakteres jelszó, érvényes e-mail, 1–255 karakteres felhasználónév. |
| 4 | A `SuccessRehashNeeded` eredményt elutasította, így egy Identity-frissítés után a régi felhasználók nem tudtak volna belépni. | Elfogadja, és belépéskor automatikusan frissíti a hash-t. |
| 5 | Nem volt rate limiting. | Login, regisztráció, e-mail küldés, jelszó-visszaállítás és admin login korlátozva (429 + `Retry-After`). |
| 6 | Kijelentkezés után a régi cookie (lejáratig) elvileg még érvényes. | Szerveroldali session: kijelentkezéskor vagy fióktörléskor azonnal megszűnik. |
| 7 | — | Nem létező felhasználónál is lefut egy ál-hash, így a válaszidőből sem derül ki, létezik-e a fiók. |

### Egyéb (kisebb) különbségek

- **`api/Schedule/stop`**: a .NET mindig `false`-t adott vissza, mert a solver kisbetűs `status` kulcsát kis/nagybetű-érzékenyen olvasta. A PHP helyesen `true`-t ad, ha a solver leállt. (A frontend ezt az értéket nem használja.)
- **`POST api/Schedule/{id}`** válaszában a `returnURL` üres, mert a visszahívási kulcs nem a böngészőre tartozik. (A frontend ezt sem használja.)
- **Varázsló**: ha az import hibás, a .NET-ben ott maradt egy üres „New Event”. Itt az egész egy tranzakció, így hiba esetén nem marad semmi.
- **Solver-eredmény mentése**: egy tranzakcióban történik, így részleges állapot nem maradhat.
- **Excel export**: a dátum `2026-03-01 09:00:00` formátumú. A .NET a szerver nyelvi beállítása szerint írta, ez gépenként más volt.
- A listák (események, résztvevők stb.) azonosító szerint rendezve jönnek, a .NET-nél a sorrend nem volt garantált.
- A `DatabaseService.cs` a .NET-ben sehol nincs regisztrálva (régi, holt kód), ezért nem került át.

---

## Éles telepítés (Linux)

```bash
sudo apt install php8.3-fpm php8.3-pgsql php8.3-zip nginx
```

- Minta konfiguráció: `deploy/nginx.conf.example`. Az nginx szolgálja ki az Angular buildet, az `/api` és `/admin` kéréseket a PHP-FPM kapja.
- Az `APP_ENV` alapból `Production`. A jelszavakat környezeti változóként is megadhatod (`Database__Password`, `MailSend__Password`, …).
- A `storage/` mappa legyen írható a PHP-FPM felhasználójának (`www-data`).
- Ha a .NET projekt mappája nincs a szerveren, az `SL_SHARED_CONFIG_DIR` környezeti változóval adhatod meg, hol vannak az `appsettings` fájlok, vagy tedd őket a `ScheduleLogic.PhpServer` mappába.

---

## Tesztelés

Részletek: `tests/README.md`. A fejlesztés során ezek futottak le sikeresen, valódi PHP 8.3 + PostgreSQL 16 környezetben:

- **`test_api.py`**: 142 ellenőrzés mind a 29 végpontra (formátumok, hibakódok, jogosultság, e-mail STARTTLS-sel, hash-kompatibilitás független Python implementációval, régi hash frissítése, CORS, rate limit, admin + CSRF, solver-kulcs, Excel tartalom),
- **`test_real_solver.py`**: a valódi OR-Tools solverrel; a kapott beosztás minden megkötést betart,
- **`e2e_frontend.js`**: a valódi Angular kliens végigkattintva headless Chromiumban, a fejlesztői felállásban is (Angular a 58921-en, a PHP Caddy mögött a 7098-on).

---

## Hibaelhárítás

| Tünet | Megoldás |
|---|---|
| `could not find driver` | Nincs bekapcsolva az `extension=pdo_pgsql` a `php.ini`-ben (`php --ini` mutatja, melyik ini töltődik be). |
| `password authentication failed` | A `../ScheduleLogic.Server/appsettings.json` `Database` szekciójában lévő adatokkal nem lehet belépni. A .NET `Program.cs`-ben van egy második, beégetett connection string is (`Username=app`), lehet, hogy eddig az működött. |
| A böngésző nem bízik a `https://localhost:7098` tanúsítványban | `caddy trust` (rendszergazdaként), majd indítsd újra a böngészőt. |
| `A 7098-as port foglalt` | Fut a .NET backend (Visual Studio), előbb azt állítsd le. |
| Az admin login mindig hibás | Nincs `AdminAuth` a configban. Készíts egy hash-t a `php dev\hash-password.php` paranccsal. |
| A naplók | `storage/logs/app-ÉÉÉÉ-HH-NN.log` (fejlesztéskor a konzolra is kiíródnak). |
