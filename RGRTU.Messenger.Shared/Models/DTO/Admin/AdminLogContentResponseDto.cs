namespace RGRTU.Messenger.Shared.Models.DTO.Admin
{
    public class AdminLogContentResponseDto
    {
        public bool IsSuccess { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
    }
}
