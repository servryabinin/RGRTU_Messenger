using System.Collections.Generic;

namespace RGRTU.Messenger.Shared.Models.DTO.Admin
{
    /// <summary>
    /// Список участников чата. Для администратора — все незаблокированные пользователи с признаком IsInChat.
    /// Для обычного пользователя — только те, кто сейчас в чате.
    /// </summary>
    public class ChatMembersResponseDto
    {
        public bool IsSuccess { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
        public bool CanManage { get; set; }
        public bool IsCurrentUserInChat { get; set; }
        public bool IsCurrentUserChatRemovedByAdmin { get; set; }
        public bool IsCurrentUserChatInvitedByAdmin { get; set; }
        public bool IsCurrentUserHasLeftChat { get; set; }
        public List<ChatMemberDto> Members { get; set; } = new();
    }
}