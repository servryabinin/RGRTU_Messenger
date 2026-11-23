namespace RGRTU.Messenger.Shared.Models.DTO.Admin
{
    /// <summary>
    /// Запрос на блокировку или разблокировку пользователя.
    /// </summary>
    public class AdminBlockUserRequestDto
    {
        public int Id { get; set; }
        public bool IsBlocked { get; set; }
        public string Reason { get; set; } = string.Empty;
    }
}
