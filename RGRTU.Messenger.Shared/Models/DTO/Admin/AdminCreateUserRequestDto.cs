namespace RGRTU.Messenger.Shared.Models.DTO.Admin
{
    /// <summary>
    /// Запрос на создание пользователя из графического интерфейса администратора.
    /// </summary>
    public class AdminCreateUserRequestDto
    {
        public string Login { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;

        /// <summary>
        /// SHA-256 хеш пароля (та же форма, что использует окно входа).
        /// </summary>
        public string PasswordHash { get; set; } = string.Empty;

        public AdminUserRole Role { get; set; } = AdminUserRole.User;
    }
}
