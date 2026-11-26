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
    LastLoginAt  TEXT    NULL,
    IsInChat     INTEGER NOT NULL DEFAULT 0,
    IsChatRemovedByAdmin INTEGER NOT NULL DEFAULT 0
);";
            await command.ExecuteNonQueryAsync();

            await EnsureColumnExistsAsync(connection, "Users", "IsInChat", "INTEGER NOT NULL DEFAULT 0");
            await EnsureColumnExistsAsync(connection, "Users", "IsChatRemovedByAdmin", "INTEGER NOT NULL DEFAULT 0");
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
            command.Parameters.AddWithValue("$reason", blocked && reason is not null ? reason : DBNull.Value);
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
        /// Возвращает незаблокированных пользователей для списка участников чата.
        /// </summary>
        public async Task<IReadOnlyList<UserRecord>> GetActiveForChatAsync()
        {
            await using var connection = CreateConnection();
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
SELECT * FROM Users
WHERE IsBlocked = 0
ORDER BY DisplayName COLLATE NOCASE, Login COLLATE NOCASE;";

            var result = new List<UserRecord>();
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                result.Add(ReadUser(reader));
            }

            return result;
        }

        /// <summary>
        /// Добавляет или исключает пользователя из общего чата.
        /// </summary>
        public async Task<bool> SetChatMemberAsync(int id, bool isInChat, bool? removedByAdmin = null)
        {
            await using var connection = CreateConnection();
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            if (removedByAdmin.HasValue)
            {
                command.CommandText = @"
UPDATE Users
SET IsInChat = $inChat, IsChatRemovedByAdmin = $removed
WHERE Id = $id AND IsBlocked = 0;";
                command.Parameters.AddWithValue("$removed", removedByAdmin.Value ? 1 : 0);
            }
            else
            {
                command.CommandText = @"
UPDATE Users
SET IsInChat = $inChat
WHERE Id = $id AND IsBlocked = 0;";
            }

            command.Parameters.AddWithValue("$inChat", isInChat ? 1 : 0);
            command.Parameters.AddWithValue("$id", id);

            return await command.ExecuteNonQueryAsync() > 0;
        }

        /// <summary>
        /// Позволяет пользователю самому вернуться в чат, если он не был исключён администратором.
        /// </summary>
        public async Task<bool> TryJoinChatAsync(int id)
        {
            await using var connection = CreateConnection();
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
UPDATE Users
SET IsInChat = 1
WHERE Id = $id AND IsBlocked = 0 AND IsChatRemovedByAdmin = 0;";
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

        private static async Task EnsureColumnExistsAsync(SqliteConnection connection, string table, string column, string definition)
        {
            var check = connection.CreateCommand();
            check.CommandText = $"PRAGMA table_info({table});";

            bool exists = false;
            await using (var reader = await check.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    if (string.Equals(reader.GetString(1), column, StringComparison.OrdinalIgnoreCase))
                    {
                        exists = true;
                        break;
                    }
                }
            }

            if (exists)
                return;

            var alter = connection.CreateCommand();
            alter.CommandText = $"ALTER TABLE {table} ADD COLUMN {column} {definition};";
            await alter.ExecuteNonQueryAsync();
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
                    : ParseDate(reader.GetString(reader.GetOrdinal("LastLoginAt"))),
                IsInChat = !reader.IsDBNull(reader.GetOrdinal("IsInChat")) &&
                           reader.GetInt32(reader.GetOrdinal("IsInChat")) != 0,
                IsChatRemovedByAdmin = !reader.IsDBNull(reader.GetOrdinal("IsChatRemovedByAdmin")) &&
                                       reader.GetInt32(reader.GetOrdinal("IsChatRemovedByAdmin")) != 0
            };
        }

        private static DateTime ParseDate(string value)
        {
            return DateTime.Parse(value, CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind);
        }
    }
}
