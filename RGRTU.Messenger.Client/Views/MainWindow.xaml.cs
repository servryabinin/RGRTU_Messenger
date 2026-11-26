using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;
using System.Windows.Media;
using RGRTU.Messenger.Client.Models;
using RGRTU.Messenger.Client.Network;
using RGRTU.Messenger.Client.Services;
using RGRTU.Messenger.Shared.Models.DTO;
using RGRTU.Messenger.Shared.Models.DTO.Admin;
using RGRTU.Messenger.Shared.Extensions;
using RGRTU.Messenger.Shared.Network;
using Serilog;

namespace RGRTU.Messenger.Client.Views
{
    /// <summary>
    /// Главное окно мессенджера: чат и, для администратора, вкладка управления пользователями.
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly MessengerClient _client;
        private readonly AuthResponseDto _currentUser;
        private readonly bool _isAdmin;

        /// <summary>
        /// Данные таблицы администратора.
        /// </summary>
        private readonly ObservableCollection<AdminUserViewModel> _adminUsers = new();

        private readonly ObservableCollection<ChatMessageViewModel> _chatMessages = new();
        private readonly ObservableCollection<ChatMemberViewModel> _chatMembers = new();
        private bool _isInChat;
        private bool _isLoggingOut;

        private string? _pendingAttachmentPath;
        private string? _pendingAttachmentName;

        private ChatMessageViewModel? _replyToMessage;
        private ChatMessageViewModel? _editingMessage;
        private bool _isSelectionMode;

        /// <summary>
        /// Чтение файлов журнала событий.
        /// </summary>
        private readonly LogFileService _logFileService = new();

        private readonly ObservableCollection<LogFileViewModel> _clientLogFiles = new();
        private readonly ObservableCollection<LogFileViewModel> _serverLogFiles = new();
        private readonly ObservableCollection<LogEntryViewModel> _logEntries = new();

        /// <summary>
        /// В какой из двух таблиц пользователь выбрал файл последним.
        /// </summary>
        private LogSourceKind _activeLogSource = LogSourceKind.Client;

        /// <summary>
        /// Все события выбранного файла до применения фильтра.
        /// </summary>
        private List<LogEntryViewModel> _allLogEntries = new();

        /// <summary>
        /// Отмена предыдущей загрузки журнала, если пользователь быстро переключает вкладки/файлы.
        /// </summary>
        private CancellationTokenSource? _logLoadCts;

        /// <summary>
        /// Защита от повторного применения фильтра при программной смене элементов управления.
        /// </summary>
        private bool _suppressLogFilterEvent;

        /// <summary>
        /// Текст результата последней административной операции, чтобы не терять его при обновлении таблицы.
        /// </summary>
        private string _lastAdminActionMessage = string.Empty;

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

            UsersDataGrid.ItemsSource = _adminUsers;
            MessagesListBox.ItemsSource = _chatMessages;
            UsersListBox.ItemsSource = _chatMembers;

            _isInChat = _currentUser.IsInChat;
            ApplyChatAccessState();

            _client.OnMessageReceived += Client_OnMessageReceived;
            _client.OnUserStatusChanged += Client_OnUserStatusChanged;
            _client.OnDisconnected += Client_OnDisconnected;
            _client.OnErrorReceived += Client_OnErrorReceived;
            _client.OnChatMembersReceived += Client_OnChatMembersReceived;
            _client.OnMessageEdited += Client_OnMessageEdited;
            _client.OnMessagesDeleted += Client_OnMessagesDeleted;

            if (_isAdmin)
            {
                AdminTabItem.Visibility = Visibility.Visible;
                LogsTabItem.Visibility = Visibility.Visible;
                _client.OnAdminUsersReceived += Client_OnAdminUsersReceived;
                _client.OnAdminActionCompleted += Client_OnAdminActionCompleted;
                Loaded += MainWindow_Loaded;

                ClientLogFilesDataGrid.ItemsSource = _clientLogFiles;
                ServerLogFilesDataGrid.ItemsSource = _serverLogFiles;
                LogEntriesDataGrid.ItemsSource = _logEntries;
                ClientLogFilesDataGrid.SelectionChanged += LogFilesDataGrid_SelectionChanged;
                ServerLogFilesDataGrid.SelectionChanged += LogFilesDataGrid_SelectionChanged;
                MainTabControl.SelectionChanged += MainTabControl_SelectionChanged;

                _suppressLogFilterEvent = true;
                FilterHourFromNumeric.Value = 0;
                FilterMinuteFromNumeric.Value = 0;
                FilterHourToNumeric.Value = 23;
                FilterMinuteToNumeric.Value = 59;
                FilterHourFromNumeric.ValueChanged += FilterNumeric_ValueChanged;
                FilterMinuteFromNumeric.ValueChanged += FilterNumeric_ValueChanged;
                FilterHourToNumeric.ValueChanged += FilterNumeric_ValueChanged;
                FilterMinuteToNumeric.ValueChanged += FilterNumeric_ValueChanged;
                FilterLevelComboBox.ItemsSource = new[] { "Все" };
                FilterLevelComboBox.SelectedItem = "Все";
                LogLegendItemsControl.ItemsSource = BuildLogLegend();
                _suppressLogFilterEvent = false;

                FilterLevelComboBox.SelectionChanged += FilterComboBox_SelectionChanged;
            }
            else
            {
                AdminTabItem.Visibility = Visibility.Collapsed;
            }

