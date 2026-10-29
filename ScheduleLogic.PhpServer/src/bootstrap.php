<?php

declare(strict_types=1);

// ---------------------------------------------------------------------------
// ScheduleLogic PHP backend - indítás
//
// Egyszerű PSR-4 autoloader (Composer nélkül), hogy a projektnek ne legyen
// külső függősége: a "ScheduleLogic\" névtér az src/ mappára képződik le.
// ---------------------------------------------------------------------------

define('APP_ROOT', dirname(__DIR__));

spl_autoload_register(static function (string $class): void {
    $prefix = 'ScheduleLogic\\';
    if (strncmp($class, $prefix, strlen($prefix)) !== 0) {
        return;
    }
    $relative = substr($class, strlen($prefix));
    $file = __DIR__ . '/' . str_replace('\\', '/', $relative) . '.php';
    if (is_file($file)) {
        require $file;
    }
});

// Minden PHP figyelmeztetésből kivétel lesz, hogy ne maradjon csendben
// elnyelt hiba (pl. nem létező tömbkulcs) - ez a .NET viselkedéséhez áll közel.
set_error_handler(static function (int $severity, string $message, string $file, int $line): bool {
    if (!(error_reporting() & $severity)) {
        return false;
    }
    throw new ErrorException($message, 0, $severity, $file, $line);
});

ini_set('display_errors', '0');
ini_set('default_mimetype', '');   // üres válasznál ne küldjön "text/html"-t
ini_set('expose_php', '0');
if (function_exists('header_remove') && PHP_SAPI !== 'cli') {
    header_remove('X-Powered-By');
}
