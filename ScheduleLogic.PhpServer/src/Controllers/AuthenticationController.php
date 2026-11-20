<?php

declare(strict_types=1);

namespace ScheduleLogic\Controllers;

use ScheduleLogic\App;
use ScheduleLogic\Core\Request;
use ScheduleLogic\Core\Response;
use ScheduleLogic\Models\InputReader;

final class AuthenticationController
{
    public function __construct(private readonly App $app)
    {
    }

    public function login(Request $request): Response
    {
        $this->app->rateLimit($request, 'Login', 20, 300);

        $body = $request->requireJsonBody();
        $in = new InputReader();
        $username = $in->requiredString($body, 'Username', 'Username');
        $password = $in->requiredString($body, 'Password', 'Password');
        $in->throwIfInvalid();

        $user = $this->app->authService()->loginUser($username, $password);
        if ($user === null) {
            return Response::text('Invalid username/email and password or not verified email!', 401);
        }

        $this->app->userAuth->signIn($request, [
            'userId' => (int)$user['user_id'],
            'name' => $user['username'],
        ]);
        return Response::ok();
    }

    public function register(Request $request): Response
    {
        $this->app->rateLimit($request, 'Register', 5, 900);

        $body = $request->requireJsonBody();
        $in = new InputReader();
        $username = $in->requiredString($body, 'Username', 'Username');
        $password = $in->requiredString($body, 'Password', 'Password');
        $email = $in->requiredString($body, 'Email', 'Email');
        $in->throwIfInvalid();

        $result = $this->app->authService()->registerUser($username, $password, $email);
        if (!$result['success']) {
            return Response::text((string)$result['error'], 400);
        }
        return Response::json(['emailSent' => $result['emailSent']]);
    }

    public function resendConfirmation(Request $request): Response
    {
        $this->app->rateLimit($request, 'EmailSending', 5, 900);

        $body = $request->requireJsonBody();
        $value = (new InputReader())->optionalString($body, 'UsernameOrEmail', 'UsernameOrEmail', '') ?? '';

        if ($value !== '') {
            $this->app->authService()->resendConfirmationEmail($value);
        }
        return Response::ok();
    }

    public function forgotPassword(Request $request): Response
    {
        $this->app->rateLimit($request, 'EmailSending', 5, 900);

        $body = $request->requireJsonBody();
        $value = (new InputReader())->optionalString($body, 'UsernameOrEmail', 'UsernameOrEmail', '') ?? '';

        if ($value !== '') {
            $this->app->authService()->forgotPassword($value);
        }
        return Response::ok();
    }

    public function resetPassword(Request $request): Response
    {
        $this->app->rateLimit($request, 'ResetPassword', 10, 900);

        $body = $request->requireJsonBody();
        $in = new InputReader();
        $token = $in->optionalString($body, 'Token', 'Token', '') ?? '';
        $newPassword = $in->optionalString($body, 'NewPassword', 'NewPassword', '') ?? '';
        $in->throwIfInvalid();

        if (trim($newPassword) === '' || strlen($newPassword) < 8) {
            return Response::text('Password must be at least 8 characters.', 400);
        }

        $success = $this->app->authService()->resetPassword($token, $newPassword);
        return $success ? Response::ok() : Response::text('Invalid or expired reset link.', 400);
    }

    public function checkToken(Request $request): Response
    {
        $this->app->requireUserId($request);
        $identity = $this->app->userAuth->identity($request);
        return Response::json(['name' => $identity['name'] ?? null]);
    }

    public function confirmEmail(Request $request): Response
    {
        $token = $request->query('token') ?? '';
        if ($token === '') {
            return Response::text('Invalid confirmation request.', 400);
        }

        $confirmed = $this->app->authService()->confirmEmail($token);
        $frontendUrl = $this->app->config->string('URLS:Frontend');
        return Response::redirect($confirmed ? $frontendUrl : $frontendUrl . '?confirm=failed');
    }

    public function deleteAccount(Request $request): Response
    {
        $userId = $this->app->requireUserId($request);
        $this->app->authService()->deleteAccount($userId);
        $this->app->userAuth->signOut($request);
        return Response::ok();
    }

    public function logout(Request $request): Response
    {
        $this->app->requireUserId($request);
        $this->app->userAuth->signOut($request);
        return Response::ok();
    }
}
