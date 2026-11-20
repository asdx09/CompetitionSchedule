<?php

declare(strict_types=1);

require __DIR__ . '/../src/bootstrap.php';

use ScheduleLogic\App;
use ScheduleLogic\Core\Request;

$app = new App(APP_ROOT);

$trustedProxies = $app->config->stringList('ForwardedHeaders:KnownProxies', ['127.0.0.1', '::1']);
$request = Request::fromGlobals($trustedProxies);

register_shutdown_function(static function () use ($app, $request): void {
    $error = error_get_last();
    if ($error !== null && in_array($error['type'], [E_ERROR, E_CORE_ERROR, E_COMPILE_ERROR, E_PARSE, E_USER_ERROR], true)) {
        $app->handleFatalError($request, $error);
    }
});

$app->handle($request)->send();
