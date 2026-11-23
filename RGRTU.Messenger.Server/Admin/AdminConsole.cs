using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using RGRTU.Messenger.Server.Configuration;
using RGRTU.Messenger.Server.Core;
using RGRTU.Messenger.Server.Data;
using Serilog;

namespace RGRTU.Messenger.Server.Admin
{
    /// <summary>
    /// Консоль администратора сервера: регистрация/вход администратора и управление
    /// пользователями (создание, редактирование, удаление, блокировка).
    /// </summary>
    public class AdminConsole
    {
        private const int MinPasswordLength = 4;

        private readonly UserRepository _users;
        private readonly MessengerServer _server;
        private UserRecord _currentAdmin = null!;

        public AdminConsole(UserRepository users, MessengerServer server)
        {
            _users = users;
            _server = server;
        }

        /// <summary>
        /// Выполняет вход администратора и открывает интерактивное меню управления.
        /// </summary>
        public async Task RunAsync()
        {
            _currentAdmin = await AuthenticateAsync();

            while (true)
            {
                ShowMenu();
                string choice = Console.ReadLine()?.Trim() ?? string.Empty;

                try
                {
                    switch (choice)
                    {
                        case "1":
                            await ShowUsersAsync();
                            break;
                        case "2":
                            await CreateUserAsync();
                            break;
                        case "3":
                            await EditUserAsync();
                            break;
                        case "4":
                            await DeleteUserAsync();
                            break;
                        case "5":
                            await ToggleBlockAsync();
                            break;
                        case "6":
                            await KickUserAsync();
                            break;
                        case "7":
                            await ChangeOwnPasswordAsync();
                            break;
                        case "0":
                            Log.Information("Администратор {Admin} завершил работу с консолью", _currentAdmin.Login);
                            Console.WriteLine("Выход из консоли администратора.");
                            return;
                        default:
                            Console.WriteLine("Неизвестный пункт меню. Повторите ввод.");
                            break;
                    }
                }
                catch (InvalidOperationException ex)
                {
                    Log.Warning(ex, "Операция администратора отклонена");
                    Console.WriteLine($"Ошибка: {ex.Message}");
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Неожиданная ошибка в консоли администратора");
                    Console.WriteLine($"Неожиданная ошибка: {ex.Message}");
                }

                Console.WriteLine();
                Console.Write("Нажмите Enter, чтобы продолжить...");
                Console.ReadLine();
            }
        }

        private async Task<UserRecord> AuthenticateAsync()
        {
            Console.WriteLine();
            Console.WriteLine("=== Авторизация администратора ===");

            while (await _users.CountAdminsAsync() == 0)
            {
                Console.WriteLine("Администратор в системе не найден. Создайте первого администратора.");

                try
                {
                    string login = ReadRequired("Логин администратора: ");
                    ValidateLogin(login);
                    string password = ReadNewPassword();
                    string passwordHash = PasswordHelperHash(password);

                    var (hash, salt) = PasswordHasher.CreateFromClientHash(passwordHash);
                    int id = await _users.CreateAsync(login, hash, salt, "Администратор", UserRole.Admin);

                    Log.Information("Создан первый администратор: {Login} (Id={Id})", login, id);
                    Console.WriteLine("Администратор создан. Выполните вход.");
                    Console.WriteLine();
                }
                catch (InvalidOperationException ex)
                {
                    Log.Warning(ex, "Не удалось создать первого администратора");
                    Console.WriteLine($"Ошибка: {ex.Message}");
                }
            }

            while (true)
            {
                string login = ReadRequired("Логин администратора: ");
                string password = ReadPassword("Пароль: ");
                var user = await _users.GetByLoginAsync(login);

                bool isValid = user is not null &&
                               user.Role == UserRole.Admin &&
                               !user.IsBlocked &&
                               PasswordHasher.VerifyClientHash(
                                   PasswordHelperHash(password),
                                   user.PasswordHash,
                                   user.Salt);

                if (isValid)
                {
                    await _users.UpdateLastLoginAsync(user!.Id);
                    Log.Information("Администратор {Login} вошёл в консоль управления", user.Login);
                    Console.WriteLine($"Добро пожаловать, {user.DisplayName}!");
                    return user;
                }

                Log.Warning("Неудачная попытка входа в консоль администратора: {Login}", login);
                Console.WriteLine("Неверный логин, пароль или учётная запись заблокирована.");
            }
        }

