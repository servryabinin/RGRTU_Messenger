using System.Windows;

namespace RGRTU.Messenger.Client.Models
{
    public class ChatMemberViewModel
    {
        public int Id { get; init; }
        public string DisplayName { get; init; } = string.Empty;
        public bool IsInChat { get; init; }
        public bool CanManage { get; init; }
        public bool IsOnline { get; init; }
        public bool IsChatRemovedByAdmin { get; init; }

        public string ActionText => IsInChat ? "Исключить" : "Добавить";
        public Visibility ActionVisibility => CanManage ? Visibility.Visible : Visibility.Collapsed;
        public string OnlineStatusText => IsOnline ? "В сети" : "Не в сети";
    }
}