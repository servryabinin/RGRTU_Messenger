using System;
using System.Windows;
using Serilog;
using RGRTU.Messenger.Client.Configuration;

namespace RGRTU.Messenger.Client
{
    /// <summary>
    /// Главный класс приложения, управляющий жизненным циклом WPF-приложения.
    /// Отвечает за инициализацию конфигурации и системы распределенного логирования при запуске,
    /// а также за корректное освобождение ресурсов и сохранение журналов при завершении работы (п. 1.f).
    /// </summary>
    public partial class App : Application
    {
        /// <summary>
        /// Вызывается средой выполнения при запуске приложения.
        /// Инициализирует загрузку настроек из appsettings.json и конфигурацию логгера 
        /// перед созданием и отображением главного окна.
        /// </summary>
        /// <param name="e">Аргументы события запуска приложения.</param>
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            ClientConfig.Load();
            ConfigureLogging();

            Log.Information("Клиентское приложение Мессенджер РГРТУ запущено");
        }

        /// <summary>
        /// Настраивает и инициализирует глобальный статический экземпляр логгера Serilog.
        /// Использует параметры из класса ClientConfig для вывода логов в консоль отладки 
        /// и в текстовый файл с поддержкой автоматической ежедневной ротации (п. 1.f).
        /// </summary>
        private void ConfigureLogging()
        {
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Information()
                .WriteTo.Console(outputTemplate: ClientConfig.ConsoleLogTemplate)
                .WriteTo.File(
                    path: ClientConfig.LogDirectory + ClientConfig.LogFileName,
                    rollingInterval: RollingInterval.Day,
                    outputTemplate: ClientConfig.FileLogTemplate)
                .CreateLogger();
        }

        /// <summary>
        /// Вызывается средой выполнения при завершении работы приложения.
        /// Гарантирует принудительную запись всех буферизированных записей журнала на диск 
        /// перед фактическим закрытием процесса, предотвращая потерю данных логирования.
        /// </summary>
        /// <param name="e">Аргументы события завершения работы приложения.</param>
        protected override void OnExit(ExitEventArgs e)
        {
            Log.Information("Клиентское приложение завершает работу");
            Log.CloseAndFlush();
            base.OnExit(e);
        }
    }
}