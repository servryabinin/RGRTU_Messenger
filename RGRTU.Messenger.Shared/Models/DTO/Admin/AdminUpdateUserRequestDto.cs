namespace RGRTU.Messenger.Shared.Models.DTO.Admin
{
    /// <summary>
    /// Запрос на редактирование пользователя.
    /// </summary>
    public class AdminUpdateUserRequestDto
    {
        public int Id { get; set; }
        public string Login { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;

        /// <summary>
        /// Если пусто — пароль остаётся прежним.
        /// </summary>
        public string PasswordHash { get; set; } = string.Empty;

        public AdminUserRole Role { get; set; } = AdminUserRole.User;
    }
}
