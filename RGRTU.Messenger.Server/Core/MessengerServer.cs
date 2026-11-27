using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using RGRTU.Messenger.Server.Data;
using RGRTU.Messenger.Shared.Models.DTO;
using RGRTU.Messenger.Shared.Models.DTO.Enums;
using RGRTU.Messenger.Shared.Network;
using Serilog;

namespace RGRTU.Messenger.Server.Core
{
    /// <summary>
    /// Основной класс сервера, отвечающий за прослушивание сетевого порта
    /// и управление жизненным циклом подключений клиентов (п. 1.b, 1.c).
    /// </summary>
    public class MessengerServer
    {
        /// <summary>
        /// Порт сервера
        /// </summary>
        private readonly int _port;

        /// <summary>
        /// Объект прослушивателя порта
        /// </summary>
        private readonly TcpListener _listener;

        /// <summary>
        /// Очередь запросов
        /// </summary>
        private readonly ConcurrentDictionary<int, ClientSession> _activeClients = new();

        /// <summary>
        /// Следующий индекс клиента
        /// </summary>
        private int _nextClientId = 1;

        /// <summary>
        /// Хранилище пользователей SQLite (п. 2.a, 2.d).
        /// </summary>
        public UserRepository Users { get; }

        /// <summary>
        /// Обработчик административных запросов от графического клиента.
        /// </summary>
        public AdminRequestHandler Admin { get; }

        /// <summary>
        /// Инициализирует новый экземпляр сервера.
        /// </summary>
        /// <param name="port">Порт для прослушивания входящих подключений.</param>
        /// <param name="users">Хранилище пользователей.</param>
        public MessengerServer(int port, UserRepository users)
        {
            _port = port;
            _listener = new TcpListener(IPAddress.Any, _port);
            Users = users;
            Admin = new AdminRequestHandler(this, users);
        }

        /// <summary>
        /// Запускает прослушивание порта и начинает асинхронное принятие клиентов.
        /// </summary>
        public async Task StartAsync()
        {
            _listener.Start();
            Log.Information("Сервер запущен и слушает порт {Port}", _port);

            while (true)
            {
                try
                {
                    TcpClient tcpClient = await _listener.AcceptTcpClientAsync();
                    int clientId = _nextClientId++;

                    var endPoint = tcpClient.Client.RemoteEndPoint?.ToString() ?? "Unknown";
                    Log.Information("Новое подключение: ClientID={ClientId}, IP={EndPoint}", clientId, endPoint);

                    var session = new ClientSession(clientId, tcpClient, this);
                    _activeClients.TryAdd(clientId, session);

                    _ = Task.Run(() => session.HandleClientAsync());
                }
                catch (SocketException ex)
                {
                    Log.Error(ex, "Ошибка сокета при приеме клиента");
                }
            }
        }

        /// <summary>
        /// Рассылает байтовый пакет всем активным клиентам, кроме указанного.
        /// </summary>
        public async Task BroadcastMessageAsync(byte[] packetData, int excludeClientId = 0)
        {
            foreach (var client in _activeClients.Values.ToArray())
            {
                if (client.Id != excludeClientId)
                {
                    await client.SendPacketAsync(packetData);
                }
            }
        }

        /// <summary>
        /// Отправляет сообщение только участникам общего чата.
        /// </summary>
        public async Task BroadcastMessageToChatMembersAsync(byte[] packetData)
        {
            foreach (var client in _activeClients.Values.Where(c => c.IsAuthenticated && c.IsInChat).ToArray())
            {
                await client.SendPacketAsync(packetData);
            }
        }

        /// <summary>
        /// Отправляет сообщение участникам общего чата, кроме указанного пользователя.
        /// </summary>
        public async Task BroadcastMessageToChatMembersExceptAsync(int excludeUserId, byte[] packetData)
        {
            foreach (var client in _activeClients.Values
                         .Where(c => c.IsAuthenticated && c.IsInChat && c.UserId != excludeUserId)
                         .ToArray())
            {
                await client.SendPacketAsync(packetData);
            }
        }

