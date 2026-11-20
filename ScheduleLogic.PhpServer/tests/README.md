# Tests

The PHP backend has three levels of tests. All of them run against the **running** backend (black box), so they can be run against the .NET backend too (`SL_BASE`) if you want to compare the two.

| File | What it checks | Requires |
|---|---|---|
| `test_api.py` | All 29 endpoints: status codes, JSON format, error messages, access control, e-mail sending (STARTTLS + AUTH), Identity-compatible password hash, automatic upgrade of old hashes, CORS, rate limit, admin UI + CSRF, solver callback with key, Excel export content (142 checks) | `mock_solver.py`, `test_smtpd.py`, PostgreSQL |
| `test_real_solver.py` | Together with the real OR-Tools solver (`ScheduleSolver/Main.py`): the schedule respects durations, locations and blocked periods, with no overlaps | running Python solver |
| `e2e_frontend.js` | Clicks through the real Angular client in headless Chromium (registration → confirmation e-mail → login → event → save → solve → view → Excel download → logout) | Playwright, built client, real solver |

## Preparation (once)

```bash
pip install requests openpyxl aiosmtpd
openssl req -x509 -newkey rsa:2048 -nodes -keyout tests/_smtp.key -out tests/_smtp.crt -days 30 -subj "/CN=localhost"
```

Start the backend with a **test** configuration (not with the production mail server!). For example in `ScheduleLogic.PhpServer/appsettings.Development.json`:

```json
{
  "MailSend": { "Host": "localhost", "Port": "2587", "User": "mailuser", "Password": "mailpass", "VerifyCertificate": false },
  "AdminAuth": { "Username": "admin", "PasswordHash": "<php dev/hash-password.php AdminPass123!>" },
  "RateLimiting": { "Register": { "PermitLimit": 1000 }, "EmailSending": { "PermitLimit": 1000 }, "ResetPassword": { "PermitLimit": 1000 }, "AdminLogin": { "PermitLimit": 1000 } },
  "AppSettings": { "PublicUrl": "https://localhost:58921" }
}
```

## Running

```bash
python tests/test_smtpd.py &          # test mail server on port 2587
python tests/mock_solver.py &         # solver mock on port 8000
APP_ENV=Development php -S 127.0.0.1:7099 -t public public/index.php &

export PGHOST=localhost PGUSER=schedulelogic_app PGPASSWORD=... PGDATABASE=scheduleLogic
python tests/test_api.py
```

For `test_real_solver.py`, start the real solver instead of the mock (`python ScheduleSolver/Main.py`).

Environment variables: `SL_BASE` (backend address), `SL_MAILDIR`, `SL_FRONTEND`, `SL_PUBLIC_URL`, `SL_ADMIN_USER`, `SL_ADMIN_PASSWORD`.
