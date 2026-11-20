<?php

declare(strict_types=1);

namespace ScheduleLogic\Core;

final class Cors
{
    public function __construct(private readonly array $allowedOrigins)
    {
    }

    public function handlePreflight(Request $request): ?Response
    {
        if ($request->method !== 'OPTIONS' || $request->header('access-control-request-method') === null) {
            return null;
        }
        $response = Response::status(204);
        $origin = $request->header('origin');
        if ($origin !== null && $this->isAllowed($origin)) {
            $response->withHeader('Access-Control-Allow-Origin', $origin)
                ->withHeader('Access-Control-Allow-Credentials', 'true')
                ->withHeader('Access-Control-Allow-Methods', (string)$request->header('access-control-request-method'));
            $requestedHeaders = $request->header('access-control-request-headers');
            if ($requestedHeaders) {
                $response->withHeader('Access-Control-Allow-Headers', $requestedHeaders);
            }
        }
        $response->withHeader('Vary', 'Origin');
        return $response;
    }

    public function apply(Request $request, Response $response): void
    {
        $origin = $request->header('origin');
        if ($origin !== null && $this->isAllowed($origin)) {
            $response->withHeader('Access-Control-Allow-Origin', $origin)
                ->withHeader('Access-Control-Allow-Credentials', 'true');
        }
        $response->headers['Vary'] = 'Origin';
    }

    private function isAllowed(string $origin): bool
    {
        foreach ($this->allowedOrigins as $allowed) {
            if (strcasecmp(rtrim($allowed, '/'), $origin) === 0) {
                return true;
            }
        }
        return false;
    }
}
