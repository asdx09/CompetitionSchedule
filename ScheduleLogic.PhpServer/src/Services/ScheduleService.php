<?php

declare(strict_types=1);

namespace ScheduleLogic\Services;

use ScheduleLogic\Core\Config;
use ScheduleLogic\Core\HttpClient;
use ScheduleLogic\Core\Logger;
use ScheduleLogic\Repositories\EventRepository;
use ScheduleLogic\Repositories\ScheduleRepository;

final class ScheduleService
{
    public function __construct(
        private readonly ScheduleRepository $schedules,
        private readonly EventRepository $events,
        private readonly HttpClient $solver,
        private readonly SolverCallbackSigner $signer,
        private readonly Config $config,
        private readonly Logger $logger,
    ) {
    }

    public function generateSchedule(int $eventId, int $userId): ?array
    {
        if (!$this->events->isOwnedByUser($eventId, $userId)) {
            return null;
        }

        $request = $this->schedules->getScheduleInfo($eventId);

        $base = $this->config->string('URLS:BackendForSolver') ?: $this->config->string('URLS:Backend');
        $request['returnURL'] = rtrim($base, '/') . '/api/Schedule/answer?key=' . $this->signer->keyFor($eventId);

        $response = $this->solver->postJson('schedule', $request);
        if ($response['status'] < 200 || $response['status'] > 299) {
            $this->logger->warning(
                'Solver rejected the schedule request for event {EventId}: {Error}',
                ['EventId' => $eventId, 'Error' => $response['body']]
            );
        }

        $request['returnURL'] = '';
        return $request;
    }

    public function getScheduleData(int $eventId, int $userId): ?array
    {
        if (!$this->events->isOwnedByUser($eventId, $userId)) {
            return null;
        }
        return $this->schedules->getScheduleData($eventId);
    }

    public function checkSolver(int $eventId, int $userId): ?bool
    {
        if (!$this->events->isOwnedByUser($eventId, $userId)) {
            return null;
        }

        $response = $this->solver->getJson('is_solver_running?EventId=' . $eventId);
        if ($response['status'] < 200 || $response['status'] > 299) {
            $this->logger->warning('Solver status check failed for event {EventId}: {Status}', ['EventId' => $eventId, 'Status' => $response['status']]);
            return false;
        }
        $json = json_decode($response['body'], true);
        return is_array($json) && (bool)self::ci($json, 'Running', false);
    }

    public function stopSolver(int $eventId, int $userId): ?bool
    {
        if (!$this->events->isOwnedByUser($eventId, $userId)) {
            return null;
        }

        $response = $this->solver->getJson('stop_solver?EventId=' . $eventId);
        if ($response['status'] < 200 || $response['status'] > 299) {
            $this->logger->warning('Solver stop failed for event {EventId}: {Status}', ['EventId' => $eventId, 'Status' => $response['status']]);
            return false;
        }
        $json = json_decode($response['body'], true);
        return is_array($json) && self::ci($json, 'Status') === 'STOPPED';
    }

    public function receiveSolverAnswer(int $eventId, string $status, array $schedule): bool
    {
        $hasSolution = in_array($status, ['FEASIBLE', 'PARTIAL_SOLUTION', 'OPTIMAL'], true);
        if (!$hasSolution) {
            $this->logger->info('No solution for event {EventId}, solver status: {Status}', ['EventId' => $eventId, 'Status' => $status]);
            return true;
        }

        $known = $this->schedules->getEventObjectIds($eventId);
        foreach ($schedule as $item) {
            if (!isset($known['participants'][$item['participantId']], $known['eventTypes'][$item['eventTypeId']], $known['locations'][$item['locationId']])) {
                $this->logger->warning('Solver answer for event {EventId} references foreign objects - rejected.', ['EventId' => $eventId]);
                return false;
            }
        }

        $this->schedules->saveSolverResult($schedule, $eventId);
        return true;
    }

    public function getScheduleFile(int $eventId, int $userId): ?string
    {
        if (!$this->events->isOwnedByUser($eventId, $userId)) {
            return null;
        }
        $data = $this->schedules->getExportData($eventId);
        if ($data === null) {
            return null;
        }

        $lightBlue = 'FFADD8E6';
        $lightCyan = 'FFE0FFFF';

        $xlsx = new XlsxWriter('Schedule');
        $xlsx->addRow(['Competitor', 'Group', 'Event Type', 'Location', 'Slot', 'Start', 'End'], $lightBlue);

        $rows = $data['timeZones'];
        $keys = array_map(static fn($tz) => $tz['groupName'] . $tz['participant'], $rows);
        array_multisort($keys, SORT_STRING | SORT_FLAG_CASE, array_keys($rows), $rows);

        $format = $this->config->string('AppSettings:ExportDateFormat', 'Y-m-d H:i:s');
        $rowNumber = 2;
        foreach ($rows as $tz) {
            $xlsx->addRow([
                $tz['participant'],
                $tz['groupName'],
                $tz['eventType'],
                $tz['location'],
                $tz['slot'],
                $tz['startTime']->format($format),
                $tz['endTime']->format($format),
            ], $rowNumber % 2 === 0 ? $lightBlue : $lightCyan);
            $rowNumber++;
        }
        $xlsx->freezeFirstRow();

        return $xlsx->toBytes();
    }

    private static function ci(array $json, string $key, mixed $default = null): mixed
    {
        foreach ($json as $k => $v) {
            if (is_string($k) && strcasecmp($k, $key) === 0) {
                return $v;
            }
        }
        return $default;
    }
}