        private async Task ShowUsersAsync()
        {
            var users = await _users.GetAllAsync();
            Console.WriteLine();
            Console.WriteLine("=== Пользователи системы ===");
            Console.WriteLine($"{"Id",4}  {"Логин",-20} {"Имя",-20} {"Роль",-14} {"Состояние",-16} Последний вход");
            Console.WriteLine(new string('-', 100));

            foreach (var user in users)
            {
                string role = user.Role == UserRole.Admin ? "Администратор" : "Пользователь";
                string state = user.IsBlocked ? $"ЗАБЛОКИРОВАН ({user.BlockReason})" : "Активен";
                string lastLogin = user.LastLoginAt?.ToLocalTime().ToString("dd.MM.yyyy HH:mm") ?? "никогда";

                Console.WriteLine($"{user.Id,4}  {user.Login,-20} {user.DisplayName,-20} {role,-14} {state,-16} {lastLogin}");
            }

            Console.WriteLine($"Всего пользователей: {users.Count}");
        }

        private async Task CreateUserAsync()
        {
            Console.WriteLine();
            Console.WriteLine("=== Создание пользователя ===");

            string login = ReadRequired("Логин: ");
            ValidateLogin(login);
            string password = ReadNewPassword();
            string displayName = ReadValue("Отображаемое имя", login);
            UserRole role = ReadRole("Роль (1 - пользователь, 2 - администратор)", UserRole.User);

            var (hash, salt) = PasswordHasher.CreateFromClientHash(PasswordHelperHash(password));
            int id = await _users.CreateAsync(login, hash, salt, displayName, role);

            Log.Information("Администратор {Admin} создал пользователя Id={Id}, Login={Login}, Role={Role}",
                _currentAdmin.Login, id, login, role);
            Console.WriteLine($"Пользователь '{login}' создан (Id={id}).");
        }

        private async Task EditUserAsync()
        {
            Console.WriteLine();
            Console.WriteLine("=== Редактирование пользователя ===");
            await ShowUsersAsync();

            var user = await SelectUserAsync();
            if (user is null)
                return;

            string login = ReadValue("Новый логин", user.Login);
            ValidateLogin(login);
            string displayName = ReadValue("Новое отображаемое имя", user.DisplayName);
            UserRole role = ReadRole("Новая роль", user.Role);

            if (user.Role == UserRole.Admin && role == UserRole.User &&
                await _users.CountAdminsAsync() == 1)
            {
                throw new InvalidOperationException("Нельзя понизить роль последнего администратора");
            }

            await _users.UpdateAsync(user.Id, login, displayName, role);

            string? newPassword = ReadPassword("Новый пароль (Enter - оставить прежний): ", allowEmpty: true);
            if (!string.IsNullOrEmpty(newPassword))
            {
                ValidatePassword(newPassword);
                var (hash, salt) = PasswordHasher.CreateFromClientHash(PasswordHelperHash(newPassword));
                await _users.UpdatePasswordAsync(user.Id, hash, salt);
            }

            if (user.IsBlocked && role == UserRole.Admin)
            {
                await _users.SetBlockedAsync(user.Id, false, null);
            }

            Log.Information("Администратор {Admin} обновил пользователя Id={Id}, Login={Login}",
                _currentAdmin.Login, user.Id, login);
            Console.WriteLine("Данные пользователя обновлены.");
        }

        private async Task DeleteUserAsync()
        {
            Console.WriteLine();
            Console.WriteLine("=== Удаление пользователя ===");
            await ShowUsersAsync();

            var user = await SelectUserAsync();
            if (user is null)
                return;

            if (user.Id == _currentAdmin.Id)
                throw new InvalidOperationException("Нельзя удалить собственную учётную запись");

            if (user.Role == UserRole.Admin && await _users.CountAdminsAsync() == 1)
                throw new InvalidOperationException("Нельзя удалить последнего администратора");

            Console.Write($"Удалить пользователя '{user.Login}'? (д/н): ");
            if (!IsYes(Console.ReadLine()))
            {
                Console.WriteLine("Удаление отменено.");
                return;
            }

            await _users.DeleteAsync(user.Id);
            int disconnected = await _server.DisconnectUserAsync(user.Id, "Ваша учётная запись удалена администратором");

            Log.Information("Администратор {Admin} удалил пользователя Id={Id}, Login={Login}. Отключено сессий: {Count}",
                _currentAdmin.Login, user.Id, user.Login, disconnected);
            Console.WriteLine($"Пользователь удалён. Активных сессий отключено: {disconnected}.");
        }

