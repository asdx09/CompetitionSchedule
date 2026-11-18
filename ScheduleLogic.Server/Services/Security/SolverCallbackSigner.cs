using System.Security.Cryptography;
using System.Text;

namespace ScheduleLogic.Server.Services.Security
{
    /// <summary>
    /// A solver visszahívásának (POST api/Schedule/answer) hitelesítése.
    ///
    /// Korábban az answer végpont csak azt nézte, hogy a kérés localhostról
    /// jön-e. Reverse proxy (nginx) mögött viszont MINDEN kérés localhostról
    /// érkezik, így bárki beküldhetett volna hamis beosztást. Helyette most
    /// versenyenként egy HMAC-SHA256 kulcs kerül a visszahívási URL-be:
    ///
    ///   https://.../api/Schedule/answer?key=HMAC(titok, "solver-answer:{eventId}")
    ///
    /// A Python solveren nem kell változtatni, mert a kapott returnURL-re küld
    /// vissza. A kulcs csak az adott versenyre érvényes, és nem lehet belőle a
    /// titkot vagy más verseny kulcsát kiszámolni. (A PHP backend
    /// SolverCallbackSigner osztályával azonos algoritmus.)
    ///
    /// A titok forrása: Solver:CallbackSecret beállítás (User Secrets /
    /// környezeti változó); ha nincs megadva, az első használatkor generál
    /// egyet az App_Data/solver-callback.key fájlba, hogy újraindítás után is
    /// érvényesek maradjanak a futó keresések visszahívásai.
    /// </summary>
    public sealed class SolverCallbackSigner
    {
        private readonly byte[] _secret;

        public SolverCallbackSigner(IConfiguration configuration, IWebHostEnvironment environment, ILogger<SolverCallbackSigner> logger)
        {
            var configured = configuration["Solver:CallbackSecret"];
            if (!string.IsNullOrWhiteSpace(configured))
            {
                if (configured.Length < 32)
                    throw new InvalidOperationException("A Solver:CallbackSecret legalább 32 karakter legyen.");
                _secret = Encoding.UTF8.GetBytes(configured);
                return;
            }

            var keyFile = Path.Combine(environment.ContentRootPath, "App_Data", "solver-callback.key");
            _secret = Encoding.UTF8.GetBytes(LoadOrCreate(keyFile));
            logger.LogInformation("Solver:CallbackSecret nincs beállítva, a titok helye: {KeyFile}", keyFile);
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
                // CreateNew: ha közben egy másik példány már létrehozta, azt használjuk.
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
