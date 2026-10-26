using Microsoft.EntityFrameworkCore;
using ScheduleLogic.Server.Class;
using ScheduleLogic.Server.Models;
using ScheduleLogic.Server.Services.Interfaces;

namespace ScheduleLogic.Server.Services
{
    public class UserRepository : IUserRepository
    {
        private readonly ScheduleLogicContext _db;

        public UserRepository(ScheduleLogicContext db)
        {
            _db = db;
        }

        public Task<User?> GetByUsernameOrEmailAsync(string usernameOrEmail) =>
            _db.Users.FirstOrDefaultAsync(u =>
                u.Username == usernameOrEmail || u.Email == usernameOrEmail);

        public Task<bool> UsernameExistsAsync(string username) =>
            _db.Users.AnyAsync(u => u.Username == username);

        public Task<bool> EmailExistsAsync(string email) =>
            _db.Users.AnyAsync(u => u.Email == email);

        public async Task<User> AddAsync(User user)
        {
            _db.Users.Add(user);
            await _db.SaveChangesAsync();
            return user;
        }

        public Task<User?> GetByValidationTokenAsync(string token) =>
            _db.Users.FirstOrDefaultAsync(u => u.Validated == token);

        public async Task<bool> ConfirmEmailAsync(string token)
        {
            var user = await _db.Users.FirstOrDefaultAsync(u => u.Validated == token);
            if (user == null) return false;

            user.Validated = "";
            await _db.SaveChangesAsync();
            return true;
        }

        public Task<long?> GetIdByEmailAsync(string email) =>
            _db.Users
                .Where(u => u.Email == email)
                .Select(u => (long?)u.UserId)
                .FirstOrDefaultAsync();

        public async Task DeleteAsync(long userId)
        {
            var user = await _db.Users.FindAsync(userId);
            if (user != null)
            {
                _db.Users.Remove(user);
                await _db.SaveChangesAsync();
            }
        }

        public Task<int> CountAsync() => _db.Users.CountAsync();

        // A username/email citext, ezért a Contains itt kis/nagybetű-
        // érzéketlen az adatbázisban, JS oldali ToLower() nélkül is.
        public async Task<List<AdminModels.AdminUserDTO>> SearchAsync(string? query)
        {
            var users = _db.Users.AsNoTracking().AsQueryable();

            if (!string.IsNullOrWhiteSpace(query))
            {
                if (long.TryParse(query, out var id))
                    users = users.Where(u => u.UserId == id
                        || u.Username.Contains(query)
                        || u.Email.Contains(query));
                else
                    users = users.Where(u => u.Username.Contains(query)
                        || u.Email.Contains(query));
            }

            return await users
                .OrderByDescending(u => u.Registrationdate)
                .Take(100) // ne lehessen egy kérésben az egész táblát kihúzni
                .Select(u => new AdminModels.AdminUserDTO
                {
                    UserId = u.UserId,
                    Username = u.Username,
                    Email = u.Email,
                    RegistrationDate = u.Registrationdate,
                    EmailConfirmed = u.Validated == ""
                })
                .ToListAsync();
        }

        public async Task<bool> ConfirmByIdAsync(long userId)
        {
            var user = await _db.Users.FindAsync(userId);
            if (user == null) return false;

            user.Validated = "";
            await _db.SaveChangesAsync();
            return true;
        }

        public async Task SetPasswordResetTokenAsync(long userId, string token, DateTime expiresUtc)
        {
            var user = await _db.Users.FindAsync(userId);
            if (user == null) return;

            user.Passwordresettoken = token;
            user.Passwordresetexpires = expiresUtc;
            await _db.SaveChangesAsync();
        }

        public Task<User?> GetByValidPasswordResetTokenAsync(string token) =>
            _db.Users.FirstOrDefaultAsync(u =>
                u.Passwordresettoken == token
                && u.Passwordresetexpires != null
                && u.Passwordresetexpires > DateTime.UtcNow);

        public async Task SetPasswordAsync(long userId, string hashedPassword)
        {
            var user = await _db.Users.FindAsync(userId);
            if (user == null) return;

            user.Password = hashedPassword;
            // A token egyszer használatos - felhasználás után törlődik,
            // hogy ugyanaz a link ne legyen újra beváltható.
            user.Passwordresettoken = null;
            user.Passwordresetexpires = null;
            await _db.SaveChangesAsync();
        }
    }
}