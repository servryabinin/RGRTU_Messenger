using System;
using System.Collections.Generic;
using System.Text;

namespace RGRTU.Messenger.Shared.Models.DTO
{
    /// <summary>
    /// Класс для передачи данных ответа на запрос авторизации
    /// </summary>
    public class AuthResponseDto
    {
        /// <summary>
        /// Успешность авторизации
        /// </summary>
        public bool IsSuccess { get; set; }

        /// <summary>
        /// Сообщение об ошибке авторизации
        /// </summary>
        public string ErrorMessage { get; set; }

        /// <summary>
        /// Id пользователя
        /// </summary>
        public int UserId { get; set; }

        /// <summary>
        /// Логин пользователя, под которым выполнен вход
        /// </summary>
        public string Login { get; set; } = string.Empty;

        /// <summary>
        /// Имя пользователя (отображается в чате)
        /// </summary>
        public string Username { get; set; }

        /// <summary>
        /// Является ли пользователь администратором
        /// </summary>
        public bool IsAdmin { get; set; } // Для требования 2.a

        /// <summary>
        /// Добавлен ли пользователь в общий чат администратором.
        /// </summary>
        public bool IsInChat { get; set; }
    }
}
