<?php

declare(strict_types=1);

namespace ScheduleLogic\Services;

use ScheduleLogic\Core\Database;
use ScheduleLogic\Core\HttpClient;
use ScheduleLogic\Core\Logger;
use ScheduleLogic\Repositories\UserRepository;

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
            $this->logger->warning('Admin health check: the database is not reachable.', [], $ex);
            $dbAvailable = false;
        }

        return [
            'userCount' => $userCount,
            'databaseAvailable' => $dbAvailable,
            'solverAvailable' => $this->checkSolverAvailable(),
        ];
    }

    private function checkSolverAvailable(): bool
    {
        try {
            $this->solver->request('GET', '', null, 3);
            return true;
        } catch (\Throwable $ex) {
            $this->logger->warning('Admin health check: the solver is not reachable.', [], $ex);
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
