using System;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using RGRTU.Messenger.Shared.Models.DTO;
using RGRTU.Messenger.Shared.Models.DTO.Admin;
using RGRTU.Messenger.Shared.Models.DTO.Enums;
using RGRTU.Messenger.Shared.Network;
using Serilog;

namespace RGRTU.Messenger.Client.Network
{
    /// <summary>
    /// Класс, отвечающий за сетевое взаимодействие клиентской части с сервером.
    /// Реализует асинхронное подключение, отправку и фоновый прием сетевых пакетов (п. 3.a).
    /// </summary>
    public class MessengerClient
    {
        /// <summary>
        /// Экземпляр TCP-клиента для установления соединения с сервером.
        /// </summary>
        private TcpClient _tcpClient;

        /// <summary>
        /// Сетевой поток для асинхронной записи и чтения байтовых данных.
        /// </summary>
        private NetworkStream _stream;

        /// <summary>
        /// Флаг, указывающий на текущее состояние логического подключения к серверу.
        /// </summary>
        private bool _isConnected;

        /// <summary>
        /// Событие, вызываемое при успешном прохождении авторизации на сервере.
        /// </summary>
        public event Action<AuthResponseDto> OnAuthSuccess;

        /// <summary>
        /// Событие, вызываемое при получении ошибки авторизации от сервера.
        /// </summary>
        public event Action<string> OnAuthFailed;

        /// <summary>
        /// Событие, вызываемое при получении нового сообщения группового чата.
        /// </summary>
        public event Action<ChatMessageDto> OnMessageReceived;

        /// <summary>
        /// Событие, вызываемое при изменении статуса любого пользователя в чате (п. 2.d).
        /// </summary>
        public event Action<UserStatusDto> OnUserStatusChanged;

        /// <summary>
        /// Событие, вызываемое при неожиданном разрыве соединения с сервером.
        /// </summary>
        public event Action OnDisconnected;

        /// <summary>
        /// Событие получения списка пользователей для админской таблицы.
        /// </summary>
        public event Action<AdminUsersResponseDto> OnAdminUsersReceived;

        /// <summary>
        /// Событие завершения административной операции.
        /// </summary>
        public event Action<AdminActionResponseDto> OnAdminActionCompleted;

        /// <summary>
        /// Семафор, гарантирующий, что запись в сетевой поток не будет перемешана разными потоками UI.
        /// </summary>
        private readonly SemaphoreSlim _sendLock = new(1, 1);

        /// <summary>
        /// Получает значение, указывающее, установлено ли в данный момент активное соединение с сервером.
        /// </summary>
        public bool IsConnected => _isConnected && _tcpClient?.Connected == true;

        /// <summary>
        /// Асинхронно устанавливает TCP-подключение к указанному серверу.
        /// </summary>
        /// <param name="ipAddress">IP-адрес сервера.</param>
        /// <param name="port">Сетевой порт сервера.</param>
        /// <param name="timeoutMs">Максимальное время ожидания подключения в миллисекундах.</param>
        /// <returns>True, если подключение успешно установлено; иначе False.</returns>
        public async Task<bool> ConnectAsync(string ipAddress, int port, int timeoutMs)
        {
            try
            {
                Log.Information("Попытка подключения к серверу {Ip}:{Port}", ipAddress, port);
                _tcpClient = new TcpClient();

                var connectTask = _tcpClient.ConnectAsync(ipAddress, port);
                if (await Task.WhenAny(connectTask, Task.Delay(timeoutMs)) == connectTask)
                {
                    await connectTask;
                    _stream = _tcpClient.GetStream();
                    _isConnected = true;
                    Log.Information("Успешное подключение к серверу");

                    _ = Task.Run(ListenAsync);
                    return true;
                }

                Log.Warning("Таймаут при подключении к серверу {Ip}:{Port}", ipAddress, port);
                return false;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Ошибка при подключении к серверу");
                return false;
            }
        }

        /// <summary>
        /// Асинхронно отправляет сериализованный сетевой пакет на сервер.
        /// </summary>
        /// <typeparam name="T">Тип полезной нагрузки (payload) пакета.</typeparam>
        /// <param name="type">Тип отправляемого сообщения согласно протоколу обмена.</param>
        /// <param name="payload">Объект данных, который будет сериализован в JSON.</param>
        /// <exception cref="InvalidOperationException">Выбрасывается, если попытка отправки происходит без активного подключения.</exception>
        public async Task SendPacketAsync<T>(MessageType type, T payload)
        {
            if (!IsConnected)
            {
                Log.Warning("Попытка отправки пакета типа {Type} без подключения к серверу", type);
                throw new InvalidOperationException("Нет подключения к серверу");
            }

            byte[] packetData = PacketSerializer.Serialize(type, payload);

            await _sendLock.WaitAsync();
            try
            {
                await _stream.WriteAsync(packetData, 0, packetData.Length);
            }
            finally
            {
                _sendLock.Release();
            }

            Log.Debug("Отправлен пакет типа {Type}", type);
        }

