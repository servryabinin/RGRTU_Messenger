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
    }
}
