namespace RGRTU.Messenger.Shared.Models.DTO
{
    /// <summary>
    /// Запрос администратора на принудительное отключение пользователя.
    /// </summary>
    public class AdminKickRequestDto
    {
        /// <summary>
        /// Идентификатор пользователя в базе данных, которого нужно отключить.
        /// </summary>
        public int UserId { get; set; }

        /// <summary>
        /// Причина отключения, отображаемая пользователю.
        /// </summary>
        public string Reason { get; set; } = string.Empty;
    }
}
