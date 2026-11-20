using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
using ScheduleLogic.Server.Models;
using ScheduleLogic.Server.Services.Interfaces;
using ScheduleLogic.Server.Services.Security;
using System.Net;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using static ScheduleLogic.Server.Class.AuthenticationModels;

namespace ScheduleLogic.Server.Services
{
    public class AuthenticationService : IAuthenticationService
    {
        private readonly IUserRepository _users;
        private readonly IEmailSender _email;
        private readonly IPasswordHasher<User> _passwordHasher;
        private readonly IConfiguration _configuration;
        private readonly ILogger<AuthenticationService> _logger;
        private readonly IHostEnvironment _environment;
        private readonly ServerSideTicketStore _sessions;

        private static readonly Lazy<string> DummyHash = new(() =>
            new PasswordHasher<User>().HashPassword(new User(), Convert.ToHexString(RandomNumberGenerator.GetBytes(16))));

        public AuthenticationService(
            IUserRepository users,
            IEmailSender email,
            IPasswordHasher<User> passwordHasher,
            IConfiguration configuration,
            ILogger<AuthenticationService> logger,
            IHostEnvironment environment,
            ServerSideTicketStore sessions)
        {
            _users = users;
            _email = email;
            _passwordHasher = passwordHasher;
            _configuration = configuration;
            _logger = logger;
            _environment = environment;
            _sessions = sessions;
        }

        private bool SkipEmailConfirmation =>
            _environment.IsDevelopment() &&
            _configuration.GetValue<bool>("DevOptions:SkipEmailConfirmation");

        public async Task<User?> LoginUser(string usernameOrEmail, string password)
        {
            var user = await _users.GetByUsernameOrEmailAsync(usernameOrEmail);

            if (user == null)
            {
                // Hash anyway for unknown users so response time does not reveal whether the account exists.
                _passwordHasher.VerifyHashedPassword(new User(), DummyHash.Value, password);
                return null;
            }

            var verification = _passwordHasher.VerifyHashedPassword(user, user.Password, password);

            if (verification == PasswordVerificationResult.Failed || user.Validated != "")
                return null;

            if (verification == PasswordVerificationResult.SuccessRehashNeeded)
                await _users.SetPasswordAsync(user.UserId, _passwordHasher.HashPassword(user, password));

            return user;
        }

        public Task<bool> ConfirmEmailAsync(string token) =>
            _users.ConfirmEmailAsync(token);

        public async Task DeleteAccountAsync(long userId)
        {
            await _users.DeleteAsync(userId);
            _sessions.RemoveAllForUser(userId);
        }

        public async Task<RegisterResult> RegisterUser(string username, string password, string email)
        {
            username = (username ?? "").Trim();
            email = (email ?? "").Trim();
            password ??= "";

            var invalid = ValidateRegistration(username, password, email);
            if (invalid != null)
                return new RegisterResult { Success = false, Error = invalid };

            if (await _users.UsernameExistsAsync(username))
                return new RegisterResult { Success = false, Error = "Username already taken!" };

            if (await _users.EmailExistsAsync(email))
                return new RegisterResult { Success = false, Error = "Email already taken!" };

            var user = new User
            {
                Username = username,
                Email = email
            };
            user.Password = _passwordHasher.HashPassword(user, password);

            if (SkipEmailConfirmation)
            {
                user.Validated = "";
                var conflict = await TryAddAsync(user);
                if (conflict != null) return new RegisterResult { Success = false, Error = conflict };
                _logger.LogInformation("DevOptions:SkipEmailConfirmation - {Username} activated without confirmation.", username);
                return new RegisterResult { Success = true, EmailSent = true };
            }

            user.Validated = GenerateValidationToken();

            var addConflict = await TryAddAsync(user);
            if (addConflict != null) return new RegisterResult { Success = false, Error = addConflict };

            bool emailSent = await TrySendValidationEmail(user.Validated, email, username);

            return new RegisterResult { Success = true, EmailSent = emailSent };
        }

        public async Task<bool> ResendConfirmationEmailAsync(string usernameOrEmail)
        {
            var user = await _users.GetByUsernameOrEmailAsync(usernameOrEmail);

            if (user == null || user.Validated == "")
                return false;

            return await TrySendValidationEmail(user.Validated, user.Email, user.Username);
        }

