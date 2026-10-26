using static ScheduleLogic.Server.Class.AdminModels;

namespace ScheduleLogic.Server.Services.Interfaces
{
    public interface IAdminService
    {
        Task<AdminStatsDTO> GetStatsAsync();
        Task<List<AdminUserDTO>> SearchUsersAsync(string? query);
        Task<bool> ConfirmUserAsync(long userId);
        Task<bool> DeleteUserAsync(long userId);
    }
}