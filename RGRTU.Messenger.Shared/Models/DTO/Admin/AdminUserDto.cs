using System;
using RGRTU.Messenger.Shared.Models.DTO.Enums;

namespace RGRTU.Messenger.Shared.Models.DTO.Admin
{
    /// <summary>
    /// Данные пользователя для административного интерфейса.
    /// </summary>
    public class AdminUserDto
    {
        public int Id { get; set; }
        public string Login { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public AdminUserRole Role { get; set; }
        public bool IsBlocked { get; set; }
        public string? BlockReason { get; set; }
        public UserStatus Status { get; set; } = UserStatus.Offline;
        public DateTime CreatedAt { get; set; }
        public DateTime? LastLoginAt { get; set; }
    }
}
