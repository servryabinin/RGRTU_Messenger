using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;

namespace RGRTU.Messenger.Server.Data
{
    /// <summary>
    /// Хранилище пользователей мессенджера на основе SQLite.
    /// </summary>
    public class UserRepository
    {
        private readonly string _databasePath;
        private static bool _sqliteInitialized;

        /// <summary>
        /// Создаёт репозиторий с указанным путём к файлу базы данных.
        /// </summary>
        public UserRepository(string databasePath)
        {
            if (string.IsNullOrWhiteSpace(databasePath))
                throw new ArgumentException("Путь к базе данных не указан", nameof(databasePath));

            _databasePath = databasePath;
        }

        /// <summary>
        /// Создаёт файл базы данных и необходимую таблицу, если их ещё нет.
        /// </summary>
        public async Task InitializeAsync()
        {
            string fullPath = Path.GetFullPath(_databasePath);
            string? directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            if (!_sqliteInitialized)
            {
                SQLitePCL.Batteries_V2.Init();
                _sqliteInitialized = true;
            }

            await using var connection = CreateConnection();
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
CREATE TABLE IF NOT EXISTS Users
(
    Id           INTEGER PRIMARY KEY AUTOINCREMENT,
    Login        TEXT    NOT NULL COLLATE NOCASE UNIQUE,
    PasswordHash TEXT    NOT NULL,
    Salt         TEXT    NOT NULL,
    DisplayName  TEXT    NOT NULL,
    Role         INTEGER NOT NULL DEFAULT 0,
    IsBlocked    INTEGER NOT NULL DEFAULT 0,
    BlockReason  TEXT    NULL,
    CreatedAt    TEXT    NOT NULL,
    LastLoginAt  TEXT    NULL
);";
            await command.ExecuteNonQueryAsync();
        }

        /// <summary>
        /// Возвращает пользователя по логину или null, если такого пользователя нет.
        /// </summary>
        public async Task<UserRecord?> GetByLoginAsync(string login)
        {
            await using var connection = CreateConnection();
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = "SELECT * FROM Users WHERE Login = $login LIMIT 1;";
            command.Parameters.AddWithValue("$login", login);

            await using var reader = await command.ExecuteReaderAsync();
            return await reader.ReadAsync() ? ReadUser(reader) : null;
        }

        /// <summary>
        /// Возвращает пользователя по идентификатору или null.
        /// </summary>
        public async Task<UserRecord?> GetByIdAsync(int id)
        {
            await using var connection = CreateConnection();
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = "SELECT * FROM Users WHERE Id = $id LIMIT 1;";
            command.Parameters.AddWithValue("$id", id);

            await using var reader = await command.ExecuteReaderAsync();
            return await reader.ReadAsync() ? ReadUser(reader) : null;
        }

        /// <summary>
        /// Возвращает всех пользователей, сначала администраторов.
        /// </summary>
        public async Task<IReadOnlyList<UserRecord>> GetAllAsync()
        {
            await using var connection = CreateConnection();
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
SELECT * FROM Users
ORDER BY Role DESC, IsBlocked ASC, Login ASC;";

            var result = new List<UserRecord>();
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                result.Add(ReadUser(reader));
            }

            return result;
        }

        /// <summary>
        /// Подсчитывает количество администраторов в системе.
        /// </summary>
        public async Task<int> CountAdminsAsync()
        {
            await using var connection = CreateConnection();
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM Users WHERE Role = $role;";
            command.Parameters.AddWithValue("$role", (int)UserRole.Admin);

            return Convert.ToInt32(await command.ExecuteScalarAsync());
        }

        /// <summary>
        /// Создаёт нового пользователя и возвращает его идентификатор.
        /// </summary>
        /// <exception cref="InvalidOperationException">Логин уже занят.</exception>
        public async Task<int> CreateAsync(
            string login,
            string passwordHash,
            string salt,
            string displayName,
            UserRole role)
        {
            if (await GetByLoginAsync(login) is not null)
                throw new InvalidOperationException($"Пользователь с логином '{login}' уже существует");

            await using var connection = CreateConnection();
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
INSERT INTO Users (Login, PasswordHash, Salt, DisplayName, Role, IsBlocked, BlockReason, CreatedAt)
VALUES ($login, $passwordHash, $salt, $displayName, $role, 0, NULL, $createdAt);
SELECT LAST_INSERT_ROWID();";
            command.Parameters.AddWithValue("$login", login);
            command.Parameters.AddWithValue("$passwordHash", passwordHash);
            command.Parameters.AddWithValue("$salt", salt);
            command.Parameters.AddWithValue("$displayName", displayName);
            command.Parameters.AddWithValue("$role", (int)role);
            command.Parameters.AddWithValue("$createdAt", DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture));

