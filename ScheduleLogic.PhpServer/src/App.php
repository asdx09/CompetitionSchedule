<?php

declare(strict_types=1);

namespace ScheduleLogic;

use ScheduleLogic\Controllers\AdminController;
use ScheduleLogic\Controllers\AuthenticationController;
use ScheduleLogic\Controllers\EventController;
use ScheduleLogic\Controllers\ScheduleController;
use ScheduleLogic\Core\Config;
use ScheduleLogic\Core\Cors;
use ScheduleLogic\Core\Database;
use ScheduleLogic\Core\DotNetTime;
use ScheduleLogic\Core\HttpClient;
use ScheduleLogic\Core\HttpException;
use ScheduleLogic\Core\Logger;
use ScheduleLogic\Core\RateLimiter;
use ScheduleLogic\Core\Request;
use ScheduleLogic\Core\Response;
use ScheduleLogic\Core\Router;
use ScheduleLogic\Core\SessionAuth;
use ScheduleLogic\Core\StaticFiles;
use ScheduleLogic\Repositories\EventRepository;
use ScheduleLogic\Repositories\ScheduleRepository;
use ScheduleLogic\Repositories\UserRepository;
use ScheduleLogic\Services\AdminService;
use ScheduleLogic\Services\AuthenticationService;
use ScheduleLogic\Services\EventService;
use ScheduleLogic\Services\PasswordHasher;
use ScheduleLogic\Services\ScheduleService;
use ScheduleLogic\Services\SmtpEmailSender;
use ScheduleLogic\Services\SolverCallbackSigner;

/**
 * Az alkalmazás "Program.cs"-e: itt történik a szolgáltatások összerakása
 * (egyszerű, kézi dependency injection), az útvonalak regisztrálása és a
 * kérés teljes életciklusa (CORS -> statikus fájlok -> routing -> hibakezelés).
 */
final class App
{
    public readonly Config $config;
    public readonly Logger $logger;
    public readonly Database $db;
    public readonly RateLimiter $rateLimiter;
    public readonly SessionAuth $userAuth;
    public readonly SessionAuth $adminAuth;

    private Cors $cors;
    private StaticFiles $staticFiles;
    private Router $router;
    /** @var array<string, object> lusta "scoped" szolgáltatások */
    private array $services = [];

    public function __construct(string $root)
    {
        $this->config = new Config($root);

        $tz = $this->config->string('AppSettings:TimeZone', 'Europe/Budapest');
        date_default_timezone_set($tz);
        DotNetTime::setLocalTimeZone($tz);

        $storage = rtrim($this->config->string('AppSettings:StoragePath') ?: $root . '/storage', '/\\');
        $this->logger = new Logger($storage . '/logs', $this->config->isDevelopment() ? 'info' : 'warning');
        $this->db = new Database($this->config);
        $this->rateLimiter = new RateLimiter($storage . '/ratelimit', $this->config->bool('RateLimiting:Enabled', true));

        $isDev = $this->config->isDevelopment();

        // Felhasználói cookie - 1 óra, sliding, perzisztens (mint a .NET-ben).
        // Javítás: élesben is SameSite=Lax (a .NET-ben None volt, ami
        // CSRF-et tett lehetővé), és nincs Domain, így csak a saját hosztra megy.
        $this->userAuth = new SessionAuth(
            cookieName: 'sl_auth',
            savePath: $storage . '/sessions/user',
            idleTimeout: 3600,
            persistent: true,
            sameSite: 'Lax',
            secure: $isDev ? null : true,
        );

        // Admin cookie - 2 óra, sliding, böngésző-session, Strict, mindig Secure.
        $this->adminAuth = new SessionAuth(
            cookieName: 'sl_admin_auth',
            savePath: $storage . '/sessions/admin',
            idleTimeout: 7200,
            persistent: false,
            sameSite: 'Strict',
            secure: true,
        );

        $origins = $this->config->stringList('Cors:AllowedOrigins');
        if (!$origins) {
            $origins = $isDev ? ['https://localhost:58921'] : ['https://schedulelogic.hu'];
        }
        $this->cors = new Cors($origins);

        // Alapból a .NET projekt wwwroot mappája (oda buildeli az Angular klienst a Visual Studio).
        $webRoot = $this->config->string('AppSettings:WebRoot')
            ?: ($this->config->sharedDir() ?? $root . '/../ScheduleLogic.Server') . '/wwwroot';
        $this->staticFiles = new StaticFiles($webRoot);

        $this->router = new Router();
        $this->registerRoutes();
    }

    // =====================================================================
    //  Útvonalak (a C# kontrollerek [Route] attribútumai alapján)
    // =====================================================================

