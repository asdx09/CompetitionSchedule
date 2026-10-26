using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using ScheduleLogic.Server.Models;
using ScheduleLogic.Server.Services.Interfaces;
using System.Net;
using System.Security.Cryptography;
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

        public AuthenticationService(
            IUserRepository users,
            IEmailSender email,
            IPasswordHasher<User> passwordHasher,
            IConfiguration configuration,
            ILogger<AuthenticationService> logger)
        {
            _users = users;
            _email = email;
            _passwordHasher = passwordHasher;
            _configuration = configuration;
            _logger = logger;
        }

        // A tényleges bejelentkeztetés (cookie kiállítása) a kontrollerben
        // történik SignInAsync-kal, mert ahhoz HttpContext kell, azt pedig
        // ne ismerje a service réteg. Itt csak a hitelesítő adatokat
        // ellenőrizzük, és a validált usert adjuk vissza.
        public async Task<User?> LoginUser(string usernameOrEmail, string password)
        {
            var user = await _users.GetByUsernameOrEmailAsync(usernameOrEmail);

            // Nincs ilyen felhasználó, vagy még nincs megerősítve az e-mail címe.
            if (user == null || user.Validated != "")
                return null;

            var verification = _passwordHasher.VerifyHashedPassword(user, user.Password, password);
            if (verification != PasswordVerificationResult.Success)
                return null;

            return user;
        }

        public Task<bool> ConfirmEmailAsync(string token) =>
            _users.ConfirmEmailAsync(token);

        public Task DeleteAccountAsync(long userId) =>
            _users.DeleteAsync(userId);

        public async Task<RegisterResult> RegisterUser(string username, string password, string email)
        {
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
            user.Validated = GenerateValidationToken();

            // A fiók innentől létezik, akkor is, ha lent az e-mail küldése
            // elhasal - a token megvan az adatbázisban, a megerősítés a
            // ResendConfirmationEmailAsync-kal bármikor újraküldhető, vagy
            // az admin panelről kézzel jóváhagyható.
            await _users.AddAsync(user);

            bool emailSent = await TrySendValidationEmail(user.Validated, email, username);

            return new RegisterResult { Success = true, EmailSent = emailSent };
        }

        // Ugyanazzal a tokennel küldi újra a levelet, amit a regisztrációkor
        // kapott a felhasználó - nincs újra-generálás, mert a régi link (ha
        // esetleg mégis megérkezett egy korábbi próbálkozásból) érvényben
        // marad, nem lesz kettő párhuzamosan élő token.
        public async Task<bool> ResendConfirmationEmailAsync(string usernameOrEmail)
        {
            var user = await _users.GetByUsernameOrEmailAsync(usernameOrEmail);

            // Nincs ilyen felhasználó, vagy már megerősítette az e-mailjét -
            // egyik esetben sem áruljuk el, melyik volt, hogy ne lehessen
            // ezzel felhasználóneveket/e-maileket találgatni.
            if (user == null || user.Validated == "")
                return false;

            return await TrySendValidationEmail(user.Validated, user.Email, user.Username);
        }

        // A hívó (kontroller) szándékosan mindig sikeres választ ad vissza,
        // hogy a válaszból ne lehessen kitalálni, létezik-e a megadott
        // felhasználó/e-mail - ezért ennek a metódusnak nincs látható jele
        // kifelé, csak akkor csinál bármit, ha tényleg talál usert.
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
            if (user == null) return false; // érvénytelen vagy lejárt token

            var hashed = _passwordHasher.HashPassword(user, newPassword);
            await _users.SetPasswordAsync(user.UserId, hashed);
            return true;
        }

        // Kriptográfiailag erős, nem kitalálható token a Guid.NewGuid() helyett.
        // TODO: a "validated" mezőnek jelenleg nincs lejárata (a jelszó-
        // visszaállítás tokenjének már van, lásd Passwordresetexpires); ha ez
        // az e-mail-megerősítésnél is fontos, ugyanígy egy lejárat-oszlop kell.
        private static string GenerateToken() =>
            Convert.ToHexString(RandomNumberGenerator.GetBytes(32));

        private static string GenerateValidationToken() => GenerateToken();

        private async Task<bool> TrySendValidationEmail(string token, string email, string username)
        {
            string publicUrl = _configuration["AppSettings:PublicUrl"] ?? "https://schedulelogic.hu";
            string confirmationLink = $"{publicUrl}/api/authentication/confirm?token={Uri.EscapeDataString(token)}";

            // A felhasználónevet HTML-encode-oljuk, mert szabadon választott
            // szöveg, és enélkül HTML-t lehetne becsempészni az e-mailbe.
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
                // A fiók már létrejött, ez a hiba csak a kézbesítést érinti -
                // ezért itt nem dobunk tovább, csak logolunk. A hívó ez
                // alapján dönti el, mit mond a kliensnek.
                _logger.LogWarning(ex, "Nem sikerült megerősítő e-mailt küldeni: {Email}", email);
                return false;
            }
        }

        private async Task<bool> TrySendPasswordResetEmail(string token, string email, string username)
        {
            string publicUrl = _configuration["AppSettings:PublicUrl"] ?? "https://schedulelogic.hu";
            // Ez a kliens (Angular) egy oldalára mutat, nem a backend
            // "confirm" végpontjára, mert itt a felhasználónak be kell
            // gépelnie az új jelszót - ehhez egy űrlap kell, nem redirect.
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
                _logger.LogWarning(ex, "Nem sikerült jelszó-visszaállító e-mailt küldeni: {Email}", email);
                return false;
            }
        }

    }
}