using System;
using System.Collections.Generic;
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
        Offline = 0,

        /// <summary>
        /// Пользователь подключен и активен
        /// </summary>
        Online = 1,

        /// <summary>
        /// Пользователь подключен, но не активен (отошел)
        /// </summary>
        Away = 2,

        /// <summary>
        /// Пользователь занят (не беспокоить)
        /// </summary>
        Busy = 3
    }
}
