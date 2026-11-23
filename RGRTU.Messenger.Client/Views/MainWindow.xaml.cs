using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using RGRTU.Messenger.Client.Models;
using RGRTU.Messenger.Client.Network;
using RGRTU.Messenger.Shared.Models.DTO;
using RGRTU.Messenger.Shared.Models.DTO.Admin;
using RGRTU.Messenger.Shared.Extensions;
using RGRTU.Messenger.Shared.Models.DTO.Enums;
using RGRTU.Messenger.Shared.Network;
using Serilog;

namespace RGRTU.Messenger.Client.Views
{
    /// <summary>
    /// Главное окно мессенджера: чат и, для администратора, вкладка управления пользователями.
    /// </summary>
    public partial class MainWindow : Window
    {
        /// <summary>
        /// Элемент выпадающего списка статусов: русское название + значение перечисления.
        /// </summary>
        private sealed class StatusOption
        {
            public UserStatus Value { get; set; }
            public string Name { get; set; } = string.Empty;
        }

        private readonly MessengerClient _client;
        private readonly AuthResponseDto _currentUser;
        private readonly bool _isAdmin;

        /// <summary>
        /// Данные таблицы администратора.
        /// </summary>
        private readonly ObservableCollection<AdminUserViewModel> _adminUsers = new();

        /// <summary>
        /// Текст результата последней административной операции, чтобы не терять его при обновлении таблицы.
        /// </summary>
        private string _lastAdminActionMessage = string.Empty;

        /// <summary>
        /// Защита от отправки статуса при программной инициализации ComboBox.
        /// </summary>
        private bool _suppressStatusEvent;

        public MainWindow(MessengerClient client, AuthResponseDto currentUser)
        {
            InitializeComponent();

            _client = client;
            _currentUser = currentUser;
            _isAdmin = _currentUser.IsAdmin;

            string displayLogin = string.IsNullOrWhiteSpace(_currentUser.Login)
                ? _currentUser.Username
                : _currentUser.Login;

            Title = $"Мессенджер РГРТУ - {displayLogin}";
            CurrentUserLoginTextBlock.Text = displayLogin;
            CurrentUserRoleTextBlock.Text = _isAdmin ? "Администратор" : "Пользователь";

            _suppressStatusEvent = true;
            StatusComboBox.ItemsSource = Enum.GetValues(typeof(UserStatus))
                .Cast<UserStatus>()
                .Select(s => new StatusOption { Value = s, Name = s.GetDescription() })
                .ToList();
            StatusComboBox.DisplayMemberPath = "Name";
            StatusComboBox.SelectedValuePath = "Value";
            StatusComboBox.SelectedValue = UserStatus.Online;
            _suppressStatusEvent = false;
            StatusComboBox.SelectionChanged += StatusComboBox_SelectionChanged;

            UsersDataGrid.ItemsSource = _adminUsers;

            _client.OnMessageReceived += Client_OnMessageReceived;
            _client.OnUserStatusChanged += Client_OnUserStatusChanged;
            _client.OnDisconnected += Client_OnDisconnected;

            if (_isAdmin)
            {
                AdminTabItem.Visibility = Visibility.Visible;
                _client.OnAdminUsersReceived += Client_OnAdminUsersReceived;
                _client.OnAdminActionCompleted += Client_OnAdminActionCompleted;
                Loaded += MainWindow_Loaded;
            }
            else
            {
                AdminTabItem.Visibility = Visibility.Collapsed;
            }
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            await RefreshUsersAsync();
        }

        #region Чат

        private void Client_OnMessageReceived(ChatMessageDto message)
        {
            Dispatcher.Invoke(() =>
            {
                MessagesListBox.Items.Add($"[{message.Timestamp:HH:mm}] {message.SenderName}: {message.Text}");
                MessagesListBox.ScrollIntoView(MessagesListBox.Items[MessagesListBox.Items.Count - 1]);
            });
        }

        private void Client_OnUserStatusChanged(UserStatusDto status)
        {
            Dispatcher.Invoke(() =>
            {
                UsersListBox.Items.Add($"{status.Username} [{status.Status.GetDescription()}]");

                // Мгновенно обновляем строку в таблице администратора, не перезапрашивая список.
                var row = _adminUsers.FirstOrDefault(u => u.Id == status.UserId);
                if (row != null)
                {
                    row.UpdateStatus(status.Status);
                }
                else if (_isAdmin)
                {
                    // Пользователь появился в системе после загрузки таблицы (например, создан с консоли).
                    _ = RefreshUsersAsync(showStatus: false);
                }

                // Если сервер прислал наш собственный статус, синхронизируем селектор.
                if (status.UserId == _currentUser.UserId && status.Status != UserStatus.Offline)
                {
                    _suppressStatusEvent = true;
                    StatusComboBox.SelectedValue = status.Status;
                    _suppressStatusEvent = false;
                }
            });
        }

        private void Client_OnDisconnected()
        {
            Dispatcher.Invoke(() =>
            {
                Log.Warning("Соединение с сервером разорвано во время работы в чате");
                MessageBox.Show("Соединение с сервером разорвано.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                Close();
            });
        }

        private async void SendButton_Click(object sender, RoutedEventArgs e)
        {
            await SendMessageAsync();
        }

        private async void MessageInputTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && !Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
            {
                await SendMessageAsync();
                e.Handled = true;
            }
        }

