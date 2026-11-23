using System;
using System.IO;

namespace RGRTU.Messenger.Client.Models
{
    /// <summary>
    /// Метаданные файла журнала для левой таблицы вкладки «Журнал событий».
    /// Для клиентских логов FullPath заполняется локальным путём, для серверных — пустой.
    /// </summary>
    public class LogFileViewModel
    {
        public LogSourceKind Source { get; set; }
        public string SourceDisplay => Source == LogSourceKind.Server ? "Сервер" : "Клиент";

        public string Name { get; set; } = string.Empty;
        public string FullPath { get; set; } = string.Empty;
        public DateTime Date { get; set; }
        public long SizeBytes { get; set; }

        public string SizeText
        {
            get
            {
                if (SizeBytes < 1024)
                    return $"{SizeBytes} Б";
                if (SizeBytes < 1024 * 1024)
                    return $"{SizeBytes / 1024.0:F1} КБ";
                return $"{SizeBytes / (1024.0 * 1024.0):F1} МБ";
            }
        }
    }
}
