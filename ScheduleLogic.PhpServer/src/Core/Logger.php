<?php

declare(strict_types=1);

namespace ScheduleLogic\Core;

/**
 * Egyszerű fájl-alapú napló (az ILogger<T> megfelelője).
 * Naponta új fájl: storage/logs/app-YYYY-MM-DD.log
 */
final class Logger
{
    private const LEVELS = ['debug' => 0, 'info' => 1, 'warning' => 2, 'error' => 3];

    public function __construct(
        private readonly string $directory,
        private readonly string $minLevel = 'info',
        private readonly string $category = 'App',
    ) {
    }

    public function forCategory(string $category): self
    {
        return new self($this->directory, $this->minLevel, $category);
    }

    public function info(string $message, array $context = []): void
    {
        $this->write('info', $message, $context);
    }

    public function warning(string $message, array $context = [], ?\Throwable $ex = null): void
    {
        $this->write('warning', $message, $context, $ex);
    }

    public function error(string $message, array $context = [], ?\Throwable $ex = null): void
    {
        $this->write('error', $message, $context, $ex);
    }

    private function write(string $level, string $message, array $context, ?\Throwable $ex = null): void
    {
        if (self::LEVELS[$level] < (self::LEVELS[$this->minLevel] ?? 1)) {
            return;
        }

        // {Placeholder} stílusú üzenetsablon, mint a .NET structured logging.
        $message = preg_replace_callback('/\{(\w+)\}/', static function ($m) use ($context) {
            if (!array_key_exists($m[1], $context)) {
                return $m[0];
            }
            $v = $context[$m[1]];
            if ($v === null) {
                return 'null';
            }
            if (is_bool($v)) {
                return $v ? 'true' : 'false';
            }
            return is_scalar($v) ? (string)$v : (string)json_encode($v);
        }, $message);

        $line = sprintf(
            "%s [%s] %s: %s%s\n",
            gmdate('Y-m-d\TH:i:s\Z'),
            strtoupper($level),
            $this->category,
            $message,
            $ex ? "\n    " . get_class($ex) . ': ' . $ex->getMessage() . "\n    " . str_replace("\n", "\n    ", $ex->getTraceAsString()) : ''
        );

        if (!is_dir($this->directory)) {
            @mkdir($this->directory, 0770, true);
        }
        $ok = @file_put_contents($this->directory . '/app-' . gmdate('Y-m-d') . '.log', $line, FILE_APPEND | LOCK_EX);
        if ($ok === false) {
            error_log(rtrim($line));
        }
        // Fejlesztői szervernél (php -S) a konzolra is kiírjuk.
        if (PHP_SAPI === 'cli-server') {
            file_put_contents('php://stderr', $line);
        }
    }
}
