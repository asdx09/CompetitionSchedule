<?php

declare(strict_types=1);

namespace ScheduleLogic\Repositories;

use PDO;
use ScheduleLogic\Core\Database;
use ScheduleLogic\Core\DotNetTime;
use ScheduleLogic\Core\Logger;

/**
 * Az event és a hozzá tartozó táblák kezelése (a C# EventRepository megfelelője).
 */
final class EventRepository
{
    public function __construct(
        private readonly Database $db,
        private readonly ?Logger $logger = null,
    ) {
    }

    /** Új, üres esemény alapértékekkel. @return int az új event_id */
    public function createEvent(int $userId): int
    {
        $now = DotNetTime::toDb(DotNetTime::nowUtc());
        $stmt = $this->db->pdo()->prepare(
            'INSERT INTO event (createdby, eventname, startdate, enddate, basepausetime, locationpausetime,
                                isprivate, locweight, compweight, groupweight, typeweight)
             VALUES (:u, :n, :s, :e, 5, 5, TRUE, 1, 1, 1, 1)
             RETURNING event_id'
        );
        $stmt->execute(['u' => $userId, 'n' => 'New Event', 's' => $now, 'e' => $now]);
        return (int)$stmt->fetchColumn();
    }

    public function isOwnedByUser(int $eventId, int $userId): bool
    {
        $stmt = $this->db->pdo()->prepare('SELECT EXISTS(SELECT 1 FROM event WHERE event_id = :e AND createdby = :u)');
        $stmt->execute(['e' => $eventId, 'u' => $userId]);
        return (bool)$stmt->fetchColumn();
    }

    /** A gyerek táblák ON DELETE CASCADE-del törlődnek. */
    public function deleteEvent(int $eventId): void
    {
        $stmt = $this->db->pdo()->prepare('DELETE FROM event WHERE event_id = :e');
        $stmt->execute(['e' => $eventId]);
    }

    public function getEvents(int $userId): array
    {
        $stmt = $this->db->pdo()->prepare('SELECT event_id, eventname FROM event WHERE createdby = :u ORDER BY event_id');
        $stmt->execute(['u' => $userId]);
        return array_map(static fn(array $e) => [
            'eventName' => $e['eventname'],
            'eventId' => (int)$e['event_id'],
        ], $stmt->fetchAll());
    }

    /**
     * Az esemény összes adata a szerkesztő felületnek (DataDTO alakban).
     * A C# kód a TimeZones listát itt üresen hagyja.
     */
    public function getEvent(int $eventId): ?array
    {
        $event = $this->fetchEventRow($eventId);
        if ($event === null) {
            return null;
        }
        return self::buildDataDto($this->db->pdo(), $event, []);
    }

    public function fetchEventRow(int $eventId): ?array
    {
        $stmt = $this->db->pdo()->prepare('SELECT * FROM event WHERE event_id = :e');
        $stmt->execute(['e' => $eventId]);
        return $stmt->fetch() ?: null;
    }

    /**
     * A DataDTO felépítése (közös az event és a schedule lekérdezésnél).
     * Az azonosítók stringként mennek ki, ahogy a C# DTO-kban is.
     */
    public static function buildDataDto(PDO $pdo, array $e, array $timeZones): array
    {
        $eventId = (int)$e['event_id'];
        $q = static function (string $sql) use ($pdo, $eventId): array {
            $stmt = $pdo->prepare($sql);
            $stmt->execute(['e' => $eventId]);
            return $stmt->fetchAll();
        };
        $id = static fn($v) => $v === null ? null : (string)$v;

        return [
            'eventData' => [
                'eventId' => (string)$eventId,
                'eventName' => $e['eventname'],
                'startDate' => DotNetTime::toJson(DotNetTime::fromDb($e['startdate'])),
                'endDate' => DotNetTime::toJson(DotNetTime::fromDb($e['enddate'])),
                'isPrivate' => (bool)$e['isprivate'],
                'basePauseTime' => (int)$e['basepausetime'],
                'locationPauseTime' => (int)$e['locationpausetime'],
                'locWeight' => (int)$e['locweight'],
                'groupWeight' => (int)$e['groupweight'],
                'typeWeight' => (int)$e['typeweight'],
                'compWeight' => (int)$e['compweight'],
            ],
            'eventTypes' => array_map(static fn($r) => [
                'eventTypeId' => (string)$r['eventtype_id'],
                'eventId' => (string)$r['event_id'],
                'typeName' => $r['typename'],
                'timeRange' => DotNetTime::timeOnlyToJson($r['timerange']),
            ], $q('SELECT * FROM eventtype WHERE event_id = :e ORDER BY eventtype_id')),
            'groups' => array_map(static fn($r) => [
                'groupId' => (string)$r['group_id'],
                'eventId' => (string)$r['event_id'],
                'groupName' => $r['groupname'],
            ], $q('SELECT * FROM groups WHERE event_id = :e ORDER BY group_id')),
            'locations' => array_map(static fn($r) => [
                'locationId' => (string)$r['location_id'],
                'eventId' => (string)$r['event_id'],
                'locationName' => $r['locationname'],
                'slots' => (int)$r['slots'],
            ], $q('SELECT * FROM location WHERE event_id = :e ORDER BY location_id')),
            'participants' => array_map(static fn($r) => [
                'participantId' => (string)$r['participant_id'],
                'competitorNumber' => (int)$r['competitornumber'],
                'participantName' => $r['participantname'],
                'eventId' => (string)$r['event_id'],
                'groupId' => $id($r['group_id']),
            ], $q('SELECT * FROM participant WHERE event_id = :e ORDER BY participant_id')),
            'registrations' => array_map(static fn($r) => [
                'registrationId' => (string)$r['registration_id'],
                'eventId' => (string)$r['event_id'],
                'participantId' => (string)$r['participant_id'],
                'eventTypeId' => (string)$r['eventtype_id'],
            ], $q('SELECT * FROM registrations WHERE event_id = :e ORDER BY registration_id')),
            'pauseTable' => array_map(static fn($r) => [
                'pauseId' => (string)$r['pause_id'],
                'eventId' => (string)$r['event_id'],
                'locationId1' => (string)$r['location_id1'],
                'locationId2' => (string)$r['location_id2'],
                'pause' => DotNetTime::timeOnlyToJson($r['pause']),
            ], $q('SELECT * FROM pausetable WHERE event_id = :e ORDER BY pause_id')),
            'locationTable' => array_map(static fn($r) => [
                'locationTableId' => (string)$r['locationtable_id'],
                'eventId' => (string)$r['event_id'],
                'eventTypeId' => (string)$r['eventtype_id'],
                'locationId' => (string)$r['location_id'],
            ], $q('SELECT * FROM locationtable WHERE event_id = :e ORDER BY locationtable_id')),
            'constraints' => array_map(static fn($r) => [
                'constraintId' => (string)$r['constraint_id'],
                'eventId' => (string)$r['event_id'],
                'objectId' => (string)$r['object_id'],
                'constraintType' => trim($r['constrainttype']),
                'startTime' => DotNetTime::toJson(DotNetTime::fromDb($r['starttime'])),
                'endTime' => DotNetTime::toJson(DotNetTime::fromDb($r['endtime'])),
            ], $q('SELECT * FROM eventconstraint WHERE event_id = :e ORDER BY constraint_id')),
            'timeZones' => $timeZones,
        ];
    }

    /**
     * Mentés a szerkesztőből: az esemény adatai frissülnek, a gyerek sorok
     * teljesen lecserélődnek. Bármilyen hiba esetén visszagörget és false-t ad
     * (a kontroller ebből BadRequest-et csinál).
     */
    public function saveEvent(int $eventId, array $data): bool
    {
        try {
            return $this->db->transaction(function (PDO $pdo) use ($eventId, $data): bool {
                $d = $data['eventData'];
                $stmt = $pdo->prepare(
                    'UPDATE event SET eventname = :n, startdate = :s, enddate = :en, isprivate = :p,
                            basepausetime = :bp, locationpausetime = :lp,
                            locweight = :lw, groupweight = :gw, typeweight = :tw, compweight = :cw
                      WHERE event_id = :e'
                );
                $stmt->execute([
                    'n' => $d['eventName'],
                    's' => DotNetTime::toDb($d['startDate']),
                    'en' => DotNetTime::toDb($d['endDate']),
                    'p' => $d['isPrivate'] ? 'true' : 'false',
                    'bp' => $d['basePauseTime'],
                    'lp' => $d['locationPauseTime'],
                    'lw' => $d['locWeight'],
                    'gw' => $d['groupWeight'],
                    'tw' => $d['typeWeight'],
                    'cw' => $d['compWeight'],
                    'e' => $eventId,
                ]);
                if ($stmt->rowCount() === 0) {
                    return false;
                }

                $this->removeChildRows($pdo, $eventId);
                $this->insertChildRows($pdo, $eventId, $data);
                return true;
            });
        } catch (\Throwable $e) {
            $this->logger?->warning('Saving event {EventId} failed, rolled back.', ['EventId' => $eventId], $e);
            return false;
        }
    }

    /**
     * Varázsló: új esemény létrehozása és feltöltése. A C# verzióban az
     * üres esemény a tranzakción kívül jött létre, így hibás importnál
     * megmaradt egy félkész "New Event" - itt egyetlen tranzakció az egész.
     */
    public function createWizardEvent(int $userId, array $data): bool
    {
        try {
            return $this->db->transaction(function (PDO $pdo) use ($userId, $data): bool {
                $eventId = $this->createEvent($userId);
                $d = $data['eventData'];
                $stmt = $pdo->prepare(
                    'UPDATE event SET eventname = :n, startdate = :s, enddate = :en, isprivate = :p,
                            basepausetime = :bp, locationpausetime = :lp
                      WHERE event_id = :e'
                );
                $stmt->execute([
                    'n' => $d['eventName'],
                    's' => DotNetTime::toDb($d['startDate']),
                    'en' => DotNetTime::toDb($d['endDate']),
                    'p' => $d['isPrivate'] ? 'true' : 'false',
                    'bp' => $d['basePauseTime'],
                    'lp' => $d['locationPauseTime'],
                    'e' => $eventId,
                ]);

                $this->insertChildRows($pdo, $eventId, $data);
                return true;
            });
        } catch (\Throwable $e) {
            $this->logger?->warning('Creating wizard event failed, rolled back.', [], $e);
            return false;
        }
    }

    private function removeChildRows(PDO $pdo, int $eventId): void
    {
        // A sorrend a külső kulcsok miatt számít (a CASCADE miatt amúgy is törlődnének).
        foreach (['eventconstraint', 'registrations', 'pausetable', 'locationtable', 'participant', 'location', 'groups', 'eventtype'] as $table) {
            $pdo->prepare("DELETE FROM {$table} WHERE event_id = :e")->execute(['e' => $eventId]);
        }
    }

    /**
     * A kliens ideiglenes azonosítókat küld (UUID-k, vagy a varázslóban az
     * Excelből jövő ID-k). Ezeket itt képezzük le az új adatbázis-ID-kra;
     * ismeretlen hivatkozásnál kivétel -> visszagörgetés (mint a C#
     * Dictionary KeyNotFoundException-je).
     */
    private function insertChildRows(PDO $pdo, int $eventId, array $data): void
    {
        $lookup = static function (array $map, ?string $key, string $what): int {
            if ($key === null || !array_key_exists($key, $map)) {
                throw new \OutOfBoundsException("Unknown {$what} reference: " . var_export($key, true));
            }
            return $map[$key];
        };

        $eventTypeMap = [];
        $stmt = $pdo->prepare('INSERT INTO eventtype (event_id, typename, timerange) VALUES (:e, :n, :t) RETURNING eventtype_id');
        foreach ($data['eventTypes'] as $item) {
            $stmt->execute(['e' => $eventId, 'n' => $item['typeName'], 't' => $item['timeRange']]);
            $eventTypeMap[$item['eventTypeId']] = (int)$stmt->fetchColumn();
        }

        $groupMap = [];
        $stmt = $pdo->prepare('INSERT INTO groups (event_id, groupname) VALUES (:e, :n) RETURNING group_id');
        foreach ($data['groups'] as $item) {
            $stmt->execute(['e' => $eventId, 'n' => $item['groupName']]);
            $groupMap[$item['groupId']] = (int)$stmt->fetchColumn();
        }

        $locationMap = [];
        $stmt = $pdo->prepare('INSERT INTO location (event_id, locationname, slots) VALUES (:e, :n, :s) RETURNING location_id');
        foreach ($data['locations'] as $item) {
            $stmt->execute(['e' => $eventId, 'n' => $item['locationName'], 's' => $item['slots']]);
            $locationMap[$item['locationId']] = (int)$stmt->fetchColumn();
        }

        $stmt = $pdo->prepare('INSERT INTO locationtable (event_id, location_id, eventtype_id) VALUES (:e, :l, :t)');
        foreach ($data['locationTable'] as $item) {
            $stmt->execute([
                'e' => $eventId,
                'l' => $lookup($locationMap, $item['locationId'], 'location'),
                't' => $lookup($eventTypeMap, $item['eventTypeId'], 'event type'),
            ]);
        }

        $participantMap = [];
        $stmt = $pdo->prepare(
            'INSERT INTO participant (participantname, event_id, competitornumber, group_id) VALUES (:n, :e, :c, :g) RETURNING participant_id'
        );
        foreach ($data['participants'] as $item) {
            $groupId = ($item['groupId'] ?? '') !== '' ? $lookup($groupMap, $item['groupId'], 'group') : null;
            $stmt->execute(['n' => $item['participantName'], 'e' => $eventId, 'c' => $item['competitorNumber'], 'g' => $groupId]);
            $participantMap[$item['participantId']] = (int)$stmt->fetchColumn();
        }

        $stmt = $pdo->prepare('INSERT INTO pausetable (event_id, location_id1, location_id2, pause) VALUES (:e, :l1, :l2, :p)');
        foreach ($data['pauseTable'] as $item) {
            $stmt->execute([
                'e' => $eventId,
                'l1' => $lookup($locationMap, $item['locationId1'], 'location'),
                'l2' => $lookup($locationMap, $item['locationId2'], 'location'),
                'p' => $item['pause'],
            ]);
        }

        $stmt = $pdo->prepare('INSERT INTO registrations (event_id, participant_id, eventtype_id) VALUES (:e, :p, :t)');
        foreach ($data['registrations'] as $item) {
            $stmt->execute([
                'e' => $eventId,
                'p' => $lookup($participantMap, $item['participantId'], 'participant'),
                't' => $lookup($eventTypeMap, $item['eventTypeId'], 'event type'),
            ]);
        }

        $stmt = $pdo->prepare(
            'INSERT INTO eventconstraint (event_id, constrainttype, object_id, starttime, endtime) VALUES (:e, :c, :o, :s, :en)'
        );
        foreach ($data['constraints'] as $item) {
            $type = $item['constraintType'][0];
            $objectId = match ($type) {
                'L' => $lookup($locationMap, $item['objectId'], 'location'),
                'G' => $lookup($groupMap, $item['objectId'], 'group'),
                'C' => $lookup($participantMap, $item['objectId'], 'participant'),
                'T' => $lookup($eventTypeMap, $item['objectId'], 'event type'),
                default => throw new \UnexpectedValueException("Unknown constraint type: {$type}"),
            };
            $stmt->execute([
                'e' => $eventId,
                'c' => $type,
                'o' => $objectId,
                's' => DotNetTime::toDb($item['startTime']),
                'en' => DotNetTime::toDb($item['endTime']),
            ]);
        }
    }
}
