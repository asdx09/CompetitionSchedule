<?php

declare(strict_types=1);

namespace ScheduleLogic\Core;

class HttpException extends \RuntimeException
{
    public function __construct(public readonly Response $response)
    {
        parent::__construct('HTTP ' . $response->status);
    }
}
