<?php

declare(strict_types=1);

namespace ScheduleLogic\Core;

final class SessionAuth
{
    private bool $started = false;
    private ?array $identity = null;
    private array $pendingCookies = [];

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
            $_SESSION = [];
            session_destroy();
            $this->started = false;
            $this->pendingCookies[] = ($this->buildCookie('', $request, expire: true));
            return null;
        }

        $_SESSION['last_activity'] = time();
        $this->pendingCookies[] = ($this->buildCookie(session_id(), $request));
        session_write_close();
        $this->started = false;

        return $this->identity = $data;
    }

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
            'use_strict_mode' => 1,
            'use_cookies' => 0,
            'use_only_cookies' => 1,
            'use_trans_sid' => 0,
            'cache_limiter' => '',
            'gc_maxlifetime' => max($this->idleTimeout, 1440),
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
