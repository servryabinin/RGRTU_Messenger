using System;
using System.IO;
using System.Text.Json;

namespace RGRTU.Messenger.Client.Configuration
{
    /// <summary>
    /// Статический класс, предоставляющий централизованный доступ к настройкам 
    /// клиентской части приложения, загружаемым из файла appsettings.json.
    /// Использование внешней конфигурации позволяет изменять параметры сети 
    /// и логирования без перекомпиляции исходного кода.
    /// </summary>
    public static class ClientConfig
    {
        #region Настройки сети

        /// <summary>
        /// IP-адрес сервера, к которому клиент пытается установить TCP-подключение (п. 3.a).
        /// </summary>
        /// <value>Строковое представление IP-адреса (например, "127.0.0.1").</value>
        public static string ServerIp { get; private set; }

        /// <summary>
        /// Сетевой порт сервера для установления входящих подключений.
        /// </summary>
        /// <value>Целочисленное значение порта (по умолчанию 8888).</value>
        public static int ServerPort { get; private set; }

        /// <summary>
        /// Максимальное время ожидания установления соединения с сервером.
        /// </summary>
        /// <value>Время ожидания в миллисекундах (по умолчанию 5000 мс).</value>
        public static int ConnectionTimeoutMs { get; private set; }

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
        /// <value>Строка с именем файла (например, "client_log_.txt").</value>
        public static string LogFileName { get; private set; }

        /// <summary>
        /// Шаблон форматирования вывода сообщений журнала в консоль отладки Visual Studio.
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
        /// Минимально допустимая длина строки логина при валидации на стороне клиента 
        /// перед отправкой запроса на авторизацию.
        /// </summary>
        /// <value>Минимальное количество символов (по умолчанию 3).</value>
        public static int MinLoginLength { get; private set; }

        #endregion

        /// <summary>
        /// Загружает настройки из указанного JSON-файла, парсит их и сохраняет значения 
        /// в статические свойства класса для дальнейшего использования в приложении.
        /// </summary>
        /// <param name="path">Относительный или абсолютный путь к файлу настроек (по умолчанию "appsettings.json").</param>
        /// <exception cref="FileNotFoundException">Выбрасывается, если файл конфигурации не найден по указанному пути.</exception>
        /// <exception cref="JsonException">Выбрасывается, если содержимое файла не является валидным JSON или не содержит требуемых ключей.</exception>
        public static void Load(string path = "appsettings.json")
        {
            if (!File.Exists(path))
                throw new FileNotFoundException($"Файл конфигурации не найден: {path}");

            string json = File.ReadAllText(path);

            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            ServerIp = root.GetProperty("Network").GetProperty("ServerIp").GetString();
            ServerPort = root.GetProperty("Network").GetProperty("ServerPort").GetInt32();
            ConnectionTimeoutMs = root.GetProperty("Network").GetProperty("ConnectionTimeoutMs").GetInt32();

            LogDirectory = root.GetProperty("Logging").GetProperty("DirectoryPath").GetString();
            LogFileName = root.GetProperty("Logging").GetProperty("FileNameTemplate").GetString();
            ConsoleLogTemplate = root.GetProperty("Logging").GetProperty("ConsoleOutputTemplate").GetString();
            FileLogTemplate = root.GetProperty("Logging").GetProperty("FileOutputTemplate").GetString();

            MinLoginLength = root.GetProperty("BusinessRules").GetProperty("MinLoginLength").GetInt32();
        }
    }
}