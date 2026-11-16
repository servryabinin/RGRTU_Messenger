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
        /// Имя пользователя
        /// </summary>
        public string Username { get; set; }

        /// <summary>
        /// Является ли пользователь администратором
        /// </summary>
        public bool IsAdmin { get; set; } // Для требования 2.a
    }
}
