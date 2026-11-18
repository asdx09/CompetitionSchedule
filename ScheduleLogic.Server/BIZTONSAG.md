# Biztonsági javítások a .NET backendben

A PHP backend írásakor talált hibák javítása a .NET változatban is, hogy a két backend azonos biztonsági szinten legyen. Mindegyiket a `ScheduleLogic.TestRunner` tesztjei ellenőrzik (zárójelben a teszt azonosítója).

| # | Korábban | Most | Hol |
|---|---|---|---|
| 1 | Az `api/Schedule/answer` csak azt nézte, hogy a kérés localhostról jön-e. Reverse proxy (nginx) mögött minden kérés onnan jön, így bárki beküldhetett hamis beosztást. | A visszahívási URL-be versenyenként egy HMAC-SHA256 kulcs kerül (`?key=...`). Kulcs nélkül vagy rossz kulccsal 403. Az eredmény csak az adott verseny versenyzőire, versenyszámaira és helyszíneire hivatkozhat, különben 400. A mentés egy tranzakció. A Python solveren nem kellett változtatni. (SEC-01) | `Services/Security/SolverCallbackSigner.cs`, `ScheduleController`, `ScheduleService`, `ScheduleRepository` |
| 2 | A bejelentkezési jegy a sütiben volt, így kijelentkezés után a régi süti a lejáratáig érvényes maradt. | Szerveroldali munkamenet (`ITicketStore`): a sütiben csak egy véletlen azonosító van. Kijelentkezéskor azonnal megszűnik; fióktörléskor (saját vagy admin) és jelszó-visszaállításkor a felhasználó összes munkamenete is. (SEC-02) | `Services/Security/ServerSideTicketStore.cs`, `Program.cs` |
| 3 | Élesben `SameSite=None` és `Domain=schedulelogic.hu`: idegen oldal a felhasználó nevében küldhetett kéréseket (CSRF). | `SameSite=Lax`, Domain nélkül, élesben mindig `Secure`. (SEC-03) | `Program.cs` |
| 4 | Nem volt rate limiting: korlátlan jelszó-próbálgatás, tömeges regisztráció és levélküldés. | IP-címenkénti fix időablak: belépés 20/5 perc, regisztráció 5/15 perc, levélküldés 5/15 perc, jelszó-visszaállítás 10/15 perc, admin belépés 5/15 perc. Túllépéskor 429 + `Retry-After`. Állítható: `RateLimiting:*`. | `Services/Security/RateLimitingSetup.cs` |
| 5 | Proxy mögött minden kérés IP-je a proxyé volt. | `UseForwardedHeaders`: a valódi kliens IP csak a megbízható proxytól (`ForwardedHeaders:KnownProxies`, alapból a localhost) jövő fejlécből. | `Program.cs` |
| 6 | Regisztrációnál nem volt ellenőrzés: rövid jelszó elfogadva, érvénytelen e-mail-címre 500-as hiba. | Felhasználónév 1–255 karakter, vezérlőkarakter nélkül; érvényes e-mail; jelszó 8–1024 karakter. Két egyidejű regisztrációnál az egyedi index ütközése sem 500, hanem érthető hiba. (VAL-04, VAL-05) | `AuthenticationService.cs`, `PasswordPolicy.cs` |
| 7 | Üres nevek (pl. helyszín) átmentek a mentésen. | `[Required]` minden kötelező szöveges mezőn (üres és csak szóköz sem). (VAL-06) | `Class/EventModels.cs`, `AuthenticationModels.cs` |
| 8 | Nem létező felhasználónál a belépés gyorsabban válaszolt, ebből kiderült, hogy létezik-e a fiók. | Nem létező fióknál is lefut egy ál-jelszóellenőrzés. | `AuthenticationService.LoginUser` |
| 9 | A `SuccessRehashNeeded` eredményt elutasította, így egy Identity-frissítés (pl. .NET 10) után a régi hash-ű felhasználók nem tudtak volna belépni. | Elfogadja, és belépéskor a mai paraméterekkel újra-hash-eli a jelszót. | `AuthenticationService.LoginUser` |
| 10 | Az adatbázis jelszava be volt égetve a `ScheduleLogicContext.cs`-be (és bekerült a nyilvános gitbe), a `Program.cs` egy második, beégetett kapcsolati stringet is regisztrált. | A kapcsolati string csak konfigurációból jön (User Secrets / környezeti változó), `NpgsqlConnectionStringBuilder`-rel (escape). Hiányzó jelszónál érthető indítási hiba. | `Program.cs`, `Models/ScheduleLogicContext.cs`, `KONFIGURACIO.md` |
| 11 | Nem volt `X-Content-Type-Options` / `X-Frame-Options`. | `nosniff` és `DENY` minden válaszon. | `Program.cs` |

Egyéb javítások:
- `api/Schedule/stop`: a solver kisbetűs `status` kulcsa miatt mindig `false`-t adott, most helyesen jelzi a leállást.
- `POST api/Schedule/{id}` válaszában a `returnURL` üres, mert a visszahívási kulcs nem tartozik a böngészőre.
- A varázsló egy tranzakcióban hozza létre és tölti fel a versenyt; hibás importnál nem marad üres „New Event”.
- Nem szám verseny-azonosítóval a mentés 400-at ad, nem 500-at.
- Eltávolítva a nem használt csomagok: `Microsoft.AspNet.Identity.Core` (régi, .NET Framework-es), `Authentication.Google`, `Authentication.JwtBearer`, `EntityFrameworkCore.SqlServer`, `Swashbuckle`.

**Tudnivaló:** a munkamenetek memóriában vannak, ezért a backend újraindításakor mindenkinek újra be kell lépnie. Több szerverpéldány esetén a `ServerSideTicketStore`-t elosztott cache-re (pl. Redis) kell cserélni.