        private async Task ToggleBlockAsync()
        {
            Console.WriteLine();
            Console.WriteLine("=== Блокировка / разблокировка пользователя ===");
            await ShowUsersAsync();

            var user = await SelectUserAsync();
            if (user is null)
                return;

            if (user.Id == _currentAdmin.Id)
                throw new InvalidOperationException("Нельзя заблокировать собственную учётную запись");

            if (user.IsBlocked)
            {
                await _users.SetBlockedAsync(user.Id, false, null);
                Log.Information("Администратор {Admin} разблокировал пользователя {Login}",
                    _currentAdmin.Login, user.Login);
                Console.WriteLine($"Пользователь '{user.Login}' разблокирован.");
                return;
            }

            if (user.Role == UserRole.Admin && await _users.CountAdminsAsync() == 1)
                throw new InvalidOperationException("Нельзя заблокировать последнего администратора");

            string reason = ReadRequired("Причина блокировки: ");
            await _users.SetBlockedAsync(user.Id, true, reason);
            int disconnected = await _server.DisconnectUserAsync(
                user.Id,
                $"Ваша учётная запись заблокирована администратором. Причина: {reason}");

            Log.Information("Администратор {Admin} заблокировал пользователя {Login}. Причина: {Reason}. Отключено сессий: {Count}",
                _currentAdmin.Login, user.Login, reason, disconnected);
            Console.WriteLine($"Пользователь заблокирован. Активных сессий отключено: {disconnected}.");
        }

        private async Task KickUserAsync()
        {
            Console.WriteLine();
            Console.WriteLine("=== Отключение пользователя ===");
            var activeUsers = await GetActiveUsersAsync();

            if (activeUsers.Count == 0)
            {
                Console.WriteLine("Нет активных пользователей.");
                return;
            }

            foreach (var session in activeUsers)
            {
                Console.WriteLine($"ClientId={session.Id,3}  UserId={session.UserId,3}  {session.Username}");
            }

            int clientId = ReadInt("ClientID пользователя для отключения: ", 1, int.MaxValue);
            var target = activeUsers.FirstOrDefault(s => s.Id == clientId);
            if (target is null)
            {
                Console.WriteLine("Пользователь с таким ClientID не подключён.");
                return;
            }

            string reason = ReadValue("Причина", "Отключён администратором");
            await _server.DisconnectSessionAsync(target.Id, reason);

            Log.Information("Администратор {Admin} отключил клиента ClientId={ClientId}, User={Login}. Причина: {Reason}",
                _currentAdmin.Login, target.Id, target.Username, reason);
            Console.WriteLine("Пользователь отключён.");
        }

        private async Task ChangeOwnPasswordAsync()
        {
            Console.WriteLine();
            Console.WriteLine("=== Смена собственного пароля ===");
            string currentPassword = ReadPassword("Текущий пароль: ");

            if (!PasswordHasher.VerifyClientHash(
                    PasswordHelperHash(currentPassword),
                    _currentAdmin.PasswordHash,
                    _currentAdmin.Salt))
            {
                Console.WriteLine("Неверный текущий пароль.");
                return;
            }

            string newPassword = ReadNewPassword();
            var (hash, salt) = PasswordHasher.CreateFromClientHash(PasswordHelperHash(newPassword));
            await _users.UpdatePasswordAsync(_currentAdmin.Id, hash, salt);

            _currentAdmin = (await _users.GetByIdAsync(_currentAdmin.Id))!;

            Log.Information("Администратор {Login} изменил свой пароль", _currentAdmin.Login);
            Console.WriteLine("Пароль изменён.");
        }

        private async Task<IReadOnlyList<ClientSession>> GetActiveUsersAsync()
        {
            return await _server.GetAuthenticatedSessionsAsync();
        }

        private async Task<UserRecord?> SelectUserAsync()
        {
            int id = ReadInt("Id пользователя: ", 1, int.MaxValue);
            var user = await _users.GetByIdAsync(id);

            if (user is null)
            {
                Console.WriteLine("Пользователь с таким Id не найден.");
                return null;
            }

            return user;
        }

        private static string PasswordHelperHash(string password)
        {
            return PasswordHasher.Sha256Hex(password);
        }

