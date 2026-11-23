using System;
using System.Security.Cryptography;
using System.Text;

namespace RGRTU.Messenger.Server.Data
{
    /// <summary>
    /// Формирует и проверяет хеши паролей пользователей.
    /// </summary>
    /// <remarks>
    /// Клиент передаёт на сервер SHA-256 хеш пароля. Сервер дополнительно применяет
    /// PBKDF2 с индивидуальной солью, поэтому в базе не хранится ни пароль, ни хеш, который
    /// можно было бы просто переслать повторно.
    /// </remarks>
    public static class PasswordHasher
    {
        private const int SaltSize = 16;
        private const int HashSize = 32;
        private const int Iterations = 10000;

        /// <summary>
        /// Вычисляет SHA-256 хеш строки в шестнадцатеричном формате (нижний регистр).
        /// Использует то же представление, что и клиентская часть мессенджера.
        /// </summary>
        public static string Sha256Hex(string rawData)
        {
            byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawData));
            return Convert.ToHexString(bytes).ToLowerInvariant();
        }

        /// <summary>
        /// Создаёт соль и хеш из пароля в открытом виде (для консоли администратора).
        /// </summary>
        public static (string Hash, string Salt) CreateFromPassword(string password)
        {
            return CreateFromClientHash(Sha256Hex(password));
        }

        /// <summary>
        /// Создаёт соль и хеш из значения, полученного от клиента.
        /// </summary>
        public static (string Hash, string Salt) CreateFromClientHash(string clientHash)
        {
            if (string.IsNullOrWhiteSpace(clientHash))
                throw new ArgumentException("Хеш пароля не может быть пустым", nameof(clientHash));

            byte[] salt = RandomNumberGenerator.GetBytes(SaltSize);
            byte[] hash = Derive(clientHash, salt);

            return (Convert.ToBase64String(hash), Convert.ToBase64String(salt));
        }

        /// <summary>
        /// Проверяет значение, полученное от клиента, против сохранённых хеша и соли.
        /// </summary>
        public static bool VerifyClientHash(string clientHash, string storedHash, string storedSalt)
        {
            if (string.IsNullOrWhiteSpace(clientHash) ||
                string.IsNullOrWhiteSpace(storedHash) ||
                string.IsNullOrWhiteSpace(storedSalt))
            {
                return false;
            }

            try
            {
                byte[] salt = Convert.FromBase64String(storedSalt);
                byte[] expectedHash = Convert.FromBase64String(storedHash);
                byte[] actualHash = Derive(clientHash, salt);

                return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
            }
            catch (FormatException)
            {
                return false;
            }
        }

        private static byte[] Derive(string credential, byte[] salt)
        {
            return Rfc2898DeriveBytes.Pbkdf2(
                Encoding.UTF8.GetBytes(credential),
                salt,
                Iterations,
                HashAlgorithmName.SHA256,
                HashSize);
        }
    }
}
