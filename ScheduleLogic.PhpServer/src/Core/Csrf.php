<?php

declare(strict_types=1);

namespace ScheduleLogic\Core;

final class Csrf
{
    private const COOKIE = 'sl_admin_csrf';
    private const FIELD = '__RequestVerificationToken';

    private ?string $token = null;

    public function __construct(private readonly Request $request)
    {
    }

    public function field(): string
    {
        return '<input name="' . self::FIELD . '" type="hidden" value="' . htmlspecialchars($this->token(), ENT_QUOTES) . '" />';
    }

    public function token(): string
    {
        if ($this->token === null) {
            $existing = $this->request->cookie(self::COOKIE);
            $this->token = ($existing !== null && preg_match('/^[a-f0-9]{64}$/', $existing))
                ? $existing
                : bin2hex(random_bytes(32));
        }
        return $this->token;
    }

    public function attachCookie(Response $response): Response
    {
        if ($this->token !== null && $this->token !== $this->request->cookie(self::COOKIE)) {
            $response->addCookie(self::COOKIE . '=' . $this->token . '; path=/admin; secure; samesite=strict; httponly');
        }
        return $response;
    }

    public function validate(): void
    {
        $cookie = $this->request->cookie(self::COOKIE);
        $field = $this->request->form(self::FIELD);
        if ($cookie === null || $field === null || !hash_equals($cookie, $field)) {
            throw new HttpException(Response::status(400));
        }
    }
}
