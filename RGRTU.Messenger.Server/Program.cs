using System;
using System.Threading.Tasks;
using Serilog;
using RGRTU.Messenger.Server.Configuration;
using RGRTU.Messenger.Server.Core;
using RGRTU.Messenger.Server.Data;

// 1. Загружаем конфигурацию из файла
ServerConfig.Load();

// 2. Настраиваем логирование на основе загруженных параметров (п. 1.f)
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console(outputTemplate: ServerConfig.ConsoleLogTemplate)
    .WriteTo.File(
        path: ServerConfig.LogDirectory + ServerConfig.LogFileName,
        rollingInterval: RollingInterval.Day,
        outputTemplate: ServerConfig.FileLogTemplate)
    .CreateLogger();

try
{
    Log.Information("=== Запуск сервера Мессенджер РГРТУ ===");
    Log.Information("Конфигурация загружена. Порт: {Port}, БД: {Database}",
        ServerConfig.Port, ServerConfig.DatabasePath);

    // 3. Инициализируем хранилище пользователей SQLite (п. 2.a)
    var userRepository = new UserRepository(ServerConfig.DatabasePath);
    await userRepository.InitializeAsync();

    // 4. При первом запуске создаём администратора автоматически, без консоли.
    if (await userRepository.CountAdminsAsync() == 0)
    {
        string adminLogin = ServerConfig.AdminLoginIdentifier;
        string adminPassword = ServerConfig.DefaultAdminPassword;
        string clientHash = PasswordHasher.Sha256Hex(adminPassword);
        var (hash, salt) = PasswordHasher.CreateFromClientHash(clientHash);

        int adminId = await userRepository.CreateAsync(
            adminLogin, hash, salt, "Администратор", UserRole.Admin);

        Log.Warning(
            "Создан администратор по умолчанию: {Login} / {Password} (Id={Id}). Измените пароль через GUI.",
            adminLogin, adminPassword, adminId);
    }

    // 5. Запускаем сетевой сервер. Больше нет консоли администратора — только GUI-клиент.
    var server = new MessengerServer(ServerConfig.Port, userRepository);
    await server.StartAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Сервер завершил работу из-за критической ошибки");
}
finally
{
    Log.Information("=== Сервер остановлен ===");
    Log.CloseAndFlush();
}
