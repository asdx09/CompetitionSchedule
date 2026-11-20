<?php

declare(strict_types=1);

namespace ScheduleLogic\Services;

use ScheduleLogic\Core\Config;
use ScheduleLogic\Core\DotNetTime;
use ScheduleLogic\Core\Logger;
use ScheduleLogic\Repositories\UserRepository;

final class AuthenticationService
{
    public function __construct(
        private readonly UserRepository $users,
        private readonly SmtpEmailSender $email,
        private readonly PasswordHasher $hasher,
        private readonly Config $config,
        private readonly Logger $logger,
    ) {
    }

    public function loginUser(string $usernameOrEmail, string $password): ?array
    {
        $user = $this->users->getByUsernameOrEmail($usernameOrEmail);

        if ($user === null) {
            $this->hasher->verify($this->dummyHash(), $password);
            return null;
        }

        $result = $this->hasher->verify($user['password'], $password);
        if ($result === PasswordHasher::FAILED || $user['validated'] !== '') {
            return null;
        }
        if ($result === PasswordHasher::SUCCESS_REHASH_NEEDED) {
            $this->users->updatePasswordHash((int)$user['user_id'], $this->hasher->hash($password));
        }
        return $user;
    }

    public function confirmEmail(string $token): bool
    {
        return $this->users->confirmEmail($token);
    }

    public function deleteAccount(int $userId): void
    {
        $this->users->delete($userId);
    }

    public function registerUser(string $username, string $password, string $email): array
    {
        $username = trim($username);
        $email = trim($email);

        if ($username === '' || self::charLength($username) > 255 || preg_match('/[\x00-\x1F\x7F]/', $username)) {
            return self::fail('Username must be 1-255 characters long!');
        }
        if (self::charLength($email) > 255 || filter_var($email, FILTER_VALIDATE_EMAIL) === false) {
            return self::fail('Invalid email address!');
        }
        if (strlen($password) < 8) {
            return self::fail('Password must be at least 8 characters.');
        }
        if (strlen($password) > 1024) {
            return self::fail('Password is too long.');
        }

        if ($this->users->usernameExists($username)) {
            return self::fail('Username already taken!');
        }
        if ($this->users->emailExists($email)) {
            return self::fail('Email already taken!');
        }

        $skipConfirmation = $this->skipEmailConfirmation();
        $token = $skipConfirmation ? '' : self::generateToken();
        try {
            $this->users->add($username, $email, $this->hasher->hash($password), $token);
        } catch (\PDOException $e) {
            if (UserRepository::isUniqueViolation($e)) {
                return self::fail(str_contains($e->getMessage(), 'email') ? 'Email already taken!' : 'Username already taken!');
            }
            throw $e;
        }

        if ($skipConfirmation) {
            $this->logger->info('DevOptions:SkipEmailConfirmation - {Username} activated without confirmation.', ['Username' => $username]);
            return ['success' => true, 'error' => null, 'emailSent' => true];
        }

        $emailSent = $this->trySendValidationEmail($token, $email, $username);

        return ['success' => true, 'error' => null, 'emailSent' => $emailSent];
    }

    public function resendConfirmationEmail(string $usernameOrEmail): bool
    {
        $user = $this->users->getByUsernameOrEmail($usernameOrEmail);
        if ($user === null || $user['validated'] === '') {
            return false;
        }
        return $this->trySendValidationEmail($user['validated'], $user['email'], $user['username']);
    }

    public function forgotPassword(string $usernameOrEmail): void
    {
        $user = $this->users->getByUsernameOrEmail($usernameOrEmail);
        if ($user === null) {
            return;
        }
        $token = self::generateToken();
        $this->users->setPasswordResetToken((int)$user['user_id'], $token, DotNetTime::nowUtc()->modify('+1 hour'));
        $this->trySendPasswordResetEmail($token, $user['email'], $user['username']);
    }

    public function resetPassword(string $token, string $newPassword): bool
    {
        $user = $this->users->getByValidPasswordResetToken($token);
        if ($user === null) {
            return false;
        }
        $this->users->setPassword((int)$user['user_id'], $this->hasher->hash($newPassword));
        return true;
    }

    private function skipEmailConfirmation(): bool
    {
        return $this->config->isDevelopment()
            && $this->config->bool('DevOptions:SkipEmailConfirmation');
    }

    private static function generateToken(): string
    {
        return strtoupper(bin2hex(random_bytes(32)));
    }

    private static function charLength(string $s): int
    {
        return preg_match('//u', $s) ? (int)preg_match_all('/./su', $s) : strlen($s);
    }

    private static function fail(string $error): array
    {
        return ['success' => false, 'error' => $error, 'emailSent' => false];
    }

    private function publicUrl(): string
    {
        return rtrim($this->config->string('AppSettings:PublicUrl', 'https://schedulelogic.hu'), '/');
    }

    private function trySendValidationEmail(string $token, string $email, string $username): bool
    {
        $link = $this->publicUrl() . '/api/authentication/confirm?token=' . rawurlencode($token);
        $safeUsername = htmlspecialchars($username, ENT_QUOTES | ENT_HTML5, 'UTF-8');
        $safeLink = htmlspecialchars($link, ENT_QUOTES | ENT_HTML5, 'UTF-8');

        $body = <<<HTML
                <h2>Welcome {$safeUsername}!</h2>
                <p>Thanks for signing up!</p>

                <p>Please confirm your email address by clicking the button below:</p>

                <a href="{$safeLink}"
                   style="display:inline-block;padding:10px 20px;background:#4CAF50;color:white;text-decoration:none;border-radius:5px;">
                   Confirm Email
                </a>

                <p>If you did not sign up, you can safely ignore this email.</p>
            HTML;

        try {
            $this->email->send($email, 'Email confirmation required - ScheduleLogic', $body);
            return true;
        } catch (\Throwable $ex) {
            $this->logger->warning('Failed to send confirmation e-mail: {Email}', ['Email' => $email], $ex);
            return false;
        }
    }

    private function trySendPasswordResetEmail(string $token, string $email, string $username): bool
    {
        $link = $this->publicUrl() . '/reset-password?token=' . rawurlencode($token);
        $safeUsername = htmlspecialchars($username, ENT_QUOTES | ENT_HTML5, 'UTF-8');
        $safeLink = htmlspecialchars($link, ENT_QUOTES | ENT_HTML5, 'UTF-8');

        $body = <<<HTML
                <h2>Password reset requested</h2>
                <p>Hi {$safeUsername},</p>

                <p>We received a request to reset your password. This link is valid for 1 hour:</p>

                <a href="{$safeLink}"
                   style="display:inline-block;padding:10px 20px;background:#4CAF50;color:white;text-decoration:none;border-radius:5px;">
                   Reset password
                </a>

                <p>If you did not request this, you can safely ignore this email - your password will not change.</p>
            HTML;

        try {
            $this->email->send($email, 'Password reset - ScheduleLogic', $body);
            return true;
        } catch (\Throwable $ex) {
            $this->logger->warning('Failed to send password reset e-mail: {Email}', ['Email' => $email], $ex);
            return false;
        }
    }

    private function dummyHash(): string
    {
        static $hash = null;
        return $hash ??= $this->hasher->hash(bin2hex(random_bytes(16)));
    }
}
