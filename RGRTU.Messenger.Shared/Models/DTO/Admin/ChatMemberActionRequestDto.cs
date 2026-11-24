namespace RGRTU.Messenger.Shared.Models.DTO.Admin
{
    /// <summary>
    /// Запрос администратора на добавление/исключение пользователя из чата.
    /// </summary>
    public class ChatMemberActionRequestDto
    {
        public int UserId { get; set; }
    }
}