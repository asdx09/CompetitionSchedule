# Tesztek

Három szintű teszt tartozik a PHP backendhez. Mindegyik a **futó** backend ellen megy (fekete doboz), ezért ugyanígy lefuttathatók a .NET backend ellen is (`SL_BASE`), ha az össze akarod hasonlítani a kettőt.

| Fájl | Mit ellenőriz | Kell hozzá |
|---|---|---|
| `test_api.py` | Mind a 29 végpont: válaszkódok, JSON formátum, hibaüzenetek, jogosultságok, e-mail küldés (STARTTLS + AUTH), Identity-kompatibilis jelszóhash, régi hash automatikus frissítése, CORS, rate limit, admin felület + CSRF, solver-visszahívás kulccsal, Excel export tartalma (142 ellenőrzés) | `mock_solver.py`, `test_smtpd.py`, PostgreSQL |
| `test_real_solver.py` | A valódi OR-Tools solverrel (`ScheduleSolver/Main.py`) együtt: a beosztás betartja az időtartamokat, helyszíneket, tiltásokat, nincs ütközés | futó Python solver |
| `e2e_frontend.js` | A valódi Angular klienst kattintja végig headless Chromiumban (regisztráció → megerősítő levél → belépés → esemény → mentés → megoldás → nézet → Excel letöltés → kijelentkezés) | Playwright, lebuildelt kliens, valódi solver |

## Előkészítés (egyszer)

```bash
pip install requests openpyxl aiosmtpd
openssl req -x509 -newkey rsa:2048 -nodes -keyout tests/_smtp.key -out tests/_smtp.crt -days 30 -subj "/CN=localhost"
```

A backendet egy **teszt** konfigurációval indítsd (ne az éles levelezővel!). A `ScheduleLogic.PhpServer/appsettings.Development.json`-ba például:

```json
{
  "MailSend": { "Host": "localhost", "Port": "2587", "User": "mailuser", "Password": "mailpass", "VerifyCertificate": false },
  "AdminAuth": { "Username": "admin", "PasswordHash": "<php dev/hash-password.php AdminPass123!>" },
  "RateLimiting": { "Register": { "PermitLimit": 1000 }, "EmailSending": { "PermitLimit": 1000 }, "ResetPassword": { "PermitLimit": 1000 }, "AdminLogin": { "PermitLimit": 1000 } },
  "AppSettings": { "PublicUrl": "https://localhost:58921" }
}
```

## Futtatás

```bash
python tests/test_smtpd.py &          # teszt levelező a 2587-es porton
python tests/mock_solver.py &         # solver utánzat a 8000-es porton
APP_ENV=Development php -S 127.0.0.1:7099 -t public public/index.php &

export PGHOST=localhost PGUSER=schedulelogic_app PGPASSWORD=... PGDATABASE=scheduleLogic
python tests/test_api.py
```

A `test_real_solver.py`-hoz a mock helyett a valódi solvert indítsd (`python ScheduleSolver/Main.py`).

Környezeti változók: `SL_BASE` (backend címe), `SL_MAILDIR`, `SL_FRONTEND`, `SL_PUBLIC_URL`, `SL_ADMIN_USER`, `SL_ADMIN_PASSWORD`.
