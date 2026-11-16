using System;
using System.Net.Sockets;
using System.Threading.Tasks;
using RGRTU.Messenger.Server.Configuration;
using RGRTU.Messenger.Shared.Models.DTO;
using RGRTU.Messenger.Shared.Models.DTO.Enums;
using RGRTU.Messenger.Shared.Network;
using Serilog;

namespace RGRTU.Messenger.Server.Core
{
    /// <summary>
    /// Представляет индивидуальную сессию подключенного клиента.
    /// Отвечает за чтение данных из сетевого потока, маршрутизацию сообщений 
    /// и управление состоянием конкретного пользователя (п. 2.c, 2.d).
    /// </summary>
    public class ClientSession
    {
        /// <summary>
        /// Уникальный идентификатор сессии (клиента).
        /// </summary>
        public int Id { get; }

        /// <summary>
        /// Объект TCP клиента
        /// </summary>
        private readonly TcpClient _tcpClient;

        /// <summary>
        /// Объект сетевого потока
        /// </summary>
        private readonly NetworkStream _stream;

        /// <summary>
        /// Объект сервера
        /// </summary>
        private readonly MessengerServer _server;

        /// <summary>
        /// Идентификатор пользователя в базе данных (0, если не авторизован).
        /// </summary>
        public int UserId { get; set; } = 0;

        /// <summary>
        /// Отображаемое имя пользователя.
        /// </summary>
        public string Username { get; set; } = ServerConfig.DefaultGuestName;

        /// <summary>
        /// Флаг, указывающий, прошел ли пользователь успешную авторизацию.
        /// </summary>
        public bool IsAuthenticated { get; set; } = false;

        /// <summary>
        /// Инициализирует новую сессию клиента.
        /// </summary>
        public ClientSession(int id, TcpClient tcpClient, MessengerServer server)
        {
            Id = id;
            _tcpClient = tcpClient;
            _stream = tcpClient.GetStream();
            _server = server;
        }

        /// <summary>
        /// Основной цикл обработки входящих сообщений от клиента.
        /// </summary>
        public async Task HandleClientAsync()
        {
            try
            {
                while (_tcpClient.Connected)
                {
                    var (messageType, payloadJson) = PacketSerializer.Deserialize(_stream);
                    Log.Information("Получен пакет от ClientID={Id}: Type={Type}", Id, messageType);

                    await ProcessMessageAsync(messageType, payloadJson);
                }
            }
            catch (EndOfStreamException)
            {
                Log.Information("Клиент {Id} корректно закрыл соединение", Id);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Ошибка при обработке сообщений от клиента {Id}", Id);
            }
            finally
            {
                await _server.BroadcastStatusAsync(this.UserId, this.Username, UserStatus.Offline);
                _server.RemoveClient(this.Id);
                _tcpClient.Close();
            }
        }

        /// <summary>
        /// Маршрутизирует полученное сообщение в зависимости от его типа.
        /// </summary>
        private async Task ProcessMessageAsync(MessageType type, string json)
        {
            switch (type)
            {
                case MessageType.AuthRequest:
                    await HandleAuthAsync(json);
                    break;

                case MessageType.SendTextMessage:
                    if (!IsAuthenticated)
                    {
                        Log.Warning("Попытка отправки сообщения неавторизованным клиентом {Id}", Id);
                        return;
                    }

                    var msg = PacketSerializer.DeserializePayload<ChatMessageDto>(json);
                    Log.Information("Чат: {Sender} пишет: {Text}", msg.SenderName, msg.Text);

                    var broadcastPacket = PacketSerializer.Serialize(MessageType.BroadcastMessage, msg);
                    await _server.BroadcastMessageAsync(broadcastPacket);
                    break;

                case MessageType.Ping:
                    await SendPacketAsync(PacketSerializer.Serialize(MessageType.Pong, new { }));
                    break;

                default:
                    Log.Warning("Необработанный тип сообщения: {Type} от клиента {Id}", type, Id);
                    break;
            }
        }

        /// <summary>
        /// Обрабатывает запрос на авторизацию пользователя.
        /// </summary>
        private async Task HandleAuthAsync(string json)
        {
            var authReq = PacketSerializer.DeserializePayload<AuthRequestDto>(json);

            bool isSuccess = authReq.Login.Length >= ServerConfig.MinLoginLength;
            bool isAdmin = authReq.Login.Equals(ServerConfig.AdminLoginIdentifier, StringComparison.OrdinalIgnoreCase);

            var response = new AuthResponseDto
            {
                IsSuccess = isSuccess,
                UserId = isSuccess ? Id : 0,
                Username = authReq.Login,
                IsAdmin = isAdmin,
                ErrorMessage = isSuccess ? string.Empty : "Неверный логин или пароль"
            };

            if (isSuccess)
            {
                this.IsAuthenticated = true;
                this.UserId = Id;
                this.Username = authReq.Login;
                Log.Information("Успешная авторизация: {Login} (Admin: {IsAdmin})", authReq.Login, isAdmin);

                await _server.BroadcastStatusAsync(this.UserId, this.Username, UserStatus.Online);
            }
            else
            {
                Log.Warning("Ошибка авторизации: {Login} (ClientID: {Id})", authReq.Login, Id);
            }

            var responsePacket = PacketSerializer.Serialize(MessageType.AuthResponse, response);
            await SendPacketAsync(responsePacket);
        }

        /// <summary>
        /// Асинхронно отправляет байтовый пакет клиенту через сетевой поток.
        /// </summary>
        public async Task SendPacketAsync(byte[] packetData)
        {
            if (_tcpClient.Connected)
            {
                await _stream.WriteAsync(packetData, 0, packetData.Length);
            }
        }
    }
}