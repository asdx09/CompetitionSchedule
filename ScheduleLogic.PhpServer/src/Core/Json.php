<?php

declare(strict_types=1);

namespace ScheduleLogic\Core;

final class Json
{
    public static function encode(mixed $data): string
    {
        return json_encode(
            $data,
            JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES | JSON_PRETTY_PRINT | JSON_PRESERVE_ZERO_FRACTION | JSON_THROW_ON_ERROR
        );
    }

    public static function lowerKeys(mixed $value): mixed
    {
        if (!is_array($value)) {
            return $value;
        }
        $out = [];
        foreach ($value as $k => $v) {
            $out[is_string($k) ? strtolower($k) : $k] = self::lowerKeys($v);
        }
        return $out;
    }
}
