<?php

declare(strict_types=1);

namespace ScheduleLogic\Core;

/**
 * A beérkező HTTP kérés. Reverse proxy (nginx / Caddy) mögött a megbízható
 * proxyktól érkező X-Forwarded-For / X-Forwarded-Proto fejléceket is
 * figyelembe veszi (ASP.NET-ben ez a ForwardedHeaders middleware dolga).
 */
final class Request
{
    /** @var array<string, string> kisbetűs fejlécnév => érték */
    private array $headers;
    /** @var array<string, string> kisbetűs query kulcs => érték */
    private array $queryLower;
    private ?array $jsonCache = null;
    private bool $jsonParsed = false;

    /** @var array<string, string> útvonal-paraméterek ({id} stb.) */
    public array $routeParams = [];

    public readonly string $method;
    public readonly string $path;
    public readonly string $clientIp;
    public readonly bool $isHttps;

    /**
     * @param list<string> $trustedProxies
     */
    public function __construct(
        array $server,
        array $query,
        private readonly array $cookies,
        private readonly array $form,
        private readonly string $rawBody,
        array $trustedProxies = [],
    ) {
        $this->method = strtoupper($server['REQUEST_METHOD'] ?? 'GET');

        $uriPath = parse_url($server['REQUEST_URI'] ?? '/', PHP_URL_PATH) ?: '/';
        $this->path = '/' . ltrim(rawurldecode($uriPath), '/');

        $headers = [];
        foreach ($server as $key => $value) {
            if (str_starts_with($key, 'HTTP_')) {
                $headers[strtolower(str_replace('_', '-', substr($key, 5)))] = (string)$value;
            }
        }
        if (isset($server['CONTENT_TYPE'])) {
            $headers['content-type'] = (string)$server['CONTENT_TYPE'];
        }
        $this->headers = $headers;

        $this->queryLower = [];
        foreach ($query as $k => $v) {
            if (is_string($v)) {
                $this->queryLower[strtolower((string)$k)] = $v;
            }
        }

        // ---- kliens IP és séma (proxy mögött is) ----
        $remote = (string)($server['REMOTE_ADDR'] ?? '');
        $ip = $remote;
        $https = (!empty($server['HTTPS']) && $server['HTTPS'] !== 'off');

        if ($remote !== '' && in_array($remote, $trustedProxies, true)) {
            $forwardedFor = $headers['x-forwarded-for'] ?? '';
            if ($forwardedFor !== '') {
                // Jobbról balra az első nem megbízható cím a valódi kliens.
                $hops = array_reverse(array_map('trim', explode(',', $forwardedFor)));
                foreach ($hops as $hop) {
                    if ($hop !== '' && !in_array($hop, $trustedProxies, true)) {
                        $ip = $hop;
                        break;
                    }
                }
            }
            $proto = strtolower(trim(explode(',', $headers['x-forwarded-proto'] ?? '')[0]));
            if ($proto !== '') {
                $https = $proto === 'https';
            }
        }

        $this->clientIp = $ip;
        $this->isHttps = $https;
    }

    public static function fromGlobals(array $trustedProxies): self
    {
        return new self($_SERVER, $_GET, $_COOKIE, $_POST, (string)file_get_contents('php://input'), $trustedProxies);
    }

    public function header(string $name): ?string
    {
        return $this->headers[strtolower($name)] ?? null;
    }

    /** Query paraméter, kis/nagybetű-érzéketlenül (mint az ASP.NET model binding). */
    public function query(string $name): ?string
    {
        return $this->queryLower[strtolower($name)] ?? null;
    }

    public function cookie(string $name): ?string
    {
        $v = $this->cookies[$name] ?? null;
        return is_string($v) ? $v : null;
    }

    public function form(string $name): ?string
    {
        $v = $this->form[$name] ?? null;
        return is_string($v) ? $v : null;
    }

    public function route(string $name): ?string
    {
        return $this->routeParams[$name] ?? null;
    }

    public function rawBody(): string
    {
        return $this->rawBody;
    }

    /**
     * A JSON törzs kisbetűs kulcsokkal (lásd Json::lowerKeys), vagy null,
     * ha nincs törzs / nem érvényes JSON objektum.
     */
    public function json(): ?array
    {
        if (!$this->jsonParsed) {
            $this->jsonParsed = true;
            $decoded = json_decode($this->rawBody, true);
            $this->jsonCache = is_array($decoded) && !array_is_list($decoded) ? Json::lowerKeys($decoded) : null;
            if ($this->jsonCache === null && $decoded === []) {
                $this->jsonCache = [];
            }
        }
        return $this->jsonCache;
    }

    /**
     * [FromBody] kötés: JSON objektumot vár, különben ugyanúgy 400-at ad,
     * mint az [ApiController].
     */
    public function requireJsonBody(): array
    {
        $contentType = strtolower($this->header('content-type') ?? '');
        if (!str_contains($contentType, 'json')) {
            throw new HttpException(Response::problem(415));
        }
        $body = $this->json();
        if ($body === null) {
            throw new HttpException(Response::validationProblem([
                '' => ['A non-empty request body is required.'],
            ]));
        }
        return $body;
    }
}
