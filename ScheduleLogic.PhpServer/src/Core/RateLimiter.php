<?php

declare(strict_types=1);

namespace ScheduleLogic\Core;

/**
 * Fix időablakos rate limiter fájl-tárolóval (az ASP.NET AddRateLimiter
 * FixedWindowLimiter-ének megfelelője). Nem kell hozzá Redis vagy APCu,
 * így a beépített PHP szerverrel is működik.
 *
 * A .NET backendben ez nem volt - brute force elleni javításként került be
 * a login / regisztráció / jelszó-visszaállítás végpontokra.
 */
final class RateLimiter
{
    public function __construct(private readonly string $directory, private readonly bool $enabled = true)
    {
    }

    /**
     * @return int|null  null, ha a kérés mehet; különben hány másodperc múlva próbálkozhat újra
     */
    public function hit(string $bucket, string $clientKey, int $limit, int $windowSeconds): ?int
    {
        if (!$this->enabled || $limit <= 0) {
            return null;
        }
        if (!is_dir($this->directory)) {
            @mkdir($this->directory, 0770, true);
        }

        $file = $this->directory . '/' . hash('sha256', $bucket . '|' . $clientKey) . '.json';
        $fh = fopen($file, 'c+');
        if ($fh === false) {
            return null; // tárolási hiba esetén ne zárjuk ki a felhasználót
        }

        try {
            flock($fh, LOCK_EX);
            $state = json_decode((string)stream_get_contents($fh), true);
            $now = time();

            if (!is_array($state) || ($state['start'] ?? 0) + $windowSeconds <= $now) {
                $state = ['start' => $now, 'count' => 0];
            }

            if ($state['count'] >= $limit) {
                return max(1, $state['start'] + $windowSeconds - $now);
            }

            $state['count']++;
            ftruncate($fh, 0);
            rewind($fh);
            fwrite($fh, json_encode($state));
            return null;
        } finally {
            flock($fh, LOCK_UN);
            fclose($fh);
        }
    }

    /** Régi bejegyzések takarítása (alkalmanként hívódik). */
    public function cleanup(int $olderThanSeconds = 86400): void
    {
        foreach (glob($this->directory . '/*.json') ?: [] as $file) {
            if (@filemtime($file) < time() - $olderThanSeconds) {
                @unlink($file);
            }
        }
    }
}
