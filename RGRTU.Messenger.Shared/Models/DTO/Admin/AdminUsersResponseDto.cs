using System.Collections.Generic;

namespace RGRTU.Messenger.Shared.Models.DTO.Admin
{
    /// <summary>
    /// Ответ сервера со списком пользователей.
    /// </summary>
    public class AdminUsersResponseDto
    {
        public bool IsSuccess { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
        public List<AdminUserDto> Users { get; set; } = new();
    }
}
