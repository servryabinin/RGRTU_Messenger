using System;

namespace RGRTU.Messenger.Client.Models
{
    /// <summary>
    /// Одна строка журнала событий. Строки без собственного времени (стек-трейсы,
    /// продолжения сообщений) приклеиваются к предыдущему событию.
    /// </summary>
    public class LogEntryViewModel
    {
        public DateTime Timestamp { get; set; }
        public string TimestampText { get; set; } = string.Empty;
        public string Level { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
    }
}
