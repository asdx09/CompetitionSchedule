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

namespace ScheduleLogic.Server.Controllers
{
    public class LoginModel
    {
        public string Username { get; set; } = null!;
        public string Password { get; set; } = null!;
    }

    public class RegisterModel
    {
        public string Username { get; set; } = null!;
        public string Password { get; set; } = null!;
        public string Email { get; set; } = null!;
    }


    [ApiController]
    [Route("api/[controller]")]
    public class AuthenticationController : ControllerBase
    {
        private readonly DatabaseService _dbService;
        private readonly IConfiguration _configuration;

        public AuthenticationController(DatabaseService dbService, IConfiguration configuration)
        {
            _dbService = dbService;
            _configuration = configuration;
        }

        [HttpPost("login")]
        public IActionResult Login([FromBody] LoginModel data)
        {
            if (_dbService.LoginUser(data.Username, data.Password))
            {
                var authClaims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, data.Username),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };

                var token = GetToken(authClaims);
                return Ok(new
                {
                    token = new JwtSecurityTokenHandler().WriteToken(token),
                    expiration = token.ValidTo
                });
            }
            return Unauthorized();
        }

        [HttpPost("register")]
        public IActionResult Register([FromBody] RegisterModel data)
        {
            string ok = _dbService.RegisterUser(data.Username, data.Password, data.Email);
            if (string.IsNullOrEmpty(ok)) return Ok();
            else return BadRequest(ok);
        }

        [Authorize]
        [HttpPost("check-token")]
        public IActionResult CheckToken()
        {
            var expClaim = User.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Exp);

            if (expClaim == null)
                return BadRequest("No expiration claim found.");

            var expUnix = long.Parse(expClaim.Value);
            var expDate = DateTimeOffset.FromUnixTimeSeconds(expUnix).UtcDateTime;

            if (!_dbService.CheckUserExist(User.Identity.Name)) return Unauthorized();

            return Ok(expDate);
        }

        private JwtSecurityToken GetToken(List<Claim> authClaims)
        {
            var authSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["JwtSettings:SecretKey"]));
            return new JwtSecurityToken(
                issuer: _configuration["JwtSettings:Issuer"],
                audience: _configuration["JwtSettings:Audience"],
                expires: DateTime.UtcNow.AddHours(1),
                claims: authClaims,
                signingCredentials: new SigningCredentials(authSigningKey, SecurityAlgorithms.HmacSha256)
            );
        }
    }
}