        /// <summary>
        /// Фоновый асинхронный цикл, непрерывно читающий входящие пакеты из сетевого потока 
        /// и маршрутизирующий их через соответствующие события (Events) для обновления UI.
        /// </summary>
        private async Task ListenAsync()
        {
            try
            {
                while (_isConnected && _tcpClient.Connected)
                {
                    var (messageType, payloadJson) = PacketSerializer.Deserialize(_stream);
                    Log.Information("Получен пакет от сервера: Type={Type}", messageType);

                    switch (messageType)
                    {
                        case MessageType.AuthResponse:
                            var authRes = PacketSerializer.DeserializePayload<AuthResponseDto>(payloadJson);
                            if (authRes.IsSuccess) OnAuthSuccess?.Invoke(authRes);
                            else OnAuthFailed?.Invoke(authRes.ErrorMessage);
                            break;

                        case MessageType.BroadcastMessage:
                            var msg = PacketSerializer.DeserializePayload<ChatMessageDto>(payloadJson);
                            OnMessageReceived?.Invoke(msg);
                            break;

                        case MessageType.UserStatusUpdate:
                            var status = PacketSerializer.DeserializePayload<UserStatusDto>(payloadJson);
                            OnUserStatusChanged?.Invoke(status);
                            break;

                        case MessageType.Error:
                            var error = PacketSerializer.DeserializePayload<ErrorDto>(payloadJson);
                            OnAuthFailed?.Invoke(error.Message);
                            break;

                        case MessageType.AdminUsersResponse:
                            var users = PacketSerializer.DeserializePayload<AdminUsersResponseDto>(payloadJson);
                            OnAdminUsersReceived?.Invoke(users);
                            break;

                        case MessageType.AdminActionResponse:
                            var actionResult = PacketSerializer.DeserializePayload<AdminActionResponseDto>(payloadJson);
                            OnAdminActionCompleted?.Invoke(actionResult);
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Соединение с сервером разорвано или произошла ошибка чтения");
            }
            finally
            {
                _isConnected = false;
                Log.Information("Клиент отключен от сервера");
                OnDisconnected?.Invoke();
            }
        }

        /// <summary>
        /// Отправляет запрос на смену собственного статуса.
        /// </summary>
        public Task SetUserStatusAsync(int userId, UserStatus status)
        {
            return SendPacketAsync(MessageType.SetUserStatus, new UserStatusDto
            {
                UserId = userId,
                Username = string.Empty,
                Status = status
            });
        }

        /// <summary>
        /// Запрашивает список пользователей для админской панели.
        /// </summary>
        public Task RequestAdminUsersAsync()
        {
            return SendPacketAsync(MessageType.AdminGetUsers, new { });
        }

        /// <summary>
        /// Отправляет запрос на создание пользователя.
        /// </summary>
        public Task CreateAdminUserAsync(AdminCreateUserRequestDto request)
        {
            return SendPacketAsync(MessageType.AdminCreateUser, request);
        }

        /// <summary>
        /// Отправляет запрос на редактирование пользователя.
        /// </summary>
        public Task UpdateAdminUserAsync(AdminUpdateUserRequestDto request)
        {
            return SendPacketAsync(MessageType.AdminUpdateUser, request);
        }

        /// <summary>
        /// Отправляет запрос на удаление пользователя.
        /// </summary>
        public Task DeleteAdminUserAsync(int userId)
        {
            return SendPacketAsync(MessageType.AdminDeleteUser, new AdminDeleteUserRequestDto { Id = userId });
        }

        /// <summary>
        /// Отправляет запрос на блокировку или разблокировку пользователя.
        /// </summary>
        public Task SetAdminUserBlockedAsync(int userId, bool isBlocked, string reason)
        {
            return SendPacketAsync(MessageType.AdminBlockUser, new AdminBlockUserRequestDto
            {
                Id = userId,
                IsBlocked = isBlocked,
                Reason = reason
            });
        }

        /// <summary>
        /// Корректно закрывает сетевое соединение и освобождает занятые ресурсы.
        /// </summary>
        public void Disconnect()
        {
            _isConnected = false;
            _stream?.Close();
            _tcpClient?.Close();
            Log.Information("Соединение закрыто локально");
        }
    }
}