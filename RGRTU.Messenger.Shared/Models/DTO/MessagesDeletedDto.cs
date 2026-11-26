using System.Collections.Generic;

namespace RGRTU.Messenger.Shared.Models.DTO
{
    public class MessagesDeletedDto
    {
        public List<string> MessageIds { get; set; } = new();
    }
}