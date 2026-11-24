namespace RGRTU.Messenger.Shared.Models.DTO.Admin
{
    /// <summary>
    /// Участник чата для панели администратора.
    /// </summary>
    public class ChatMemberDto
    {
        public int Id { get; set; }
        public string Login { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public bool IsInChat { get; set; }
    }
}