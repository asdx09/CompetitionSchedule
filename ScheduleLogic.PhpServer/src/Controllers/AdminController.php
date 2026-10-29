<?php

declare(strict_types=1);

namespace ScheduleLogic\Controllers;

use ScheduleLogic\App;
use ScheduleLogic\Core\Csrf;
use ScheduleLogic\Core\HttpException;
use ScheduleLogic\Core\Request;
use ScheduleLogic\Core\Response;
use ScheduleLogic\Services\PasswordHasher;

/**
 * /admin - a C# AdminController megfelelője (szerveroldalon renderelt
 * oldalak + egy kis JSON API az admin felület JavaScriptjének).
 *
 * Hitelesítés: külön "sl_admin_auth" session; a felhasználónév és a jelszó
 * hash-e a configban van (AdminAuth:Username, AdminAuth:PasswordHash -
 * ugyanaz a formátum, mint a .NET PasswordHasher-é, így a meglévő érték
 * változatlanul használható).
 */
final class AdminController
{
    public function __construct(private readonly App $app)
    {
    }

    // GET /admin   [Authorize(AdminCookie)]
    public function index(Request $request): Response
    {
        $this->authorizePage($request);
        $csrf = new Csrf($request);
        return $csrf->attachCookie($this->view('index', ['csrf' => $csrf]));
    }

    // GET /admin/login   [AllowAnonymous]
    public function loginPage(Request $request): Response
    {
        $csrf = new Csrf($request);
        return $csrf->attachCookie($this->view('login', ['csrf' => $csrf, 'errors' => []]));
    }

    // POST /admin/login   [AllowAnonymous, ValidateAntiForgeryToken]
    public function login(Request $request): Response
    {
        $csrf = new Csrf($request);
        $csrf->validate();
        $this->app->rateLimit($request, 'AdminLogin', 5, 900);

        $username = $request->form('username') ?? '';
        $password = $request->form('password') ?? '';

        if (!$this->isValidCredential($username, $password)) {
            return $csrf->attachCookie($this->view('login', [
                'csrf' => $csrf,
                'errors' => ['Hibás felhasználónév vagy jelszó.'],
            ]));
        }

        $this->app->adminAuth->signIn($request, ['name' => $username]);
        return Response::redirect('/admin');
    }

    // POST /admin/logout   [ValidateAntiForgeryToken]
    public function logout(Request $request): Response
    {
        $this->authorizePage($request);
        (new Csrf($request))->validate();
        $this->app->adminAuth->signOut($request);
        return Response::redirect('/admin/login');
    }

    // GET /admin/api/stats
    public function getStats(Request $request): Response
    {
        $this->authorizeApi($request);
        return Response::json($this->app->adminService()->getStats());
    }

    // GET /admin/api/users?q=
    public function searchUsers(Request $request): Response
    {
        $this->authorizeApi($request);
        return Response::json($this->app->adminService()->searchUsers($request->query('q')));
    }

    // POST /admin/api/users/{id:long}/confirm
    public function confirmUser(Request $request): Response
    {
        $this->authorizeApi($request);
        $confirmed = $this->app->adminService()->confirmUser((int)$request->route('id'));
        return $confirmed ? Response::ok() : Response::problem(404);
    }

    // DELETE /admin/api/users/{id:long}
    public function deleteUser(Request $request): Response
    {
        $this->authorizeApi($request);
        $this->app->adminService()->deleteUser((int)$request->route('id'));
        return Response::ok();
    }

    // ------------------------------------------------------------------

    /** Oldalaknál: bejelentkezés hiányában átirányítás a login oldalra. */
    private function authorizePage(Request $request): void
    {
        if ($this->app->adminAuth->identity($request) === null) {
            throw new HttpException(Response::redirect('/admin/login?ReturnUrl=' . rawurlencode($request->path)));
        }
    }

    /** /admin/api/* alatt: 401 átirányítás helyett (mint a .NET OnRedirectToLogin). */
    private function authorizeApi(Request $request): void
    {
        if ($this->app->adminAuth->identity($request) === null) {
            throw new HttpException(Response::status(401));
        }
    }

    private function isValidCredential(string $username, string $password): bool
    {
        $expectedUsername = $this->app->config->string('AdminAuth:Username');
        $expectedHash = $this->app->config->string('AdminAuth:PasswordHash');

        if ($expectedUsername === '' || $expectedHash === '') {
            return false;
        }

        $usernameOk = hash_equals($expectedUsername, $username);
        $result = $this->app->passwordHasher()->verify($expectedHash, $password);
        $passwordOk = $result !== PasswordHasher::FAILED;

        return $usernameOk && $passwordOk;
    }

    private function view(string $name, array $vars): Response
    {
        $e = static fn(?string $s): string => htmlspecialchars((string)$s, ENT_QUOTES | ENT_HTML5, 'UTF-8');
        extract($vars, EXTR_SKIP);
        ob_start();
        try {
            require APP_ROOT . '/views/admin/' . $name . '.php';
            $html = (string)ob_get_clean();
        } catch (\Throwable $t) {
            ob_end_clean();
            throw $t;
        }
        return Response::html($html)
            ->withHeader('Cache-Control', 'no-store')
            ->withHeader('X-Frame-Options', 'DENY');
    }
}
