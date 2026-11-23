namespace RGRTU.Messenger.Server.Data
{
    /// <summary>
    /// Роль пользователя в системе мессенджера (п. 2.a задания).
    /// </summary>
    public enum UserRole
    {
        /// <summary>
        /// Обычный пользователь чата.
        /// </summary>
        User = 0,

        /// <summary>
        /// Администратор системы.
        /// </summary>
        Admin = 1
    }
}
