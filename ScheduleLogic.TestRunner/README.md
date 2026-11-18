# ScheduleLogic tesztelő és terhelésmérő

Három projekt tartozik hozzá:

| Projekt | Mi ez |
|---|---|
| `ScheduleLogic.TestKit` | Osztálykönyvtár: API-kliens, a tesztek, a beosztás-validátor, a terhelésmotor, riportok. Külső NuGet csomag nélkül. |
| `ScheduleLogic.TestRunner` | WinForms felület: gombokkal futtatható tesztek, két futás összehasonlítása, terheléses mérés élő grafikonnal. |
| `ScheduleLogic.TestCli` | Ugyanez parancssorból (`sltest`), szkriptből vagy CI-ból. |

A tesztek **fekete dobozosak**: csak a HTTP API-t használják, ezért ugyanazok futnak a .NET és a PHP backend ellen is.

## Előfeltételek (fejlesztői gépen)

1. **Az e-mail-megerősítés kikapcsolása fejlesztői módban.** A `ScheduleLogic.Server/appsettings.Development.json`-ban:
   ```json
   "DevOptions": { "SkipEmailConfirmation": true }
   ```
   Ezt a .NET és a PHP backend is innen olvassa. Csak `Development` környezetben hat, élesben a beállítás értékétől függetlenül mindig kell a megerősítés.
2. **A rate limit kikapcsolása fejlesztéskor** (különben a sok regisztráció 429-et kap): `"RateLimiting": { "Enabled": false }`, szintén a `ScheduleLogic.Server/appsettings.Development.json`-ban (alapból így van). Élesben be van kapcsolva.
3. A **Beosztás (solver)** tesztekhez fusson a Python solver (`ScheduleSolver/Main.py`).

## Használat (GUI)

Visual Studióban: jobb klikk a `ScheduleLogic.TestRunner` projekten, majd *Set as Startup Project* (vagy *Debug → Start new instance*), miközben a backend fut.

- **Felső sáv:** a backend címkéje (`.NET` / `PHP`) és címe (alapból `https://localhost:7098`). A két backend ugyanazon a porton fut, ezért a címke mondja meg, melyik válaszolt.
- **Tesztek fül:** gombok csoportonként, *Összes teszt*, vagy dupla kattintás egy soron. Alul a kijelölt teszt leírása és teljes naplója (kérések, válaszok) látszik. Minden futás után JSON riport készül ide: `Dokumentumok\ScheduleLogic\TestResults`.
- **Összehasonlítás fül:** futtasd az összes tesztet a .NET-tel, indítsd el helyette a PHP-t, futtasd újra `PHP` címkével, majd itt válaszd ki a két riportot. Az összevetés a teszt eredményén túl minden kérés státuszkódját és a válasz JSON-szerkezetét is nézi (azonosítók és időpontok nélkül).
- **Terhelés fül:** lépcsős mérés (pl. `1,5,10,25,50` felhasználó), élő grafikon, és a végén CSV-k ide: `Dokumentumok\ScheduleLogic\LoadResults` (Excelben közvetlenül megnyithatók).

Minden teszt saját, véletlen nevű (`sltest_...`) felhasználót hoz létre, és a végén törli, a fióktörlés pedig kaszkádoltan viszi a versenyeit is.

## Tesztcsoportok

| Csoport | Mit néz |
|---|---|
| Alap | Elérhető-e az API |
| Hitelesítés | Regisztráció, belépés (névvel, e-maillel), kijelentkezés, foglalt név/e-mail, védett végpontok, felhasználó-felderítés elleni védelem, jelszó-visszaállítás, fióktörlés |
| Versenyek | Új verseny, teljes mentés és visszaolvasás (azonosító-csere, hivatkozások), szerkesztés, hibás hivatkozás → 400 + visszagörgetés, varázsló, törlés |
| Jogosultság | Más felhasználó versenye semmilyen végponton nem érhető el |
| Bemenet-ellenőrzés | Hibás JSON, nem szám azonosító, rövid jelszó, rossz e-mail, üres név, negatív szünet, túl nagy súly, fordított dátumok |
| Beosztás (solver) | Beosztás készítése, ellenőrzése **független validátorral**, Excel export, újramentés törli a régi beosztást, futó keresés leállítása |
| Biztonság | Hamis solver-válasz, munkamenet-visszajátszás kijelentkezés után, süti-attribútumok, CORS, hibaüzenetek szivárgása |

A validátor (`ScheduleValidator`) nem a solver kódját használja, csak a bemenetet és az eredményt: minden nevezés pontosan egyszer, helyes hossz, megengedett helyszín, létező lőállás, nincs átfedés lőálláson és versenyzőnél, a kapacitás nincs túllépve, egy versenyszám egy helyszínen.

## Terheléses mérés

- **Zárt modell:** N virtuális felhasználó, mindegyik a válasz után azonnal újra kér. Ez arra jó, hogy mennyit bír a szerver.
- **Nyitott modell:** fix kérés/másodperc. A késleltetést a *tervezett* indítástól méri, így ha a szerver lelassul, az látszik (nincs „coordinated omission”).
- Forgatókönyvek: `CheckToken` (keretrendszer-alapköltség), `GetEvents`, `GetEvent` (nagy verseny, 9 tábla), `SaveEvent` (tranzakciós mentés), `Mixed`.
- A **figyelt folyamatok** CPU- és memóriahasználata másodpercenként mintavételezve (.NET: `ScheduleLogic.Server`, PHP: `php-cgi` / `php-fpm` / `php`). A mérés előtti tétlen memória is bekerül az összesítőbe.

Hogy a mérés fair legyen:
- .NET: Release build, és ne a debuggerből fusson (`dotnet run -c Release` vagy a lefordított exe).
- PHP: bekapcsolt OPcache, és **ne** a beépített szerver (`php -S`), mert az egyszálú; méréshez `php-cgi` / `php-fpm` több munkafolyamattal, nginx vagy Caddy mögött.
- Mindkettő ugyanúgy proxy mögött legyen, vagy egyik sem.
- Bemelegítés legyen (alapból 5 s), és minden mérést ismételj meg 3–5-ször.

## Parancssor

```
sltest test --url https://localhost:7098 --label NET
sltest test --url https://localhost:7098 --label PHP --group Biztonság
sltest compare <A.json> <B.json>
sltest load --url https://localhost:7098 --label NET --scenario GetEvent --mode closed --levels 1,5,10,25,50 --duration 30 --warmup 5 --users 10 --event-size 100 --monitor ScheduleLogic.Server
```

A `test` kilépési kódja 1, ha van hibás teszt, így CI-ban is használható.
