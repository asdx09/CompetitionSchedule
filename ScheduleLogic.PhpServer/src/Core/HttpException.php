<?php

declare(strict_types=1);

namespace ScheduleLogic\Core;

/**
 * Kontrollerekből dobható kivétel, ami közvetlenül HTTP válasszá alakul.
 * A Response-t tartalmazza, így bármilyen (szöveges, JSON, üres) válasz lehet.
 */
class HttpException extends \RuntimeException
{
    public function __construct(public readonly Response $response)
    {
        parent::__construct('HTTP ' . $response->status);
    }
}