        public async Task ForgotPasswordAsync(string usernameOrEmail)
        {
            var user = await _users.GetByUsernameOrEmailAsync(usernameOrEmail);
            if (user == null) return;

            string token = GenerateToken();
            var expiresUtc = DateTime.UtcNow.AddHours(1);

            await _users.SetPasswordResetTokenAsync(user.UserId, token, expiresUtc);
            await TrySendPasswordResetEmail(token, user.Email, user.Username);
        }

        public async Task<bool> ResetPasswordAsync(string token, string newPassword)
        {
            var user = await _users.GetByValidPasswordResetTokenAsync(token);
            if (user == null) return false;

            var hashed = _passwordHasher.HashPassword(user, newPassword);
            await _users.SetPasswordAsync(user.UserId, hashed);

            _sessions.RemoveAllForUser(user.UserId);
            return true;
        }

        private static readonly Regex ControlChars = new(@"[\x00-\x1F\x7F]", RegexOptions.Compiled);

                public static string? ValidateRegistration(string username, string password, string email)
        {
            if (username.Length == 0 || username.Length > 255 || ControlChars.IsMatch(username))
                return "Username must be 1-255 characters long!";
            if (email.Length > 255 || !IsValidEmail(email))
                return "Invalid email address!";
            if (password.Length < PasswordPolicy.MinLength)
                return $"Password must be at least {PasswordPolicy.MinLength} characters.";
            if (password.Length > PasswordPolicy.MaxLength)
                return "Password is too long.";
            return null;
        }

        private static bool IsValidEmail(string email)
        {
            if (!MailAddress.TryCreate(email, out var parsed) || parsed.Address != email)
                return false;
            var domain = parsed.Host;
            return domain.Contains('.') && !domain.StartsWith('.') && !domain.EndsWith('.');
        }

                private async Task<string?> TryAddAsync(User user)
        {
            try
            {
                await _users.AddAsync(user);
                return null;
            }
            catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } pg)
            {
                return (pg.ConstraintName ?? "").Contains("email", StringComparison.OrdinalIgnoreCase)
                    ? "Email already taken!"
                    : "Username already taken!";
            }
        }

        private static string GenerateToken() =>
            Convert.ToHexString(RandomNumberGenerator.GetBytes(32));

        private static string GenerateValidationToken() => GenerateToken();

        private async Task<bool> TrySendValidationEmail(string token, string email, string username)
        {
            string publicUrl = _configuration["AppSettings:PublicUrl"] ?? "https://schedulelogic.hu";
            string confirmationLink = $"{publicUrl}/api/authentication/confirm?token={Uri.EscapeDataString(token)}";

            string safeUsername = WebUtility.HtmlEncode(username);

            string body = $@"
                <h2>Welcome {safeUsername}!</h2>
                <p>Thanks for signing up!</p>

                <p>Please confirm your email address by clicking the button below:</p>

                <a href=""{confirmationLink}""
                   style=""display:inline-block;padding:10px 20px;background:#4CAF50;color:white;text-decoration:none;border-radius:5px;"">
                   Confirm Email
                </a>

                <p>If you did not sign up, you can safely ignore this email.</p>
            ";

            try
            {
                await _email.SendAsync(email, "Email confirmation required - ScheduleLogic", body);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to send confirmation e-mail: {Email}", email);
                return false;
            }
        }

        private async Task<bool> TrySendPasswordResetEmail(string token, string email, string username)
        {
            string publicUrl = _configuration["AppSettings:PublicUrl"] ?? "https://schedulelogic.hu";
            string resetLink = $"{publicUrl}/reset-password?token={Uri.EscapeDataString(token)}";

            string safeUsername = WebUtility.HtmlEncode(username);

            string body = $@"
                <h2>Password reset requested</h2>
                <p>Hi {safeUsername},</p>

                <p>We received a request to reset your password. This link is valid for 1 hour:</p>

                <a href=""{resetLink}""
                   style=""display:inline-block;padding:10px 20px;background:#4CAF50;color:white;text-decoration:none;border-radius:5px;"">
                   Reset password
                </a>

                <p>If you did not request this, you can safely ignore this email - your password will not change.</p>
            ";

            try
            {
                await _email.SendAsync(email, "Password reset - ScheduleLogic", body);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to send password reset e-mail: {Email}", email);
                return false;
            }
        }

    }
}