#!/usr/bin/env bash
# ScheduleLogic PHP backend - fejlesztői indítás (Linux / macOS)
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
export APP_ENV=Development
php "$ROOT/dev/check-env.php"
php -S 127.0.0.1:7099 -t "$ROOT/public" "$ROOT/public/index.php" &
PHP_PID=$!
trap 'kill $PHP_PID 2>/dev/null' EXIT
echo "ScheduleLogic PHP backend: https://localhost:7098"
caddy run --config "$ROOT/dev/Caddyfile" --adapter caddyfile
