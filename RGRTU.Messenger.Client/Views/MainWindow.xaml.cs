using System;
using System.Windows;
using System.Windows.Input;
using RGRTU.Messenger.Client.Network;
using RGRTU.Messenger.Shared.Models.DTO;
using RGRTU.Messenger.Shared.Network;
using Serilog;

namespace RGRTU.Messenger.Client.Views
{
    /// <summary>
    /// Класс, реализующий логику взаимодействия для главного окна группового чата (п. 3.b).
    /// Отвечает за отображение истории сообщений, списка участников и отправку новых сообщений на сервер.
    /// </summary>
    public partial class MainWindow : Window
    {
        /// <summary>
        /// Экземпляр сетевого клиента для отправки сообщений и получения событий от сервера.
        /// </summary>
        private readonly MessengerClient _client;

        /// <summary>
        /// Данные текущего авторизованного пользователя (ID, имя, роль администратора).
        /// </summary>
        private readonly AuthResponseDto _currentUser;

        /// <summary>
        /// Инициализирует новое главное окно чата, настраивает заголовок 
        /// и подписывается на события сетевого клиента для безопасного обновления интерфейса.
        /// </summary>
        /// <param name="client">Активный экземпляр сетевого клиента.</param>
        /// <param name="currentUser">DTO с данными текущего авторизованного пользователя.</param>
        public MainWindow(MessengerClient client, AuthResponseDto currentUser)
        {
            InitializeComponent();

            _client = client;
            _currentUser = currentUser;

            Title = $"Мессенджер РГРТУ - {_currentUser.Username}";

            _client.OnMessageReceived += Client_OnMessageReceived;
            _client.OnUserStatusChanged += Client_OnUserStatusChanged;
            _client.OnDisconnected += Client_OnDisconnected;
        }

        /// <summary>
        /// Обработчик события получения нового сообщения группового чата от сервера.
        /// Безопасно обновляет элемент управления ListBox в потоке пользовательского интерфейса (UI) 
        /// и выполняет автоматическую прокрутку к последнему сообщению.
        /// </summary>
        /// <param name="message">Объект DTO, содержащий данные полученного сообщения.</param>
        private void Client_OnMessageReceived(ChatMessageDto message)
        {
            Dispatcher.Invoke(() =>
            {
                MessagesListBox.Items.Add($"[{message.Timestamp:HH:mm}] {message.SenderName}: {message.Text}");
                MessagesListBox.ScrollIntoView(MessagesListBox.Items[MessagesListBox.Items.Count - 1]);
            });
        }

        /// <summary>
        /// Обработчик события изменения статуса пользователя (подключен/отключен/активен) (п. 2.d).
        /// Обновляет список участников в пользовательском интерфейсе.
        /// </summary>
        /// <param name="status">Объект DTO, содержащий обновленные данные о статусе пользователя.</param>
        private void Client_OnUserStatusChanged(UserStatusDto status)
        {
            Dispatcher.Invoke(() =>
            {
                UsersListBox.Items.Add($"{status.Username} [{status.Status}]");
            });
        }

        /// <summary>
        /// Обработчик события разрыва соединения с сервером во время активной работы в чате.
        /// Информирует пользователя об ошибке и инициирует закрытие окна.
        /// </summary>
        private void Client_OnDisconnected()
        {
            Dispatcher.Invoke(() =>
            {
                Log.Warning("Соединение с сервером разорвано во время работы в чате");
                MessageBox.Show("Соединение с сервером разорвано.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                Close();
            });
        }

        /// <summary>
        /// Обработчик события нажатия на кнопку отправки сообщения в интерфейсе.
        /// </summary>
        /// <param name="sender">Источник события (кнопка).</param>
        /// <param name="e">Аргументы события.</param>
        private async void SendButton_Click(object sender, RoutedEventArgs e)
        {
            await SendMessageAsync();
        }

        /// <summary>
        /// Обработчик события нажатия клавиш в поле ввода сообщения.
        /// Позволяет отправить сообщение по нажатию клавиши Enter (игнорируя Shift+Enter для переноса строки).
        /// </summary>
        /// <param name="sender">Источник события (TextBox).</param>
        /// <param name="e">Аргументы события клавиатуры.</param>
        private async void MessageInputTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && !Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
            {
                await SendMessageAsync();
                e.Handled = true;
            }
        }

        /// <summary>
        /// Асинхронно формирует объект сообщения и отправляет его на сервер для рассылки всем участникам чата (п. 1.e).
        /// </summary>
        /// <returns>Задача (Task), представляющая асинхронную операцию отправки сетевого пакета.</returns>
        private async System.Threading.Tasks.Task SendMessageAsync()
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
    }
}