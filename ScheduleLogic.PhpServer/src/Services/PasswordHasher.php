<?php

declare(strict_types=1);

namespace ScheduleLogic\Services;

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
            . pack('N', 2)
            . pack('N', $this->iterations)
            . pack('N', $this->saltSize)
            . $salt
            . $subkey
        );
    }

    public function verify(string $hashedPassword, string $providedPassword): int
    {
        $decoded = base64_decode($hashedPassword, true);
        if ($decoded === false || $decoded === '') {
            return self::FAILED;
        }

        return match (ord($decoded[0])) {
            0x00 => $this->verifyV2($decoded, $providedPassword)
                ? self::SUCCESS_REHASH_NEEDED
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
        return ($iterations < $this->iterations || $prf !== 2)
            ? self::SUCCESS_REHASH_NEEDED
            : self::SUCCESS;
    }
}
