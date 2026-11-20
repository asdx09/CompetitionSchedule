<?php

declare(strict_types=1);

namespace ScheduleLogic\Services;

final class SolverStatus
{
    public const QUEUED = 'QUEUED';
    public const RUNNING = 'RUNNING';
    public const OPTIMAL = 'OPTIMAL';
    public const FEASIBLE = 'FEASIBLE';
    public const INFEASIBLE = 'INFEASIBLE';
    public const UNKNOWN = 'UNKNOWN';
    public const MODEL_INVALID = 'MODEL_INVALID';
    public const STOPPED = 'STOPPED';
    public const MEMORY_LIMIT = 'MEMORY_LIMIT';
    public const ERROR = 'ERROR';

    private const FINAL = [
        self::OPTIMAL, self::FEASIBLE, self::INFEASIBLE, self::UNKNOWN,
        self::MODEL_INVALID, self::STOPPED, self::MEMORY_LIMIT, self::ERROR,
    ];

    public static function fromSolver(?string $status): string
    {
        $s = strtoupper(trim($status ?? ''));
        if ($s === 'PARTIAL_SOLUTION') {
            return self::RUNNING;
        }
        return in_array($s, self::FINAL, true) ? $s : self::ERROR;
    }
}
