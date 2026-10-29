<?php

declare(strict_types=1);

namespace ScheduleLogic\Repositories;

use PDO;
use ScheduleLogic\Core\Database;
use ScheduleLogic\Core\DotNetTime;

/**
 * Ütemezéssel kapcsolatos adatbázis-műveletek (a C# ScheduleRepository megfelelője).
 */
final class ScheduleRepository
{
    /** A C# kódban is 1: minden idő percben értendő. */
    private const MINUTE_CONVERSION = 1;

    /** Két egymást követő foglalás között ennyi perc kell, hogy ugyanazt a slotot kapják. */
    private const SLOT_GAP_MINUTES = 5;

    public function __construct(private readonly Database $db)
    {
    }

    /**
     * A solvernek küldött kérés összeállítása (ScheduleRequestForSolver).
     * A JSON kulcsok camelCase-ek, ahogy a .NET PostAsJsonAsync küldi őket,
     * és ahogy a Python oldali pydantic modell (models.py) várja.
     */
    public function getScheduleInfo(int $eventId): array
    {
        $pdo = $this->db->pdo();
        $evt = $this->fetchOne($pdo, 'SELECT * FROM event WHERE event_id = :e', $eventId);
        if ($evt === null) {
            throw new \RuntimeException("Event {$eventId} not found.");
        }
        $start = DotNetTime::fromDb($evt['startdate']);
        $end = DotNetTime::fromDb($evt['enddate']);

        $locations = array_map(static fn($l) => [
            'id' => (int)$l['location_id'],
            'name' => $l['locationname'],
            'capacity' => (int)$l['slots'],
        ], $this->fetchAll($pdo, 'SELECT * FROM location WHERE event_id = :e ORDER BY location_id', $eventId));

        $eventTypes = $this->fetchAll($pdo, 'SELECT * FROM eventtype WHERE event_id = :e ORDER BY eventtype_id', $eventId);
        $locationTables = $this->fetchAll(
            $pdo,
            'SELECT lt.* FROM locationtable lt JOIN eventtype et ON et.eventtype_id = lt.eventtype_id
              WHERE et.event_id = :e ORDER BY lt.locationtable_id',
            $eventId
        );

        $events = array_map(static fn($t) => [
            'id' => (int)$t['eventtype_id'],
            'name' => $t['typename'],
            'duration' => intdiv(DotNetTime::timeOnlyToMinutes($t['timerange']), self::MINUTE_CONVERSION),
            'possibleLocations' => array_values(array_map(
                static fn($lt) => (int)$lt['location_id'],
                array_filter($locationTables, static fn($lt) => (int)$lt['eventtype_id'] === (int)$t['eventtype_id'])
            )),
        ], $eventTypes);

        $competitors = array_map(static fn($p) => [
            'id' => (int)$p['participant_id'],
            'name' => $p['participantname'],
            'groupId' => $p['group_id'] === null ? -1 : (int)$p['group_id'],
        ], $this->fetchAll($pdo, 'SELECT * FROM participant WHERE event_id = :e ORDER BY participant_id', $eventId));

        $entries = array_map(static fn($r) => [
            'id' => (int)$r['registration_id'],
            'competitorId' => (int)$r['participant_id'],
            'eventId' => (int)$r['eventtype_id'],
        ], $this->fetchAll($pdo, 'SELECT * FROM registrations WHERE event_id = :e ORDER BY registration_id', $eventId));

        $travel = array_map(static fn($p) => [
            'id' => (int)$p['pause_id'],
            'locationID1' => (int)$p['location_id1'],
            'locationID2' => (int)$p['location_id2'],
            'pause' => DotNetTime::timeOnlyToMinutes($p['pause']),
        ], $this->fetchAll($pdo, 'SELECT * FROM pausetable WHERE event_id = :e ORDER BY pause_id', $eventId));

        // (int)TimeSpan.TotalMinutes: nulla felé csonkol
        $minutesFromStart = static fn(string $ts) => (int)((
            (float)DotNetTime::fromDb($ts)->format('U.u') - (float)$start->format('U.u')
        ) / 60);

        $constraints = array_map(static fn($c) => [
            'id' => (int)$c['constraint_id'],
            'objectId' => (int)$c['object_id'],
            'constraintType' => trim($c['constrainttype']),
            'startTime' => intdiv($minutesFromStart($c['starttime']), self::MINUTE_CONVERSION),
            'endTime' => intdiv($minutesFromStart($c['endtime']), self::MINUTE_CONVERSION),
        ], $this->fetchAll($pdo, 'SELECT * FROM eventconstraint WHERE event_id = :e ORDER BY constraint_id', $eventId));

        // (Enddate - Startdate).Days + 1  (a TimeSpan.Days egész napokra csonkol)
        $totalSeconds = (float)$end->format('U.u') - (float)$start->format('U.u');
        $maxDays = (int)($totalSeconds / 86400) + 1;

        return [
            'returnURL' => '',
            'eventId' => $eventId,
            'locations' => $locations,
            'events' => $events,
            'competitors' => $competitors,
            'entries' => $entries,
            'travel' => $travel,
            'constraints' => $constraints,
            'dayLength' => intdiv(24 * 60 - 1, self::MINUTE_CONVERSION),
            'maxDays' => $maxDays,
            'breakTimeLoc' => intdiv((int)$evt['locationpausetime'], self::MINUTE_CONVERSION),
            'basePauseTime' => intdiv((int)$evt['basepausetime'], self::MINUTE_CONVERSION),
            'locWeight' => min(500, (int)$evt['locweight']),
            'groupWeight' => min(500, (int)$evt['groupweight']),
            'typeWeight' => min(500, (int)$evt['typeweight']),
            'compWeight' => min(500, (int)$evt['compweight']),
        ];
    }

