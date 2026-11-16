using System;
using System.Collections.Generic;
using System.Text;

namespace RGRTU.Messenger.Shared.Models.DTO
{
    /// <summary>
    /// Класс для передачи сообщений в чате
    /// </summary>
    public class ChatMessageDto
    {
        /// <summary>
        /// Id отправителя
        /// </summary>
        public int SenderId { get; set; }

        /// <summary>
        /// Имя отправителя
        /// </summary>
        public string SenderName { get; set; }

        /// <summary>
        /// Текст сообщения
        /// </summary>
        public string Text { get; set; }

        /// <summary>
        /// Временная метка отправки сообщения
        /// </summary>
        public DateTime Timestamp { get; set; }
    }
}