            return Convert.ToInt32(await command.ExecuteScalarAsync());
        }

        /// <summary>
        /// Обновляет логин, отображаемое имя и роль пользователя.
        /// </summary>
        /// <exception cref="InvalidOperationException">Новый логин уже занят другим пользователем.</exception>
        public async Task<bool> UpdateAsync(int id, string login, string displayName, UserRole role)
        {
            var existing = await GetByLoginAsync(login);
            if (existing is not null && existing.Id != id)
                throw new InvalidOperationException($"Пользователь с логином '{login}' уже существует");

            await using var connection = CreateConnection();
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
UPDATE Users
SET Login = $login, DisplayName = $displayName, Role = $role
WHERE Id = $id;";
            command.Parameters.AddWithValue("$login", login);
            command.Parameters.AddWithValue("$displayName", displayName);
            command.Parameters.AddWithValue("$role", (int)role);
            command.Parameters.AddWithValue("$id", id);

            return await command.ExecuteNonQueryAsync() > 0;
        }

        /// <summary>
        /// Удаляет пользователя из базы данных.
        /// </summary>
        public async Task<bool> DeleteAsync(int id)
        {
            await using var connection = CreateConnection();
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM Users WHERE Id = $id;";
            command.Parameters.AddWithValue("$id", id);

            return await command.ExecuteNonQueryAsync() > 0;
        }

        /// <summary>
        /// Блокирует или разблокирует пользователя с указанием причины.
        /// </summary>
        public async Task<bool> SetBlockedAsync(int id, bool blocked, string? reason)
        {
            await using var connection = CreateConnection();
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
UPDATE Users
SET IsBlocked = $blocked, BlockReason = $reason
WHERE Id = $id;";
            command.Parameters.AddWithValue("$blocked", blocked ? 1 : 0);
            command.Parameters.AddWithValue("$reason", blocked ? reason : null);
            command.Parameters.AddWithValue("$id", id);

            return await command.ExecuteNonQueryAsync() > 0;
        }

        /// <summary>
        /// Обновляет пароль пользователя.
        /// </summary>
        public async Task<bool> UpdatePasswordAsync(int id, string passwordHash, string salt)
        {
            await using var connection = CreateConnection();
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
UPDATE Users
SET PasswordHash = $passwordHash, Salt = $salt
WHERE Id = $id;";
            command.Parameters.AddWithValue("$passwordHash", passwordHash);
            command.Parameters.AddWithValue("$salt", salt);
            command.Parameters.AddWithValue("$id", id);

            return await command.ExecuteNonQueryAsync() > 0;
        }

        /// <summary>
        /// Отмечает время последнего успешного входа пользователя.
        /// </summary>
        public async Task UpdateLastLoginAsync(int id)
        {
            await using var connection = CreateConnection();
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = "UPDATE Users SET LastLoginAt = $time WHERE Id = $id;";
            command.Parameters.AddWithValue("$time", DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture));
            command.Parameters.AddWithValue("$id", id);

            await command.ExecuteNonQueryAsync();
        }

        private SqliteConnection CreateConnection()
        {
            var builder = new SqliteConnectionStringBuilder
            {
                DataSource = _databasePath,
                Mode = SqliteOpenMode.ReadWriteCreate,
                Cache = SqliteCacheMode.Shared,
                Pooling = true
            };

            return new SqliteConnection(builder.ConnectionString);
        }

        private static UserRecord ReadUser(SqliteDataReader reader)
        {
            return new UserRecord
            {
                Id = reader.GetInt32(reader.GetOrdinal("Id")),
                Login = reader.GetString(reader.GetOrdinal("Login")),
                PasswordHash = reader.GetString(reader.GetOrdinal("PasswordHash")),
                Salt = reader.GetString(reader.GetOrdinal("Salt")),
                DisplayName = reader.GetString(reader.GetOrdinal("DisplayName")),
                Role = (UserRole)reader.GetInt32(reader.GetOrdinal("Role")),
                IsBlocked = reader.GetInt32(reader.GetOrdinal("IsBlocked")) != 0,
                BlockReason = reader.IsDBNull(reader.GetOrdinal("BlockReason"))
                    ? null
                    : reader.GetString(reader.GetOrdinal("BlockReason")),
                CreatedAt = ParseDate(reader.GetString(reader.GetOrdinal("CreatedAt"))),
                LastLoginAt = reader.IsDBNull(reader.GetOrdinal("LastLoginAt"))
                    ? null
                    : ParseDate(reader.GetString(reader.GetOrdinal("LastLoginAt")))
            };
        }

        private static DateTime ParseDate(string value)
        {
            return DateTime.Parse(value, CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind);
        }
    }
}
