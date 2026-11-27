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
        public bool IsOnline { get; set; }
        public bool IsChatRemovedByAdmin { get; set; }
        public bool IsChatInvitedByAdmin { get; set; }
        public bool HasLeftChat { get; set; }
    }
}