using System.ComponentModel;
using System.Runtime.CompilerServices;
using RGRTU.Messenger.Shared.Extensions;
using RGRTU.Messenger.Shared.Models.DTO.Admin;
using RGRTU.Messenger.Shared.Models.DTO.Enums;

namespace RGRTU.Messenger.Client.Models
{
    /// <summary>
    /// Обёртка над DTO пользователя для отображения в DataGrid админской панели.
    /// Поддерживает мгновенное обновление статуса без полной перезагрузки таблицы.
    /// </summary>
    public class AdminUserViewModel : INotifyPropertyChanged
    {
        private UserStatus _status;

        public AdminUserViewModel(AdminUserDto user)
        {
            User = user;
            _status = user.Status;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public AdminUserDto User { get; }

        public int Id => User.Id;
        public string Login => User.Login;
        public string DisplayName => User.DisplayName;
        public string BlockReason => User.BlockReason ?? string.Empty;

        public string RoleDisplay => User.Role == AdminUserRole.Admin
            ? "Администратор"
            : "Пользователь";

        public string IsBlockedDisplay => User.IsBlocked ? "Да" : "Нет";

        /// <summary>
        /// Статус, который обновляется из сетевых событий в реальном времени.
        /// </summary>
        public UserStatus Status
        {
            get => _status;
            private set
            {
                if (_status == value) return;
                _status = value;
                User.Status = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(StatusDisplay));
            }
        }

        public string StatusDisplay => User.IsBlocked
            ? "Заблокирован"
            : _status.GetDescription();

        public string BlockButtonText => User.IsBlocked
            ? "Разблокировать"
            : "Заблокировать";

        /// <summary>
        /// Вызывается при получении сообщения об изменении статуса пользователя.
        /// </summary>
        public void UpdateStatus(UserStatus status)
        {
            Status = status;
        }

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
