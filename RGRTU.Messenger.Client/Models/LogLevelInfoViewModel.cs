using System.Windows.Media;

namespace RGRTU.Messenger.Client.Models
{
    /// <summary>
    /// Уровень события журнала: используется и в статистике файла, и в легенде.
    /// </summary>
    public class LogLevelInfoViewModel
    {
        public string Code { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int Count { get; set; }
        public Brush Color { get; set; } = Brushes.Gray;

        /// <summary>
        /// Строка для блока статистики: «INF: 120».
        /// </summary>
        public string StatDisplay => $"{Code}: {Count}";

        /// <summary>
        /// Строка для легенды: «INF — информация».
        /// </summary>
        public string LegendDisplay => string.IsNullOrWhiteSpace(Description)
            ? Code
            : $"{Code} — {Description}";
    }
}
