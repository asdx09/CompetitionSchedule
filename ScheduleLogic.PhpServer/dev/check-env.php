<?php

declare(strict_types=1);

if (PHP_VERSION_ID < 80100) {
    fwrite(STDERR, "ERROR: PHP 8.1 or newer is required (current: " . PHP_VERSION . ")\n");
    exit(1);
}

require __DIR__ . '/../src/bootstrap.php';

if (!getenv('APP_ENV')) {
    putenv('APP_ENV=Development');
}

$ok = true;
$line = static function (bool $pass, string $what, string $hint = '') use (&$ok): void {
    $ok = $ok && $pass;
    echo ($pass ? '  [OK]   ' : '  [ERROR] ') . $what . ($pass || $hint === '' ? '' : "\n         -> $hint") . "\n";
};
$warn = static function (bool $pass, string $what, string $hint = ''): void {
    echo ($pass ? '  [OK]   ' : '  [WARN] ') . $what . ($pass || $hint === '' ? '' : "\n         -> $hint") . "\n";
};

echo "PHP " . PHP_VERSION . " (" . PHP_OS_FAMILY . ")\n";
echo "php.ini: " . (php_ini_loaded_file() ?: '(none loaded!)') . "\n\n";

echo "Extensions:\n";
foreach (['pdo_pgsql' => 'database', 'openssl' => 'e-mail sending (STARTTLS)', 'zip' => 'Excel export'] as $ext => $why) {
    $line(extension_loaded($ext), "$ext ($why)", "remove the ; in front of the 'extension=$ext' line in php.ini");
}

echo "\nConfiguration (APP_ENV=" . getenv('APP_ENV') . "):\n";
$app = new ScheduleLogic\App(APP_ROOT);
$cfg = $app->config;
foreach ($cfg->loadedFiles() as $f) {
    echo "  loaded: $f\n";
}
$line($cfg->string('Database:Host') !== '', 'Database section', 'must be in ../ScheduleLogic.Server/appsettings.json');
$warn($cfg->string('MailSend:Host') !== '', 'MailSend:Host', 'without e-mail sending, registrations can only be confirmed from the admin UI');
$line($cfg->string('URLS:Backend') !== '' && $cfg->string('URLS:Frontend') !== '', 'URLS:Backend / URLS:Frontend');
$warn($cfg->string('AdminAuth:Username') !== '' && $cfg->string('AdminAuth:PasswordHash') !== '',
    'AdminAuth (for the admin UI)', 'not set - admin login will not work (see README)');

echo "\nConnections:\n";
try {
    $app->db->pdo()->query('SELECT 1');
    $line(true, 'PostgreSQL: ' . $cfg->string('Database:Host') . ':' . $cfg->string('Database:Port') . '/' . $cfg->string('Database:Name'));
    $count = (int)$app->db->pdo()->query('SELECT COUNT(*) FROM users')->fetchColumn();
    echo "         number of users: $count\n";
} catch (Throwable $e) {
    $line(false, 'PostgreSQL', $e->getMessage());
}
try {
    $app->solverClient()->request('GET', '', null, 3);
    $line(true, 'Solver: ' . $cfg->string('URLS:Solver'));
} catch (Throwable $e) {
    $warn(false, 'Solver: ' . $cfg->string('URLS:Solver'), 'not running? (python ScheduleSolver/Main.py) - the backend starts without it too');
}

echo "\nFiles:\n";
$storage = APP_ROOT . '/storage';
$line(is_writable($storage), "storage/ writable ($storage)");
$webRoot = $cfg->string('AppSettings:WebRoot') ?: (($cfg->sharedDir() ?? APP_ROOT . '/../ScheduleLogic.Server') . '/wwwroot');
$hasIndex = is_file($webRoot . '/index.html');
echo ($hasIndex ? '  [OK]   ' : '  [INFO] ') . "Angular build (wwwroot/index.html): " . ($hasIndex ? $webRoot : 'missing - not needed in development, "npm start" serves it') . "\n";

echo "\n" . ($ok ? "All good, you can start: dev\\start-dev.cmd\n" : "Fix the errors above, then run this again.\n");
exit($ok ? 0 : 1);
