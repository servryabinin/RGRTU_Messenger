using System;
using System.IO;
using System.Text.Json;

namespace RGRTU.Messenger.Server.Configuration
{
    /// <summary>
    /// Статический класс, предоставляющий централизованный доступ к настройкам 
    /// серверной части приложения, загружаемым из файла appsettings.json.
    /// Использование внешней конфигурации позволяет изменять сетевые порты, 
    /// параметры логирования и бизнес-правила без перекомпиляции исходного кода.
    /// </summary>
    public static class ServerConfig
    {
        #region Настройки сервера

        /// <summary>
        /// Сетевой порт, на котором сервер ожидает входящие TCP-подключения.
        /// </summary>
        /// <value>Целочисленное значение порта (по умолчанию 8888).</value>
        public static int Port { get; private set; }

        /// <summary>
        /// Максимально допустимый размер полезной нагрузки (payload) в байтах.
        /// Используется для защиты сервера от переполнения памяти при некорректных или вредоносных запросах.
        /// </summary>
        /// <value>Максимальный размер в байтах (по умолчанию 10 000 000).</value>
        public static int MaxPayloadSizeBytes { get; private set; }

        /// <summary>
        /// Путь к файлу базы данных SQLite с учётными записями пользователей (п. 2.a).
        /// </summary>
        /// <value>Относительный или абсолютный путь (по умолчанию "data/messenger.db").</value>
        public static string DatabasePath { get; private set; } = "data/messenger.db";

        #endregion

        #region Настройки логирования

        /// <summary>
        /// Относительный путь к директории для сохранения файлов журналов событий (п. 1.f).
        /// </summary>
        /// <value>Путь к папке (например, "logs/").</value>
        public static string LogDirectory { get; private set; }

        /// <summary>
        /// Шаблон имени файла журнала с поддержкой ежедневной автоматической ротации (rolling).
        /// </summary>
        /// <value>Строка с именем файла (например, "server_log_.txt").</value>
        public static string LogFileName { get; private set; }

        /// <summary>
        /// Шаблон форматирования вывода сообщений журнала в консоль отладки.
        /// </summary>
        /// <value>Строка формата Serilog, включающая время, уровень и сообщение.</value>
        public static string ConsoleLogTemplate { get; private set; }

        /// <summary>
        /// Шаблон форматирования вывода сообщений журнала в текстовый файл.
        /// </summary>
        /// <value>Строка формата Serilog, включающая дату, время, уровень, источник и сообщение.</value>
        public static string FileLogTemplate { get; private set; }

        #endregion

        #region Бизнес-правила

        /// <summary>
        /// Имя пользователя по умолчанию для неавторизованной сессии до момента успешного входа.
        /// </summary>
        /// <value>Строковое значение (по умолчанию "Guest").</value>
        public static string DefaultGuestName { get; private set; }

        /// <summary>
        /// Устаревший идентификатор администратора: сейчас роль хранится в базе данных SQLite (п. 2.a).
        /// Оставлен в конфигурации для обратной совместимости.
        /// </summary>
        /// <value>Строковое значение идентификатора (по умолчанию "admin").</value>
        public static string AdminLoginIdentifier { get; private set; }

        /// <summary>
        /// Минимально допустимая длина строки логина при валидации на стороне сервера.
        /// </summary>
        /// <value>Минимальное количество символов (по умолчанию 3).</value>
        public static int MinLoginLength { get; private set; }

        #endregion

        /// <summary>
        /// Загружает настройки из указанного JSON-файла, парсит их и сохраняет значения 
        /// в статические свойства класса для дальнейшего использования в приложении.
        /// Значения извлекаются немедленно внутри блока `using`, чтобы избежать 
        /// исключения ObjectDisposedException при последующем обращении к свойствам 
        /// после уничтожения объекта JsonDocument.
        /// </summary>
        /// <param name="path">Относительный или абсолютный путь к файлу настроек (по умолчанию "appsettings.json").</param>
        /// <exception cref="FileNotFoundException">Выбрасывается, если файл конфигурации не найден по указанному пути.</exception>
        /// <exception cref="InvalidOperationException">Выбрасывается, если JSON не содержит требуемых ключей конфигурации.</exception>
        public static void Load(string path = "appsettings.json")
        {
            if (!File.Exists(path))
                throw new FileNotFoundException($"Файл конфигурации не найден: {path}");

            string json = File.ReadAllText(path);

            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            var serverSection = root.GetProperty("Server");

            Port = serverSection.GetProperty("Port").GetInt32();
            MaxPayloadSizeBytes = serverSection.GetProperty("MaxPayloadSizeBytes").GetInt32();

            DatabasePath = serverSection.TryGetProperty("DatabasePath", out var databasePath) &&
                           databasePath.ValueKind == System.Text.Json.JsonValueKind.String
                ? databasePath.GetString() ?? "data/messenger.db"
                : "data/messenger.db";

            LogDirectory = root.GetProperty("Logging").GetProperty("DirectoryPath").GetString();
            LogFileName = root.GetProperty("Logging").GetProperty("FileNameTemplate").GetString();
            ConsoleLogTemplate = root.GetProperty("Logging").GetProperty("ConsoleOutputTemplate").GetString();
            FileLogTemplate = root.GetProperty("Logging").GetProperty("FileOutputTemplate").GetString();

            DefaultGuestName = root.GetProperty("BusinessRules").GetProperty("DefaultGuestName").GetString();
            AdminLoginIdentifier = root.GetProperty("BusinessRules").GetProperty("AdminLoginIdentifier").GetString();
            MinLoginLength = root.GetProperty("BusinessRules").GetProperty("MinLoginLength").GetInt32();
        }
    }
}