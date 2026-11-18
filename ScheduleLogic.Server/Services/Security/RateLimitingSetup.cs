using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace ScheduleLogic.Server.Services.Security
{
    /// <summary>
    /// Jelszó-próbálgatás és tömeges regisztráció / levélküldés elleni
    /// korlátozás, kliens IP-címenként, fix időablakkal (a PHP backend
    /// RateLimiter osztályával azonos szabályok és alapértékek).
    ///
    /// Beállítás (appsettings / környezeti változó), pl.:
    ///   "RateLimiting": { "Enabled": true, "Login": { "PermitLimit": 20, "WindowSeconds": 300 } }
    /// Fejlesztéskor / terheléses méréskor kikapcsolható: RateLimiting:Enabled = false.
    ///
    /// Reverse proxy mögött a valódi kliens IP-hez a UseForwardedHeaders kell
    /// (Program.cs), különben mindenki a proxy IP-jével osztozna egy kereten.
    /// </summary>
    public static class RateLimitingSetup
    {
        public const string Login = "Login";
        public const string Register = "Register";
        public const string EmailSending = "EmailSending";
        public const string ResetPassword = "ResetPassword";
        public const string AdminLogin = "AdminLogin";

        private static readonly (string Name, int Limit, int WindowSeconds)[] Defaults =
        {
            (Login, 20, 300),
            (Register, 5, 900),
            (EmailSending, 5, 900),
            (ResetPassword, 10, 900),
            (AdminLogin, 5, 900),
        };

        public static IServiceCollection AddScheduleLogicRateLimiting(this IServiceCollection services, IConfiguration config)
        {
            bool enabled = config.GetValue("RateLimiting:Enabled", true);

            services.AddRateLimiter(options =>
            {
                options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
                options.OnRejected = (context, _) =>
                {
                    if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                    {
                        context.HttpContext.Response.Headers.RetryAfter =
                            ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(CultureInfo.InvariantCulture);
                    }
                    return ValueTask.CompletedTask;
                };

                foreach (var (name, defaultLimit, defaultWindow) in Defaults)
                {
                    int limit = config.GetValue($"RateLimiting:{name}:PermitLimit", defaultLimit);
                    int window = config.GetValue($"RateLimiting:{name}:WindowSeconds", defaultWindow);

                    options.AddPolicy(name, httpContext =>
                    {
                        if (!enabled || limit <= 0)
                            return RateLimitPartition.GetNoLimiter("off");

                        var ip = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                        return RateLimitPartition.GetFixedWindowLimiter(ip, _ => new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = limit,
                            Window = TimeSpan.FromSeconds(window),
                            QueueLimit = 0,
                            AutoReplenishment = true,
                        });
                    });
                }
            });
            return services;
        }
    }
}
