using Microsoft.Extensions.Logging;
using ScheduleLogic.Server.Models;
using ScheduleLogic.Server.Services.Interfaces;
using static ScheduleLogic.Server.Class.AdminModels;

namespace ScheduleLogic.Server.Services
{
    public class AdminService : IAdminService
    {
        private readonly IUserRepository _users;
        private readonly ScheduleLogicContext _db;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<AdminService> _logger;

        public AdminService(
            IUserRepository users,
            ScheduleLogicContext db,
            IHttpClientFactory httpClientFactory,
            ILogger<AdminService> logger)
        {
            _users = users;
            _db = db;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
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
                _logger.LogWarning(ex, "Admin health check: az adatbázis nem érhető el.");
                stats.DatabaseAvailable = false;
            }

            stats.SolverAvailable = await CheckSolverAvailableAsync();
            return stats;
        }

        // Nincs ismert /health végpontunk a solvernél, ezért csak azt nézzük,
        // fogad-e egyáltalán kapcsolatot HTTP szinten. Bármilyen válasz
        // (akár 404 is) azt jelenti, hogy a folyamat fut és figyel; csak a
        // kapcsolódási hiba/timeout számít "nem elérhető"-nek.
        // Ha a solvernek van tényleges health/status végpontja, cseréld le
        // a GetAsync("") hívást arra.
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
                _logger.LogWarning(ex, "Admin health check: a solver nem érhető el.");
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
            return true;
        }
    }
}