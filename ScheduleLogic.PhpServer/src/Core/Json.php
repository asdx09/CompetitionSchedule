<?php

declare(strict_types=1);

namespace ScheduleLogic\Core;

final class Json
{
    /**
     * A .NET oldali beállításoknak megfelelően: camelCase kulcsok (ezt a
     * hívó adja meg), nem escape-elt Unicode (UnsafeRelaxedJsonEscaping),
     * behúzott kimenet (WriteIndented = true).
     */
    public static function encode(mixed $data): string
    {
        return json_encode(
            $data,
            JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES | JSON_PRETTY_PRINT | JSON_PRESERVE_ZERO_FRACTION | JSON_THROW_ON_ERROR
        );
    }

    /**
     * A System.Text.Json (web alapbeállítás) kis/nagybetű-érzéketlenül
     * köti a property-ket: {"Username":..} és {"username":..} is működik.
     * Ezt úgy érjük el, hogy minden kulcsot kisbetűsítünk.
     */
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
