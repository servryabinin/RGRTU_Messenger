using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using RGRTU.Messenger.Server.Configuration;
using RGRTU.Messenger.Server.Data;
using RGRTU.Messenger.Shared.Models.DTO.Admin;
using Serilog;

namespace RGRTU.Messenger.Server.Core
{
    /// <summary>
    /// Обрабатывает административные запросы, пришедшие от WPF-клиента.
    /// Все операции доступны только авторизованному пользователю с ролью администратора.
    /// </summary>
    public class AdminRequestHandler
    {
        private readonly MessengerServer _server;
        private readonly UserRepository _users;

        public AdminRequestHandler(MessengerServer server, UserRepository users)
        {
            _server = server;
            _users = users;
        }

        /// <summary>
        /// Возвращает список пользователей для таблицы администратора.
        /// </summary>
        public async Task<AdminUsersResponseDto> GetUsersAsync(ClientSession session)
        {
            if (!IsAdmin(session))
                return FailUsers("Недостаточно прав для просмотра пользователей");

            try
            {
                var users = await _users.GetAllAsync();
                return new AdminUsersResponseDto
                {
                    IsSuccess = true,
                    Users = users.Select(ToDto).ToList()
                };
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Ошибка получения списка пользователей администратором {Admin}", session.Username);
                return FailUsers("Не удалось получить список пользователей");
            }
        }

        /// <summary>
        /// Создаёт пользователя.
        /// </summary>
        public async Task<AdminActionResponseDto> CreateAsync(
            ClientSession session,
            AdminCreateUserRequestDto request)
        {
            if (!IsAdmin(session))
                return Fail("Недостаточно прав для создания пользователей");

            try
            {
                string login = (request.Login ?? string.Empty).Trim();
                string displayName = string.IsNullOrWhiteSpace(request.DisplayName)
                    ? login
                    : request.DisplayName.Trim();

                if (login.Length < ServerConfig.MinLoginLength)
                    return Fail($"Логин должен быть не короче {ServerConfig.MinLoginLength} символов");
                if (string.IsNullOrWhiteSpace(request.PasswordHash))
                    return Fail("Пароль не может быть пустым");

                var (hash, salt) = PasswordHasher.CreateFromClientHash(request.PasswordHash);
                int id = await _users.CreateAsync(
                    login,
                    hash,
                    salt,
                    displayName,
                    ToServerRole(request.Role));

                Log.Information("Администратор {Admin} создал пользователя Id={Id}, Login={Login}, Role={Role}",
                    session.Username, id, login, request.Role);

                await _server.BroadcastChatMembersAsync();

                return Ok($"Пользователь '{login}' создан");
            }
            catch (InvalidOperationException ex)
            {
                return Fail(ex.Message);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Ошибка создания пользователя администратором {Admin}", session.Username);
                return Fail("Не удалось создать пользователя");
            }
        }

        /// <summary>
        /// Обновляет данные пользователя, при необходимости меняет пароль.
        /// </summary>
        public async Task<AdminActionResponseDto> UpdateAsync(
            ClientSession session,
            AdminUpdateUserRequestDto request)
        {
            if (!IsAdmin(session))
                return Fail("Недостаточно прав для редактирования пользователей");

            try
            {
                var user = await _users.GetByIdAsync(request.Id);
                if (user is null)
                    return Fail("Пользователь не найден");

                string login = (request.Login ?? string.Empty).Trim();
                string displayName = string.IsNullOrWhiteSpace(request.DisplayName)
                    ? login
                    : request.DisplayName.Trim();

                if (login.Length < ServerConfig.MinLoginLength)
                    return Fail($"Логин должен быть не короче {ServerConfig.MinLoginLength} символов");

                var newRole = ToServerRole(request.Role);
                if (user.Role == Data.UserRole.Admin &&
                    newRole == Data.UserRole.User &&
                    await _users.CountAdminsAsync() == 1)
                {
                    return Fail("Нельзя понизить роль последнего администратора");
                }

                await _users.UpdateAsync(request.Id, login, displayName, newRole);

                if (!string.IsNullOrWhiteSpace(request.PasswordHash))
                {
                    var (hash, salt) = PasswordHasher.CreateFromClientHash(request.PasswordHash);
                    await _users.UpdatePasswordAsync(request.Id, hash, salt);
                }

                Log.Information("Администратор {Admin} обновил пользователя Id={Id}, Login={Login}",
                    session.Username, request.Id, login);

                return Ok($"Данные пользователя '{login}' обновлены");
            }
            catch (InvalidOperationException ex)
            {
                return Fail(ex.Message);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Ошибка обновления пользователя администратором {Admin}", session.Username);
                return Fail("Не удалось обновить пользователя");
            }
        }

