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
using System.Net.Mail;
using System.Net;
using DocumentFormat.OpenXml.Office2016.Drawing.ChartDrawing;
using DocumentFormat.OpenXml.Office2021.DocumentTasks;
using System.Numerics;
using System.Security.Cryptography;
using DocumentFormat.OpenXml.Spreadsheet;
using Microsoft.AspNetCore.Http.HttpResults;

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
            var authClaims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, username),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };

            var token = GetToken(authClaims);
            var jwtToken = new JwtSecurityTokenHandler().WriteToken(token);

            return jwtToken;
        }

        public async Task<String?> RegisterUser(string username, string password, string email)
        {
            string ok = await _dbService.RegisterUser(username, password, email, this);
            return ok;
        }

        public async System.Threading.Tasks.Task NewValidationEmail(string token, string email, string username)
        {
            string confirmationLink = $"https://schedulelogic.hu/api/authentication/confirm?token={token}";
            string body = $@"
                <h2>Welcome {username}!</h2>
                <p>Thanks for signing up!</p>

                <p>Please confirm your email address by clicking the button below:</p>

                <a href=""{confirmationLink}"" 
                   style=""display:inline-block;padding:10px 20px;background:#4CAF50;color:white;text-decoration:none;border-radius:5px;"">
                   Confirm Email
                </a>

                <p>If you did not sign up, you can safely ignore this email.</p>
                
            ";
            await SendEmailAsync(email, "Email confirmation required - ScheduleLogic", body);
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

        public async System.Threading.Tasks.Task SendEmailAsync(string email, string subject, string _message)
        {
            using var message = new MailMessage("noreply@schedulelogic.hu", email, subject, _message)
            {
                IsBodyHtml = true
            };

            var client = new SmtpClient("smtp.mailersend.net")
            {
                Port = 587,
                Credentials = new NetworkCredential(_configuration["MailSend:User"], _configuration["MailSend:Password"]),
                EnableSsl = true,
            };

            await client.SendMailAsync(message);
        }
    }

}
