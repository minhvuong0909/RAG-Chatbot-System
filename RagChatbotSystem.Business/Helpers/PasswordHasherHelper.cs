using System;
using System.Security.Cryptography;

namespace RagChatbotSystem.Business.Helpers
{
    public static class PasswordHasherHelper
    {
        private const int SaltSize = 16; // 128 bit
        private const int KeySize = 32;  // 256 bit
        private const int Iterations = 210_000;
        private const int LegacyIterations = 10_000;
        private const string AlgorithmPrefix = "pbkdf2-sha256";

        public static string HashPassword(string password)
        {
            ArgumentException.ThrowIfNullOrEmpty(password);

            var salt = RandomNumberGenerator.GetBytes(SaltSize);
            var key = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, KeySize);
            return $"{AlgorithmPrefix}${Iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(key)}";
        }

        public static bool VerifyPassword(string password, string hashedPassword)
        {
            if (string.IsNullOrEmpty(hashedPassword))
                return false;

            try
            {
                if (hashedPassword.StartsWith($"{AlgorithmPrefix}$", StringComparison.Ordinal))
                {
                    return VerifyVersionedHash(password, hashedPassword);
                }

                // Backward compatibility for accounts created before hashes were versioned.
                var hashBytes = Convert.FromBase64String(hashedPassword);
                if (hashBytes.Length != SaltSize + KeySize)
                    return false;

                var salt = new byte[SaltSize];
                Array.Copy(hashBytes, 0, salt, 0, SaltSize);

                var key = new byte[KeySize];
                Array.Copy(hashBytes, SaltSize, key, 0, KeySize);

                var keyToCheck = Rfc2898DeriveBytes.Pbkdf2(password, salt, LegacyIterations, HashAlgorithmName.SHA256, KeySize);
                return CryptographicOperations.FixedTimeEquals(key, keyToCheck);
            }
            catch
            {
                return false;
            }
        }

        private static bool VerifyVersionedHash(string password, string hashedPassword)
        {
            var parts = hashedPassword.Split('$');
            if (parts.Length != 4
                || !string.Equals(parts[0], AlgorithmPrefix, StringComparison.Ordinal)
                || !int.TryParse(parts[1], out var iterations)
                || iterations < LegacyIterations
                || iterations > 1_000_000)
            {
                return false;
            }

            var salt = Convert.FromBase64String(parts[2]);
            var expectedKey = Convert.FromBase64String(parts[3]);
            if (salt.Length != SaltSize || expectedKey.Length != KeySize)
            {
                return false;
            }

            var actualKey = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, KeySize);
            return CryptographicOperations.FixedTimeEquals(expectedKey, actualKey);
        }
    }
}
