<?php

declare(strict_types=1);

namespace ScheduleLogic\Core;

/**
 * A .NET IConfiguration megfelelője.
 *
 * Ugyanazokat az appsettings fájlokat olvassa, mint a C# backend, így a két
 * backend ugyanazzal az adatbázis-kapcsolattal és levelezési beállítással
 * fut. Betöltési sorrend (a későbbi felülírja a korábbit):
 *
 *   1. <megosztott mappa>/appsettings.json            (alapból ../ScheduleLogic.Server)
 *   2. <megosztott mappa>/appsettings.{Környezet}.json
 *   3. .NET User Secrets (csak Development-ben, a .csproj UserSecretsId-ja alapján)
 *   4. <PHP projekt>/appsettings.json                 (csak PHP-specifikus értékek)
 *   5. <PHP projekt>/appsettings.{Környezet}.json
 *   6. környezeti változók "__" elválasztóval, pl. Database__Password
 *
 * A kulcsok - akárcsak .NET-ben - kis/nagybetű-érzéketlenek, és ":"-tal
 * lehet szekciókba lépni: Config::get('Database:Host').
 */
final class Config
{
    /** @var array<string, mixed> */
    private array $data = [];
    private string $environment;
    private ?string $sharedDir = null;
    /** @var list<string> */
    private array $loadedFiles = [];

    public function __construct(string $projectRoot)
    {
        // .NET-ben az ASPNETCORE_ENVIRONMENT hiányában "Production" az
        // alapértelmezés - itt is, hogy élesben véletlenül se fusson dev módban.
        $this->environment = getenv('APP_ENV') ?: (getenv('ASPNETCORE_ENVIRONMENT') ?: 'Production');

        $sharedDir = getenv('SL_SHARED_CONFIG_DIR') ?: $projectRoot . '/../ScheduleLogic.Server';
        $dirs = [];
        if (is_dir($sharedDir)) {
            $this->sharedDir = realpath($sharedDir);
            $dirs[] = $this->sharedDir;
        }
        if (($projectDir = realpath($projectRoot)) !== false) {
            $dirs[] = $projectDir;
        }

        foreach (array_unique($dirs) as $dir) {
            $this->loadFile($dir . '/appsettings.json');
            $this->loadFile($dir . '/appsettings.' . $this->environment . '.json');
            // A .NET Development módban a User Secrets-et is betölti (pl. AdminAuth).
            if ($dir === $this->sharedDir && $this->isDevelopment()) {
                $this->loadUserSecrets($dir);
            }
        }
        $this->loadEnvironmentVariables();
    }

    /**
     * %APPDATA%\Microsoft\UserSecrets\<id>\secrets.json (Windows), illetve
     * ~/.microsoft/usersecrets/<id>/secrets.json (Linux/macOS).
     */
    private function loadUserSecrets(string $dotnetProjectDir): void
    {
        foreach (glob($dotnetProjectDir . '/*.csproj') ?: [] as $csproj) {
            if (!preg_match('#<UserSecretsId>\s*([^<\s]+)\s*</UserSecretsId>#', (string)file_get_contents($csproj), $m)) {
                continue;
            }
            $candidates = [];
            if ($appData = getenv('APPDATA')) {
                $candidates[] = $appData . '/Microsoft/UserSecrets/' . $m[1] . '/secrets.json';
            }
            if ($home = getenv('HOME') ?: getenv('USERPROFILE')) {
                $candidates[] = $home . '/.microsoft/usersecrets/' . $m[1] . '/secrets.json';
            }
            foreach ($candidates as $file) {
                if (is_file($file)) {
                    $this->loadFile($file);
                    return;
                }
            }
        }
    }

    private function loadEnvironmentVariables(): void
    {
        foreach (getenv() as $name => $value) {
            if (str_contains($name, '__')) {
                $this->data = self::merge($this->data, self::expandFlat([str_replace('__', ':', $name) => $value]));
            }
        }
    }

    /** {"AdminAuth:Username": "x"} -> {"adminauth": {"username": "x"}} (a secrets.json lapos is lehet) */
    private static function expandFlat(array $input): array
    {
        $out = [];
        foreach ($input as $key => $value) {
            $value = is_array($value) ? self::expandFlat($value) : $value;
            $parts = explode(':', strtolower((string)$key));
            $node = &$out;
            foreach ($parts as $i => $part) {
                if ($i === count($parts) - 1) {
                    $node[$part] = (is_array($value) && isset($node[$part]) && is_array($node[$part]))
                        ? self::merge($node[$part], $value)
                        : $value;
                } else {
                    if (!isset($node[$part]) || !is_array($node[$part])) {
                        $node[$part] = [];
                    }
                    $node = &$node[$part];
                }
            }
            unset($node);
        }
        return $out;
    }

    public function environment(): string
    {
        return $this->environment;
    }

    /** A .NET projekt mappája (ahonnan a közös appsettings jön), ha megvan. */
    public function sharedDir(): ?string
    {
        return $this->sharedDir;
    }

    public function isDevelopment(): bool
    {
        return strcasecmp($this->environment, 'Development') === 0;
    }

    /** @return list<string> */
    public function loadedFiles(): array
    {
        return $this->loadedFiles;
    }

    public function get(string $key, mixed $default = null): mixed
    {
        $node = $this->data;
        foreach (explode(':', strtolower($key)) as $part) {
            if (!is_array($node) || !array_key_exists($part, $node)) {
                return $default;
            }
            $node = $node[$part];
        }
        return $node;
    }

    public function string(string $key, string $default = ''): string
    {
        $value = $this->get($key);
        if ($value === null || is_array($value)) {
            return $default;
        }
        if (is_bool($value)) {
            return $value ? 'true' : 'false';
        }
        return (string)$value;
    }

    public function int(string $key, int $default = 0): int
    {
        $value = $this->get($key);
        return is_numeric($value) ? (int)$value : $default;
    }

    public function bool(string $key, bool $default = false): bool
    {
        $value = $this->get($key);
        if (is_bool($value)) {
            return $value;
        }
        if (is_string($value)) {
            return in_array(strtolower($value), ['1', 'true', 'yes'], true);
        }
        return $default;
    }

    /** @return list<string> */
    public function stringList(string $key, array $default = []): array
    {
        $value = $this->get($key);
        if (!is_array($value)) {
            return $default;
        }
        return array_values(array_map('strval', array_filter($value, 'is_scalar')));
    }

    private function loadFile(string $path): void
    {
        if (!is_file($path)) {
            return;
        }
        $raw = file_get_contents($path);
        // A Visual Studio gyakran BOM-mal menti a JSON-t.
        if (str_starts_with($raw, "\xEF\xBB\xBF")) {
            $raw = substr($raw, 3);
        }
        $parsed = json_decode($raw, true);
        if (!is_array($parsed)) {
            throw new \RuntimeException("Invalid JSON configuration file: {$path}");
        }
        $this->data = self::merge($this->data, self::expandFlat($parsed));
        $this->loadedFiles[] = $path;
    }


    private static function merge(array $base, array $override): array
    {
        foreach ($override as $k => $v) {
            if (is_array($v) && isset($base[$k]) && is_array($base[$k]) && !array_is_list($v)) {
                $base[$k] = self::merge($base[$k], $v);
            } else {
                $base[$k] = $v;
            }
        }
        return $base;
    }
}
