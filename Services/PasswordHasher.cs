using System.Security.Cryptography;
using System.Text;

namespace rent_a_car.Services
{
    /// <summary>
    /// PBKDF2 (HMAC-SHA256) password hashing with a random salt per password.
    /// Stored format: "{iterations}.{saltBase64}.{hashBase64}".
    ///
    /// Also verifies against the legacy unsalted-SHA256 hash format used earlier in this
    /// project, so accounts created before this change keep working. A successful legacy
    /// verification sets <c>needsUpgrade</c> so the caller can transparently rehash and
    /// persist the stronger hash.
    /// </summary>
    public static class PasswordHasher
    {
        private const int SaltSize = 16;
        private const int KeySize = 32;
        private const int Iterations = 100_000;
        private static readonly HashAlgorithmName Algorithm = HashAlgorithmName.SHA256;

        public static string Hash(string password)
        {
            var salt = RandomNumberGenerator.GetBytes(SaltSize);
            var key = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, Algorithm, KeySize);
            return $"{Iterations}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(key)}";
        }

        public static bool Verify(string password, string storedHash, out bool needsUpgrade)
        {
            needsUpgrade = false;
            if (string.IsNullOrEmpty(storedHash)) return false;

            var parts = storedHash.Split('.');
            if (parts.Length == 3 && int.TryParse(parts[0], out var iterations))
            {
                var salt = Convert.FromBase64String(parts[1]);
                var expectedKey = Convert.FromBase64String(parts[2]);
                var actualKey = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, Algorithm, expectedKey.Length);
                return CryptographicOperations.FixedTimeEquals(actualKey, expectedKey);
            }

            // Legacy format: plain, unsalted SHA256 hash (base64).
            var legacyHash = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(password)));
            if (string.Equals(legacyHash, storedHash, StringComparison.Ordinal))
            {
                needsUpgrade = true;
                return true;
            }
            return false;
        }
    }
}
