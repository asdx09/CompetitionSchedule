<?php

declare(strict_types=1);

namespace ScheduleLogic\Services;

use ScheduleLogic\Core\Database;
use ScheduleLogic\Repositories\EventRepository;

/**
 * Események üzleti logikája (a C# EventService megfelelője): minden művelet
 * előtt ellenőrzi, hogy az esemény a bejelentkezett felhasználóé-e.
 */
final class EventService
{
    public function __construct(
        private readonly EventRepository $events,
        private readonly Database $db,
    ) {
    }

    public function newEvent(int $userId): void
    {
        $this->events->createEvent($userId);
    }

    public function deleteEvent(int $eventId, int $userId): bool
    {
        if (!$this->events->isOwnedByUser($eventId, $userId)) {
            return false;
        }
        $this->events->deleteEvent($eventId);
        return true;
    }

    public function getEvents(int $userId): array
    {
        return $this->events->getEvents($userId);
    }

    public function getEvent(int $eventId, int $userId): ?array
    {
        if (!$this->events->isOwnedByUser($eventId, $userId)) {
            return null;
        }
        return $this->events->getEvent($eventId);
    }

    public function saveEvent(array $data, int $userId): bool
    {
        $eventId = self::parseLong($data['eventData']['eventId']);
        if ($eventId === null || !$this->events->isOwnedByUser($eventId, $userId)) {
            return false;
        }
        return $this->events->saveEvent($eventId, $data);
    }

    public function newWizardEvent(array $data, int $userId): bool
    {
        return $this->events->createWizardEvent($userId, $data);
    }

    private static function parseLong(string $value): ?int
    {
        $value = trim($value);
        return preg_match('/^-?\d{1,18}$/', $value) ? (int)$value : null;
    }
}
