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
        /// Уникальный идентификатор сообщения.
        /// </summary>
        public string MessageId { get; set; } = string.Empty;

        /// <summary>
        /// Id отправителя
        /// </summary>
        public int SenderId { get; set; }

        /// <summary>
        /// Имя отправителя
        /// </summary>
        public string SenderName { get; set; } = string.Empty;

        /// <summary>
        /// Текст сообщения
        /// </summary>
        public string Text { get; set; } = string.Empty;

        /// <summary>
        /// Временная метка отправки сообщения
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// Имя прикреплённого файла (null, если вложения нет).
        /// </summary>
        public string? AttachmentFileName { get; set; }

        /// <summary>
        /// Размер прикреплённого файла в байтах.
        /// </summary>
        public long AttachmentFileSize { get; set; }

        /// <summary>
        /// Содержимое файла в Base64.
        /// </summary>
        public string? AttachmentBase64 { get; set; }

        /// <summary>
        /// Является ли сообщение стикером/эмодзи.
        /// </summary>
        public bool IsSticker { get; set; }

        /// <summary>
        /// Сообщение было отредактировано.
        /// </summary>
        public bool IsEdited { get; set; }

        /// <summary>
        /// ID сообщения, на которое отвечает это сообщение.
        /// </summary>
        public string? ReplyToMessageId { get; set; }

        /// <summary>
        /// Имя автора цитируемого сообщения.
        /// </summary>
        public string? ReplyToSenderName { get; set; }

        /// <summary>
        /// Текст/описание цитируемого сообщения.
        /// </summary>
        public string? ReplyToText { get; set; }
    }
}
