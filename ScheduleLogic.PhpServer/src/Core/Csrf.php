<?php

declare(strict_types=1);

namespace ScheduleLogic\Core;

/**
 * Antiforgery token az admin űrlapokhoz ([ValidateAntiForgeryToken] +
 * @Html.AntiForgeryToken() megfelelője), "double submit cookie" módszerrel:
 * egy véletlen érték megy HttpOnly cookie-ban és a rejtett űrlapmezőben is;
 * POST-nál a kettőnek egyeznie kell. Más oldal a cookie-t nem tudja
 * kiolvasni, így érvényes űrlapot sem tud beküldeni.
 */
final class Csrf
{
    private const COOKIE = 'sl_admin_csrf';
    private const FIELD = '__RequestVerificationToken';

    private ?string $token = null;

    public function __construct(private readonly Request $request)
    {
    }

    /** A rejtett mező HTML-je az űrlapba. */
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

    /** A válaszhoz csatolja a cookie-t (ha újonnan generáltuk). */
    public function attachCookie(Response $response): Response
    {
        if ($this->token !== null && $this->token !== $this->request->cookie(self::COOKIE)) {
            $response->addCookie(self::COOKIE . '=' . $this->token . '; path=/admin; secure; samesite=strict; httponly');
        }
        return $response;
    }

    /** POST ellenőrzés; hibás tokennél 400, mint az ASP.NET antiforgery. */
    public function validate(): void
    {
        $cookie = $this->request->cookie(self::COOKIE);
        $field = $this->request->form(self::FIELD);
        if ($cookie === null || $field === null || !hash_equals($cookie, $field)) {
            throw new HttpException(Response::status(400));
        }
    }
}
