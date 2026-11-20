<?php

declare(strict_types=1);

namespace ScheduleLogic\Core;

use DateTimeImmutable;
use DateTimeZone;

final class DotNetTime
{
    private static ?DateTimeZone $utc = null;
    private static ?DateTimeZone $local = null;

    public static function setLocalTimeZone(string $tz): void
    {
        self::$local = new DateTimeZone($tz);
    }

    public static function utc(): DateTimeZone
    {
        return self::$utc ??= new DateTimeZone('UTC');
    }

    public static function local(): DateTimeZone
    {
        return self::$local ??= new DateTimeZone(date_default_timezone_get());
    }

    public static function nowUtc(): DateTimeImmutable
    {
        return new DateTimeImmutable('now', self::utc());
    }

    public static function fromDb(string $value): DateTimeImmutable
    {
        return (new DateTimeImmutable($value))->setTimezone(self::utc());
    }

    public static function toDb(DateTimeImmutable $value): string
    {
        return $value->setTimezone(self::utc())->format('Y-m-d H:i:s.uP');
    }

    public static function toJson(DateTimeImmutable $value): string
    {
        $v = $value->setTimezone(self::utc());
        $fraction = rtrim($v->format('u'), '0');
        return $v->format('Y-m-d\TH:i:s') . ($fraction !== '' ? '.' . $fraction : '') . 'Z';
    }

    public static function parseClient(mixed $value): ?DateTimeImmutable
    {
        if (!is_string($value) || trim($value) === '') {
            return null;
        }
        $value = trim($value);
        if (!preg_match('/^\d{4}-\d{2}-\d{2}([T ]\d{2}:\d{2}(:\d{2}(\.\d{1,7})?)?)?(Z|[+-]\d{2}:?\d{2})?$/i', $value, $m)) {
            return null;
        }
        $normalized = preg_replace('/(\.\d{6})\d+/', '$1', $value);
        $hasZone = isset($m[4]) && $m[4] !== '';
        try {
            $dt = new DateTimeImmutable($normalized, $hasZone ? null : self::local());
        } catch (\Exception) {
            return null;
        }
        $errors = DateTimeImmutable::getLastErrors();
        if ($errors !== false && ($errors['warning_count'] > 0 || $errors['error_count'] > 0)) {
            return null;
        }
        return $dt->setTimezone(self::utc());
    }

    public static function parseTimeOnly(mixed $value): ?string
    {
        if (!is_string($value) || !preg_match('/^(\d{1,2}):(\d{2})(?::(\d{2})(?:\.(\d{1,7}))?)?$/', trim($value), $m)) {
            return null;
        }
        [$h, $i, $s] = [(int)$m[1], (int)$m[2], (int)($m[3] ?? 0)];
        if ($h > 23 || $i > 59 || $s > 59) {
            return null;
        }
        $frac = isset($m[4]) && $m[4] !== '' ? '.' . substr(str_pad($m[4], 6, '0'), 0, 6) : '';
        return sprintf('%02d:%02d:%02d', $h, $i, $s) . $frac;
    }

    public static function timeOnlyToJson(string $dbTime): string
    {
        if (!preg_match('/^(\d{2}:\d{2}:\d{2})(?:\.(\d+))?/', $dbTime, $m)) {
            return $dbTime;
        }
        $frac = rtrim($m[2] ?? '', '0');
        return $m[1] . ($frac !== '' ? '.' . $frac : '');
    }

    public static function timeOnlyToMinutes(string $dbTime): int
    {
        [$h, $i] = array_map('intval', explode(':', $dbTime));
        return $h * 60 + $i;
    }

    public static function localWallClockMinutes(DateTimeImmutable $from, DateTimeImmutable $to): float
    {
        $wall = static fn(DateTimeImmutable $d) => new DateTimeImmutable(
            $d->setTimezone(self::local())->format('Y-m-d H:i:s.u'),
            self::utc()
        );
        $a = $wall($from);
        $b = $wall($to);
        return ((float)$b->format('U.u') - (float)$a->format('U.u')) / 60.0;
    }
}
