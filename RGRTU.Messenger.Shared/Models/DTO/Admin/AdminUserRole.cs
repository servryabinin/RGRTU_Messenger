namespace RGRTU.Messenger.Shared.Models.DTO.Admin
{
    /// <summary>
    /// Роль пользователя, передаваемая между клиентской и серверной частями.
    /// </summary>
    public enum AdminUserRole
    {
        /// <summary>
        /// Обычный пользователь.
        /// </summary>
        User = 0,

        /// <summary>
        /// Администратор системы.
        /// </summary>
        Admin = 1
    }
}
