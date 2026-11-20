<?php

declare(strict_types=1);

namespace ScheduleLogic\Controllers;

use ScheduleLogic\App;
use ScheduleLogic\Core\HttpException;
use ScheduleLogic\Core\Request;
use ScheduleLogic\Core\Response;
use ScheduleLogic\Models\EventInput;

final class EventController
{
    private int $userId;

    public function __construct(private readonly App $app)
    {
    }

    public function newEvent(Request $request): Response
    {
        $this->authorize($request);
        $this->app->eventService()->newEvent($this->userId);
        return Response::ok();
    }

    public function deleteEvent(Request $request): Response
    {
        $this->authorize($request);
        $deleted = $this->app->eventService()->deleteEvent(self::queryLong($request, 'id'), $this->userId);
        return $deleted ? Response::ok() : Response::problem(404);
    }

    public function getEvents(Request $request): Response
    {
        $this->authorize($request);
        return Response::json($this->app->eventService()->getEvents($this->userId));
    }

    public function getEvent(Request $request): Response
    {
        $this->authorize($request);
        $result = $this->app->eventService()->getEvent(self::queryLong($request, 'id'), $this->userId);
        return $result !== null ? Response::json($result) : Response::problem(404);
    }

    public function saveEvent(Request $request): Response
    {
        $this->authorize($request);
        $data = EventInput::parse($request->requireJsonBody());
        $ok = $this->app->eventService()->saveEvent($data, $this->userId);
        return $ok ? Response::ok() : Response::problem(400);
    }

    public function newWizardEvent(Request $request): Response
    {
        $this->authorize($request);
        $data = EventInput::parse($request->requireJsonBody());
        $ok = $this->app->eventService()->newWizardEvent($data, $this->userId);
        return $ok ? Response::ok() : Response::problem(400);
    }

    private function authorize(Request $request): void
    {
        $this->userId = $this->app->requireUserId($request);
    }

    public static function queryLong(Request $request, string $name): int
    {
        $value = $request->query($name);
        if ($value === null || $value === '') {
            return 0;
        }
        if (!preg_match('/^-?\d{1,18}$/', trim($value))) {
            throw new HttpException(Response::validationProblem([
                $name => ["The value '{$value}' is not valid."],
            ]));
        }
        return (int)$value;
    }
}
