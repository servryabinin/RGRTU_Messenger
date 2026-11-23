using System;
using System.Security.Cryptography;
using System.Text;

namespace RGRTU.Messenger.Client.Security
{
    /// <summary>
    /// Вычисляет SHA-256 хеш пароля в том же формате, в котором сервер ожидает его в протоколе.
    /// </summary>
    public static class ClientPasswordHasher
    {
        public static string Sha256Hex(string rawData)
        {
            byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawData));
            return Convert.ToHexString(bytes).ToLowerInvariant();
        }
    }
}
