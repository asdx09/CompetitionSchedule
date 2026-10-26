using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ScheduleLogic.Server.Services.Interfaces;
using System.Security.Claims;
using static ScheduleLogic.Server.Class.AuthenticationModels;
using IAuthenticationService = ScheduleLogic.Server.Services.Interfaces.IAuthenticationService;

namespace ScheduleLogic.Server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthenticationController : ControllerBase
    {
        private readonly IAuthenticationService _authService;
        private readonly IConfiguration _configuration;

        public AuthenticationController(IAuthenticationService authService, IConfiguration configuration)
        {
            _authService = authService;
            _configuration = configuration;
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginModel data)
        {
            var user = await _authService.LoginUser(data.Username, data.Password);
            if (user == null)
                return Unauthorized("Invalid username/email and password or not verified email!");

            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, user.UserId.ToString()),
                new(ClaimTypes.Name, user.Username)
            };
            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);

            // Az ExpiresUtc-t szándékosan nem adjuk meg: a cookie séma
            // options.ExpireTimeSpan-je (1 óra) és a SlidingExpiration
            // már beállítja, itt csak felülírná feleslegesen.
            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(identity),
                new AuthenticationProperties { IsPersistent = true }
            );

            return Ok();
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterModel data)
        {
            var result = await _authService.RegisterUser(data.Username, data.Password, data.Email);
            if (!result.Success) return BadRequest(result.Error);

            // A fiók mindkét esetben létrejött; az emailSent jelzi a
            // kliensnek, ha a levél kézbesítése nem sikerült, hogy fel
            // tudja ajánlani az újraküldést, ne csak csendben landoljon
            // a felhasználó egy soha meg nem erősíthető fiókban.
            return Ok(new { emailSent = result.EmailSent });
        }

        [HttpPost("resend-confirmation")]
        public async Task<IActionResult> ResendConfirmation([FromBody] ResendConfirmationModel data)
        {
            // Szándékosan mindig Ok()-ot adunk vissza, hogy a válaszból ne
            // lehessen kitalálni, létezik-e a megadott felhasználó/e-mail.
            await _authService.ResendConfirmationEmailAsync(data.UsernameOrEmail);
            return Ok();
        }

        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordModel data)
        {
            // Ugyanaz az elv, mint a resend-confirmation-nél: mindig Ok(),
            // hogy ne lehessen vele felhasználót/e-mailt találgatni.
            await _authService.ForgotPasswordAsync(data.UsernameOrEmail);
            return Ok();
        }

        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordModel data)
        {
            if (string.IsNullOrWhiteSpace(data.NewPassword) || data.NewPassword.Length < 8)
                return BadRequest("Password must be at least 8 characters.");

            bool success = await _authService.ResetPasswordAsync(data.Token, data.NewPassword);

            // Itt már NEM kell elrejteni, hogy a token érvénytelen/lejárt -
            // ezt a felhasználó úgyis a saját linkjéről tudja, nem más
            // fiókjáról szerez vele infót.
            return success ? Ok() : BadRequest("Invalid or expired reset link.");
        }

        [Authorize]
        [HttpGet("check-token")]
        public IActionResult CheckToken()
        {
            // A felhasználónév már benne van a cookie-ban lévő claimekben,
            // erre nem kell adatbázist hívni.
            var username = User.FindFirstValue(ClaimTypes.Name);
            return Ok(new { name = username });
        }

        [HttpGet("confirm")]
        public async Task<IActionResult> ConfirmEmail(string token)
        {
            if (string.IsNullOrEmpty(token))
                return BadRequest("Invalid confirmation request.");

            bool confirmed = await _authService.ConfirmEmailAsync(token);

            string frontendUrl = _configuration["URLS:Frontend"] ?? "";
            return Redirect(confirmed ? frontendUrl : $"{frontendUrl}?confirm=failed");
        }

        [Authorize]
        [HttpDelete("delete")]
        public async Task<IActionResult> DeleteAccount()
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!long.TryParse(userIdClaim, out var userId))
                return Unauthorized();

            await _authService.DeleteAccountAsync(userId);
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

            return Ok();
        }

        [Authorize]
        [HttpPost("logout")]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Ok();
        }
    }
}