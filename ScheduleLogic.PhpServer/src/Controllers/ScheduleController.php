<?php

declare(strict_types=1);

namespace ScheduleLogic\Controllers;

use ScheduleLogic\App;
use ScheduleLogic\Core\Request;
use ScheduleLogic\Core\Response;
use ScheduleLogic\Models\InputReader;

/**
 * api/Schedule - a C# ScheduleController megfelelője.
 */
final class ScheduleController
{
    public function __construct(private readonly App $app)
    {
    }

    // POST api/Schedule/{id:long}   [Authorize]
    public function generateSchedule(Request $request): Response
    {
        $userId = $this->app->requireUserId($request);
        $result = $this->app->scheduleService()->generateSchedule((int)$request->route('id'), $userId);
        return $result !== null ? Response::json($result) : Response::problem(404);
    }

    // GET api/Schedule/{id:long}   [Authorize]
    public function getSchedule(Request $request): Response
    {
        $userId = $this->app->requireUserId($request);
        $result = $this->app->scheduleService()->getScheduleData((int)$request->route('id'), $userId);
        return $result !== null ? Response::json($result) : Response::problem(404);
    }

    // GET api/Schedule/isRunning?id=   [Authorize]
    public function checkSolver(Request $request): Response
    {
        $userId = $this->app->requireUserId($request);
        $result = $this->app->scheduleService()->checkSolver(EventController::queryLong($request, 'id'), $userId);
        return $result === null ? Response::problem(404) : Response::json($result);
    }

    // POST api/Schedule/stop?id=   [Authorize]
    public function stopSolver(Request $request): Response
    {
        $userId = $this->app->requireUserId($request);
        $result = $this->app->scheduleService()->stopSolver(EventController::queryLong($request, 'id'), $userId);
        return $result === null ? Response::problem(404) : Response::json($result);
    }

    /**
     * POST api/Schedule/answer?key=...
     *
     * A solver hívja vissza, bejelentkezett felhasználó nélkül. Jogosultság:
     * a returnURL-be tett, eseményhez kötött HMAC kulcs (SolverCallbackSigner).
     * A .NET verzió csak a localhost IP-t nézte, ami reverse proxy mögött
     * bárkit átengedett.
     */
    public function solverAnswer(Request $request): Response
    {
        $body = $request->requireJsonBody();

        $in = new InputReader();
        $eventId = $in->int($body, 'EventId', 'EventId');
        $status = $in->requiredString($body, 'Status', 'Status');
        $rawSchedule = $in->list($body, 'Schedule', 'Schedule');

        $schedule = [];
        foreach ($rawSchedule as $i => $item) {
            if (!is_array($item)) {
                $in->addError("Schedule[$i]", 'The JSON value could not be converted to ScheduleModel.');
                continue;
            }
            $p = "Schedule[$i]";
            $schedule[] = [
                'participantId' => $in->int($item, 'ParticipantId', "$p.ParticipantId"),
                'eventTypeId' => $in->int($item, 'EventTypeId', "$p.EventTypeId"),
                'locationId' => $in->int($item, 'LocationId', "$p.LocationId"),
                'start' => $in->int($item, 'Start', "$p.Start"),
                'end' => $in->int($item, 'End', "$p.End"),
                'slot' => $in->int($item, 'Slot', "$p.Slot"),
            ];
        }

        if (!$this->app->solverSigner()->verify($eventId, $request->query('key'))) {
            $this->app->logger->warning(
                'Rejected /answer call with invalid key for event {EventId} from {Ip}',
                ['EventId' => $eventId, 'Ip' => $request->clientIp]
            );
            return Response::status(403);
        }
        $in->throwIfInvalid();

        $accepted = $this->app->scheduleService()->receiveSolverAnswer($eventId, $status, $schedule);
        return $accepted ? Response::ok() : Response::problem(400);
    }

    // GET api/Schedule/export?id=   [Authorize]
    public function getExport(Request $request): Response
    {
        $userId = $this->app->requireUserId($request);
        $content = $this->app->scheduleService()->getScheduleFile(EventController::queryLong($request, 'id'), $userId);
        if ($content === null) {
            return Response::problem(404);
        }
        return Response::file(
            $content,
            'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',
            'schedule.xlsx'
        );
    }
}
