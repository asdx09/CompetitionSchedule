<?php

declare(strict_types=1);

namespace ScheduleLogic\Core;

use PDO;

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

    private function connect(?int $timeoutSeconds = null): PDO
    {
        $timeoutSeconds ??= $this->config->int('Database:ConnectTimeout', 5);
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

        $pdo->exec("SET TIME ZONE 'UTC'");
        return $pdo;
    }

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
