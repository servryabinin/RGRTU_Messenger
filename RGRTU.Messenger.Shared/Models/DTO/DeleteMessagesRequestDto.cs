using System.Collections.Generic;

namespace RGRTU.Messenger.Shared.Models.DTO
{
    public class DeleteMessagesRequestDto
    {
        public List<string> MessageIds { get; set; } = new();
    }
}