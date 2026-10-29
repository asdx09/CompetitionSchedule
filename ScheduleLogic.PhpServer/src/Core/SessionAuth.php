<?php

declare(strict_types=1);

namespace ScheduleLogic\Core;

/**
 * Cookie-alapú bejelentkezés - az ASP.NET Cookie Authentication sémájának
 * PHP-s megfelelője.
 *
 * Különbség: a .NET a claimeket titkosítva magában a cookie-ban tárolja,
 * itt a cookie csak egy véletlen session-azonosító, az adatok a szerveren
 * (storage/sessions) vannak. Ennek előnye, hogy kijelentkezéskor vagy
 * fióktörléskor a session szerveroldalon is azonnal megszűnik.
 *
 * Egy séma = egy cookie név + beállítások (a .NET-ben is két séma van:
 * a felhasználói "sl_auth" és az admin "sl_admin_auth").
 */
final class SessionAuth
{
    private bool $started = false;
    private ?array $identity = null;
    /** @var list<string> a válaszhoz csatolandó Set-Cookie értékek */
    private array $pendingCookies = [];

    /**
     * @param int  $idleTimeout   ExpireTimeSpan másodpercben (sliding expiration)
     * @param bool $persistent    IsPersistent: a cookie túléli a böngésző bezárását
     * @param string $sameSite    Lax | Strict | None
     * @param bool|null $secure   null = a kérés sémája szerint (CookieSecurePolicy.SameAsRequest)
     */
    public function __construct(
        private readonly string $cookieName,
        private readonly string $savePath,
        private readonly int $idleTimeout,
        private readonly bool $persistent,
        private readonly string $sameSite,
        private readonly ?bool $secure,
        private readonly ?string $domain = null,
    ) {
    }

    /**
     * A bejelentkezett felhasználó claimjei, vagy null. A sessiont rögtön
     * le is zárja írásra, hogy a párhuzamos kérések (pl. a solver állapotának
     * pollozása) ne várjanak egymásra a session-zárolás miatt.
     */
    public function identity(Request $request): ?array
    {
        if ($this->identity !== null) {
            return $this->identity;
        }
        if ($request->cookie($this->cookieName) === null) {
            return null;
        }

        $this->open($request);
        $data = $_SESSION['identity'] ?? null;
        $last = (int)($_SESSION['last_activity'] ?? 0);

        if (!is_array($data) || time() - $last > $this->idleTimeout) {
            // Lejárt vagy üres session: eldobjuk, és a cookie-t is töröljük.
            $_SESSION = [];
            session_destroy();
            $this->started = false;
            $this->pendingCookies[] = ($this->buildCookie('', $request, expire: true));
            return null;
        }

        // Sliding expiration: minden kérés meghosszabbítja az élettartamot.
        $_SESSION['last_activity'] = time();
        $this->pendingCookies[] = ($this->buildCookie(session_id(), $request));
        session_write_close();
        $this->started = false;

        return $this->identity = $data;
    }

    /** SignInAsync megfelelője. Új session-azonosítót kap (session fixation ellen). */
    public function signIn(Request $request, array $claims): void
    {
        $this->open($request);
        session_regenerate_id(true);
        $_SESSION = [
            'identity' => $claims,
            'last_activity' => time(),
            'created' => time(),
        ];
        $this->pendingCookies[] = ($this->buildCookie(session_id(), $request));
        session_write_close();
        $this->started = false;
        $this->identity = $claims;
    }

    /** SignOutAsync megfelelője: a session a szerveren is megszűnik. */
    public function signOut(Request $request): void
    {
        if ($request->cookie($this->cookieName) !== null) {
            $this->open($request);
            $_SESSION = [];
            session_destroy();
            $this->started = false;
        }
        $this->identity = null;
        $this->pendingCookies[] = ($this->buildCookie('', $request, expire: true));
    }

    /** A válasz elküldése előtt az App ezzel veszi át a beállítandó cookie-kat. */
    public function takeCookies(): array
    {
        $cookies = $this->pendingCookies;
        $this->pendingCookies = [];
        return $cookies;
    }

    private function open(Request $request): void
    {
        if ($this->started) {
            return;
        }
        if (session_status() === PHP_SESSION_ACTIVE) {
            session_write_close();
        }
        if (!is_dir($this->savePath)) {
            @mkdir($this->savePath, 0770, true);
        }

        session_name($this->cookieName);
        session_save_path($this->savePath);
        $id = $request->cookie($this->cookieName);
        if ($id !== null && preg_match('/^[A-Za-z0-9,-]{22,256}$/', $id)) {
            session_id($id);
        }

        session_start([
            'use_strict_mode' => 1,      // ismeretlen azonosítót nem fogad el
            'use_cookies' => 0,          // a cookie-t mi küldjük, saját beállításokkal
            'use_only_cookies' => 1,
            'use_trans_sid' => 0,
            'cache_limiter' => '',
            'gc_maxlifetime' => max($this->idleTimeout, 1440),
            // Debian/Ubuntu alapból kikapcsolja a beépített takarítást (cron
            // végzi, de csak az alapértelmezett mappában) - itt bekapcsoljuk.
            'gc_probability' => 1,
            'gc_divisor' => 100,
        ]);
        $this->started = true;
    }

    private function buildCookie(string $value, Request $request, bool $expire = false): string
    {
        $secure = $this->secure ?? $request->isHttps;
        $parts = [$this->cookieName . '=' . ($expire ? '' : rawurlencode($value)), 'path=/'];

        if ($expire) {
            $parts[] = 'expires=Thu, 01 Jan 1970 00:00:00 GMT';
        } elseif ($this->persistent) {
            $parts[] = 'expires=' . gmdate('D, d M Y H:i:s', time() + $this->idleTimeout) . ' GMT';
        }
        if ($this->domain) {
            $parts[] = 'domain=' . $this->domain;
        }
        if ($secure) {
            $parts[] = 'secure';
        }
        $parts[] = 'samesite=' . strtolower($this->sameSite);
        $parts[] = 'httponly';

        return implode('; ', $parts);
    }
}
