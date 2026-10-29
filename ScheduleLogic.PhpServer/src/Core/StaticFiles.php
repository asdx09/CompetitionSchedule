<?php

declare(strict_types=1);

namespace ScheduleLogic\Core;

/**
 * UseDefaultFiles + UseStaticFiles + MapFallbackToFile("/index.html")
 * megfelelője: kiszolgálja a lebuildelt Angular klienst a wwwroot mappából.
 *
 * Élesben ezt jobb az nginx-re bízni (lásd deploy/nginx.conf.example),
 * de így a beépített PHP szerverrel is teljes az alkalmazás.
 */
final class StaticFiles
{
    private const MIME = [
        'html' => 'text/html; charset=utf-8',
        'js' => 'text/javascript; charset=utf-8',
        'mjs' => 'text/javascript; charset=utf-8',
        'css' => 'text/css; charset=utf-8',
        'json' => 'application/json; charset=utf-8',
        'map' => 'application/json; charset=utf-8',
        'svg' => 'image/svg+xml',
        'png' => 'image/png',
        'jpg' => 'image/jpeg',
        'jpeg' => 'image/jpeg',
        'gif' => 'image/gif',
        'webp' => 'image/webp',
        'ico' => 'image/x-icon',
        'woff' => 'font/woff',
        'woff2' => 'font/woff2',
        'ttf' => 'font/ttf',
        'txt' => 'text/plain; charset=utf-8',
        'xml' => 'application/xml',
        'webmanifest' => 'application/manifest+json',
    ];

    private ?string $root;

    public function __construct(string $webRoot)
    {
        $real = realpath($webRoot);
        $this->root = ($real !== false && is_dir($real)) ? $real : null;
    }

    public function isAvailable(): bool
    {
        return $this->root !== null;
    }

    /** Létező fájlt szolgál ki, vagy null-t ad. */
    public function tryServe(Request $request): ?Response
    {
        if ($this->root === null || !in_array($request->method, ['GET', 'HEAD'], true)) {
            return null;
        }
        $path = $request->path === '/' ? '/index.html' : $request->path;
        $file = realpath($this->root . $path);

        // Path traversal védelem: csak a webroot alatti fájl jöhet szóba.
        if ($file === false || !is_file($file) || !str_starts_with($file, $this->root . DIRECTORY_SEPARATOR)) {
            return null;
        }
        return $this->fileResponse($file);
    }

    /** SPA fallback: minden ismeretlen (nem /api, nem /admin) útvonalra az index.html. */
    public function fallback(Request $request): ?Response
    {
        if ($this->root === null || !in_array($request->method, ['GET', 'HEAD'], true)) {
            return null;
        }
        $index = $this->root . DIRECTORY_SEPARATOR . 'index.html';
        return is_file($index) ? $this->fileResponse($index) : null;
    }

    private function fileResponse(string $file): Response
    {
        $ext = strtolower(pathinfo($file, PATHINFO_EXTENSION));
        $response = new Response(200, (string)file_get_contents($file), self::MIME[$ext] ?? 'application/octet-stream');
        $response->withHeader('Last-Modified', gmdate('D, d M Y H:i:s', (int)filemtime($file)) . ' GMT');
        return $response;
    }
}
