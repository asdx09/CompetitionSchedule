using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace ScheduleLogic.Server.Services.Interfaces
{
    public interface IAuthenticationService
    {

        public Task<String?> LoginUser(string username, string password);

        public Task<String?> RegisterUser(string username, string password, string email);

    }
}
