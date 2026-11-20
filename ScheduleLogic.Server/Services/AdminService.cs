using Microsoft.Extensions.Logging;
using ScheduleLogic.Server.Models;
using ScheduleLogic.Server.Services.Interfaces;
using ScheduleLogic.Server.Services.Security;
using static ScheduleLogic.Server.Class.AdminModels;

namespace ScheduleLogic.Server.Services
{
    public class AdminService : IAdminService
    {
        private readonly IUserRepository _users;
        private readonly ScheduleLogicContext _db;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<AdminService> _logger;
        private readonly ServerSideTicketStore _sessions;

        public AdminService(
            IUserRepository users,
            ScheduleLogicContext db,
            IHttpClientFactory httpClientFactory,
            ILogger<AdminService> logger,
            ServerSideTicketStore sessions)
        {
            _users = users;
            _db = db;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
            _sessions = sessions;
        }

        public async Task<AdminStatsDTO> GetStatsAsync()
        {
            var stats = new AdminStatsDTO();

            try
            {
                using var dbTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                stats.DatabaseAvailable = await _db.Database.CanConnectAsync(dbTimeout.Token);
                stats.UserCount = stats.DatabaseAvailable ? await _users.CountAsync() : 0;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Admin health check: the database is not reachable.");
                stats.DatabaseAvailable = false;
            }

            stats.SolverAvailable = await CheckSolverAvailableAsync();
            return stats;
        }

        private async Task<bool> CheckSolverAvailableAsync()
        {
            try
            {
                var client = _httpClientFactory.CreateClient("Solver");
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                await client.GetAsync("", cts.Token);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Admin health check: the solver is not reachable.");
                return false;
            }
        }

        public Task<List<AdminUserDTO>> SearchUsersAsync(string? query) =>
            _users.SearchAsync(query);

        public Task<bool> ConfirmUserAsync(long userId) =>
            _users.ConfirmByIdAsync(userId);

        public async Task<bool> DeleteUserAsync(long userId)
        {
            await _users.DeleteAsync(userId);
            _sessions.RemoveAllForUser(userId);
            return true;
        }
    }
}