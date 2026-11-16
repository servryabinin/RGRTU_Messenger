using System;
using System.Collections.Generic;
using System.Text;

namespace RGRTU.Messenger.Shared.Network
{
    /// <summary>
    /// Перечисление типов сообщений, которые могут быть отправлены и получены в сети.
    /// </summary>
    public enum MessageType : int
    {
        #region Авторизация

        /// <summary>
        /// Клиент -> Сервер: Логин/Пароль
        /// </summary>
        AuthRequest = 1,
        /// <summary>
        /// Сервер -> Клиент: Успех/Ошибка + данные юзера
        /// </summary>
        AuthResponse = 2,

        #endregion

        #region Чат

        /// <summary>
        /// Клиент -> Сервер: Текст сообщения
        /// </summary>
        SendTextMessage = 10,
        /// <summary>
        /// Сервер -> Всем клиентам: Новое сообщение в чате
        /// </summary>
        BroadcastMessage = 11,

        #endregion

        #region Файлы (Требование 1.e)
        /// <summary>
        ///  Запрос на отправку файла (имя, размер)
        /// </summary>
        FileMetadata = 20,
        /// <summary>
        /// Кусок файла (байты)
        /// </summary>
        FileChunk = 21,

        #endregion

        #region Статусы и Админка (Требования 2.c, 2.d)
        /// <summary>
        /// Сервер -> Клиентам: Кто-то зашел/вышел/сменил статус
        /// </summary>
        UserStatusUpdate = 30,
        /// <summary>
        /// Админ -> Сервер: Отключить юзера
        /// </summary>
        KickUser = 40,

        #endregion

        #region Системные сообщения (Требование 2.a)
        /// <summary>
        /// 
        /// </summary>
        Ping = 98,
        /// <summary>
        /// 
        /// </summary>
        Pong = 99,
        /// <summary>
        /// 
        /// </summary>
        Error = 100
        #endregion
    }
}
