using System.Collections.Concurrent;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Caching.Memory;

namespace ScheduleLogic.Server.Services.Security
{
        public sealed class ServerSideTicketStore : ITicketStore
    {
        private const string KeyPrefix = "auth-ticket:";

        private readonly IMemoryCache _cache;

        private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, byte>> _byUser = new();

        public ServerSideTicketStore(IMemoryCache cache)
        {
            _cache = cache;
        }

        public Task<string> StoreAsync(AuthenticationTicket ticket)
        {
            var key = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
            Put(key, ticket);
            return Task.FromResult(key);
        }

        public Task RenewAsync(string key, AuthenticationTicket ticket)
        {
            Put(key, ticket);
            return Task.CompletedTask;
        }

        public Task<AuthenticationTicket?> RetrieveAsync(string key)
        {
            _cache.TryGetValue(KeyPrefix + key, out AuthenticationTicket? ticket);
            return Task.FromResult(ticket);
        }

        public Task RemoveAsync(string key)
        {
            if (_cache.TryGetValue(KeyPrefix + key, out AuthenticationTicket? ticket) && ticket != null)
                Unindex(UserKey(ticket), key);
            _cache.Remove(KeyPrefix + key);
            return Task.CompletedTask;
        }

                public void RemoveAllForUser(long userId)
        {
            if (_byUser.TryRemove(UserKey(CookieAuthenticationDefaults.AuthenticationScheme, userId.ToString()), out var keys))
            {
                foreach (var key in keys.Keys)
                    _cache.Remove(KeyPrefix + key);
            }
        }

        private void Put(string key, AuthenticationTicket ticket)
        {
            var options = new MemoryCacheEntryOptions();
            if (ticket.Properties.ExpiresUtc is { } expires)
                options.SetAbsoluteExpiration(expires);
            else
                options.SetSlidingExpiration(TimeSpan.FromHours(2));

            var userKey = UserKey(ticket);
            options.RegisterPostEvictionCallback((_, _, reason, _) =>
            {
                if (reason != EvictionReason.Replaced) Unindex(userKey, key);
            });

            _cache.Set(KeyPrefix + key, ticket, options);
            _byUser.GetOrAdd(userKey, _ => new ConcurrentDictionary<string, byte>())[key] = 0;
        }

        private void Unindex(string userKey, string key)
        {
            if (_byUser.TryGetValue(userKey, out var keys))
            {
                keys.TryRemove(key, out _);
                if (keys.IsEmpty) _byUser.TryRemove(new KeyValuePair<string, ConcurrentDictionary<string, byte>>(userKey, keys));
            }
        }

        private static string UserKey(AuthenticationTicket ticket) =>
            UserKey(ticket.AuthenticationScheme,
                    ticket.Principal.FindFirstValue(ClaimTypes.NameIdentifier)
                    ?? ticket.Principal.FindFirstValue(ClaimTypes.Name)
                    ?? "");

        private static string UserKey(string scheme, string user) => scheme + "|" + user;
    }
}
