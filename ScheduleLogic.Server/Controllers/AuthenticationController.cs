using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ScheduleLogic.Server.Services.Security;
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
        [EnableRateLimiting(RateLimitingSetup.Login)]
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

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(identity),
                new AuthenticationProperties { IsPersistent = true }
            );

            return Ok();
        }

        [HttpPost("register")]
        [EnableRateLimiting(RateLimitingSetup.Register)]
        public async Task<IActionResult> Register([FromBody] RegisterModel data)
        {
            var result = await _authService.RegisterUser(data.Username, data.Password, data.Email);
            if (!result.Success) return BadRequest(result.Error);

            return Ok(new { emailSent = result.EmailSent });
        }

        [HttpPost("resend-confirmation")]
        [EnableRateLimiting(RateLimitingSetup.EmailSending)]
        public async Task<IActionResult> ResendConfirmation([FromBody] ResendConfirmationModel data)
        {
            await _authService.ResendConfirmationEmailAsync(data.UsernameOrEmail);
            return Ok();
        }

        [HttpPost("forgot-password")]
        [EnableRateLimiting(RateLimitingSetup.EmailSending)]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordModel data)
        {
            await _authService.ForgotPasswordAsync(data.UsernameOrEmail);
            return Ok();
        }

        [HttpPost("reset-password")]
        [EnableRateLimiting(RateLimitingSetup.ResetPassword)]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordModel data)
        {
            if (string.IsNullOrWhiteSpace(data.NewPassword) || data.NewPassword.Length < PasswordPolicy.MinLength)
                return BadRequest($"Password must be at least {PasswordPolicy.MinLength} characters.");
            if (data.NewPassword.Length > PasswordPolicy.MaxLength)
                return BadRequest("Password is too long.");

            bool success = await _authService.ResetPasswordAsync(data.Token, data.NewPassword);

            return success ? Ok() : BadRequest("Invalid or expired reset link.");
        }

        [Authorize]
        [HttpGet("check-token")]
        public IActionResult CheckToken()
        {
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