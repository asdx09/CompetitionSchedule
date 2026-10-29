<?php

declare(strict_types=1);

namespace ScheduleLogic\Repositories;

use PDO;
use ScheduleLogic\Core\Database;
use ScheduleLogic\Core\DotNetTime;

/**
 * A "users" tábla kezelése (a C# UserRepository megfelelője).
 * A username és email oszlop citext, így az összehasonlítások
 * kis/nagybetű-érzéketlenek az adatbázisban.
 */
final class UserRepository
{
    private const COLUMNS = 'user_id, username, email, password, validated, registrationdate, passwordresettoken, passwordresetexpires';

    public function __construct(private readonly Database $db)
    {
    }

    public function getByUsernameOrEmail(string $usernameOrEmail): ?array
    {
        $stmt = $this->db->pdo()->prepare(
            'SELECT ' . self::COLUMNS . ' FROM users WHERE username = :u OR email = :e ORDER BY user_id LIMIT 1'
        );
        $stmt->execute(['u' => $usernameOrEmail, 'e' => $usernameOrEmail]);
        return $stmt->fetch() ?: null;
    }

    public function usernameExists(string $username): bool
    {
        $stmt = $this->db->pdo()->prepare('SELECT EXISTS(SELECT 1 FROM users WHERE username = :v)');
        $stmt->execute(['v' => $username]);
        return (bool)$stmt->fetchColumn();
    }

    public function emailExists(string $email): bool
    {
        $stmt = $this->db->pdo()->prepare('SELECT EXISTS(SELECT 1 FROM users WHERE email = :v)');
        $stmt->execute(['v' => $email]);
        return (bool)$stmt->fetchColumn();
    }

    /** @return int az új user_id */
    public function add(string $username, string $email, string $passwordHash, string $validationToken): int
    {
        $stmt = $this->db->pdo()->prepare(
            'INSERT INTO users (username, email, password, validated) VALUES (:u, :e, :p, :v) RETURNING user_id'
        );
        $stmt->execute(['u' => $username, 'e' => $email, 'p' => $passwordHash, 'v' => $validationToken]);
        return (int)$stmt->fetchColumn();
    }

    public function confirmEmail(string $token): bool
    {
        // Üres tokennel ne lehessen "megerősíteni" (az a már megerősített állapot).
        if ($token === '') {
            return false;
        }
        $stmt = $this->db->pdo()->prepare("UPDATE users SET validated = '' WHERE validated = :t");
        $stmt->execute(['t' => $token]);
        return $stmt->rowCount() > 0;
    }

    public function delete(int $userId): void
    {
        // Az event és alattuk minden tábla ON DELETE CASCADE-del törlődik.
        $stmt = $this->db->pdo()->prepare('DELETE FROM users WHERE user_id = :id');
        $stmt->execute(['id' => $userId]);
    }

    public function count(): int
    {
        return (int)$this->db->pdo()->query('SELECT COUNT(*) FROM users')->fetchColumn();
    }

    /**
     * Admin keresés felhasználónévre, e-mailre vagy azonosítóra.
     * Legfeljebb 100 találat, hogy ne lehessen egy kérésben a teljes táblát lekérni.
     */
    public function search(?string $query): array
    {
        $sql = 'SELECT user_id, username, email, registrationdate, validated FROM users';
        $params = [];

        if ($query !== null && trim($query) !== '') {
            // LIKE helyett strpos: a felhasználó által beírt % és _ ne legyen joker
            $sql .= ' WHERE strpos(lower(username::text), lower(:q1)) > 0 OR strpos(lower(email::text), lower(:q2)) > 0';
            $params['q1'] = $query;
            $params['q2'] = $query;
            if (preg_match('/^-?\d{1,18}$/', $query)) {
                $sql .= ' OR user_id = :id';
                $params['id'] = (int)$query;
            }
        }
        $sql .= ' ORDER BY registrationdate DESC, user_id DESC LIMIT 100';

        $stmt = $this->db->pdo()->prepare($sql);
        $stmt->execute($params);

        return array_map(static fn(array $u) => [
            'userId' => (int)$u['user_id'],
            'username' => $u['username'],
            'email' => $u['email'],
            'registrationDate' => $u['registrationdate'],   // DateOnly -> "2026-10-26"
            'emailConfirmed' => $u['validated'] === '',
        ], $stmt->fetchAll());
    }

    public function confirmById(int $userId): bool
    {
        $stmt = $this->db->pdo()->prepare("UPDATE users SET validated = '' WHERE user_id = :id");
        $stmt->execute(['id' => $userId]);
        return $stmt->rowCount() > 0;
    }

    public function setPasswordResetToken(int $userId, string $token, \DateTimeImmutable $expiresUtc): void
    {
        $stmt = $this->db->pdo()->prepare(
            'UPDATE users SET passwordresettoken = :t, passwordresetexpires = :e WHERE user_id = :id'
        );
        $stmt->execute(['t' => $token, 'e' => DotNetTime::toDb($expiresUtc), 'id' => $userId]);
    }

    public function getByValidPasswordResetToken(string $token): ?array
    {
        if ($token === '') {
            return null;
        }
        $stmt = $this->db->pdo()->prepare(
            'SELECT ' . self::COLUMNS . ' FROM users
              WHERE passwordresettoken = :t
                AND passwordresetexpires IS NOT NULL
                AND passwordresetexpires > now()
              LIMIT 1'
        );
        $stmt->execute(['t' => $token]);
        return $stmt->fetch() ?: null;
    }

    /** Új jelszó beállítása; a visszaállító token egyszer használatos, ezért törlődik. */
    public function setPassword(int $userId, string $passwordHash): void
    {
        $stmt = $this->db->pdo()->prepare(
            'UPDATE users SET password = :p, passwordresettoken = NULL, passwordresetexpires = NULL WHERE user_id = :id'
        );
        $stmt->execute(['p' => $passwordHash, 'id' => $userId]);
    }

    /** Csak a hash cseréje (pl. régi formátumú hash frissítése belépéskor). */
    public function updatePasswordHash(int $userId, string $passwordHash): void
    {
        $stmt = $this->db->pdo()->prepare('UPDATE users SET password = :p WHERE user_id = :id');
        $stmt->execute(['p' => $passwordHash, 'id' => $userId]);
    }

    public static function isUniqueViolation(\Throwable $e): bool
    {
        return $e instanceof \PDOException && ($e->errorInfo[0] ?? $e->getCode()) === '23505';
    }
}
