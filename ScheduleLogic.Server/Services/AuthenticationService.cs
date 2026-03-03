using Azure;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using ScheduleLogic.Server.Models;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNet.Identity;
using Microsoft.Extensions.Configuration;
using ScheduleLogic.Server.Services.Interfaces;

namespace ScheduleLogic.Server.Services
{
    public class AuthenticationService : IAuthenticationService
    {
        private readonly IDatabaseService _dbService;
        private readonly IConfiguration _configuration;

        public AuthenticationService(IDatabaseService dbService, IConfiguration configuration)
        {
            _dbService = dbService;
            _configuration = configuration;
        }

        public async Task<String?> LoginUser(string username, string password)
        {
            if (await _dbService.LoginUser(username, password))
            {
                var authClaims = new List<Claim>
                {
                    new Claim(ClaimTypes.Name, username),
                    new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
                };

                var token = GetToken(authClaims);
                var jwtToken = new JwtSecurityTokenHandler().WriteToken(token);

                return jwtToken;
            }
            return null;
        }

        public async Task<String?> RegisterUser(string username, string password, string email)
        {
            string ok = await _dbService.RegisterUser(username, password, email);
            return ok;
        }

        private JwtSecurityToken GetToken(List<Claim> authClaims)
        {
            var authSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["JwtSettings:SecretKey"]!));
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
