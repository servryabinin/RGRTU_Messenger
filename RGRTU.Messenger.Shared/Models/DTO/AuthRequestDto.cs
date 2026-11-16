using System;
using System.Collections.Generic;
using System.Text;

namespace RGRTU.Messenger.Shared.Models.DTO
{
    /// <summary>
    /// Класс для передачи данных запроса авторизации.
    /// </summary>
    public class AuthRequestDto
    {
        /// <summary>
        /// Логин пользователя.
        /// </summary>
        public string Login { get; set; }

        /// <summary>
        /// Захешированный пароль пользователя.
        /// </summary>
        public string PasswordHash { get; set; }
    }
}
