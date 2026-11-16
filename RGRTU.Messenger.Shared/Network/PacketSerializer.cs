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
        /// Читает один пакет из сетевого потока TCP. ВАЖНО: Этот метод блокирующий, он ждет, пока придут все байты!
        /// </summary>
        /// <param name="stream">Сетевой поток</param>
        /// <returns></returns>
        /// <exception cref="InvalidDataException"></exception>
        public static (MessageType Type, string PayloadJson) Deserialize(NetworkStream stream)
        {
            using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);

            int typeInt = reader.ReadInt32();                             // 1. Читаем заголовок (8 байт)
            int length = reader.ReadInt32();

            if (length < AppConstants.MIN_PACKET_SIZE || length > AppConstants.MAX_PACKET_SIZE)
                throw new InvalidDataException(AppConstants.ERROR_INVALID_PACKET_SIZE);

            byte[] payloadBytes = reader.ReadBytes(length);               // 2. Читаем тело (JSON)
            string json = Encoding.UTF8.GetString(payloadBytes);

            return ((MessageType)typeInt, json);
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