        /// <summary>
        /// Удаляет пользователя и отключает его активные сессии.
        /// </summary>
        public async Task<AdminActionResponseDto> DeleteAsync(
            ClientSession session,
            AdminDeleteUserRequestDto request)
        {
            if (!IsAdmin(session))
                return Fail("Недостаточно прав для удаления пользователей");

            try
            {
                var user = await _users.GetByIdAsync(request.Id);
                if (user is null)
                    return Fail("Пользователь не найден");

                if (user.Id == session.UserId)
                    return Fail("Нельзя удалить собственную учётную запись");

                if (user.Role == Data.UserRole.Admin && await _users.CountAdminsAsync() == 1)
                    return Fail("Нельзя удалить последнего администратора");

                await _users.SetChatMemberAsync(request.Id, false, removedByAdmin: true, invitedByAdmin: false);
                await _users.DeleteAsync(request.Id);
                int disconnected = await _server.DisconnectUserAsync(
                    request.Id,
                    "Ваша учётная запись удалена администратором");
                await _server.BroadcastChatMembersAsync();

                Log.Information("Администратор {Admin} удалил пользователя Id={Id}, Login={Login}. Отключено сессий: {Count}",
                    session.Username, request.Id, user.Login, disconnected);

                return Ok($"Пользователь '{user.Login}' удалён");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Ошибка удаления пользователя администратором {Admin}", session.Username);
                return Fail("Не удалось удалить пользователя");
            }
        }

        /// <summary>
        /// Блокирует или разблокирует пользователя; при блокировке отключает его сессии.
        /// </summary>
        public async Task<AdminActionResponseDto> SetBlockedAsync(
            ClientSession session,
            AdminBlockUserRequestDto request)
        {
            if (!IsAdmin(session))
                return Fail("Недостаточно прав для блокировки пользователей");

            try
            {
                var user = await _users.GetByIdAsync(request.Id);
                if (user is null)
                    return Fail("Пользователь не найден");

                if (user.Id == session.UserId)
                    return Fail("Нельзя заблокировать собственную учётную запись");

                if (request.IsBlocked &&
                    user.Role == Data.UserRole.Admin &&
                    await _users.CountAdminsAsync() == 1)
                {
                    return Fail("Нельзя заблокировать последнего администратора");
                }

                string reason = string.IsNullOrWhiteSpace(request.Reason)
                    ? "Заблокирован администратором"
                    : request.Reason.Trim();

                await _users.SetBlockedAsync(request.Id, request.IsBlocked, request.IsBlocked ? reason : null);

                int disconnected = 0;
                if (request.IsBlocked)
                {
                    await _users.SetChatMemberAsync(request.Id, false, removedByAdmin: true, invitedByAdmin: false);
                    disconnected = await _server.DisconnectUserAsync(
                        request.Id,
                        $"Ваша учётная запись заблокирована администратором. Причина: {reason}");
                }

                if (request.IsBlocked)
                    await _server.BroadcastChatMembersAsync();

                Log.Information("Администратор {Admin} {Action} пользователя {Login}. Причина: {Reason}. Отключено сессий: {Count}",
                    session.Username,
                    request.IsBlocked ? "заблокировал" : "разблокировал",
                    user.Login,
                    request.IsBlocked ? reason : "-",
                    disconnected);

                return Ok(request.IsBlocked
                    ? $"Пользователь '{user.Login}' заблокирован"
                    : $"Пользователь '{user.Login}' разблокирован");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Ошибка блокировки пользователя администратором {Admin}", session.Username);
                return Fail("Не удалось изменить состояние пользователя");
            }
        }

