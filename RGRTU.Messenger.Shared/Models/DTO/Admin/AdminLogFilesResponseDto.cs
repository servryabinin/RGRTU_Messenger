using System.Collections.Generic;

namespace RGRTU.Messenger.Shared.Models.DTO.Admin
{
    public class AdminLogFilesResponseDto
    {
        public bool IsSuccess { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
        public List<AdminLogFileDto> Files { get; set; } = new();
    }
}
