using System;
using System.Threading.Tasks;
using Serilog;
using RGRTU.Messenger.Server.Admin;
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

    // 4. Запускаем сетевой сервер и консоль администратора параллельно
    var server = new MessengerServer(ServerConfig.Port, userRepository);
    _ = server.StartAsync();

    var adminConsole = new AdminConsole(userRepository, server);
    await adminConsole.RunAsync();

    Log.Information("Консоль администратора закрыта, сервер останавливается");
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
