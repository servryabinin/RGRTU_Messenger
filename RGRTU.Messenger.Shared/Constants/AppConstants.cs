using System;
using System.Collections.Generic;
using System.Text;

namespace RGRTU.Messenger.Shared.Constants
{
    /// <summary>
    /// Класс, содержащий глобальные константы приложения для избежания магических чисел и строк в коде.
    /// </summary>
    public static class AppConstants
    {
        /// <summary>
        /// Максимальный допустимый размер одного сетевого пакета (10 МБ) для защиты от переполнения памяти.
        /// </summary>
        public const int MAX_PACKET_SIZE = 10_000_000;

        /// <summary>
        /// Минимальный допустимый размер полезной нагрузки пакета.
        /// </summary>
        public const int MIN_PACKET_SIZE = 0;

        /// <summary>
        /// Сообщение об ошибке при некорректном размере пакета.
        /// </summary>
        public const string ERROR_INVALID_PACKET_SIZE = "Некорректная длина пакета";

        /// <summary>
        /// Максимальный размер передаваемого файла — 1 ГБ.
        /// </summary>
        public const long MAX_FILE_SIZE = 1L * 1024 * 1024 * 1024;

        /// <summary>
        /// Размер одного куска файла при потоковой передаче (128 КБ).
        /// </summary>
        public const int FILE_CHUNK_SIZE = 128 * 1024;

        /// <summary>
        /// Текст ошибки при превышении максимального размера файла.
        /// </summary>
        public const string ERROR_FILE_TOO_LARGE = "Файл не может быть больше 1 Гб";
    }
}