        private async Task SendMessageAsync()
        {
            string text = MessageInputTextBox.Text.Trim();
            if (string.IsNullOrEmpty(text)) return;

            var messageDto = new ChatMessageDto
            {
                SenderId = _currentUser.UserId,
                SenderName = _currentUser.Username,
                Text = text,
                Timestamp = DateTime.Now
            };

            Log.Information("Отправка сообщения в чат: {Text}", text);
            await _client.SendPacketAsync(MessageType.SendTextMessage, messageDto);
            MessageInputTextBox.Clear();
        }

        #endregion

        #region Профиль и выход

        private async void StatusComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressStatusEvent || StatusComboBox.SelectedValue is not UserStatus status)
                return;

            try
            {
                await _client.SetUserStatusAsync(_currentUser.UserId, status);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Не удалось изменить статус пользователя");
            }
        }

        private void LogoutButton_Click(object sender, RoutedEventArgs e)
        {
            // Сначала отписываемся, чтобы закрытие соединения не вызвало MessageBox и Close.
            _client.OnMessageReceived -= Client_OnMessageReceived;
            _client.OnUserStatusChanged -= Client_OnUserStatusChanged;
            _client.OnDisconnected -= Client_OnDisconnected;
            StatusComboBox.SelectionChanged -= StatusComboBox_SelectionChanged;
            Loaded -= MainWindow_Loaded;

            if (_isAdmin)
            {
                _client.OnAdminUsersReceived -= Client_OnAdminUsersReceived;
                _client.OnAdminActionCompleted -= Client_OnAdminActionCompleted;
            }

            _client.Disconnect();

            var loginWindow = new LoginWindow();
            loginWindow.Show();
            Close();
        }

        #endregion

        #region Администрирование

        private void Client_OnAdminUsersReceived(AdminUsersResponseDto response)
        {
            Dispatcher.Invoke(() =>
            {
                if (!response.IsSuccess)
                {
                    SetAdminStatus(response.ErrorMessage, true);
                    MessageBox.Show(response.ErrorMessage, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                _adminUsers.Clear();
                foreach (var user in response.Users)
                {
                    _adminUsers.Add(new AdminUserViewModel(user));
                }

                SetAdminStatus(string.IsNullOrEmpty(_lastAdminActionMessage)
                    ? $"Загружено пользователей: {response.Users.Count}"
                    : $"{_lastAdminActionMessage} • Пользователей: {response.Users.Count}");
            });
        }

        private void Client_OnAdminActionCompleted(AdminActionResponseDto response)
        {
            Dispatcher.Invoke(() =>
            {
                _lastAdminActionMessage = response.ErrorMessage;
                SetAdminStatus(response.ErrorMessage, !response.IsSuccess);

                if (!response.IsSuccess)
                {
                    MessageBox.Show(response.ErrorMessage, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            });

            // Таблица обновляется вызывающей кнопкой после отправки операции.
        }

        private async void AddUserButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new UserEditWindow(null) { Owner = this };
            if (dialog.ShowDialog() != true || dialog.CreateRequest is null)
                return;

            await ExecuteAdminActionAsync(() => _client.CreateAdminUserAsync(dialog.CreateRequest));
        }

        private async void EditUserButton_Click(object sender, RoutedEventArgs e)
        {
            if (UsersDataGrid.SelectedItem is not AdminUserViewModel selected)
            {
                MessageBox.Show("Выберите пользователя в таблице.", "Редактирование",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dialog = new UserEditWindow(selected.User) { Owner = this };
            if (dialog.ShowDialog() != true || dialog.UpdateRequest is null)
                return;

            await ExecuteAdminActionAsync(() => _client.UpdateAdminUserAsync(dialog.UpdateRequest));
        }

        private async void DeleteUserButton_Click(object sender, RoutedEventArgs e)
        {
            if (UsersDataGrid.SelectedItem is not AdminUserViewModel selected)
            {
                MessageBox.Show("Выберите пользователя в таблице.", "Удаление",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var confirm = MessageBox.Show(
                $"Удалить пользователя '{selected.Login}'?",
                "Подтверждение удаления",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirm != MessageBoxResult.Yes)
                return;

            await ExecuteAdminActionAsync(() => _client.DeleteAdminUserAsync(selected.Id));
        }

        private async void BlockUserButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button || button.Tag is not AdminUserViewModel user)
                return;

            bool shouldBlock = !user.User.IsBlocked;
            string reason = shouldBlock ? "Заблокирован администратором через интерфейс" : string.Empty;

            await ExecuteAdminActionAsync(() =>
                _client.SetAdminUserBlockedAsync(user.Id, shouldBlock, reason));
        }

        /// <summary>
        /// Отправляет административное действие и сразу перезапрашивает список пользователей,
        /// чтобы таблица обновилась без перезапуска приложения.
        /// </summary>
        private async Task ExecuteAdminActionAsync(Func<Task> action)
        {
            try
            {
                await action();
                await RefreshUsersAsync(showStatus: false);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Ошибка выполнения административного действия");
                SetAdminStatus("Ошибка выполнения операции", true);
                MessageBox.Show("Не удалось выполнить операцию. Подробности смотрите в логах.",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task RefreshUsersAsync(bool showStatus = true)
        {
            try
            {
                if (showStatus)
                    SetAdminStatus("Обновление списка...");

                await _client.RequestAdminUsersAsync();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Не удалось запросить список пользователей");
                SetAdminStatus("Ошибка запроса списка пользователей", true);
            }
        }

        private void SetAdminStatus(string text, bool isError = false)
        {
            AdminStatusTextBlock.Text = text;
            AdminStatusTextBlock.Foreground = isError
                ? new SolidColorBrush(Color.FromRgb(220, 38, 38))
                : new SolidColorBrush(Color.FromRgb(100, 116, 139));
        }

        #endregion
    }
}