    /**
     * A solver (rész)eredményének mentése: a régi beosztás törlődik, az új
     * bekerül, majd minden helyszínen kiosztjuk a slotokat (párhuzamos
     * pályák) időrendben - ugyanazzal az algoritmussal, mint a C# kód.
     *
     * @param list<array{participantId:int,eventTypeId:int,locationId:int,start:int,end:int,slot:int}> $schedule
     */
    public function saveSolverResult(array $schedule, int $eventId): bool
    {
        return $this->db->transaction(function (PDO $pdo) use ($schedule, $eventId): bool {
            $evt = $this->fetchOne($pdo, 'SELECT event_id, startdate FROM event WHERE event_id = :e FOR UPDATE', $eventId);
            if ($evt === null) {
                return false;
            }
            $start = DotNetTime::fromDb($evt['startdate']);

            $pdo->prepare(
                'DELETE FROM schedule WHERE eventtype_id IN (SELECT eventtype_id FROM eventtype WHERE event_id = :e)'
            )->execute(['e' => $eventId]);

            $slotsByLocation = [];
            foreach ($this->fetchAll($pdo, 'SELECT location_id, slots FROM location WHERE event_id = :e', $eventId) as $l) {
                $slotsByLocation[(int)$l['location_id']] = (int)$l['slots'];
            }

            // Sorok előkészítése + időpontok kiszámítása
            $rows = [];
            foreach ($schedule as $i => $item) {
                $rows[] = [
                    'order' => $i,
                    'participantId' => $item['participantId'],
                    'eventTypeId' => $item['eventTypeId'],
                    'locationId' => $item['locationId'],
                    'start' => $start->modify(sprintf('%+d minutes', $item['start'] * self::MINUTE_CONVERSION)),
                    'end' => $start->modify(sprintf('%+d minutes', $item['end'] * self::MINUTE_CONVERSION)),
                    'slot' => $item['slot'],
                ];
            }

            // Slot-kiosztás helyszínenként, kezdési idő szerint (stabil rendezés,
            // mint a LINQ OrderBy).
            $byLocation = [];
            foreach ($rows as $idx => $row) {
                $byLocation[$row['locationId']][] = $idx;
            }
            foreach ($byLocation as $locationId => $indices) {
                if (!isset($slotsByLocation[$locationId])) {
                    continue;
                }
                usort($indices, static fn($a, $b) => [$rows[$a]['start'], $rows[$a]['order']] <=> [$rows[$b]['start'], $rows[$b]['order']]);

                $active = [];
                foreach ($indices as $idx) {
                    $entryStart = $rows[$idx]['start'];
                    $active = array_values(array_filter(
                        $active,
                        static fn($a) => $rows[$a]['end']->modify('+' . self::SLOT_GAP_MINUTES . ' minutes') > $entryStart
                    ));
                    for ($slot = 1; $slot <= $slotsByLocation[$locationId]; $slot++) {
                        $taken = false;
                        foreach ($active as $a) {
                            if ($rows[$a]['slot'] === $slot) {
                                $taken = true;
                                break;
                            }
                        }
                        if (!$taken) {
                            $rows[$idx]['slot'] = $slot;
                            $active[] = $idx;
                            break;
                        }
                    }
                }
            }

            $insert = $pdo->prepare(
                'INSERT INTO schedule (participant_id, starttime, endtime, location_id, slot, eventtype_id)
                 VALUES (:p, :s, :en, :l, :slot, :t)'
            );
            foreach ($rows as $row) {
                $insert->execute([
                    'p' => $row['participantId'],
                    's' => DotNetTime::toDb($row['start']),
                    'en' => DotNetTime::toDb($row['end']),
                    'l' => $row['locationId'],
                    'slot' => $row['slot'],
                    't' => $row['eventTypeId'],
                ]);
            }
            return true;
        });
    }

