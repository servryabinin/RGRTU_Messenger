using System;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using RGRTU.Messenger.Server.Data;
using RGRTU.Messenger.Shared.Extensions;
using RGRTU.Messenger.Shared.Models.DTO;
using RGRTU.Messenger.Shared.Models.DTO.Admin;
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
        public int UserId { get; private set; }

        /// <summary>
        /// Отображаемое имя пользователя.
        /// </summary>
        public string Username { get; private set; }

        /// <summary>
        /// Роль авторизованного пользователя.
        /// </summary>
        public UserRole Role { get; private set; } = UserRole.User;

        /// <summary>
        /// Флаг, указывающий, прошел ли пользователь успешную авторизацию.
        /// </summary>
        public bool IsAuthenticated { get; private set; }

        /// <summary>
        /// Текущий статус пользователя в этой сессии.
        /// </summary>
        public UserStatus Status { get; private set; } = UserStatus.Offline;

        /// <summary>
        /// Признак того, что соединение закрыто по инициативе администратора.
        /// </summary>
        private int _closedByAdmin;

        /// <summary>
        /// Инициализирует новую сессию клиента.
        /// </summary>
        public ClientSession(int id, TcpClient tcpClient, MessengerServer server)
        {
            Id = id;
            _tcpClient = tcpClient;
            _stream = tcpClient.GetStream();
            _server = server;
            Username = "Guest";
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
            catch (System.IO.IOException)
            {
                Log.Information("Соединение с клиентом {Id} закрыто", Id);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Ошибка при обработке сообщений от клиента {Id}", Id);
            }
            finally
            {
                if (IsAuthenticated)
                {
                    Status = UserStatus.Offline;
                    await _server.BroadcastStatusAsync(UserId, Username, UserStatus.Offline);
                }

                _server.RemoveClient(Id);
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
                    await HandleChatMessageAsync(json);
                    break;

                case MessageType.KickUser:
                    await HandleKickAsync(json);
                    break;

                case MessageType.SetUserStatus:
                    await HandleSetStatusAsync(json);
                    break;

                case MessageType.AdminGetUsers:
                    await SendPacketAsync(PacketSerializer.Serialize(
                        MessageType.AdminUsersResponse,
                        await _server.Admin.GetUsersAsync(this)));
                    break;

                case MessageType.AdminCreateUser:
                    await SendPacketAsync(PacketSerializer.Serialize(
                        MessageType.AdminActionResponse,
                        await _server.Admin.CreateAsync(
                            this,
                            PacketSerializer.DeserializePayload<AdminCreateUserRequestDto>(json))));
                    break;

                case MessageType.AdminUpdateUser:
                    await SendPacketAsync(PacketSerializer.Serialize(
                        MessageType.AdminActionResponse,
                        await _server.Admin.UpdateAsync(
                            this,
                            PacketSerializer.DeserializePayload<AdminUpdateUserRequestDto>(json))));
                    break;

                case MessageType.AdminDeleteUser:
                    await SendPacketAsync(PacketSerializer.Serialize(
                        MessageType.AdminActionResponse,
                        await _server.Admin.DeleteAsync(
                            this,
                            PacketSerializer.DeserializePayload<AdminDeleteUserRequestDto>(json))));
                    break;

                case MessageType.AdminBlockUser:
                    await SendPacketAsync(PacketSerializer.Serialize(
                        MessageType.AdminActionResponse,
                        await _server.Admin.SetBlockedAsync(
                            this,
                            PacketSerializer.DeserializePayload<AdminBlockUserRequestDto>(json))));
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
        /// Обрабатывает текстовое сообщение, подменяя данные отправителя серверными.
        /// </summary>
        private async Task HandleChatMessageAsync(string json)
        {
            if (!IsAuthenticated)
            {
                Log.Warning("Попытка отправки сообщения неавторизованным клиентом {Id}", Id);
                return;
            }

            var msg = PacketSerializer.DeserializePayload<ChatMessageDto>(json);
            msg.SenderId = UserId;
            msg.SenderName = Username;
            msg.Timestamp = DateTime.Now;

            if (string.IsNullOrWhiteSpace(msg.Text))
                return;

            Log.Information("Чат: {Sender} пишет: {Text}", msg.SenderName, msg.Text);

            var broadcastPacket = PacketSerializer.Serialize(MessageType.BroadcastMessage, msg);
            await _server.BroadcastMessageAsync(broadcastPacket);
        }

        /// <summary>
        /// Обрабатывает запрос администратора на отключение пользователя.
        /// </summary>
        private async Task HandleKickAsync(string json)
        {
            if (!IsAuthenticated || Role != UserRole.Admin)
            {
                Log.Warning("Попытка kick без прав администратора: ClientID={Id}", Id);
                return;
            }

            var request = PacketSerializer.DeserializePayload<AdminKickRequestDto>(json);
            int disconnected = await _server.DisconnectUserAsync(
                request.UserId,
                string.IsNullOrWhiteSpace(request.Reason) ? "Отключён администратором" : request.Reason);

            Log.Information("Администратор {Admin} отключил пользователя Id={Target}. Сессий: {Count}",
                Username, request.UserId, disconnected);
        }

        /// <summary>
        /// Меняет статус текущей сессии и рассылает обновление всем клиентам.
        /// </summary>
        private async Task HandleSetStatusAsync(string json)
        {
            if (!IsAuthenticated)
            {
                Log.Warning("Попытка смены статуса неавторизованным клиентом {Id}", Id);
                return;
            }

            var request = PacketSerializer.DeserializePayload<UserStatusDto>(json);

            // Игнорируем чужой UserId: статус меняется только у текущей сессии.
            if (request.UserId != 0 && request.UserId != UserId)
            {
                Log.Warning("Клиент {Id} попытался изменить статус другого пользователя {Target}", Id, request.UserId);
                return;
            }

            if (request.Status == UserStatus.Offline)
                return;

            Status = request.Status;
            Log.Information("Пользователь {User} сменил статус на {Status}", Username, Status.GetDescription());
            await _server.BroadcastStatusAsync(UserId, Username, Status);
        }

        /// <summary>
        /// Обрабатывает запрос на авторизацию пользователя.
        /// </summary>
        private async Task HandleAuthAsync(string json)
        {
            AuthResponseDto response;

            try
            {
                var authReq = PacketSerializer.DeserializePayload<AuthRequestDto>(json);
                string login = authReq.Login?.Trim() ?? string.Empty;
                string passwordHash = authReq.PasswordHash ?? string.Empty;

                var user = await _server.Users.GetByLoginAsync(login);

                string? error = null;
                if (user is null)
                {
                    error = "Неверный логин или пароль";
                }
                else if (user.IsBlocked)
                {
                    error = string.IsNullOrWhiteSpace(user.BlockReason)
                        ? "Пользователь заблокирован администратором"
                        : $"Пользователь заблокирован. Причина: {user.BlockReason}";
                }
                else if (!PasswordHasher.VerifyClientHash(passwordHash, user.PasswordHash, user.Salt))
                {
                    error = "Неверный логин или пароль";
                }

                if (error is not null)
                {
                    response = new AuthResponseDto
                    {
                        IsSuccess = false,
                        ErrorMessage = error
                    };

                    Log.Warning("Ошибка авторизации: {Login} (ClientID: {Id}) - {Error}", login, Id, error);
                }
                else
                {
                    IsAuthenticated = true;
                    UserId = user!.Id;
                    Username = user.DisplayName;
                    Role = user.Role;
                    Status = UserStatus.Online;

                    await _server.Users.UpdateLastLoginAsync(user.Id);

                    response = new AuthResponseDto
                    {
                        IsSuccess = true,
                        UserId = user.Id,
                        Login = user.Login,
                        Username = user.DisplayName,
                        IsAdmin = user.Role == UserRole.Admin,
                        ErrorMessage = string.Empty
                    };

                    Log.Information("Успешная авторизация: {Login} (UserId={UserId}, Admin={IsAdmin})",
                        user.Login, user.Id, response.IsAdmin);

                    await _server.BroadcastStatusAsync(user.Id, user.DisplayName, UserStatus.Online);
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Некорректный запрос авторизации от ClientID={Id}", Id);
                response = new AuthResponseDto
                {
                    IsSuccess = false,
                    ErrorMessage = "Некорректный запрос авторизации"
                };
            }

            await SendPacketAsync(PacketSerializer.Serialize(MessageType.AuthResponse, response));
        }

        /// <summary>
        /// Закрывает соединение по инициативе администратора, сообщая причину клиенту.
        /// </summary>
        public async Task CloseAsync(string reason)
        {
            if (Interlocked.Exchange(ref _closedByAdmin, 1) == 1)
                return;

            try
            {
                var error = new ErrorDto { Message = reason };
                await SendPacketAsync(PacketSerializer.Serialize(MessageType.Error, error));
                await _stream.FlushAsync();
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Не удалось отправить причину отключения клиенту {Id}", Id);
            }
            finally
            {
                _tcpClient.Close();
            }
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
