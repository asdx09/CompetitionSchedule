<?php

declare(strict_types=1);

namespace ScheduleLogic\Models;

use ScheduleLogic\Core\DotNetTime;
use ScheduleLogic\Core\HttpException;
use ScheduleLogic\Core\Response;

/**
 * A model binding + automatikus validáció ([ApiController]) megfelelője.
 *
 * A C# DTO-kban a nem-nullable string property-k (pl. "public string
 * TypeName { get; set; } = null!;") implicit [Required]-ek: hiányozhatnak
 * vagy lehetnek üresek -> 400 ProblemDetails "errors" listával. Ez az
 * osztály ugyanilyen hibaüzeneteket gyűjt, ugyanilyen kulcsokkal
 * (pl. "EventTypes[0].TypeName").
 */
final class InputReader
{
    /** @var array<string, list<string>> */
    private array $errors = [];

    public function hasErrors(): bool
    {
        return $this->errors !== [];
    }

    public function throwIfInvalid(): void
    {
        if ($this->errors) {
            throw new HttpException(Response::validationProblem($this->errors));
        }
    }

    public function addError(string $path, string $message): void
    {
        $this->errors[$path][] = $message;
    }

    /** Kötelező, nem üres string (implicit [Required]). */
    public function requiredString(array $data, string $field, string $path): string
    {
        $value = $data[strtolower($field)] ?? null;
        if (is_int($value) || is_float($value)) {
            $value = (string)$value; // engedékenyebb a .NET-nél: számot is elfogad
        }
        if (!is_string($value) || trim($value) === '') {
            $this->addError($path, "The {$field} field is required.");
            return '';
        }
        return $value;
    }

    /** Opcionális (nullable) string. */
    public function optionalString(array $data, string $field, string $path, ?string $default = null): ?string
    {
        if (!array_key_exists(strtolower($field), $data) || $data[strtolower($field)] === null) {
            return $default;
        }
        $value = $data[strtolower($field)];
        if (is_int($value) || is_float($value)) {
            return (string)$value;
        }
        if (!is_string($value)) {
            $this->addError($path, "The JSON value could not be converted to System.String. Path: \$.{$path}");
            return $default;
        }
        return $value;
    }

    /** int mező: hiányzik -> 0 (C# alapérték); a web JSON beállítás stringből is olvas számot. */
    public function int(array $data, string $field, string $path): int
    {
        $key = strtolower($field);
        if (!array_key_exists($key, $data)) {
            return 0;
        }
        $value = $data[$key];
        if (is_int($value)) {
            return $value;
        }
        if (is_string($value) && preg_match('/^-?\d{1,10}$/', trim($value))) {
            return (int)$value;
        }
        if (is_float($value) && floor($value) === $value) {
            return (int)$value;
        }
        $this->addError($path, "The JSON value could not be converted to System.Int32. Path: \$.{$path}");
        return 0;
    }

    public function bool(array $data, string $field, string $path): bool
    {
        $key = strtolower($field);
        if (!array_key_exists($key, $data)) {
            return false;
        }
        if (!is_bool($data[$key])) {
            $this->addError($path, "The JSON value could not be converted to System.Boolean. Path: \$.{$path}");
            return false;
        }
        return $data[$key];
    }

    /** DateTime mező, ToUniversalTime()-mal; hiányzik -> null (a hívó dönt). */
    public function dateTime(array $data, string $field, string $path): ?\DateTimeImmutable
    {
        $key = strtolower($field);
        if (!array_key_exists($key, $data)) {
            return null;
        }
        $parsed = DotNetTime::parseClient($data[$key]);
        if ($parsed === null) {
            $this->addError($path, "The JSON value could not be converted to System.DateTime. Path: \$.{$path}");
        }
        return $parsed;
    }

    /** TimeOnly mező ("HH:mm[:ss]"). */
    public function timeOnly(array $data, string $field, string $path): string
    {
        $key = strtolower($field);
        if (!array_key_exists($key, $data)) {
            return '00:00:00';
        }
        $parsed = DotNetTime::parseTimeOnly($data[$key]);
        if ($parsed === null) {
            $this->addError($path, "The JSON value could not be converted to System.TimeOnly. Path: \$.{$path}");
            return '00:00:00';
        }
        return $parsed;
    }

    /** Lista mező; hiányzik -> üres lista (a C# DTO inicializálja). */
    public function list(array $data, string $field, string $path): array
    {
        $key = strtolower($field);
        if (!array_key_exists($key, $data)) {
            return [];
        }
        $value = $data[$key];
        if ($value === null) {
            $this->addError($path, "The {$field} field is required.");
            return [];
        }
        if (!is_array($value) || !array_is_list($value)) {
            $this->addError($path, "The JSON value could not be converted to a list. Path: \$.{$path}");
            return [];
        }
        return $value;
    }

    public function object(array $data, string $field, string $path): array
    {
        $value = $data[strtolower($field)] ?? null;
        if ($value === null) {
            return [];
        }
        if (!is_array($value) || (array_is_list($value) && $value !== [])) {
            $this->addError($path, "The JSON value could not be converted to an object. Path: \$.{$path}");
            return [];
        }
        return $value;
    }
}
