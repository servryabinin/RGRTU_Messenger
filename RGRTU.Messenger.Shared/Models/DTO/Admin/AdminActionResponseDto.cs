namespace RGRTU.Messenger.Shared.Models.DTO.Admin
{
    /// <summary>
    /// Результат административной операции (создание/изменение/удаление/блокировка).
    /// </summary>
    public class AdminActionResponseDto
    {
        public bool IsSuccess { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
    }
}
