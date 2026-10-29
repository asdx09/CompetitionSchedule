<?php

declare(strict_types=1);

namespace ScheduleLogic\Core;

/** A KeyNotFoundException megfelelője: a hibakezelő 404-et csinál belőle. */
final class NotFoundException extends \RuntimeException
{
}
