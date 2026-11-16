using System;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using RGRTU.Messenger.Client.Configuration;
using RGRTU.Messenger.Client.Network;
using RGRTU.Messenger.Shared.Models.DTO;
using RGRTU.Messenger.Shared.Network;
using Serilog;

namespace RGRTU.Messenger.Client.Views
{
    /// <summary>
    /// Класс, реализующий логику взаимодействия для окна авторизации пользователя (п. 3.a).
    /// Отвечает за сбор учетных данных, их валидацию, хэширование пароля и сетевое взаимодействие с сервером.
    /// </summary>
    public partial class LoginWindow : Window
    {
        /// <summary>
        /// Экземпляр сетевого клиента, используемый для установки соединения и обмена пакетами с сервером.
        /// </summary>
        private readonly MessengerClient _client;

        /// <summary>
        /// Инициализирует новый экземпляр окна авторизации, настраивает компоненты UI 
        /// и подписывается на события сетевого клиента.
        /// </summary>
        public LoginWindow()
        {
            InitializeComponent();
            _client = new MessengerClient();

            _client.OnAuthSuccess += Client_OnAuthSuccess;
            _client.OnAuthFailed += Client_OnAuthFailed;
            _client.OnDisconnected += Client_OnDisconnected;
        }

        /// <summary>
        /// Обработчик события нажатия на кнопку "Войти".
        /// Выполняет валидацию введенных данных, устанавливает сетевое соединение 
        /// и отправляет запрос на авторизацию.
        /// </summary>
        /// <param name="sender">Источник события (кнопка).</param>
        /// <param name="e">Аргументы события.</param>
        private async void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            string login = LoginTextBox.Text.Trim();
            string password = PasswordBox.Password;

            if (string.IsNullOrEmpty(login) || string.IsNullOrEmpty(password))
            {
                Log.Warning("Попытка входа с пустыми полями");
                ShowError("Введите логин и пароль");
                return;
            }

            if (login.Length < ClientConfig.MinLoginLength)
            {
                Log.Warning("Попытка входа с коротким логином: {Login}", login);
                ShowError($"Логин должен быть не менее {ClientConfig.MinLoginLength} символов");
                return;
            }

            Log.Information("Инициализация входа для пользователя: {Login}", login);
            LoginButton.IsEnabled = false;
            LoginButton.Content = "Подключение...";
            ErrorTextBlock.Visibility = Visibility.Collapsed;

            bool connected = await _client.ConnectAsync(
                ClientConfig.ServerIp,
                ClientConfig.ServerPort,
                ClientConfig.ConnectionTimeoutMs);

            if (!connected)
            {
                Log.Error("Не удалось установить сетевое соединение с сервером {Ip}:{Port}", ClientConfig.ServerIp, ClientConfig.ServerPort);
                ShowError("Не удалось подключиться к серверу. Проверьте, запущен ли он.");
                ResetLoginState();
                return;
            }

            string passwordHash = ComputeSha256Hash(password);
            Log.Debug("Пароль хэширован, отправка запроса авторизации");

            var authRequest = new AuthRequestDto { Login = login, PasswordHash = passwordHash };
            await _client.SendPacketAsync(MessageType.AuthRequest, authRequest);
        }

        /// <summary>
        /// Обработчик события успешной авторизации на сервере.
        /// Открывает главное окно чата и закрывает текущее окно авторизации.
        /// </summary>
        /// <param name="response">DTO с данными авторизованного пользователя.</param>
        private void Client_OnAuthSuccess(AuthResponseDto response)
        {
            Dispatcher.Invoke(() =>
            {
                Log.Information("Успешная авторизация пользователя: {Username} (Admin: {IsAdmin})", response.Username, response.IsAdmin);
                var mainWindow = new MainWindow(_client, response);
                mainWindow.Show();
                this.Close();
            });
        }

        /// <summary>
        /// Обработчик события ошибки авторизации, полученной от сервера.
        /// Отображает текст ошибки пользователю и сбрасывает состояние интерфейса.
        /// </summary>
        /// <param name="errorMessage">Текст ошибки, возвращенный сервером.</param>
        private void Client_OnAuthFailed(string errorMessage)
        {
            Dispatcher.Invoke(() =>
            {
                Log.Warning("Ошибка авторизации на сервере: {Message}", errorMessage);
                ShowError(errorMessage);
                ResetLoginState();
                _client.Disconnect();
            });
        }

        /// <summary>
        /// Обработчик события неожиданного разрыва соединения с сервером.
        /// Информирует пользователя и возвращает интерфейс в исходное состояние.
        /// </summary>
        private void Client_OnDisconnected()
        {
            Dispatcher.Invoke(() =>
            {
                Log.Warning("Соединение с сервером разорвано во время авторизации");
                ShowError("Соединение с сервером разорвано");
                ResetLoginState();
            });
        }

        /// <summary>
        /// Сбрасывает состояние элементов управления окна авторизации в исходное положение,
        /// разблокируя кнопку входа и восстанавливая её текст.
        /// </summary>
        private void ResetLoginState()
        {
            LoginButton.IsEnabled = true;
            LoginButton.Content = "Войти";
        }

        /// <summary>
        /// Отображает текстовое сообщение об ошибке в соответствующем элементе интерфейса.
        /// </summary>
        /// <param name="message">Текст ошибки для отображения пользователю.</param>
        private void ShowError(string message)
        {
            ErrorTextBlock.Text = message;
            ErrorTextBlock.Visibility = Visibility.Visible;
        }

        /// <summary>
        /// Вычисляет криптографический хэш SHA-256 для переданной строки.
        /// Используется для безопасной передачи пароля по сети в зашифрованном виде.
        /// </summary>
        /// <param name="rawData">Исходная строка (пароль в открытом виде).</param>
        /// <returns>Строковое представление хэша в шестнадцатеричном формате (нижний регистр).</returns>
        private string ComputeSha256Hash(string rawData)
        {
            using SHA256 sha256Hash = SHA256.Create();
            byte[] bytes = sha256Hash.ComputeHash(Encoding.UTF8.GetBytes(rawData));
            StringBuilder builder = new StringBuilder();
            foreach (byte t in bytes)
            {
                builder.Append(t.ToString("x2"));
            }
            return builder.ToString();
        }
    }
}