<?php

declare(strict_types=1);

namespace ScheduleLogic\Controllers;

use ScheduleLogic\App;
use ScheduleLogic\Core\Csrf;
use ScheduleLogic\Core\HttpException;
use ScheduleLogic\Core\Request;
use ScheduleLogic\Core\Response;
use ScheduleLogic\Services\PasswordHasher;

final class AdminController
{
    public function __construct(private readonly App $app)
    {
    }

    public function index(Request $request): Response
    {
        $this->authorizePage($request);
        $csrf = new Csrf($request);
        return $csrf->attachCookie($this->view('index', ['csrf' => $csrf]));
    }

    public function loginPage(Request $request): Response
    {
        $csrf = new Csrf($request);
        return $csrf->attachCookie($this->view('login', ['csrf' => $csrf, 'errors' => []]));
    }

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
                'errors' => ['Invalid username or password.'],
            ]));
        }

        $this->app->adminAuth->signIn($request, ['name' => $username]);
        return Response::redirect('/admin');
    }

    public function logout(Request $request): Response
    {
        $this->authorizePage($request);
        (new Csrf($request))->validate();
        $this->app->adminAuth->signOut($request);
        return Response::redirect('/admin/login');
    }

    public function getStats(Request $request): Response
    {
        $this->authorizeApi($request);
        return Response::json($this->app->adminService()->getStats());
    }

    public function searchUsers(Request $request): Response
    {
        $this->authorizeApi($request);
        return Response::json($this->app->adminService()->searchUsers($request->query('q')));
    }

    public function confirmUser(Request $request): Response
    {
        $this->authorizeApi($request);
        $confirmed = $this->app->adminService()->confirmUser((int)$request->route('id'));
        return $confirmed ? Response::ok() : Response::problem(404);
    }

    public function deleteUser(Request $request): Response
    {
        $this->authorizeApi($request);
        $this->app->adminService()->deleteUser((int)$request->route('id'));
        return Response::ok();
    }

    private function authorizePage(Request $request): void
    {
        if ($this->app->adminAuth->identity($request) === null) {
            throw new HttpException(Response::redirect('/admin/login?ReturnUrl=' . rawurlencode($request->path)));
        }
    }

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
