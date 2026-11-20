<?php

declare(strict_types=1);

require __DIR__ . '/../src/bootstrap.php';

$password = $argv[1] ?? null;
if ($password === null || $password === '') {
    fwrite(STDERR, "Usage: php dev/hash-password.php \"password\"\n");
    exit(1);
}

echo (new ScheduleLogic\Services\PasswordHasher())->hash($password), PHP_EOL;
