<?php

declare(strict_types=1);

namespace ScheduleLogic\Core;

/**
 * Minimális router az ASP.NET attribútum-routing megfelelőjeként.
 *
 *  - az útvonalak kis/nagybetű-érzéketlenek (/api/Authentication == /api/authentication)
 *  - paraméterek: {id} vagy típusmegkötéssel {id:long}
 *  - ha az útvonal stimmel, de a metódus nem: 405
 */
final class Router
{
    /** @var list<array{method:string, regex:string, params:list<string>, handler:callable}> */
    private array $routes = [];

    public function get(string $pattern, callable $handler): void
    {
        $this->add('GET', $pattern, $handler);
    }

    public function post(string $pattern, callable $handler): void
    {
        $this->add('POST', $pattern, $handler);
    }

    public function delete(string $pattern, callable $handler): void
    {
        $this->add('DELETE', $pattern, $handler);
    }

    public function add(string $method, string $pattern, callable $handler): void
    {
        $params = [];
        $regex = '';
        $parts = preg_split('/(\{\w+(?::\w+)?\})/', rtrim($pattern, '/'), -1, PREG_SPLIT_DELIM_CAPTURE);
        foreach ($parts as $part) {
            if (preg_match('/^\{(\w+)(?::(\w+))?\}$/', $part, $m)) {
                $params[] = $m[1];
                $regex .= match ($m[2] ?? '') {
                    'long', 'int' => '(-?\d+)',
                    default => '([^/]+)',
                };
            } else {
                $regex .= preg_quote($part, '#');
            }
        }

        $this->routes[] = [
            'method' => $method,
            'regex' => '#^' . $regex . '/?$#i',
            'params' => $params,
            'handler' => $handler,
        ];
    }

    /**
     * @return array{0: callable, 1: array<string,string>}|null  null, ha nincs ilyen útvonal
     * @throws HttpException 405 esetén
     */
    public function match(string $method, string $path): ?array
    {
        $pathMatched = false;
        foreach ($this->routes as $route) {
            if (!preg_match($route['regex'], $path, $m)) {
                continue;
            }
            $pathMatched = true;
            if ($route['method'] !== $method && !($method === 'HEAD' && $route['method'] === 'GET')) {
                continue;
            }
            $values = array_slice($m, 1);
            $params = $route['params'] ? array_combine($route['params'], $values) : [];

            // long megkötés: a 64 biten túlcsorduló számot ne fogadjuk el
            foreach ($params as $v) {
                if (preg_match('/^-?\d+$/', $v) && !self::fitsInLong($v)) {
                    continue 2;
                }
            }
            return [$route['handler'], $params];
        }

        if ($pathMatched) {
            throw new HttpException(Response::status(405));
        }
        return null;
    }

    private static function fitsInLong(string $v): bool
    {
        $digits = ltrim(ltrim($v, '-'), '0');
        if (strlen($digits) !== 19) {
            return strlen($digits) < 19;
        }
        return strcmp($digits, str_starts_with($v, '-') ? '9223372036854775808' : '9223372036854775807') <= 0;
    }
}