    /**
     * A beosztás nézet adatai: a DataDTO + a TimeZones lista, ahol a kezdés
     * és befejezés az esemény első napjának helyi éjfélétől számított perc.
     */
    public function getScheduleData(int $eventId): array
    {
        $pdo = $this->db->pdo();
        $evt = $this->fetchOne($pdo, 'SELECT * FROM event WHERE event_id = :e', $eventId);
        if ($evt === null) {
            throw new \RuntimeException("Event {$eventId} not found.");
        }

        // startDate.Date helyi időben (ToLocalTime().Date)
        $localStart = DotNetTime::fromDb($evt['startdate'])->setTimezone(DotNetTime::local());
        $localMidnight = new \DateTimeImmutable($localStart->format('Y-m-d') . ' 00:00:00', DotNetTime::local());

        $timeZones = array_map(static fn($s) => [
            'scheduleId' => (int)$s['schedule_id'],
            'eventTypeId' => (int)$s['eventtype_id'],
            'participantId' => (int)$s['participant_id'],
            'locationId' => (int)$s['location_id'],
            'startTime' => self::roundToInt(DotNetTime::localWallClockMinutes($localMidnight, DotNetTime::fromDb($s['starttime']))),
            'endTime' => self::roundToInt(DotNetTime::localWallClockMinutes($localMidnight, DotNetTime::fromDb($s['endtime']))),
            'slot' => (int)$s['slot'],
        ], $this->fetchAll(
            $pdo,
            'SELECT s.* FROM schedule s JOIN eventtype et ON et.eventtype_id = s.eventtype_id
              WHERE et.event_id = :e ORDER BY s.schedule_id',
            $eventId
        ));

        $data = EventRepository::buildDataDto($pdo, $evt, $timeZones);
        // A C# GetScheduleDataAsync a csoport nélküli résztvevőnél null-t ad
        foreach ($data['participants'] as &$p) {
            if ($p['groupId'] === '') {
                $p['groupId'] = null;
            }
        }
        unset($p);
        return $data;
    }

    /** Az Excel exporthoz szükséges, már feloldott (nevekkel kitöltött) sorok. */
    public function getExportData(int $eventId): ?array
    {
        $pdo = $this->db->pdo();
        $evt = $this->fetchOne($pdo, 'SELECT * FROM event WHERE event_id = :e', $eventId);
        if ($evt === null) {
            return null;
        }

        $rows = $this->fetchAll(
            $pdo,
            "SELECT et.typename,
                    p.participantname, p.competitornumber,
                    COALESCE(g.groupname, '') AS groupname,
                    l.locationname,
                    s.starttime, s.endtime, s.slot
               FROM schedule s
               JOIN eventtype et   ON et.eventtype_id = s.eventtype_id
               JOIN participant p  ON p.participant_id = s.participant_id
               JOIN location l     ON l.location_id = s.location_id
               LEFT JOIN groups g  ON g.group_id = p.group_id
              WHERE et.event_id = :e
              ORDER BY s.schedule_id",
            $eventId
        );

        $local = DotNetTime::local();
        return [
            'eventName' => $evt['eventname'],
            'startDate' => DotNetTime::fromDb($evt['startdate'])->setTimezone($local),
            'endDate' => DotNetTime::fromDb($evt['enddate'])->setTimezone($local),
            'timeZones' => array_map(static fn($r) => [
                'eventType' => $r['typename'],
                'participant' => $r['participantname'] . ' (' . $r['competitornumber'] . ')',
                'groupName' => $r['groupname'],
                'location' => $r['locationname'],
                'startTime' => DotNetTime::fromDb($r['starttime'])->setTimezone($local),
                'endTime' => DotNetTime::fromDb($r['endtime'])->setTimezone($local),
                'slot' => (int)$r['slot'],
            ], $rows),
        ];
    }

    /**
     * A solvertől jött azonosítók ellenőrzéséhez: az esemény saját
     * résztvevői, versenyszámai és helyszínei.
     *
     * @return array{participants: array<int,true>, eventTypes: array<int,true>, locations: array<int,true>}
     */
    public function getEventObjectIds(int $eventId): array
    {
        $pdo = $this->db->pdo();
        $ids = static fn(array $rows, string $col) => array_fill_keys(array_map(static fn($r) => (int)$r[$col], $rows), true);
        return [
            'participants' => $ids($this->fetchAll($pdo, 'SELECT participant_id FROM participant WHERE event_id = :e', $eventId), 'participant_id'),
            'eventTypes' => $ids($this->fetchAll($pdo, 'SELECT eventtype_id FROM eventtype WHERE event_id = :e', $eventId), 'eventtype_id'),
            'locations' => $ids($this->fetchAll($pdo, 'SELECT location_id FROM location WHERE event_id = :e', $eventId), 'location_id'),
        ];
    }

    /** Convert.ToInt32(double): legközelebbi egészre, .5-nél párosra kerekít. */
    private static function roundToInt(float $value): int
    {
        return (int)round($value, 0, PHP_ROUND_HALF_EVEN);
    }

    private function fetchOne(PDO $pdo, string $sql, int $eventId): ?array
    {
        $stmt = $pdo->prepare($sql);
        $stmt->execute(['e' => $eventId]);
        return $stmt->fetch() ?: null;
    }

    private function fetchAll(PDO $pdo, string $sql, int $eventId): array
    {
        $stmt = $pdo->prepare($sql);
        $stmt->execute(['e' => $eventId]);
        return $stmt->fetchAll();
    }
}
