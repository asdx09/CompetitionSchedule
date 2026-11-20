using System.Security.Cryptography;
using System.Text;

namespace ScheduleLogic.Server.Services.Security
{
        public sealed class SolverCallbackSigner
    {
        private readonly byte[] _secret;

        public SolverCallbackSigner(IConfiguration configuration, IWebHostEnvironment environment, ILogger<SolverCallbackSigner> logger)
        {
            var configured = configuration["Solver:CallbackSecret"];
            if (!string.IsNullOrWhiteSpace(configured))
            {
                if (configured.Length < 32)
                    throw new InvalidOperationException("Solver:CallbackSecret must be at least 32 characters long.");
                _secret = Encoding.UTF8.GetBytes(configured);
                return;
            }

            var keyFile = Path.Combine(environment.ContentRootPath, "App_Data", "solver-callback.key");
            _secret = Encoding.UTF8.GetBytes(LoadOrCreate(keyFile));
            logger.LogInformation("Solver:CallbackSecret is not set, using the secret stored in: {KeyFile}", keyFile);
        }

        public string KeyFor(long eventId)
        {
            var mac = HMACSHA256.HashData(_secret, Encoding.UTF8.GetBytes("solver-answer:" + eventId));
            return Convert.ToHexString(mac).ToLowerInvariant();
        }

        public bool Verify(long eventId, string? key)
        {
            if (string.IsNullOrEmpty(key)) return false;
            var expected = Encoding.ASCII.GetBytes(KeyFor(eventId));
            var actual = Encoding.ASCII.GetBytes(key.ToLowerInvariant());
            return CryptographicOperations.FixedTimeEquals(expected, actual);
        }

        private static string LoadOrCreate(string keyFile)
        {
            if (File.Exists(keyFile))
            {
                var stored = File.ReadAllText(keyFile).Trim();
                if (stored.Length >= 32) return stored;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(keyFile)!);
            var generated = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
            try
            {
                using (var fs = new FileStream(keyFile, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                using (var writer = new StreamWriter(fs))
                    writer.Write(generated);
                if (!OperatingSystem.IsWindows())
                    File.SetUnixFileMode(keyFile, UnixFileMode.UserRead | UnixFileMode.UserWrite);
                return generated;
            }
            catch (IOException) when (File.Exists(keyFile))
            {
                return File.ReadAllText(keyFile).Trim();
            }
        }
    }
}
