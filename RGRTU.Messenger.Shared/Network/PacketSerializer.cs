using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using RGRTU.Messenger.Shared.Network;
using RGRTU.Messenger.Shared.Constants;

namespace RGRTU.Messenger.Shared.Network
{
    /// <summary>
    /// Класс для сериализации и десериализации пакетов сообщений.
    /// </summary>
    public static class PacketSerializer
    {
        /// <summary>
        /// Настройки JSON (camelCase, как принято в веб/сетевых API)
        /// </summary>
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };

        /// <summary>
        /// Упаковывает объект в байты для отправки по TCP
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="type">Тип сообщения</param>
        /// <param name="payload">Тело сообщения</param>
        /// <returns></returns>
        public static byte[] Serialize<T>(MessageType type, T payload)
        {
            string json = JsonSerializer.Serialize(payload, JsonOptions);   // 1. Превращаем объект в JSON-строку, а затем в байты
            byte[] payloadBytes = Encoding.UTF8.GetBytes(json);

            using var ms = new MemoryStream();                              // 2. Создаем буфер для всего пакета (4 байта тип + 4 байта длина + сам payload)
            using var writer = new BinaryWriter(ms);

            writer.Write((int)type);           // 4 байта: Тип сообщения    // 3. Пишем заголовок
            writer.Write(payloadBytes.Length); // 4 байта: Длина тела

            writer.Write(payloadBytes);                                     // 4. Пишем тело

            return ms.ToArray();
        }

        /// <summary>
        /// Читает один пакет из сетевого потока TCP. Блокируется, пока пакет не будет прочитан целиком.
        /// </summary>
        public static (MessageType Type, string PayloadJson) Deserialize(NetworkStream stream)
        {
            byte[] header = ReadExactly(stream, 8);
            int typeInt = BitConverter.ToInt32(header, 0);
            int length = BitConverter.ToInt32(header, 4);

            if (length < AppConstants.MIN_PACKET_SIZE || length > AppConstants.MAX_PACKET_SIZE)
                throw new InvalidDataException(AppConstants.ERROR_INVALID_PACKET_SIZE);

            byte[] payloadBytes = length == 0 ? Array.Empty<byte>() : ReadExactly(stream, length);
            string json = Encoding.UTF8.GetString(payloadBytes);

            return ((MessageType)typeInt, json);
        }

        private static byte[] ReadExactly(NetworkStream stream, int count)
        {
            byte[] buffer = new byte[count];
            int offset = 0;
            while (offset < count)
            {
                int read = stream.Read(buffer, offset, count - offset);
                if (read <= 0)
                    throw new EndOfStreamException("Соединение закрыто во время чтения пакета.");
                offset += read;
            }

            return buffer;
        }

        /// <summary>
        /// Вспомогательный метод для быстрого парсинга JSON в нужный DTO
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="json">Сообщение в формате JSON</param>
        /// <returns></returns>
        public static T DeserializePayload<T>(string json)
        {
            return JsonSerializer.Deserialize<T>(json, JsonOptions);
        }
    }
}