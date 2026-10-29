<?php

declare(strict_types=1);

namespace ScheduleLogic\Services;

use ScheduleLogic\Core\Database;
use ScheduleLogic\Core\HttpClient;
use ScheduleLogic\Core\Logger;
use ScheduleLogic\Repositories\UserRepository;

/**
 * Admin felület logikája (a C# AdminService megfelelője).
 */
final class AdminService
{
    public function __construct(
        private readonly UserRepository $users,
        private readonly Database $db,
        private readonly HttpClient $solver,
        private readonly Logger $logger,
    ) {
    }

    public function getStats(): array
    {
        $dbAvailable = false;
        $userCount = 0;
        try {
            $dbAvailable = $this->db->canConnect(3);
            $userCount = $dbAvailable ? $this->users->count() : 0;
        } catch (\Throwable $ex) {
            $this->logger->warning('Admin health check: az adatbázis nem érhető el.', [], $ex);
            $dbAvailable = false;
        }

        return [
            'userCount' => $userCount,
            'databaseAvailable' => $dbAvailable,
            'solverAvailable' => $this->checkSolverAvailable(),
        ];
    }

    /**
     * A solvernek nincs /health végpontja, ezért azt nézzük, fogad-e
     * kapcsolatot: bármilyen HTTP válasz (akár 404) azt jelenti, hogy fut.
     */
    private function checkSolverAvailable(): bool
    {
        try {
            $this->solver->request('GET', '', null, 3);
            return true;
        } catch (\Throwable $ex) {
            $this->logger->warning('Admin health check: a solver nem érhető el.', [], $ex);
            return false;
        }
    }

    public function searchUsers(?string $query): array
    {
        return $this->users->search($query);
    }

    public function confirmUser(int $userId): bool
    {
        return $this->users->confirmById($userId);
    }

    public function deleteUser(int $userId): void
    {
        $this->users->delete($userId);
    }
}
