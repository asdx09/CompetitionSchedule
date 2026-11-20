$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$env:APP_ENV = 'Development'

foreach ($cmd in 'php', 'caddy') {
    if (-not (Get-Command $cmd -ErrorAction SilentlyContinue)) {
        Write-Host "Command '$cmd' not found. Install it (see README.md), then open a new terminal." -ForegroundColor Red
        exit 1
    }
}

$busy = Get-NetTCPConnection -LocalPort 7098 -State Listen -ErrorAction SilentlyContinue
if ($busy) {
    Write-Host "Port 7098 is in use (is the .NET backend running?). Stop it first." -ForegroundColor Red
    exit 1
}

& php "$root\dev\check-env.php"
if ($LASTEXITCODE -ne 0) {
    Write-Host "The environment check found errors, see above." -ForegroundColor Red
    exit 1
}

$phpArgs = @('-S', '127.0.0.1:7099', '-t', "`"$root\public`"", "`"$root\public\index.php`"")
$php = Start-Process -FilePath 'php' -ArgumentList $phpArgs -NoNewWindow -PassThru

Write-Host ""
Write-Host "ScheduleLogic PHP backend: https://localhost:7098   (admin: https://localhost:7098/admin)" -ForegroundColor Green
Write-Host "Frontend: run 'npm start' in the schedulelogic.client folder -> https://localhost:58921" -ForegroundColor Green
Write-Host ""

try {
    & caddy run --config "$root\dev\Caddyfile" --adapter caddyfile
}
finally {
    if ($php -and -not $php.HasExited) { Stop-Process -Id $php.Id -Force }
}
