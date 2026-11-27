using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;

namespace RGRTU.Messenger.Client.Models
{
    public class ChatMemberViewModel : INotifyPropertyChanged
    {
        private bool _isOnline;

        public event PropertyChangedEventHandler? PropertyChanged;

        public int Id { get; init; }
        public string DisplayName { get; init; } = string.Empty;
        public bool IsInChat { get; init; }
        public bool CanManage { get; init; }
        public bool IsChatRemovedByAdmin { get; init; }
        public bool IsChatInvitedByAdmin { get; init; }
        public bool HasLeftChat { get; init; }

        public bool IsOnline
        {
            get => _isOnline;
            set
            {
                if (_isOnline == value) return;
                _isOnline = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(OnlineStatusText));
            }
        }

        public string ActionText => IsInChat ? "Исключить" : IsChatInvitedByAdmin ? "Приглашён" : "Добавить";
        public bool ActionEnabled => !IsChatInvitedByAdmin;
        public Visibility ActionVisibility => CanManage ? Visibility.Visible : Visibility.Collapsed;
        public string OnlineStatusText => IsOnline ? "В сети" : "Не в сети";

        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}