        /// <summary>
        /// Возвращает список файлов журнала самого сервера. Запрос приходит по тому же TCP-соединению,
        /// поэтому работает и для сервера, поднятого на другом ПК в локальной сети.
        /// </summary>
        public async Task<AdminLogFilesResponseDto> GetLogFilesAsync(ClientSession session)
        {
            if (!IsAdmin(session))
                return new AdminLogFilesResponseDto
                {
                    IsSuccess = false,
                    ErrorMessage = "Недостаточно прав для просмотра журналов сервера"
                };

            try
            {
                string directory = ServerConfig.LogDirectory;
                if (!Directory.Exists(directory))
                    return new AdminLogFilesResponseDto
                    {
                        IsSuccess = true,
                        Files = new List<AdminLogFileDto>()
                    };

                var files = await Task.Run(() => Directory.GetFiles(directory, "*.txt")
                    .Select(path => new FileInfo(path))
                    .OrderByDescending(file => file.LastWriteTime)
                    .Select(file => new AdminLogFileDto
                    {
                        Name = file.Name,
                        Date = file.LastWriteTime,
                        SizeBytes = file.Length
                    })
                    .ToList());

                return new AdminLogFilesResponseDto { IsSuccess = true, Files = files };
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Ошибка получения списка логов сервера администратором {Admin}", session.Username);
                return new AdminLogFilesResponseDto
                {
                    IsSuccess = false,
                    ErrorMessage = "Не удалось получить список логов сервера"
                };
            }
        }

        /// <summary>
        /// Читает содержимое одного файла журнала сервера.
        /// </summary>
        public async Task<AdminLogContentResponseDto> GetLogContentAsync(
            ClientSession session,
            AdminLogContentRequestDto request)
        {
            if (!IsAdmin(session))
                return new AdminLogContentResponseDto
                {
                    IsSuccess = false,
                    ErrorMessage = "Недостаточно прав для просмотра журналов сервера"
                };

            string fileName = request.FileName ?? string.Empty;
            if (string.IsNullOrWhiteSpace(fileName) ||
                Path.GetFileName(fileName) != fileName ||
                fileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            {
                return new AdminLogContentResponseDto
                {
                    IsSuccess = false,
                    ErrorMessage = "Некорректное имя файла журнала"
                };
            }

            try
            {
                string path = Path.Combine(ServerConfig.LogDirectory, fileName);
                if (!File.Exists(path))
                    return new AdminLogContentResponseDto
                    {
                        IsSuccess = false,
                        ErrorMessage = "Файл журнала не найден на сервере"
                    };

                string content = await Task.Run(() =>
                {
                    // Serilog продолжает писать в файл — разрешаем параллельное чтение.
                    using var stream = new FileStream(
                        path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                    using var reader = new StreamReader(stream);
                    return reader.ReadToEnd();
                });

                // Защита от превышения максимального размера сетевого пакета.
                const int maxContentChars = 2_000_000;
                if (content.Length > maxContentChars)
                    content = "[Показаны только последние записи файла]\n" +
                              content.Substring(content.Length - maxContentChars);

                return new AdminLogContentResponseDto
                {
                    IsSuccess = true,
                    FileName = fileName,
                    Content = content
                };
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Ошибка чтения лога сервера {File} администратором {Admin}", fileName, session.Username);
                return new AdminLogContentResponseDto
                {
                    IsSuccess = false,
                    ErrorMessage = "Не удалось прочитать лог сервера"
                };
            }
        }

        private static bool IsAdmin(ClientSession session)
        {
            return session.IsAuthenticated && session.Role == Data.UserRole.Admin;
        }

        private AdminUserDto ToDto(UserRecord user)
        {
            return new AdminUserDto
            {
                Id = user.Id,
                Login = user.Login,
                DisplayName = user.DisplayName,
                Role = ToDtoRole(user.Role),
                IsBlocked = user.IsBlocked,
                BlockReason = user.BlockReason,
                Status = _server.GetUserStatus(user.Id),
                CreatedAt = user.CreatedAt,
                LastLoginAt = user.LastLoginAt
            };
        }

        private static Data.UserRole ToServerRole(AdminUserRole role)
        {
            return role == AdminUserRole.Admin ? Data.UserRole.Admin : Data.UserRole.User;
        }

        private static AdminUserRole ToDtoRole(Data.UserRole role)
        {
            return role == Data.UserRole.Admin ? AdminUserRole.Admin : AdminUserRole.User;
        }

        private static AdminActionResponseDto Ok(string message)
        {
            return new AdminActionResponseDto { IsSuccess = true, ErrorMessage = message };
        }

        private static AdminActionResponseDto Fail(string message)
        {
            return new AdminActionResponseDto { IsSuccess = false, ErrorMessage = message };
        }

        private static AdminUsersResponseDto FailUsers(string message)
        {
            return new AdminUsersResponseDto { IsSuccess = false, ErrorMessage = message };
        }
    }
}
