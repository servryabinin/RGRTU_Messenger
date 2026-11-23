using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using RGRTU.Messenger.Client.Configuration;
using RGRTU.Messenger.Client.Models;

namespace RGRTU.Messenger.Client.Services
{
    /// <summary>
    /// Читает и разбирает файлы журналов Serilog — как локальные (клиент), так и
    /// полученные по сети с сервера.
    /// </summary>
    public class LogFileService
    {
        private static readonly Regex EntryPattern = new(
            @"^\[(?<time>\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2})\]\s+" +
            @"\[(?<level>[A-Za-z]{3,5})\]\s+" +
            @"(?:\[(?<source>[^\]]+)\]\s+)?(?<message>.*)$",
            RegexOptions.Compiled);

        private readonly string _directory;

        public LogFileService()
        {
            _directory = ClientConfig.LogDirectory;
        }

        /// <summary>
        /// Возвращает локальные файлы журналов клиента, отсортированные по дате изменения.
        /// </summary>
        public List<LogFileViewModel> GetLocalFiles()
        {
            if (string.IsNullOrWhiteSpace(_directory) || !Directory.Exists(_directory))
                return new List<LogFileViewModel>();

            return Directory.GetFiles(_directory, "*.txt")
                .Select(path => new FileInfo(path))
                .OrderByDescending(file => file.LastWriteTime)
                .Select(file => new LogFileViewModel
                {
                    Source = LogSourceKind.Client,
                    Name = file.Name,
                    FullPath = file.FullName,
                    Date = file.LastWriteTime,
                    SizeBytes = file.Length
                })
                .ToList();
        }

        /// <summary>
        /// Читает локальный файл клиента. Serilog держит его открытым для записи,
        /// поэтому используем FileShare.ReadWrite | Delete вместо File.ReadAllLines.
        /// </summary>
        public List<LogEntryViewModel> ReadEntries(string filePath)
        {
            using var stream = new FileStream(
                filePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(stream);

            return ParseContent(reader.ReadToEnd());
        }

        /// <summary>
        /// Разбирает текст лога (локального или присланного сервером) на события.
        /// Строки без собственного времени приклеиваются к предыдущему событию.
        /// </summary>
        public List<LogEntryViewModel> ParseContent(string content)
        {
            var result = new List<LogEntryViewModel>();

            using var stringReader = new StringReader(content ?? string.Empty);
            string? rawLine;
            while ((rawLine = stringReader.ReadLine()) != null)
            {
                Match match = EntryPattern.Match(rawLine);

                if (match.Success)
                {
                    string timeText = match.Groups["time"].Value;
                    DateTime.TryParseExact(
                        timeText,
                        "yyyy-MM-dd HH:mm:ss",
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.None,
                        out DateTime timestamp);

                    result.Add(new LogEntryViewModel
                    {
                        Timestamp = timestamp,
                        TimestampText = timeText,
                        Level = match.Groups["level"].Value,
                        Source = match.Groups["source"].Success ? match.Groups["source"].Value : string.Empty,
                        Message = match.Groups["message"].Value
                    });
                }
                else if (result.Count > 0)
                {
                    // Продолжение предыдущего события (например, стек-трейс).
                    result[^1].Message += Environment.NewLine + rawLine;
                }
                else
                {
                    result.Add(new LogEntryViewModel
                    {
                        TimestampText = string.Empty,
                        Level = string.Empty,
                        Message = rawLine
                    });
                }
            }

            return result;
        }

        /// <summary>
        /// Строит строку вида «INF: 12; WRN: 3; ERR: 1».
        /// </summary>
        public string BuildTypeStats(IEnumerable<LogEntryViewModel> entries)
        {
            var groups = entries
                .Where(entry => !string.IsNullOrWhiteSpace(entry.Level))
                .GroupBy(entry => entry.Level)
                .OrderByDescending(group => group.Count())
                .Select(group => $"{group.Key}: {group.Count()}");

            string stats = string.Join("; ", groups);
            return string.IsNullOrEmpty(stats) ? "нет данных" : stats;
        }
    }
}
