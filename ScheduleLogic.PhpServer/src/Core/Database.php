<?php

declare(strict_types=1);

namespace ScheduleLogic\Core;

use PDO;

/**
 * PostgreSQL kapcsolat (az EF Core DbContext helyett sima PDO).
 *
 * A kapcsolat lusta: csak akkor nyílik meg, amikor egy végpont tényleg
 * az adatbázishoz nyúl (pl. a check-token nem).
 */
final class Database
{
    private ?PDO $pdo = null;

    public function __construct(private readonly Config $config)
    {
    }

    public function pdo(): PDO
    {
        if ($this->pdo === null) {
            $this->pdo = $this->connect();
        }
        return $this->pdo;
    }

    /** Admin health checkhez: rövid timeouttal próbál kapcsolódni. */
    public function canConnect(int $timeoutSeconds = 3): bool
    {
        try {
            $pdo = $this->pdo ?? $this->connect($timeoutSeconds);
            $pdo->query('SELECT 1');
            $this->pdo ??= $pdo;
            return true;
        } catch (\Throwable) {
            return false;
        }
    }

    private function connect(int $timeoutSeconds = 10): PDO
    {
        $db = fn(string $k, string $d = '') => $this->config->string("Database:$k", $d);

        $dsn = sprintf(
            'pgsql:host=%s;port=%s;dbname=%s;connect_timeout=%d',
            $db('Host', 'localhost'),
            $db('Port', '5432'),
            $db('Name', 'scheduleLogic'),
            $timeoutSeconds
        );

        $pdo = new PDO($dsn, $db('User'), $db('Password'), [
            PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION,
            PDO::ATTR_DEFAULT_FETCH_MODE => PDO::FETCH_ASSOC,
            PDO::ATTR_EMULATE_PREPARES => false,
        ]);

        // Minden időbélyeg UTC-ben jön vissza - ugyanígy kezeli az Npgsql is
        // a timestamptz oszlopokat (DateTimeKind.Utc).
        $pdo->exec("SET TIME ZONE 'UTC'");
        return $pdo;
    }

    /**
     * Tranzakcióba csomagolja a callbacket; kivétel esetén visszagörget
     * és továbbdobja a kivételt.
     *
     * @template T
     * @param callable(PDO):T $work
     * @return T
     */
    public function transaction(callable $work): mixed
    {
        $pdo = $this->pdo();
        $pdo->beginTransaction();
        try {
            $result = $work($pdo);
            $pdo->commit();
            return $result;
        } catch (\Throwable $e) {
            if ($pdo->inTransaction()) {
                $pdo->rollBack();
            }
            throw $e;
        }
    }
}
