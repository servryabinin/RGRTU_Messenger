using System;
using System.Collections.Concurrent;
using System.Net;
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
        /// Инициализирует новый экземпляр сервера.
        /// </summary>
        /// <param name="port">Порт для прослушивания входящих подключений.</param>
        public MessengerServer(int port)
        {
            _port = port;
            _listener = new TcpListener(IPAddress.Any, _port);
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

                    await BroadcastStatusAsync(clientId, ServerConfig.DefaultGuestName, UserStatus.Online);

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
            foreach (var client in _activeClients.Values)
            {
                if (client.Id != excludeClientId)
                {
                    await client.SendPacketAsync(packetData);
                }
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