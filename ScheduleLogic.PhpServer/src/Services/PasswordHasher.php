<?php

declare(strict_types=1);

namespace ScheduleLogic\Services;

/**
 * Az ASP.NET Core Identity PasswordHasher<T> pontos PHP megfelelője, hogy a
 * .NET backenddel regisztrált felhasználók itt is be tudjanak lépni (és
 * fordítva). A hash formátuma (base64):
 *
 *   V3 (.NET Core Identity, ezt használja a projekt):
 *     [0x01][PRF: uint32 BE][iterációk: uint32 BE][só hossza: uint32 BE][só][alkulcs]
 *     PRF: 0 = HMAC-SHA1, 1 = HMAC-SHA256, 2 = HMAC-SHA512
 *     .NET 7+ alapértelmezés: HMAC-SHA512, 100 000 iteráció, 128 bites só, 256 bites alkulcs
 *
 *   V2 (régi ASP.NET Identity):
 *     [0x00][só: 16 bájt][alkulcs: 32 bájt]  PBKDF2-HMAC-SHA1, 1000 iteráció
 */
final class PasswordHasher
{
    public const FAILED = 0;
    public const SUCCESS = 1;
    public const SUCCESS_REHASH_NEEDED = 2;

    private const PRF_NAMES = [0 => 'sha1', 1 => 'sha256', 2 => 'sha512'];

    public function __construct(
        private readonly int $iterations = 100_000,
        private readonly int $saltSize = 16,
        private readonly int $subkeySize = 32,
    ) {
    }

    public function hash(string $password): string
    {
        $salt = random_bytes($this->saltSize);
        $subkey = hash_pbkdf2('sha512', $password, $salt, $this->iterations, $this->subkeySize, true);

        return base64_encode(
            "\x01"
            . pack('N', 2)                // PRF = HMAC-SHA512
            . pack('N', $this->iterations)
            . pack('N', $this->saltSize)
            . $salt
            . $subkey
        );
    }

    /** @return int  FAILED | SUCCESS | SUCCESS_REHASH_NEEDED (mint a PasswordVerificationResult) */
    public function verify(string $hashedPassword, string $providedPassword): int
    {
        $decoded = base64_decode($hashedPassword, true);
        if ($decoded === false || $decoded === '') {
            return self::FAILED;
        }

        return match (ord($decoded[0])) {
            0x00 => $this->verifyV2($decoded, $providedPassword)
                ? self::SUCCESS_REHASH_NEEDED   // régi formátum: érdemes újrahash-elni
                : self::FAILED,
            0x01 => $this->verifyV3($decoded, $providedPassword),
            default => self::FAILED,
        };
    }

    private function verifyV2(string $decoded, string $password): bool
    {
        if (strlen($decoded) !== 1 + 16 + 32) {
            return false;
        }
        $salt = substr($decoded, 1, 16);
        $expected = substr($decoded, 17, 32);
        $actual = hash_pbkdf2('sha1', $password, $salt, 1000, 32, true);
        return hash_equals($expected, $actual);
    }

    private function verifyV3(string $decoded, string $password): int
    {
        if (strlen($decoded) < 13) {
            return self::FAILED;
        }
        $prf = unpack('N', substr($decoded, 1, 4))[1];
        $iterations = unpack('N', substr($decoded, 5, 4))[1];
        $saltLength = unpack('N', substr($decoded, 9, 4))[1];

        if (!isset(self::PRF_NAMES[$prf]) || $iterations < 1 || $saltLength < 16) {
            return self::FAILED;
        }
        $subkeyLength = strlen($decoded) - 13 - $saltLength;
        if ($subkeyLength < 16) {
            return self::FAILED;
        }

        $salt = substr($decoded, 13, $saltLength);
        $expected = substr($decoded, 13 + $saltLength);
        $actual = hash_pbkdf2(self::PRF_NAMES[$prf], $password, $salt, $iterations, $subkeyLength, true);

        if (!hash_equals($expected, $actual)) {
            return self::FAILED;
        }
        // Gyengébb paraméterekkel készült hash: sikeres, de érdemes frissíteni.
        return ($iterations < $this->iterations || $prf !== 2)
            ? self::SUCCESS_REHASH_NEEDED
            : self::SUCCESS;
    }
}
