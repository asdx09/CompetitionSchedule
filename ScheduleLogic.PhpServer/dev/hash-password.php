<?php

declare(strict_types=1);

/*
 * Jelszó-hash készítése az admin felülethez (AdminAuth:PasswordHash).
 * Ugyanazt a formátumot adja, mint a .NET PasswordHasher, így a hash
 * a C# és a PHP backenddel is működik.
 *
 *   php dev/hash-password.php "AzAdminJelszavam"
 */

require __DIR__ . '/../src/bootstrap.php';

$password = $argv[1] ?? null;
if ($password === null || $password === '') {
    fwrite(STDERR, "Használat: php dev/hash-password.php \"jelszó\"\n");
    exit(1);
}

echo (new ScheduleLogic\Services\PasswordHasher())->hash($password), PHP_EOL;
