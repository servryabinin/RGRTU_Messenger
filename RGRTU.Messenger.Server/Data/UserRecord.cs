using System;

namespace RGRTU.Messenger.Server.Data
{
    /// <summary>
    /// Запись о пользователе, хранящаяся в базе данных SQLite.
    /// </summary>
    public class UserRecord
    {
        /// <summary>
        /// Уникальный идентификатор пользователя в базе данных.
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// Логин пользователя, используемый для входа.
        /// </summary>
        public string Login { get; set; } = string.Empty;

        /// <summary>
        /// Соль (salt) для хеширования пароля.
        /// </summary>
        public string Salt { get; set; } = string.Empty;

        /// <summary>
        /// Хеш пароля пользователя.
        /// </summary>
        public string PasswordHash { get; set; } = string.Empty;

        /// <summary>
        /// Имя, отображаемое в чате.
        /// </summary>
        public string DisplayName { get; set; } = string.Empty;

        /// <summary>
        /// Роль пользователя.
        /// </summary>
        public UserRole Role { get; set; } = UserRole.User;

        /// <summary>
        /// Признак блокировки учётной записи.
        /// </summary>
        public bool IsBlocked { get; set; }

        /// <summary>
        /// Причина блокировки (если пользователь заблокирован).
        /// </summary>
        public string? BlockReason { get; set; }

        /// <summary>
        /// Дата и время создания учётной записи.
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// Дата и время последнего успешного входа.
        /// </summary>
        public DateTime? LastLoginAt { get; set; }
    }
}
