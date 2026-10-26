using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using ScheduleLogic.Server.Services.Interfaces;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace ScheduleLogic.Server.Controllers
{
    [Route("admin")]
    [Authorize(AuthenticationSchemes = "AdminCookie")]
    public class AdminController : Controller
    {
        private readonly IAdminService _adminService;
        private readonly IConfiguration _configuration;

        public AdminController(IAdminService adminService, IConfiguration configuration)
        {
            _adminService = adminService;
            _configuration = configuration;
        }

        [HttpGet]
        public IActionResult Index() => View();

        [AllowAnonymous]
        [HttpGet("login")]
        public IActionResult Login() => View();

        [AllowAnonymous]
        [HttpPost("login")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login([FromForm] string username, [FromForm] string password)
        {
            if (!IsValidCredential(username, password))
            {
                ModelState.AddModelError("", "Hibás felhasználónév vagy jelszó.");
                return View();
            }

            var claims = new[] { new Claim(ClaimTypes.Name, username) };
            var identity = new ClaimsIdentity(claims, "AdminCookie");

            await HttpContext.SignInAsync(
                "AdminCookie",
                new ClaimsPrincipal(identity),
                new AuthenticationProperties { IsPersistent = false });

            return Redirect("/admin");
        }

        [HttpPost("logout")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync("AdminCookie");
            return Redirect("/admin/login");
        }

        [HttpGet("api/stats")]
        public async Task<IActionResult> GetStats()
        {
            var stats = await _adminService.GetStatsAsync();
            return Ok(stats);
        }

        [HttpGet("api/users")]
        public async Task<IActionResult> SearchUsers([FromQuery] string? q)
        {
            var users = await _adminService.SearchUsersAsync(q);
            return Ok(users);
        }

        [HttpPost("api/users/{id:long}/confirm")]
        public async Task<IActionResult> ConfirmUser(long id)
        {
            var confirmed = await _adminService.ConfirmUserAsync(id);
            return confirmed ? Ok() : NotFound();
        }

        [HttpDelete("api/users/{id:long}")]
        public async Task<IActionResult> DeleteUser(long id)
        {
            await _adminService.DeleteUserAsync(id);
            return Ok();
        }

        private bool IsValidCredential(string username, string password)
        {
            var expectedUsername = _configuration["AdminAuth:Username"];
            var expectedHash = _configuration["AdminAuth:PasswordHash"];

            if (string.IsNullOrEmpty(expectedUsername) || string.IsNullOrEmpty(expectedHash))
                return false;

            bool usernameOk = FixedTimeEquals(username, expectedUsername);

            var hasher = new PasswordHasher<object>();
            var result = hasher.VerifyHashedPassword(new object(), expectedHash, password);
            bool passwordOk = result is PasswordVerificationResult.Success
                or PasswordVerificationResult.SuccessRehashNeeded;

            return usernameOk && passwordOk;
        }

        private static bool FixedTimeEquals(string a, string b)
        {
            var aBytes = Encoding.UTF8.GetBytes(a);
            var bBytes = Encoding.UTF8.GetBytes(b);
            if (aBytes.Length != bBytes.Length) return false;
            return CryptographicOperations.FixedTimeEquals(aBytes, bBytes);
        }
    }
}