    private function registerRoutes(): void
    {
        $r = $this->router;

        // ---- api/Authentication ----
        $auth = fn() => new AuthenticationController($this);
        $r->post('/api/Authentication/login', fn(Request $q) => $auth()->login($q));
        $r->post('/api/Authentication/register', fn(Request $q) => $auth()->register($q));
        $r->post('/api/Authentication/resend-confirmation', fn(Request $q) => $auth()->resendConfirmation($q));
        $r->post('/api/Authentication/forgot-password', fn(Request $q) => $auth()->forgotPassword($q));
        $r->post('/api/Authentication/reset-password', fn(Request $q) => $auth()->resetPassword($q));
        $r->get('/api/Authentication/check-token', fn(Request $q) => $auth()->checkToken($q));
        $r->get('/api/Authentication/confirm', fn(Request $q) => $auth()->confirmEmail($q));
        $r->delete('/api/Authentication/delete', fn(Request $q) => $auth()->deleteAccount($q));
        $r->post('/api/Authentication/logout', fn(Request $q) => $auth()->logout($q));

        // ---- api/Event ----
        $event = fn() => new EventController($this);
        $r->post('/api/Event/new-event', fn(Request $q) => $event()->newEvent($q));
        $r->delete('/api/Event/delete-event', fn(Request $q) => $event()->deleteEvent($q));
        $r->get('/api/Event/get-events', fn(Request $q) => $event()->getEvents($q));
        $r->get('/api/Event/get-event', fn(Request $q) => $event()->getEvent($q));
        $r->post('/api/Event/save-event', fn(Request $q) => $event()->saveEvent($q));
        $r->post('/api/Event/new-wizard', fn(Request $q) => $event()->newWizardEvent($q));

        // ---- api/Schedule ----
        $schedule = fn() => new ScheduleController($this);
        $r->get('/api/Schedule/isRunning', fn(Request $q) => $schedule()->checkSolver($q));
        $r->post('/api/Schedule/stop', fn(Request $q) => $schedule()->stopSolver($q));
        $r->post('/api/Schedule/answer', fn(Request $q) => $schedule()->solverAnswer($q));
        $r->get('/api/Schedule/export', fn(Request $q) => $schedule()->getExport($q));
        $r->post('/api/Schedule/{id:long}', fn(Request $q) => $schedule()->generateSchedule($q));
        $r->get('/api/Schedule/{id:long}', fn(Request $q) => $schedule()->getSchedule($q));

        // ---- admin ----
        $admin = fn() => new AdminController($this);
        $r->get('/admin', fn(Request $q) => $admin()->index($q));
        $r->get('/admin/login', fn(Request $q) => $admin()->loginPage($q));
        $r->post('/admin/login', fn(Request $q) => $admin()->login($q));
        $r->post('/admin/logout', fn(Request $q) => $admin()->logout($q));
        $r->get('/admin/api/stats', fn(Request $q) => $admin()->getStats($q));
        $r->get('/admin/api/users', fn(Request $q) => $admin()->searchUsers($q));
        $r->post('/admin/api/users/{id:long}/confirm', fn(Request $q) => $admin()->confirmUser($q));
        $r->delete('/admin/api/users/{id:long}', fn(Request $q) => $admin()->deleteUser($q));
    }

    // =====================================================================
    //  Kérés feldolgozása
    // =====================================================================

    public function handle(Request $request): Response
    {
        try {
            $response = $this->dispatch($request);
        } catch (HttpException $e) {
            $response = $e->response;
        } catch (\Throwable $e) {
            $response = $this->handleException($e);
        }

        foreach ([...$this->userAuth->takeCookies(), ...$this->adminAuth->takeCookies()] as $cookie) {
            $response->addCookie($cookie);
        }
        $this->cors->apply($request, $response);
        $response->withHeader('X-Content-Type-Options', 'nosniff');

        // A rate limiter fájljait alkalmanként takarítjuk.
        if (random_int(1, 200) === 1) {
            $this->rateLimiter->cleanup();
        }
        return $response;
    }

    private function dispatch(Request $request): Response
    {
        if ($preflight = $this->cors->handlePreflight($request)) {
            return $preflight;
        }

        $isBackendPath = (bool)preg_match('#^/(api|admin)(/|$)#i', $request->path);

        // UseStaticFiles: a lebuildelt Angular kliens fájljai
        if (!$isBackendPath && ($static = $this->staticFiles->tryServe($request))) {
            return $static;
        }

        $match = $this->router->match($request->method, $request->path);
        if ($match !== null) {
            [$handler, $params] = $match;
            $request->routeParams = $params;
            return $handler($request);
        }

        // MapFallbackToFile("/index.html"): az Angular router útvonalai
        if (!$isBackendPath && ($fallback = $this->staticFiles->fallback($request))) {
            return $fallback;
        }
        return Response::problem(404);
    }