        /// <summary>
        /// Формирует список участников чата для конкретной сессии.
        /// </summary>
        public async Task<RGRTU.Messenger.Shared.Models.DTO.Admin.ChatMembersResponseDto> BuildChatMembersResponseAsync(ClientSession session)
        {
            var users = await Users.GetActiveForChatAsync();
            bool isAdmin = session.Role == RGRTU.Messenger.Server.Data.UserRole.Admin;
            var self = users.FirstOrDefault(u => u.Id == session.UserId);

            var members = users
                .Where(u => isAdmin || u.IsInChat)
                .Select(u => new RGRTU.Messenger.Shared.Models.DTO.Admin.ChatMemberDto
                {
                    Id = u.Id,
                    Login = u.Login,
                    DisplayName = u.DisplayName,
                    IsInChat = u.IsInChat,
                    IsOnline = GetUserStatus(u.Id) != UserStatus.Offline,
                    IsChatRemovedByAdmin = u.IsChatRemovedByAdmin,
                    IsChatInvitedByAdmin = u.IsChatInvitedByAdmin,
                    HasLeftChat = u.HasLeftChat
                })
                .ToList();

            return new RGRTU.Messenger.Shared.Models.DTO.Admin.ChatMembersResponseDto
            {
                IsSuccess = true,
                CanManage = isAdmin,
                IsCurrentUserInChat = self?.IsInChat == true,
                IsCurrentUserChatRemovedByAdmin = self?.IsChatRemovedByAdmin == true,
                IsCurrentUserChatInvitedByAdmin = self?.IsChatInvitedByAdmin == true,
                IsCurrentUserHasLeftChat = self?.HasLeftChat == true,
                Members = members
            };
        }

        /// <summary>
        /// Отправляет актуальный список участников чата одной сессии.
        /// </summary>
        public async Task SendChatMembersToSessionAsync(ClientSession session)
        {
            if (!session.IsAuthenticated)
                return;

            var response = await BuildChatMembersResponseAsync(session);
            var packet = PacketSerializer.Serialize(MessageType.ChatMembersResponse, response);
            await session.SendPacketAsync(packet);
        }

        /// <summary>
        /// Рассылает актуальный список участников всем авторизованным сессиям.
        /// </summary>
        public async Task BroadcastChatMembersAsync()
        {
            var sessions = _activeClients.Values.Where(s => s.IsAuthenticated).ToArray();
            foreach (var session in sessions)
            {
                var user = await Users.GetByIdAsync(session.UserId);
                session.SetChatMembership(user?.IsInChat == true && user.IsBlocked == false);
                await SendChatMembersToSessionAsync(session);
            }
        }

        /// <summary>
        /// Формирует и рассылает всем клиентам уведомление об изменении статуса пользователя (п. 2.d).
        /// </summary>
        public async Task BroadcastStatusAsync(int userId, string username, UserStatus status)
        {
            var statusDto = new UserStatusDto
            {
                UserId = userId,
                Username = username,
                Status = status
            };
            var packet = PacketSerializer.Serialize(MessageType.UserStatusUpdate, statusDto);
            await BroadcastMessageAsync(packet);
        }

        /// <summary>
        /// Возвращает список авторизованных подключённых пользователей.
        /// </summary>
        public Task<IReadOnlyList<ClientSession>> GetAuthenticatedSessionsAsync()
        {
            IReadOnlyList<ClientSession> sessions = _activeClients.Values
                .Where(s => s.IsAuthenticated)
                .OrderBy(s => s.Id)
                .ToList();

            return Task.FromResult(sessions);
        }

        /// <summary>
        /// Возвращает текущий статус пользователя по его активным сессиям.
        /// </summary>
        public UserStatus GetUserStatus(int userId)
        {
            var statuses = _activeClients.Values
                .Where(s => s.IsAuthenticated && s.UserId == userId)
                .Select(s => s.Status)
                .ToArray();

            if (statuses.Length == 0)
                return UserStatus.Offline;

            return statuses.Contains(UserStatus.Online)
                ? UserStatus.Online
                : statuses[0];
        }

        /// <summary>
        /// Отключает все активные сессии указанного пользователя (блокировка/удаление).
        /// </summary>
        /// <returns>Количество отключённых сессий.</returns>
        public async Task<int> DisconnectUserAsync(int userId, string reason)
        {
            var sessions = _activeClients.Values
                .Where(s => s.IsAuthenticated && s.UserId == userId)
                .ToArray();

            foreach (var session in sessions)
            {
                await DisconnectSessionAsync(session.Id, reason);
            }

            return sessions.Length;
        }

        /// <summary>
        /// Отключает конкретную сессию по инициативе администратора.
        /// </summary>
        public async Task DisconnectSessionAsync(int clientId, string reason)
        {
            if (_activeClients.TryGetValue(clientId, out var session))
            {
                await session.CloseAsync(reason);
            }
        }

        /// <summary>
        /// Удаляет клиента из списка активных при его отключении (п. 2.c).
        /// </summary>
        public void RemoveClient(int clientId)
        {
            if (_activeClients.TryRemove(clientId, out _))
            {
                Log.Information("Клиент {ClientId} отключен. Активных клиентов: {Count}", clientId, _activeClients.Count);
            }
        }
    }
}