            // На случай, если список участников пришёл до открытия окна.
            if (_client.LastChatMembersResponse != null)
                Client_OnChatMembersReceived(_client.LastChatMembersResponse);
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
                var vm = ChatMessageViewModel.Create(message, _currentUser.UserId);
                vm.IsSelectionMode = _isSelectionMode;
                _chatMessages.Add(vm);
                if (_chatMessages.Count > 0)
                    MessagesListBox.ScrollIntoView(_chatMessages[^1]);
            });
        }

        private void Client_OnMessageEdited(MessageEditedDto dto)
        {
            Dispatcher.Invoke(() =>
            {
                var msg = _chatMessages.FirstOrDefault(m => m.MessageId == dto.MessageId);
                if (msg == null) return;
                msg.Text = dto.NewText;
                msg.IsEdited = true;
            });
        }

        private void Client_OnMessagesDeleted(MessagesDeletedDto dto)
        {
            Dispatcher.Invoke(() =>
            {
                var ids = dto.MessageIds?.ToHashSet() ?? new HashSet<string>();
                for (int i = _chatMessages.Count - 1; i >= 0; i--)
                {
                    if (ids.Contains(_chatMessages[i].MessageId))
                        _chatMessages.RemoveAt(i);
                }
            });
        }

        private void Client_OnUserStatusChanged(UserStatusDto status)
        {
            Dispatcher.Invoke(() =>
            {
                var row = _adminUsers.FirstOrDefault(u => u.Id == status.UserId);
                if (row != null)
                    row.UpdateStatus(status.Status);
                else if (_isAdmin)
                    _ = RefreshUsersAsync(showStatus: false);
            });
        }

        private void Client_OnChatMembersReceived(ChatMembersResponseDto response)
        {
            Dispatcher.Invoke(() =>
            {
                if (!response.IsSuccess)
                {
                    Log.Warning("Ошибка получения участников чата: {Error}", response.ErrorMessage);
                    return;
                }

                _isInChat = response.IsCurrentUserInChat || _isAdmin && response.Members.Any(m => m.Id == _currentUser.UserId && m.IsInChat);
                // Для обычного пользователя сервер задаёт IsCurrentUserInChat; для админа он тоже в списке.
                if (_isAdmin)
                {
                    var self = response.Members.FirstOrDefault(m => m.Id == _currentUser.UserId);
                    _isInChat = self?.IsInChat ?? response.IsCurrentUserInChat;
                }
                else
                {
                    _isInChat = response.IsCurrentUserInChat;
                }

                _chatMembers.Clear();
                foreach (var m in response.Members)
                {
                    _chatMembers.Add(new ChatMemberViewModel
                    {
                        Id = m.Id,
                        DisplayName = m.DisplayName,
                        IsInChat = m.IsInChat,
                        CanManage = response.CanManage
                    });
                }

                ApplyChatAccessState();
            });
        }

        private void ApplyChatAccessState()
        {
            bool canChat = _isInChat;
            MessagesListBox.Visibility = canChat ? Visibility.Visible : Visibility.Collapsed;
            NotInChatTextBlock.Visibility = canChat ? Visibility.Collapsed : Visibility.Visible;
            ChatInputGrid.Visibility = canChat ? Visibility.Visible : Visibility.Collapsed;
        }

        private void Client_OnErrorReceived(string errorMessage)
        {
            Dispatcher.Invoke(() =>
            {
                if (_isLoggingOut || string.IsNullOrWhiteSpace(errorMessage))
                    return;

                // Блокировка/удаление аккаунта — принудительный выход на авторизацию.
                if (errorMessage.Contains("заблокирован", StringComparison.OrdinalIgnoreCase) ||
                    errorMessage.Contains("удалена", StringComparison.OrdinalIgnoreCase) ||
                    errorMessage.Contains("удалён", StringComparison.OrdinalIgnoreCase))
                {
                    _isLoggingOut = true;
                    MessageBox.Show(errorMessage, "Доступ ограничен", MessageBoxButton.OK, MessageBoxImage.Warning);
                    ForceLogoutToLogin();
                }
            });
        }

        private void Client_OnDisconnected()
        {
            Dispatcher.Invoke(() =>
            {
                if (_isLoggingOut) return;
                _isLoggingOut = true;
                Log.Warning("Соединение с сервером разорвано во время работы в чате");
                MessageBox.Show("Соединение с сервером разорвано.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                ForceLogoutToLogin();
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
            if (!_isInChat) return;
            if (_isSelectionMode) return;

            if (_editingMessage != null)
            {
                await ConfirmEditAsync();
                return;
            }

            string text = MessageInputTextBox.Text.Trim();
            bool hasAttachment = !string.IsNullOrWhiteSpace(_pendingAttachmentPath);
            if (string.IsNullOrEmpty(text) && !hasAttachment) return;

            string? attachmentBase64 = null;
            string? attachmentName = null;
            long attachmentSize = 0;

            if (hasAttachment)
            {
                try
                {
                    byte[] bytes = await System.IO.File.ReadAllBytesAsync(_pendingAttachmentPath!);
                    attachmentBase64 = Convert.ToBase64String(bytes);
                    attachmentName = _pendingAttachmentName;
                    attachmentSize = bytes.LongLength;
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Не удалось прочитать файл: {ex.Message}", "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
            }

            var messageDto = new ChatMessageDto
            {
                MessageId = Guid.NewGuid().ToString("N"),
                SenderId = _currentUser.UserId,
                SenderName = _currentUser.Username,
                Text = text,
                Timestamp = DateTime.Now,
                AttachmentFileName = attachmentName,
                AttachmentFileSize = attachmentSize,
                AttachmentBase64 = attachmentBase64,
                IsSticker = false,
                ReplyToMessageId = _replyToMessage?.MessageId,
                ReplyToSenderName = _replyToMessage?.SenderName,
                ReplyToText = _replyToMessage != null ? GetReplyPreviewText(_replyToMessage) : null
            };

            Log.Information("Отправка сообщения в чат: {Text}{File}", text,
                hasAttachment ? $" [file:{attachmentName}]" : string.Empty);
            await _client.SendPacketAsync(MessageType.SendTextMessage, messageDto);
            MessageInputTextBox.Clear();
            ClearPendingAttachment();
            ClearReply();
        }

        private static string GetReplyPreviewText(ChatMessageViewModel msg)
        {
            if (!string.IsNullOrWhiteSpace(msg.Text)) return msg.Text;
            if (msg.HasAttachment) return $"Файл: {msg.AttachmentFileName}";
            return "Сообщение";
        }

        private void AttachButton_Click(object sender, RoutedEventArgs e)
        {
            AttachPopup.IsOpen = !AttachPopup.IsOpen;
        }

        private void PickFileButton_Click(object sender, RoutedEventArgs e)
        {
            AttachPopup.IsOpen = false;
            var dialog = new OpenFileDialog
            {
                Title = "Выберите файл",
                Filter = "Все файлы (*.*)|*.*"
            };

            if (dialog.ShowDialog(this) == true)
            {
                _pendingAttachmentPath = dialog.FileName;
                _pendingAttachmentName = System.IO.Path.GetFileName(dialog.FileName);
                AttachmentPreviewTextBlock.Text = $"📎 {_pendingAttachmentName}";
                AttachmentPreviewBorder.Visibility = Visibility.Visible;
            }
        }

        private void RemoveAttachmentButton_Click(object sender, RoutedEventArgs e)
        {
            ClearPendingAttachment();
        }

        private void ClearPendingAttachment()
        {
            _pendingAttachmentPath = null;
            _pendingAttachmentName = null;
            AttachmentPreviewTextBlock.Text = string.Empty;
            AttachmentPreviewBorder.Visibility = Visibility.Collapsed;
        }

        #region Context menu / reply / edit / select / delete

        private void MessageBorder_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.Tag is ChatMessageViewModel msg && fe.ContextMenu != null)
            {
                fe.ContextMenu.Tag = msg;
                fe.ContextMenu.PlacementTarget = fe;
                fe.ContextMenu.IsOpen = true;
                e.Handled = true;
            }
        }

        private async void ReplyPreview_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is not FrameworkElement fe || fe.Tag is not ChatMessageViewModel msg) return;
            if (string.IsNullOrWhiteSpace(msg.ReplyToMessageId)) return;

            var target = _chatMessages.FirstOrDefault(m => m.MessageId == msg.ReplyToMessageId);
            if (target == null) return;

            MessagesListBox.ScrollIntoView(target);
            target.HighlightOpacity = 1.0;
            await Task.Delay(900);
            // Soft fade out
            for (int i = 10; i >= 0; i--)
            {
                target.HighlightOpacity = i / 10.0;
                await Task.Delay(40);
            }
        }

        private void MessageContextMenu_Opened(object sender, RoutedEventArgs e)
        {
            if (sender is not ContextMenu menu) return;
            // ContextMenu.Tag was set from border right click; fallback via PlacementTarget
            var msg = menu.Tag as ChatMessageViewModel
                      ?? (menu.PlacementTarget as FrameworkElement)?.Tag as ChatMessageViewModel;
            if (msg == null) return;
            menu.Tag = msg;

            bool isOwn = msg.IsOwn;
            // Items: Ответить, Изменить, Копировать текст, Удалить, Выделить
            ((MenuItem)menu.Items[0]).IsEnabled = !_isSelectionMode; // Reply
            ((MenuItem)menu.Items[1]).Visibility = isOwn && !_isSelectionMode ? Visibility.Visible : Visibility.Collapsed; // Edit
            ((MenuItem)menu.Items[2]).IsEnabled = !string.IsNullOrWhiteSpace(msg.Text); // Copy
            ((MenuItem)menu.Items[3]).Visibility = isOwn && !_isSelectionMode ? Visibility.Visible : Visibility.Collapsed; // Delete
            ((MenuItem)menu.Items[4]).Visibility = isOwn ? Visibility.Visible : Visibility.Collapsed; // Select
        }

        private ChatMessageViewModel? GetMenuMessage(object sender)
        {
            if (sender is MenuItem mi && mi.Parent is ContextMenu cm)
                return cm.Tag as ChatMessageViewModel ?? (cm.PlacementTarget as FrameworkElement)?.Tag as ChatMessageViewModel;
            return null;
        }

        private void ReplyMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var msg = GetMenuMessage(sender);
            if (msg == null) return;
            _replyToMessage = msg;
            ReplyPreviewSenderTextBlock.Text = msg.SenderName;
            ReplyPreviewTextBlock.Text = GetReplyPreviewText(msg);
            ReplyPreviewBorder.Visibility = Visibility.Visible;
            MessageInputTextBox.Focus();
        }

        private void CancelReplyButton_Click(object sender, RoutedEventArgs e) => ClearReply();

        private void ClearReply()
        {
            _replyToMessage = null;
            ReplyPreviewBorder.Visibility = Visibility.Collapsed;
            ReplyPreviewSenderTextBlock.Text = string.Empty;
            ReplyPreviewTextBlock.Text = string.Empty;
        }

        private void EditMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var msg = GetMenuMessage(sender);
            if (msg == null || !msg.IsOwn) return;

            _editingMessage = msg;
            MessageInputTextBox.Text = msg.Text;
            EditPreviewTextBlock.Text = $"Редактирование: {GetReplyPreviewText(msg)}";
            EditPreviewBorder.Visibility = Visibility.Visible;
            SendButton.Visibility = Visibility.Collapsed;
            EditActionsPanel.Visibility = Visibility.Visible;
            SelectionActionsPanel.Visibility = Visibility.Collapsed;
            MessageInputTextBox.Focus();
            MessageInputTextBox.CaretIndex = MessageInputTextBox.Text.Length;
        }

        private async void ConfirmEditButton_Click(object sender, RoutedEventArgs e) => await ConfirmEditAsync();

        private async Task ConfirmEditAsync()
        {
            if (_editingMessage == null) return;
            string newText = MessageInputTextBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(newText)) return;

            await _client.EditChatMessageAsync(_editingMessage.MessageId, newText);
            CancelEditMode();
        }

        private void CancelEditButton_Click(object sender, RoutedEventArgs e) => CancelEditMode();

        private void CancelEditMode()
        {
            _editingMessage = null;
            MessageInputTextBox.Clear();
            EditPreviewBorder.Visibility = Visibility.Collapsed;
            EditActionsPanel.Visibility = Visibility.Collapsed;
            if (!_isSelectionMode) SendButton.Visibility = Visibility.Visible;
        }

        private void CopyMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var msg = GetMenuMessage(sender);
            if (msg == null || string.IsNullOrWhiteSpace(msg.Text)) return;
            try { Clipboard.SetText(msg.Text); } catch { }
        }

        private async void DeleteMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var msg = GetMenuMessage(sender);
            if (msg == null || !msg.IsOwn) return;

            var confirm = MessageBox.Show("Удалить это сообщение?", "Подтверждение",
                MessageBoxButton.OKCancel, MessageBoxImage.Question);
            if (confirm != MessageBoxResult.OK) return;

            await _client.DeleteChatMessagesAsync(new[] { msg.MessageId });
        }

        private void SelectMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var msg = GetMenuMessage(sender);
            if (msg == null || !msg.IsOwn) return;
            EnterSelectionMode();
            msg.IsSelected = true;
        }

        private void EnterSelectionMode()
        {
            if (_isSelectionMode) return;
            _isSelectionMode = true;
            foreach (var m in _chatMessages) { m.IsSelectionMode = true; m.IsSelected = false; }
            SendButton.Visibility = Visibility.Collapsed;
            EditActionsPanel.Visibility = Visibility.Collapsed;
            SelectionActionsPanel.Visibility = Visibility.Visible;
        }

        private void ExitSelectionMode()
        {
            _isSelectionMode = false;
            foreach (var m in _chatMessages) { m.IsSelectionMode = false; m.IsSelected = false; }
            SelectionActionsPanel.Visibility = Visibility.Collapsed;
            if (_editingMessage != null) EditActionsPanel.Visibility = Visibility.Visible;
            else SendButton.Visibility = Visibility.Visible;
        }

        private void SelectionCircle_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.Tag is ChatMessageViewModel msg && msg.IsOwn)
                msg.IsSelected = !msg.IsSelected;
        }

        private void CancelSelectionButton_Click(object sender, RoutedEventArgs e) => ExitSelectionMode();

        private async void DeleteSelectedButton_Click(object sender, RoutedEventArgs e)
        {
            var selected = _chatMessages.Where(m => m.IsOwn && m.IsSelected).Select(m => m.MessageId).ToList();
            if (selected.Count == 0) return;

            var confirm = MessageBox.Show("Удалить это сообщение?", "Подтверждение",
                MessageBoxButton.OKCancel, MessageBoxImage.Question);
            if (confirm != MessageBoxResult.OK) return;

            await _client.DeleteChatMessagesAsync(selected);
            ExitSelectionMode();
        }

        #endregion

        private void EmojiButton_Click(object sender, RoutedEventArgs e)
        {
            EmojiPopup.IsOpen = !EmojiPopup.IsOpen;
        }

        private void EmojiButton_MouseEnter(object sender, MouseEventArgs e)
        {
            EmojiPopup.IsOpen = true;
        }

        private void EmojiPickButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not System.Windows.Controls.Button btn || btn.Content is not string emoji)
                return;

            int caret = MessageInputTextBox.CaretIndex;
            string current = MessageInputTextBox.Text ?? string.Empty;
            MessageInputTextBox.Text = current.Insert(Math.Min(caret, current.Length), emoji);
            MessageInputTextBox.CaretIndex = Math.Min(caret + emoji.Length, MessageInputTextBox.Text.Length);
            MessageInputTextBox.Focus();
            EmojiPopup.IsOpen = false;
        }

        private void AttachmentBlock_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is not System.Windows.FrameworkElement fe || fe.Tag is not ChatMessageViewModel msg)
                return;
            if (!msg.HasAttachment) return;

            var dialog = new SaveFileDialog
            {
                FileName = msg.AttachmentFileName,
                Title = "Сохранить файл"
            };

            if (dialog.ShowDialog(this) == true)
            {
                try
                {
                    byte[] bytes = Convert.FromBase64String(msg.AttachmentBase64!);
                    System.IO.File.WriteAllBytes(dialog.FileName, bytes);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Не удалось сохранить файл: {ex.Message}", "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private async void ChatMemberActionButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_isAdmin) return;
            if (sender is not System.Windows.Controls.Button btn || btn.Tag is not int userId)
                return;

            var member = _chatMembers.FirstOrDefault(m => m.Id == userId);
            if (member == null) return;

            if (member.IsInChat)
                await _client.RemoveChatMemberAsync(userId);
            else
                await _client.AddChatMemberAsync(userId);
        }

        #endregion

        #region Профиль и выход

        private void LogoutButton_Click(object sender, RoutedEventArgs e)
        {
            ForceLogoutToLogin();
        }

        private void ForceLogoutToLogin()
        {
            _isLoggingOut = true;
            _client.OnMessageReceived -= Client_OnMessageReceived;
            _client.OnUserStatusChanged -= Client_OnUserStatusChanged;
            _client.OnDisconnected -= Client_OnDisconnected;
            _client.OnErrorReceived -= Client_OnErrorReceived;
            _client.OnChatMembersReceived -= Client_OnChatMembersReceived;
            _client.OnMessageEdited -= Client_OnMessageEdited;
            _client.OnMessagesDeleted -= Client_OnMessagesDeleted;
            Loaded -= MainWindow_Loaded;
            _logLoadCts?.Cancel();

            if (_isAdmin)
            {
                _client.OnAdminUsersReceived -= Client_OnAdminUsersReceived;
                _client.OnAdminActionCompleted -= Client_OnAdminActionCompleted;
                ClientLogFilesDataGrid.SelectionChanged -= LogFilesDataGrid_SelectionChanged;
                ServerLogFilesDataGrid.SelectionChanged -= LogFilesDataGrid_SelectionChanged;
                MainTabControl.SelectionChanged -= MainTabControl_SelectionChanged;
            }

            _client.Disconnect();

            var loginWindow = new LoginWindow();
            loginWindow.Show();
            Close();
        }

        #endregion

        #region Журнал событий

        /// <summary>
        /// Перечитываем список файлов при каждом открытии вкладки: журнал постоянно дописывается.
        /// </summary>
        /// <summary>
        /// Перезаполняем одну из двух таблиц файлов, сохраняя выбранный файл если он ещё есть.
        /// </summary>
        private LogFileViewModel? FillLogGrid(
            DataGrid grid,
            ObservableCollection<LogFileViewModel> items,
            List<LogFileViewModel> files,
            LogSourceKind source,
            LogFileViewModel? previous)
        {
            LogFileViewModel? previousInGrid = grid.SelectedItem as LogFileViewModel
                                               ?? (previous?.Source == source ? previous : null);

            grid.SelectionChanged -= LogFilesDataGrid_SelectionChanged;
            items.Clear();
            foreach (var file in files)
                items.Add(file);

            LogFileViewModel? selection = previousInGrid != null
                ? items.FirstOrDefault(f => f.Source == previousInGrid.Source && f.Name == previousInGrid.Name)
                  ?? (items.Count > 0 ? items[0] : null)
                : (items.Count > 0 ? items[0] : null);

            grid.SelectedItem = selection;
            grid.SelectionChanged += LogFilesDataGrid_SelectionChanged;
            return selection;
        }

        private async void MainTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (MainTabControl.SelectedItem == LogsTabItem)
                await LoadLogFilesAsync();
            else
                _logLoadCts?.Cancel();
        }

        private async Task LoadLogFilesAsync()
        {
            _logLoadCts?.Cancel();
            var cts = new CancellationTokenSource();
            _logLoadCts = cts;

            try
            {
                // Локальные логи клиента.
                List<LogFileViewModel> clientFiles =
                    await Task.Run(() => _logFileService.GetLocalFiles(), cts.Token);
                cts.Token.ThrowIfCancellationRequested();

                // Логи сервера — по тому же TCP-соединению, поэтому работает с удалённым сервером в сети.
                List<LogFileViewModel> serverFiles;
                try
                {
                    AdminLogFilesResponseDto response = await _client.GetAdminLogFilesAsync();
                    cts.Token.ThrowIfCancellationRequested();

                    serverFiles = response.IsSuccess
                        ? response.Files.Select(file => new LogFileViewModel
                        {
                            Source = LogSourceKind.Server,
                            Name = file.Name,
                            FullPath = string.Empty,
                            Date = file.Date,
                            SizeBytes = file.SizeBytes
                        }).ToList()
                        : new List<LogFileViewModel>();

                    if (!response.IsSuccess)
                        Log.Warning("Сервер вернул ошибку списка логов: {Error}", response.ErrorMessage);
                }
                catch (OperationCanceledException)
                {
                    // Пользователь ушёл с вкладки — outer catch обработает отмену без предупреждения.
                    throw;
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Не удалось получить список логов сервера");
                    serverFiles = new List<LogFileViewModel>();
                }

                clientFiles = clientFiles.OrderByDescending(file => file.Date).ToList();
                serverFiles = serverFiles.OrderByDescending(file => file.Date).ToList();

                LogFileViewModel? selected = await Dispatcher.InvokeAsync(() =>
                {
                    LogFileViewModel? previous =
                        (_activeLogSource == LogSourceKind.Server
                            ? ServerLogFilesDataGrid.SelectedItem
                            : ClientLogFilesDataGrid.SelectedItem) as LogFileViewModel;

                    LogFileViewModel? clientSelection = FillLogGrid(
                        ClientLogFilesDataGrid,
                        _clientLogFiles,
                        clientFiles,
                        LogSourceKind.Client,
                        previous);

                    LogFileViewModel? serverSelection = FillLogGrid(
                        ServerLogFilesDataGrid,
                        _serverLogFiles,
                        serverFiles,
                        LogSourceKind.Server,
                        previous);

                    // Активной остаётся та таблица, из которой пользователь выбрал файл последней.
                    LogFileViewModel? result = _activeLogSource == LogSourceKind.Server
                        ? serverSelection ?? clientSelection
                        : clientSelection ?? serverSelection;

                    if (result != null)
                        _activeLogSource = result.Source;

                    // В таблице должен быть выбран только один файл.
                    DataGrid inactiveGrid = _activeLogSource == LogSourceKind.Client
                        ? ServerLogFilesDataGrid
                        : ClientLogFilesDataGrid;
                    inactiveGrid.SelectionChanged -= LogFilesDataGrid_SelectionChanged;
                    inactiveGrid.SelectedItem = null;
                    inactiveGrid.SelectionChanged += LogFilesDataGrid_SelectionChanged;

                    if (result == null)
                        ClearLogDetails();

                    return result;
                });

                if (selected != null)
                    await LoadEntriesAsync(selected);
            }
            catch (OperationCanceledException)
            {
                // Пользователь ушёл с вкладки — старая загрузка больше не нужна.
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Не удалось загрузить список файлов журнала");
            }
            finally
            {
                if (_logLoadCts == cts)
                    _logLoadCts = null;
            }
        }

        private async void LogFilesDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is not DataGrid grid || grid.SelectedItem is not LogFileViewModel file)
            {
                _logLoadCts?.Cancel();
                ClearLogDetails();
                return;
            }

            _activeLogSource = file.Source;

            // Снимаем выделение с другой таблицы, чтобы активным был ровно один файл.
            DataGrid otherGrid = grid == ClientLogFilesDataGrid
                ? ServerLogFilesDataGrid
                : ClientLogFilesDataGrid;
            if (otherGrid.SelectedItem != null)
            {
                otherGrid.SelectionChanged -= LogFilesDataGrid_SelectionChanged;
                otherGrid.SelectedItem = null;
                otherGrid.SelectionChanged += LogFilesDataGrid_SelectionChanged;
            }

            await LoadEntriesAsync(file);
        }

        private async Task LoadEntriesAsync(LogFileViewModel file)
        {
            _logLoadCts?.Cancel();
            var cts = new CancellationTokenSource();
            _logLoadCts = cts;

            try
            {
                List<LogEntryViewModel> entries;

                if (file.Source == LogSourceKind.Server)
                {
                    AdminLogContentResponseDto response =
                        await _client.GetAdminLogContentAsync(file.Name);
                    cts.Token.ThrowIfCancellationRequested();

                    if (!response.IsSuccess)
                        throw new InvalidOperationException(response.ErrorMessage);

                    entries = await Task.Run(
                        () => _logFileService.ParseContent(response.Content),
                        cts.Token);
                }
                else
                {
                    // Serilog продолжает писать в локальный файл — читаем с разрешённым shared access.
                    entries = await Task.Run(
                        () => _logFileService.ReadEntries(file.FullPath),
                        cts.Token);
                }

                cts.Token.ThrowIfCancellationRequested();
                await Dispatcher.InvokeAsync(() => ShowLogFile(file, entries));
            }
            catch (OperationCanceledException)
            {
                // Выбран другой файл или вкладка закрыта.
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Не удалось прочитать файл журнала {Source}/{Name}",
                    file.SourceDisplay, file.Name);
                await Dispatcher.InvokeAsync(() =>
                {
                    _allLogEntries.Clear();
                    _logEntries.Clear();
                    LogNameTextBlock.Text = file.Name;
                    LogTypesItemsControl.ItemsSource = Array.Empty<LogLevelInfoViewModel>();
                    LogCountTextBlock.Text = "Всего событий: 0";
                });
            }
            finally
            {
                if (_logLoadCts == cts)
                    _logLoadCts = null;
            }
        }

        private void ShowLogFile(LogFileViewModel file, List<LogEntryViewModel> entries)
        {
            _allLogEntries = entries;

            LogNameTextBlock.Text = file.Name;

            UpdateLogLevelFilter(entries);
            ApplyLogFilter();
        }

        /// <summary>
        /// Заполняем список типов событий только теми уровнями, которые есть в файле.
        /// </summary>
        private void UpdateLogLevelFilter(List<LogEntryViewModel> entries)
        {
            string? previous = FilterLevelComboBox.SelectedItem as string;

            var levels = new[] { "Все" }
                .Concat(entries
                    .Select(entry => entry.Level)
                    .Where(level => !string.IsNullOrWhiteSpace(level))
                    .Distinct()
                    .OrderBy(level => level))
                .ToList();

            _suppressLogFilterEvent = true;
            FilterLevelComboBox.ItemsSource = levels;
            FilterLevelComboBox.SelectedItem = levels.Contains(previous ?? string.Empty)
                ? previous
                : "Все";
            _suppressLogFilterEvent = false;
        }

        /// <summary>
        /// Легенда уровней событий с цветовыми значками.
        /// </summary>
        private static List<LogLevelInfoViewModel> BuildLogLegend()
        {
            return new List<LogLevelInfoViewModel>
            {
                new() { Code = "INF", Description = "информация", Color = GetLevelBrush("INF") },
                new() { Code = "WRN", Description = "предупреждение (WARN)", Color = GetLevelBrush("WRN") },
                new() { Code = "ERR", Description = "ошибка", Color = GetLevelBrush("ERR") },
                new() { Code = "DBG", Description = "отладка", Color = GetLevelBrush("DBG") },
                new() { Code = "FTL", Description = "критическая ошибка", Color = GetLevelBrush("FTL") }
            };
        }

        /// <summary>
        /// Приводит разные варианты написания уровня к одному коду.
        /// </summary>
        private static string NormalizeLevel(string level)
        {
            return level.Trim().ToUpperInvariant() switch
            {
                "INFO" => "INF",
                "WARN" or "WARNING" => "WRN",
                "ERROR" => "ERR",
                "DEBUG" => "DBG",
                "FATAL" or "CRITICAL" => "FTL",
                var other => other
            };
        }

        private static Brush GetLevelBrush(string level)
        {
            string resourceKey = NormalizeLevel(level) switch
            {
                "INF" => "InfoLevelBrush",
                "WRN" => "WarnLevelBrush",
                "ERR" => "ErrorLevelBrush",
                "DBG" => "DebugLevelBrush",
                "FTL" => "FatalLevelBrush",
                _ => "AccentBrush"
            };

            return Application.Current?.TryFindResource(resourceKey) as Brush ?? Brushes.Gray;
        }

        /// <summary>
        /// Статистика по всему файлу: «INF: 120», «WRN: 1» и т.д. с цветными точками.
        /// </summary>
        private static List<LogLevelInfoViewModel> BuildLogStats(IEnumerable<LogEntryViewModel> entries)
        {
            return entries
                .Where(entry => !string.IsNullOrWhiteSpace(entry.Level))
                .GroupBy(entry => NormalizeLevel(entry.Level))
                .OrderByDescending(group => group.Count())
                .Select(group => new LogLevelInfoViewModel
                {
                    Code = group.Key,
                    Count = group.Count(),
                    Color = GetLevelBrush(group.Key)
                })
                .ToList();
        }

        private void FilterComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_suppressLogFilterEvent)
                ApplyLogFilter();
        }

        private void FilterNumeric_ValueChanged(object? sender, EventArgs e)
        {
            if (!_suppressLogFilterEvent)
                ApplyLogFilter();
        }

        private void ApplyFilterButton_Click(object sender, RoutedEventArgs e)
        {
            ApplyLogFilter();
        }

        private void ResetFilterButton_Click(object sender, RoutedEventArgs e)
        {
            _suppressLogFilterEvent = true;
            FilterHourFromNumeric.Value = 0;
            FilterMinuteFromNumeric.Value = 0;
            FilterHourToNumeric.Value = 23;
            FilterMinuteToNumeric.Value = 59;
            FilterLevelComboBox.SelectedItem = "Все";
            _suppressLogFilterEvent = false;

            ApplyLogFilter();
        }

        private void ApplyLogFilter()
        {
            if (FilterHourFromNumeric == null || FilterMinuteFromNumeric == null ||
                FilterHourToNumeric == null || FilterMinuteToNumeric == null ||
                FilterLevelComboBox == null || LogEntriesDataGrid == null)
                return;

            var from = new TimeSpan(FilterHourFromNumeric.Value, FilterMinuteFromNumeric.Value, 0);
            var to = new TimeSpan(FilterHourToNumeric.Value, FilterMinuteToNumeric.Value, 59);
            bool wrapsMidnight = from > to;
            bool isFullDay = from == TimeSpan.Zero && to == new TimeSpan(23, 59, 59);
            string level = FilterLevelComboBox.SelectedItem as string ?? "Все";

            var filtered = _allLogEntries
                .Where(entry =>
                {
                    // Строки без распознанного времени (стек-трейсы) показываем только за весь день.
                    if (entry.Timestamp == default)
                        return isFullDay && (level == "Все" || entry.Level == level);

                    TimeSpan time = entry.Timestamp.TimeOfDay;
                    bool timeOk = wrapsMidnight
                        ? time >= from || time <= to
                        : time >= from && time <= to;

                    return timeOk && (level == "Все" || entry.Level == level);
                })
                .ToList();

            _logEntries.Clear();
            foreach (var entry in filtered)
                _logEntries.Add(entry);

            // Статистика типов — по всему файлу, счётчик — с учётом фильтра.
            LogTypesItemsControl.ItemsSource = BuildLogStats(_allLogEntries);
            LogCountTextBlock.Text = filtered.Count == _allLogEntries.Count
                ? $"Всего событий: {_allLogEntries.Count}"
                : $"Всего событий: {filtered.Count} из {_allLogEntries.Count}";
        }

        private void ClearLogDetails()
        {
            _allLogEntries.Clear();
            _logEntries.Clear();
            LogNameTextBlock.Text = string.Empty;
            LogTypesItemsControl.ItemsSource = Array.Empty<LogLevelInfoViewModel>();
            LogCountTextBlock.Text = string.Empty;
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
