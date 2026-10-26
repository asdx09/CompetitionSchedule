using ScheduleLogic.Server.Class;
using ScheduleLogic.Server.Models;

namespace ScheduleLogic.Server.Services.Interfaces
{
    public interface IUserRepository
    {
        Task<User?> GetByUsernameOrEmailAsync(string usernameOrEmail);
        Task<bool> UsernameExistsAsync(string username);
        Task<bool> EmailExistsAsync(string email);
        Task<User> AddAsync(User user);
        Task<User?> GetByValidationTokenAsync(string token);
        Task<bool> ConfirmEmailAsync(string token);
        Task<long?> GetIdByEmailAsync(string email);
        Task DeleteAsync(long userId);

        // Admin
        Task<int> CountAsync();
        Task<List<AdminModels.AdminUserDTO>> SearchAsync(string? query);
        Task<bool> ConfirmByIdAsync(long userId);

        // Pass-reset
        Task SetPasswordResetTokenAsync(long userId, string token, DateTime expiresUtc);
        Task<User?> GetByValidPasswordResetTokenAsync(string token);
        Task SetPasswordAsync(long userId, string hashedPassword);
    }
}