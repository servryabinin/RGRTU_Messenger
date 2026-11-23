using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;

namespace RGRTU.Messenger.Shared.Models.DTO.Enums
{
    /// <summary>
    /// Перечисление возможных статусов пользователя (Требование 2.d)
    /// </summary>
    public enum UserStatus
    {
        /// <summary>
        /// Пользователь отключен (по умолчанию)
        /// </summary>
        [Description("Не в сети")]
        Offline = 0,

        /// <summary>
        /// Пользователь подключен и активен
        /// </summary>
        [Description("В сети")]
        Online = 1,

        /// <summary>
        /// Пользователь подключен, но не активен (отошел)
        /// </summary>
        [Description("Отошёл")]
        Away = 2,

        /// <summary>
        /// Пользователь занят (не беспокоить)
        /// </summary>
        [Description("Занят")]
        Busy = 3
    }
}
