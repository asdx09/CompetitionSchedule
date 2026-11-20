using ScheduleLogic.Server.Models;
using static ScheduleLogic.Server.Class.AuthenticationModels;

namespace ScheduleLogic.Server.Services.Interfaces
{
    public interface IAuthenticationService
    {
        Task<User?> LoginUser(string usernameOrEmail, string password);
        Task<RegisterResult> RegisterUser(string username, string password, string email);
        Task<bool> ConfirmEmailAsync(string token);
        Task<bool> ResendConfirmationEmailAsync(string usernameOrEmail);
        Task DeleteAccountAsync(long userId);
        Task ForgotPasswordAsync(string usernameOrEmail);
        Task<bool> ResetPasswordAsync(string token, string newPassword);
    }
}