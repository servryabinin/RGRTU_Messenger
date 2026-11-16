using System;
using System.Threading.Tasks;
using Serilog;
using RGRTU.Messenger.Server.Core;
using RGRTU.Messenger.Server.Configuration;

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
    Log.Information("Конфигурация загружена. Порт: {Port}", ServerConfig.Port);

    // 3. Инициализируем сервер с портом из настроек
    var server = new MessengerServer(ServerConfig.Port);
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