    /**
     * Végzetes PHP hiba (pl. időtúllépés, memóriahiány) esetén a normál
     * hibakezelés nem fut le. Ilyenkor a shutdown függvényből ezzel küldünk
     * egy 500-as választ CORS fejlécekkel, hogy a böngészőben a valódi hiba
     * látsszon, ne egy félrevezető "CORS error".
     */
    public function handleFatalError(Request $request, array $error): void
    {
        $this->logger->error('Fatal PHP error: {Message} in {File}:{Line}', [
            'Message' => $error['message'] ?? '',
            'File' => $error['file'] ?? '',
            'Line' => $error['line'] ?? 0,
        ]);
        if (headers_sent()) {
            return;
        }
        while (ob_get_level() > 0) {
            ob_end_clean();
        }
        $response = Response::json(['title' => 'Internal server error', 'status' => 500], 500);
        $this->cors->apply($request, $response);
        $response->send();
    }

    /** A C# ExceptionHandler megfelelője. */
    private function handleException(\Throwable $e): Response
    {
        $this->logger->error('Unhandled exception', [], $e);

        $status = match (true) {
            $e instanceof Core\NotFoundException => 404,
            $e instanceof \InvalidArgumentException, $e instanceof \ValueError => 400,
            default => 500,
        };
        $title = match ($status) {
            404 => 'Resource not found',
            400 => 'Invalid request',
            default => 'Internal server error',
        };
        return Response::json(['title' => $title, 'status' => $status], $status);
    }

    // =====================================================================
    //  Segédfüggvények a kontrollereknek
    // =====================================================================

    /**
     * [Authorize] megfelelője: a bejelentkezett felhasználó azonosítója,
     * különben 401 (a .NET OnRedirectToLogin is 401-et ad, átirányítás nélkül).
     */
    public function requireUserId(Request $request): int
    {
        $identity = $this->userAuth->identity($request);
        if ($identity === null || !isset($identity['userId'])) {
            throw new HttpException(Response::status(401));
        }
        return (int)$identity['userId'];
    }

    /** 429-et dob, ha a kliens túllépte a keretet. */
    public function rateLimit(Request $request, string $bucket, int $defaultLimit, int $defaultWindowSeconds): void
    {
        $limit = $this->config->int("RateLimiting:{$bucket}:PermitLimit", $defaultLimit);
        $window = $this->config->int("RateLimiting:{$bucket}:WindowSeconds", $defaultWindowSeconds);
        $retryAfter = $this->rateLimiter->hit($bucket, $request->clientIp, $limit, $window);
        if ($retryAfter !== null) {
            $this->logger->warning('Rate limit exceeded on {Bucket} from {Ip}', ['Bucket' => $bucket, 'Ip' => $request->clientIp]);
            throw new HttpException(
                Response::text('Too many requests. Please try again later.', 429)
                    ->withHeader('Retry-After', (string)$retryAfter)
            );
        }
    }

    // ---- szolgáltatások (lusta példányosítás) ----

    public function users(): UserRepository
    {
        return $this->services[__FUNCTION__] ??= new UserRepository($this->db);
    }

    public function events(): EventRepository
    {
        return $this->services[__FUNCTION__] ??= new EventRepository($this->db, $this->logger->forCategory('EventRepository'));
    }

    public function schedules(): ScheduleRepository
    {
        return $this->services[__FUNCTION__] ??= new ScheduleRepository($this->db);
    }

    public function passwordHasher(): PasswordHasher
    {
        return $this->services[__FUNCTION__] ??= new PasswordHasher();
    }

    public function authService(): AuthenticationService
    {
        return $this->services[__FUNCTION__] ??= new AuthenticationService(
            $this->users(),
            new SmtpEmailSender($this->config),
            $this->passwordHasher(),
            $this->config,
            $this->logger->forCategory('AuthenticationService'),
        );
    }

    public function eventService(): EventService
    {
        return $this->services[__FUNCTION__] ??= new EventService($this->events(), $this->db);
    }

    public function solverClient(): HttpClient
    {
        return $this->services[__FUNCTION__] ??= new HttpClient(
            $this->config->string('URLS:Solver') ?: 'http://127.0.0.1:8000/',
            $this->config->int('Solver:TimeoutSeconds', 100),
        );
    }

    public function solverSigner(): SolverCallbackSigner
    {
        return $this->services[__FUNCTION__] ??= new SolverCallbackSigner(
            $this->config->string('Solver:CallbackSecret'),
            rtrim($this->config->string('AppSettings:StoragePath') ?: APP_ROOT . '/storage', '/\\') . '/solver-callback.key',
        );
    }

    public function scheduleService(): ScheduleService
    {
        return $this->services[__FUNCTION__] ??= new ScheduleService(
            $this->schedules(),
            $this->events(),
            $this->solverClient(),
            $this->solverSigner(),
            $this->config,
            $this->logger->forCategory('ScheduleService'),
        );
    }

    public function adminService(): AdminService
    {
        return $this->services[__FUNCTION__] ??= new AdminService(
            $this->users(),
            $this->db,
            $this->solverClient(),
            $this->logger->forCategory('AdminService'),
        );
    }
}
