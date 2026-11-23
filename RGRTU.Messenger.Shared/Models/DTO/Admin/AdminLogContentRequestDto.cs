namespace RGRTU.Messenger.Shared.Models.DTO.Admin
{
    public class AdminLogContentRequestDto
    {
        /// <summary>
        /// Только имя файла без пути — сервер сам ищет его в своей папке логов.
        /// </summary>
        public string FileName { get; set; } = string.Empty;
    }
}
