<?php

declare(strict_types=1);

namespace ScheduleLogic\Core;

/**
 * Egyszerű HTTP kliens (az IHttpClientFactory "Solver" nevű kliense helyett).
 * PHP stream-eket használ, így nem kell hozzá a curl kiterjesztés.
 */
final class HttpClient
{
    public function __construct(
        private readonly string $baseUrl,
        private readonly int $timeoutSeconds = 100,
    ) {
    }

    /**
     * @return array{status:int, body:string}
     * @throws \RuntimeException ha a szerver nem érhető el (mint a HttpRequestException)
     */
    public function request(string $method, string $relativeUrl, ?string $jsonBody = null, ?int $timeout = null): array
    {
        $url = rtrim($this->baseUrl, '/') . '/' . ltrim($relativeUrl, '/');
        $headers = ['Accept: application/json'];
        if ($jsonBody !== null) {
            $headers[] = 'Content-Type: application/json; charset=utf-8';
        }

        $context = stream_context_create([
            'http' => [
                'method' => $method,
                'header' => implode("\r\n", $headers),
                'content' => $jsonBody ?? '',
                'timeout' => $timeout ?? $this->timeoutSeconds,
                'ignore_errors' => true, // 4xx/5xx-nél is kapjuk meg a törzset
                'follow_location' => 0,
            ],
        ]);

        $body = @file_get_contents($url, false, $context);
        if ($body === false) {
            $err = error_get_last()['message'] ?? 'unknown error';
            throw new \RuntimeException("HTTP request to {$url} failed: {$err}");
        }

        $status = 0;
        // PHP 8.4+: http_get_last_response_headers(); régebben a
        // $http_response_header változót tölti ki a file_get_contents.
        $responseHeaders = function_exists('http_get_last_response_headers')
            ? (http_get_last_response_headers() ?? [])
            : ($http_response_header ?? []);
        foreach ($responseHeaders as $line) {
            if (preg_match('#^HTTP/\S+\s+(\d{3})#', $line, $m)) {
                $status = (int)$m[1]; // átirányításnál az utolsó számít
            }
        }

        return ['status' => $status, 'body' => $body];
    }

    public function getJson(string $relativeUrl, ?int $timeout = null): array
    {
        return $this->request('GET', $relativeUrl, null, $timeout);
    }

    public function postJson(string $relativeUrl, mixed $data): array
    {
        return $this->request('POST', $relativeUrl, json_encode($data, JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES));
    }
}