        private static void ShowMenu()
        {
            Console.WriteLine();
            Console.WriteLine("=== МЕНЮ АДМИНИСТРАТОРА ===");
            Console.WriteLine("1 - Список пользователей");
            Console.WriteLine("2 - Создать пользователя");
            Console.WriteLine("3 - Редактировать пользователя");
            Console.WriteLine("4 - Удалить пользователя");
            Console.WriteLine("5 - Заблокировать / разблокировать пользователя");
            Console.WriteLine("6 - Отключить подключённого пользователя");
            Console.WriteLine("7 - Сменить собственный пароль");
            Console.WriteLine("0 - Выход");
            Console.Write("Выберите действие: ");
        }

        private static void ValidateLogin(string login)
        {
            if (login.Length < ServerConfig.MinLoginLength)
                throw new InvalidOperationException($"Логин должен быть не короче {ServerConfig.MinLoginLength} символов");
        }

        private static void ValidatePassword(string password)
        {
            if (password.Length < MinPasswordLength)
                throw new InvalidOperationException($"Пароль должен быть не короче {MinPasswordLength} символов");
        }

        private static string ReadRequired(string prompt)
        {
            while (true)
            {
                Console.Write(prompt);
                string? value = Console.ReadLine()?.Trim();
                if (!string.IsNullOrEmpty(value))
                    return value;

                Console.WriteLine("Значение не может быть пустым.");
            }
        }

        private static string ReadValue(string prompt, string defaultValue)
        {
            Console.Write($"{prompt} (Enter - '{defaultValue}'): ");
            string? value = Console.ReadLine()?.Trim();
            return string.IsNullOrEmpty(value) ? defaultValue : value;
        }

        private static string ReadNewPassword()
        {
            while (true)
            {
                string password = ReadPassword("Пароль: ");
                if (password.Length < MinPasswordLength)
                {
                    Console.WriteLine($"Пароль должен быть не короче {MinPasswordLength} символов.");
                    continue;
                }

                string confirm = ReadPassword("Повторите пароль: ");
                if (password != confirm)
                {
                    Console.WriteLine("Пароли не совпадают.");
                    continue;
                }

                return password;
            }
        }

        private static string ReadPassword(string prompt, bool allowEmpty = false)
        {
            Console.Write(prompt);

            try
            {
                var password = new System.Text.StringBuilder();
                while (true)
                {
                    ConsoleKeyInfo key = Console.ReadKey(intercept: true);
                    if (key.Key == ConsoleKey.Enter)
                    {
                        Console.WriteLine();
                        string result = password.ToString();
                        if (!allowEmpty && result.Length == 0)
                            continue;
                        return result;
                    }

                    if (key.Key == ConsoleKey.Backspace && password.Length > 0)
                    {
                        password.Length--;
                        Console.Write("\b \b");
                        continue;
                    }

                    if (!char.IsControl(key.KeyChar))
                    {
                        password.Append(key.KeyChar);
                        Console.Write("*");
                    }
                }
            }
            catch (InvalidOperationException)
            {
                // Консоль может быть перенаправлена (например, при тестировании).
                string? value = Console.ReadLine();
                return value ?? string.Empty;
            }
        }

        private static int ReadInt(string prompt, int min, int max)
        {
            while (true)
            {
                Console.Write(prompt);
                string? value = Console.ReadLine()?.Trim();

                if (int.TryParse(value, out int result) && result >= min && result <= max)
                    return result;

                Console.WriteLine($"Введите целое число от {min} до {max}.");
            }
        }

        private static UserRole ReadRole(string prompt, UserRole defaultValue)
        {
            Console.WriteLine(prompt);
            Console.WriteLine("1 - пользователь");
            Console.WriteLine("2 - администратор");
            Console.Write("Выбор (Enter - оставить '{0}'): ", defaultValue == UserRole.Admin ? "администратор" : "пользователь");

            string? value = Console.ReadLine()?.Trim();
            return value switch
            {
                "" => defaultValue,
                "1" => UserRole.User,
                "2" => UserRole.Admin,
                _ => throw new InvalidOperationException("Неверный номер роли")
            };
        }

        private static bool IsYes(string? answer)
        {
            return answer?.Trim().Equals("д", StringComparison.OrdinalIgnoreCase) == true ||
                   answer?.Trim().Equals("да", StringComparison.OrdinalIgnoreCase) == true ||
                   answer?.Trim().Equals("y", StringComparison.OrdinalIgnoreCase) == true ||
                   answer?.Trim().Equals("yes", StringComparison.OrdinalIgnoreCase) == true;
        }
    }
}
