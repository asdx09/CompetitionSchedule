<?php

declare(strict_types=1);

namespace ScheduleLogic\Models;

/**
 * A C# EventModels.DataDTO beolvasása a kérés törzséből (save-event, new-wizard).
 *
 * A visszaadott tömb szerkezete megegyezik a DataDTO-val, csak a dátumok
 * már UTC DateTimeImmutable-ök, az időtartamok pedig normalizált
 * "HH:mm:ss" stringek.
 */
final class EventInput
{
    public static function parse(array $body): array
    {
        $in = new InputReader();

        $ev = $in->object($body, 'EventData', 'EventData');
        $eventData = [
            'eventId' => $in->requiredString($ev, 'EventId', 'EventData.EventId'),
            'eventName' => $in->requiredString($ev, 'EventName', 'EventData.EventName'),
            'startDate' => self::requiredDate($in, $ev, 'StartDate', 'EventData.StartDate'),
            'endDate' => self::requiredDate($in, $ev, 'EndDate', 'EventData.EndDate'),
            'isPrivate' => $in->bool($ev, 'IsPrivate', 'EventData.IsPrivate'),
            'basePauseTime' => $in->int($ev, 'BasePauseTime', 'EventData.BasePauseTime'),
            'locationPauseTime' => $in->int($ev, 'LocationPauseTime', 'EventData.LocationPauseTime'),
            'locWeight' => $in->int($ev, 'LocWeight', 'EventData.LocWeight'),
            'groupWeight' => $in->int($ev, 'GroupWeight', 'EventData.GroupWeight'),
            'typeWeight' => $in->int($ev, 'TypeWeight', 'EventData.TypeWeight'),
            'compWeight' => $in->int($ev, 'CompWeight', 'EventData.CompWeight'),
        ];

        $result = ['eventData' => $eventData];

        $result['eventTypes'] = self::mapList($in, $body, 'EventTypes', static fn(array $r, string $p) => [
            'eventTypeId' => $in->requiredString($r, 'EventTypeId', "$p.EventTypeId"),
            'eventId' => $in->requiredString($r, 'EventId', "$p.EventId"),
            'typeName' => $in->requiredString($r, 'TypeName', "$p.TypeName"),
            'timeRange' => $in->timeOnly($r, 'TimeRange', "$p.TimeRange"),
        ]);

        $result['groups'] = self::mapList($in, $body, 'Groups', static fn(array $r, string $p) => [
            'groupId' => $in->requiredString($r, 'GroupId', "$p.GroupId"),
            'eventId' => $in->requiredString($r, 'EventId', "$p.EventId"),
            'groupName' => $in->requiredString($r, 'GroupName', "$p.GroupName"),
        ]);

        $result['locations'] = self::mapList($in, $body, 'Locations', static fn(array $r, string $p) => [
            'locationId' => $in->requiredString($r, 'LocationId', "$p.LocationId"),
            'eventId' => $in->requiredString($r, 'EventId', "$p.EventId"),
            'locationName' => $in->requiredString($r, 'LocationName', "$p.LocationName"),
            'slots' => $in->int($r, 'Slots', "$p.Slots"),
        ]);

        $result['participants'] = self::mapList($in, $body, 'Participants', static fn(array $r, string $p) => [
            'participantId' => $in->requiredString($r, 'ParticipantId', "$p.ParticipantId"),
            'competitorNumber' => $in->int($r, 'CompetitorNumber', "$p.CompetitorNumber"),
            'participantName' => $in->requiredString($r, 'ParticipantName', "$p.ParticipantName"),
            'eventId' => $in->requiredString($r, 'EventId', "$p.EventId"),
            'groupId' => $in->optionalString($r, 'GroupId', "$p.GroupId"),
        ]);

        $result['registrations'] = self::mapList($in, $body, 'Registrations', static fn(array $r, string $p) => [
            'registrationId' => $in->requiredString($r, 'RegistrationId', "$p.RegistrationId"),
            'eventId' => $in->requiredString($r, 'EventId', "$p.EventId"),
            'participantId' => $in->requiredString($r, 'ParticipantId', "$p.ParticipantId"),
            'eventTypeId' => $in->requiredString($r, 'EventTypeId', "$p.EventTypeId"),
        ]);

        $result['pauseTable'] = self::mapList($in, $body, 'PauseTable', static fn(array $r, string $p) => [
            'pauseId' => $in->requiredString($r, 'PauseId', "$p.PauseId"),
            'eventId' => $in->requiredString($r, 'EventId', "$p.EventId"),
            'locationId1' => $in->requiredString($r, 'LocationId1', "$p.LocationId1"),
            'locationId2' => $in->requiredString($r, 'LocationId2', "$p.LocationId2"),
            'pause' => $in->timeOnly($r, 'Pause', "$p.Pause"),
        ]);

        $result['locationTable'] = self::mapList($in, $body, 'LocationTable', static fn(array $r, string $p) => [
            'locationTableId' => $in->requiredString($r, 'LocationTableId', "$p.LocationTableId"),
            'eventId' => $in->requiredString($r, 'EventId', "$p.EventId"),
            'eventTypeId' => $in->requiredString($r, 'EventTypeId', "$p.EventTypeId"),
            'locationId' => $in->requiredString($r, 'LocationId', "$p.LocationId"),
        ]);

        $result['constraints'] = self::mapList($in, $body, 'Constraints', static fn(array $r, string $p) => [
            'constraintId' => $in->requiredString($r, 'ConstraintId', "$p.ConstraintId"),
            'eventId' => $in->requiredString($r, 'EventId', "$p.EventId"),
            'objectId' => $in->requiredString($r, 'ObjectId', "$p.ObjectId"),
            'constraintType' => $in->requiredString($r, 'ConstraintType', "$p.ConstraintType"),
            'startTime' => self::requiredDate($in, $r, 'StartTime', "$p.StartTime"),
            'endTime' => self::requiredDate($in, $r, 'EndTime', "$p.EndTime"),
        ]);

        // A TimeZones listát a kliens visszaküldi, de a mentés nem használja.

        $in->throwIfInvalid();
        return $result;
    }

    private static function mapList(InputReader $in, array $body, string $field, callable $mapper): array
    {
        $out = [];
        foreach ($in->list($body, $field, $field) as $i => $row) {
            $path = "{$field}[{$i}]";
            if (!is_array($row) || (array_is_list($row) && $row !== [])) {
                $in->addError($path, "The JSON value could not be converted to an object. Path: \$.{$path}");
                continue;
            }
            $out[] = $mapper($row, $path);
        }
        return $out;
    }

    private static function requiredDate(InputReader $in, array $data, string $field, string $path): ?\DateTimeImmutable
    {
        $value = $in->dateTime($data, $field, $path);
        if ($value === null && !array_key_exists(strtolower($field), $data)) {
            $in->addError($path, "The {$field} field is required.");
        }
        return $value;
    }
}
