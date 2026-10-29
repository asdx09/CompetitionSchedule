<?php

declare(strict_types=1);

namespace ScheduleLogic\Core;

/**
 * HTTP válasz. A statikus gyártófüggvények az ASP.NET Core
 * Ok()/BadRequest()/NotFound()/... eredményeinek felelnek meg, ugyanazzal
 * a formátummal (pl. a hibák RFC 7807 ProblemDetails JSON-ként mennek ki).
 */
final class Response
{
    /** @var array<string, string> */
    public array $headers = [];
    /** @var list<string> Set-Cookie fejlécek */
    public array $cookies = [];

    public function __construct(
        public int $status = 200,
        public string $body = '',
        ?string $contentType = null,
    ) {
        if ($contentType !== null) {
            $this->headers['Content-Type'] = $contentType;
        }
    }

    // ----------------------------------------------------------- gyártók

    public static function ok(mixed $data = null): self
    {
        return $data === null ? new self(200) : self::json($data);
    }

    public static function json(mixed $data, int $status = 200): self
    {
        return new self($status, Json::encode($data), 'application/json; charset=utf-8');
    }

    /** Szöveges üzenet, pl. Unauthorized("...") / BadRequest("...") .NET-ben. */
    public static function text(string $message, int $status): self
    {
        return new self($status, $message, 'text/plain; charset=utf-8');
    }

    public static function status(int $status): self
    {
        return new self($status);
    }

    /** Body nélküli NotFound()/BadRequest() - az [ApiController] ProblemDetailst ad ilyenkor. */
    public static function problem(int $status, ?string $title = null, array $extra = []): self
    {
        static $types = [
            400 => ['https://tools.ietf.org/html/rfc9110#section-15.5.1', 'Bad Request'],
            401 => ['https://tools.ietf.org/html/rfc9110#section-15.5.2', 'Unauthorized'],
            403 => ['https://tools.ietf.org/html/rfc9110#section-15.5.4', 'Forbidden'],
            404 => ['https://tools.ietf.org/html/rfc9110#section-15.5.5', 'Not Found'],
            405 => ['https://tools.ietf.org/html/rfc9110#section-15.5.6', 'Method Not Allowed'],
            415 => ['https://tools.ietf.org/html/rfc9110#section-15.5.16', 'Unsupported Media Type'],
            429 => ['https://tools.ietf.org/html/rfc6585#section-4', 'Too Many Requests'],
            500 => ['https://tools.ietf.org/html/rfc9110#section-15.6.1', 'An error occurred while processing your request.'],
        ];
        [$type, $defaultTitle] = $types[$status] ?? ['about:blank', 'Error'];

        $body = ['type' => $type, 'title' => $title ?? $defaultTitle, 'status' => $status] + $extra;
        return new self($status, Json::encode($body), 'application/problem+json; charset=utf-8');
    }

    /** Model-validációs hiba, ugyanolyan alakban, mint az [ApiController] automatikus 400-a. */
    public static function validationProblem(array $errors): self
    {
        return self::problem(400, 'One or more validation errors occurred.', ['errors' => $errors]);
    }

    public static function redirect(string $location): self
    {
        $r = new self(302);
        $r->headers['Location'] = $location;
        return $r;
    }

    public static function file(string $content, string $contentType, string $fileName): self
    {
        $r = new self(200, $content, $contentType);
        $r->headers['Content-Disposition'] = 'attachment; filename=' . $fileName
            . "; filename*=UTF-8''" . rawurlencode($fileName);
        return $r;
    }

    public static function html(string $html, int $status = 200): self
    {
        return new self($status, $html, 'text/html; charset=utf-8');
    }

    // ----------------------------------------------------------- módosítók

    public function withHeader(string $name, string $value): self
    {
        $this->headers[$name] = $value;
        return $this;
    }

    public function addCookie(string $setCookieValue): self
    {
        $this->cookies[] = $setCookieValue;
        return $this;
    }

    public function send(): void
    {
        http_response_code($this->status);
        foreach ($this->headers as $name => $value) {
            header("$name: $value");
        }
        foreach ($this->cookies as $cookie) {
            header('Set-Cookie: ' . $cookie, false);
        }
        if ($this->body !== '') {
            header('Content-Length: ' . strlen($this->body));
            echo $this->body;
        }
    }
}
