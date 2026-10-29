# ---------------------------------------------------------------------------
# ScheduleLogic PHP backend - fejlesztői indítás (Windows / PowerShell)
#
#   PHP beépített szerver  ->  http://127.0.0.1:7099   (belső)
#   Caddy (HTTPS)          ->  https://localhost:7098  (ezt hívja a frontend, ugyanaz a port, mint a .NET-é)
#
# Leállítás: Ctrl+C (mindkettő leáll).
# ---------------------------------------------------------------------------
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$env:APP_ENV = 'Development'

foreach ($cmd in 'php', 'caddy') {
    if (-not (Get-Command $cmd -ErrorAction SilentlyContinue)) {
        Write-Host "Nem található a(z) '$cmd' parancs. Telepítsd (lásd README.md), majd nyiss új terminált." -ForegroundColor Red
        exit 1
    }
}

$busy = Get-NetTCPConnection -LocalPort 7098 -State Listen -ErrorAction SilentlyContinue
if ($busy) {
    Write-Host "A 7098-as port foglalt (fut a .NET backend?). Előbb állítsd le." -ForegroundColor Red
    exit 1
}

& php "$root\dev\check-env.php"
if ($LASTEXITCODE -ne 0) {
    Write-Host "A környezet-ellenőrzés hibát talált, lásd fent." -ForegroundColor Red
    exit 1
}

$phpArgs = @('-S', '127.0.0.1:7099', '-t', "`"$root\public`"", "`"$root\public\index.php`"")
$php = Start-Process -FilePath 'php' -ArgumentList $phpArgs -NoNewWindow -PassThru

Write-Host ""
Write-Host "ScheduleLogic PHP backend: https://localhost:7098   (admin: https://localhost:7098/admin)" -ForegroundColor Green
Write-Host "Frontend: a schedulelogic.client mappában 'npm start' -> https://localhost:58921" -ForegroundColor Green
Write-Host ""

try {
    & caddy run --config "$root\dev\Caddyfile" --adapter caddyfile
}
finally {
    if ($php -and -not $php.HasExited) { Stop-Process -Id $php.Id -Force }
}
