<?php

declare(strict_types=1);

/*
 * Környezet-ellenőrző: futtasd így:   php dev/check-env.php
 * Megmondja, hogy minden megvan-e a PHP backend futtatásához.
 */

if (PHP_VERSION_ID < 80100) {
    fwrite(STDERR, "HIBA: PHP 8.1 vagy újabb kell (most: " . PHP_VERSION . ")\n");
    exit(1);
}

require __DIR__ . '/../src/bootstrap.php';

if (!getenv('APP_ENV')) {
    putenv('APP_ENV=Development');
}

$ok = true;
$line = static function (bool $pass, string $what, string $hint = '') use (&$ok): void {
    $ok = $ok && $pass;
    echo ($pass ? '  [OK]   ' : '  [HIBA] ') . $what . ($pass || $hint === '' ? '' : "\n         -> $hint") . "\n";
};
// nem végzetes: figyelmeztet, de az indítást nem akadályozza
$warn = static function (bool $pass, string $what, string $hint = ''): void {
    echo ($pass ? '  [OK]   ' : '  [FIGY] ') . $what . ($pass || $hint === '' ? '' : "\n         -> $hint") . "\n";
};

echo "PHP " . PHP_VERSION . " (" . PHP_OS_FAMILY . ")\n";
echo "php.ini: " . (php_ini_loaded_file() ?: '(nincs betöltve!)') . "\n\n";

echo "Kiterjesztések:\n";
foreach (['pdo_pgsql' => 'adatbázis', 'openssl' => 'e-mail küldés (STARTTLS)', 'zip' => 'Excel export'] as $ext => $why) {
    $line(extension_loaded($ext), "$ext ($why)", "a php.ini-ben vedd ki a ;-t az 'extension=$ext' sor elől");
}

echo "\nKonfiguráció (APP_ENV=" . getenv('APP_ENV') . "):\n";
$app = new ScheduleLogic\App(APP_ROOT);
$cfg = $app->config;
foreach ($cfg->loadedFiles() as $f) {
    echo "  betöltve: $f\n";
}
$line($cfg->string('Database:Host') !== '', 'Database szekció', 'a ../ScheduleLogic.Server/appsettings.json-ban kell lennie');
$warn($cfg->string('MailSend:Host') !== '', 'MailSend:Host', 'e-mail küldés nélkül a regisztráció megerősítése csak az admin felületről megy');
$line($cfg->string('URLS:Backend') !== '' && $cfg->string('URLS:Frontend') !== '', 'URLS:Backend / URLS:Frontend');
$warn($cfg->string('AdminAuth:Username') !== '' && $cfg->string('AdminAuth:PasswordHash') !== '',
    'AdminAuth (admin felülethez)', 'nincs megadva - az admin login nem fog működni (lásd README)');

echo "\nKapcsolatok:\n";
try {
    $app->db->pdo()->query('SELECT 1');
    $line(true, 'PostgreSQL: ' . $cfg->string('Database:Host') . ':' . $cfg->string('Database:Port') . '/' . $cfg->string('Database:Name'));
    $count = (int)$app->db->pdo()->query('SELECT COUNT(*) FROM users')->fetchColumn();
    echo "         felhasználók száma: $count\n";
} catch (Throwable $e) {
    $line(false, 'PostgreSQL', $e->getMessage());
}
try {
    $app->solverClient()->request('GET', '', null, 3);
    $line(true, 'Solver: ' . $cfg->string('URLS:Solver'));
} catch (Throwable $e) {
    $warn(false, 'Solver: ' . $cfg->string('URLS:Solver'), 'nem fut? (python ScheduleSolver/Main.py) - a backend enélkül is elindul');
}

echo "\nFájlok:\n";
$storage = APP_ROOT . '/storage';
$line(is_writable($storage), "storage/ írható ($storage)");
$webRoot = $cfg->string('AppSettings:WebRoot') ?: (($cfg->sharedDir() ?? APP_ROOT . '/../ScheduleLogic.Server') . '/wwwroot');
$hasIndex = is_file($webRoot . '/index.html');
echo ($hasIndex ? '  [OK]   ' : '  [INFO] ') . "Angular build (wwwroot/index.html): " . ($hasIndex ? $webRoot : 'nincs - fejlesztéskor nem kell, az "npm start" szolgálja ki') . "\n";

echo "\n" . ($ok ? "Minden rendben, indulhat: dev\\start-dev.cmd\n" : "Javítsd a fenti hibákat, majd futtasd újra.\n");
exit($ok ? 0 : 1);
