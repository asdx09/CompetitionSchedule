using Microsoft.AspNetCore.Mvc;
using ScheduleLogic.Server.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using ScheduleLogic.Server.Models;
using Microsoft.AspNet.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Authorization;
using static ScheduleLogic.Server.Class.AuthenticationModels;
using ScheduleLogic.Server.Services.Interfaces;
using Microsoft.AspNetCore.WebUtilities;
using System.Diagnostics;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication;
using IAuthenticationService = ScheduleLogic.Server.Services.Interfaces.IAuthenticationService;

namespace ScheduleLogic.Server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthenticationController : ControllerBase
    {
        private readonly IDatabaseService _dbService;
        private readonly IAuthenticationService _authService;
        private readonly IConfiguration _configuration;

        public AuthenticationController(IDatabaseService dbService, IAuthenticationService authService, IConfiguration configuration)
        {
            _authService = authService;
            _dbService = dbService;
            _configuration = configuration;
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginModel data)
        {

            string res = await _dbService.LoginUser(data.Username, data.Password);
            if (res == "")
            {
                var jwtToken = await _authService.LoginUser(data.Username, data.Password);

                if (jwtToken != null)
                {
                    Response.Cookies.Append("jwt", jwtToken, new CookieOptions
                    {
                        HttpOnly = true,
                        Secure = false,
                        SameSite = SameSiteMode.Lax,
                        Expires = DateTime.UtcNow.AddHours(1),
                    });
                    return Ok();
                }
            }
            return Unauthorized(res);
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterModel data)
        {
            string result = await _authService.RegisterUser(data.Username, data.Password,data.Email) ?? "ERROR";
            if (string.IsNullOrEmpty(result)) return Ok();
            else return BadRequest(result);
        }

        [Authorize]
        [HttpPost("check-token")]
        public async Task<IActionResult> CheckToken()
        {
            var expClaim = User.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Exp);

            if (expClaim == null)
                return BadRequest("No expiration claim found.");

            var expUnix = long.Parse(expClaim.Value);
            var expDate = DateTimeOffset.FromUnixTimeSeconds(expUnix).UtcDateTime;

            var username = User.Identity?.Name;
            if (!string.IsNullOrEmpty(username) && await _dbService.CheckUserExist(username) == false) return Unauthorized();

            return Ok(expDate);
        }


        [HttpGet("confirm")]
        public async Task<IActionResult> ConfirmEmail(string token)
        {
            if (string.IsNullOrEmpty(token))
                return BadRequest("Invalid confirmation request.");


            await _dbService.ValidateEmail(token);

            return Redirect(_configuration["URLS:Frontend"] ?? "");
        }

        [Authorize]
        [HttpDelete("delete")]
        public async Task<IActionResult> DeleteAccount()
        {
            await _dbService.DeleteUser(User.Identity?.Name);
            return Ok();
        }

        [HttpPost("logout")]
        [Authorize]
        public async Task<IActionResult> Logout()
        {
            if (Request.Cookies.ContainsKey("jwt"))
            {
                Response.Cookies.Delete("jwt");
            }
            return Ok();
        }
    }
}
