namespace RGRTU.Messenger.Shared.Models.DTO
{
    public class EditMessageRequestDto
    {
        public string MessageId { get; set; } = string.Empty;
        public string NewText { get; set; } = string.Empty;
    }
}