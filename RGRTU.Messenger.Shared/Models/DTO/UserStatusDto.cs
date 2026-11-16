using System;
using System.Collections.Generic;
using System.Text;
using RGRTU.Messenger.Shared.Models.DTO.Enums;

namespace RGRTU.Messenger.Shared.Models.DTO
{
    /// <summary>
    /// Класс DTO для передачи статуса пользователя
    /// </summary>
    public class UserStatusDto
    {
        /// <summary>
        /// Id пользователя
        /// </summary>
        public int UserId { get; set; }

        /// <summary>
        /// Имя пользователя
        /// </summary>
        public string Username { get; set; }

        /// <summary>
        /// Статус пользователя (например, "online", "offline", "away")
        /// </summary>
        public UserStatus Status { get; set; }
    }
}
