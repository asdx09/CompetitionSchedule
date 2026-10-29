<?php

declare(strict_types=1);

namespace ScheduleLogic\Services;

/**
 * A solver visszahívásának (POST api/Schedule/answer) hitelesítése.
 *
 * A .NET verzió csak azt nézte, hogy a kérés localhostról jön-e - reverse
 * proxy (nginx) mögött viszont MINDEN kérés localhostról jön, így bárki
 * beküldhetett volna hamis eredményt. Itt helyette eseményenként egy
 * HMAC-SHA256 kulcs kerül a visszahívási URL-be:
 *
 *   https://.../api/Schedule/answer?key=<HMAC(titok, "solver-answer:<eventId>")>
 *
 * A Python solveren nem kell semmit módosítani, hiszen az a kapott
 * returnURL-re küld vissza, változatlanul. A kulcs csak az adott eseményre
 * érvényes, és nem lehet belőle a titkot vagy más esemény kulcsát kiszámolni.
 */
final class SolverCallbackSigner
{
    private ?string $secret = null;

    /**
     * @param string $configuredSecret  Solver:CallbackSecret a configból (ha üres, generál egyet)
     * @param string $keyFile           ide menti a generált titkot, hogy újraindítás után is érvényes maradjon
     */
    public function __construct(
        private readonly string $configuredSecret,
        private readonly string $keyFile,
    ) {
    }

    public function keyFor(int $eventId): string
    {
        return hash_hmac('sha256', 'solver-answer:' . $eventId, $this->secret());
    }

    public function verify(int $eventId, ?string $key): bool
    {
        return $key !== null && $key !== '' && hash_equals($this->keyFor($eventId), $key);
    }

    private function secret(): string
    {
        if ($this->secret !== null) {
            return $this->secret;
        }
        if ($this->configuredSecret !== '') {
            return $this->secret = $this->configuredSecret;
        }

        if (is_file($this->keyFile)) {
            $stored = trim((string)file_get_contents($this->keyFile));
            if (strlen($stored) >= 32) {
                return $this->secret = $stored;
            }
        }

        $dir = dirname($this->keyFile);
        if (!is_dir($dir)) {
            mkdir($dir, 0770, true);
        }
        $generated = bin2hex(random_bytes(32));
        // 'x' mód: ha egy párhuzamos kérés már létrehozta, azt használjuk
        $fh = @fopen($this->keyFile, 'x');
        if ($fh !== false) {
            fwrite($fh, $generated);
            fclose($fh);
            @chmod($this->keyFile, 0600);
            return $this->secret = $generated;
        }
        return $this->secret = trim((string)file_get_contents($this->keyFile));
    }
}
