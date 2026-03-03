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

namespace ScheduleLogic.Server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthenticationController : ControllerBase
    {
        private readonly IDatabaseService _dbService;
        private readonly IAuthenticationService _authService;

        public AuthenticationController(IDatabaseService dbService, IAuthenticationService authService)
        {
            _authService = authService;
            _dbService = dbService;
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginModel data)
        {

            var jwtToken = await _authService.LoginUser(data.Username, data.Password);

            if (jwtToken != null)
            {
                Response.Cookies.Append("jwt", jwtToken, new CookieOptions
                {
                    HttpOnly = true,
                    Secure = true,
                    SameSite = SameSiteMode.Strict,
                    Expires = DateTime.UtcNow.AddHours(1),
                });
                return Ok();
            }
            return Unauthorized();
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterModel data)
        {
            var result = await _authService.RegisterUser(data.Username, data.Password,data.Email);
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
    }
}
