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
        /// Клиент -> Сервер: смена собственного статуса (Online/Away/Busy)
        /// </summary>
        SetUserStatus = 31,
        /// <summary>
        /// Админ -> Сервер: Отключить юзера
        /// </summary>
        KickUser = 40,

        /// <summary>
        /// Админ -> Сервер: запрос списка пользователей
        /// </summary>
        AdminGetUsers = 41,
        /// <summary>
        /// Сервер -> Админ: список пользователей
        /// </summary>
        AdminUsersResponse = 42,
        /// <summary>
        /// Админ -> Сервер: создать пользователя
        /// </summary>
        AdminCreateUser = 43,
        /// <summary>
        /// Админ -> Сервер: обновить пользователя
        /// </summary>
        AdminUpdateUser = 44,
        /// <summary>
        /// Админ -> Сервер: удалить пользователя
        /// </summary>
        AdminDeleteUser = 45,
        /// <summary>
        /// Админ -> Сервер: заблокировать/разблокировать пользователя
        /// </summary>
        AdminBlockUser = 46,
        /// <summary>
        /// Сервер -> Админ: результат операции
        /// </summary>
        AdminActionResponse = 47,
        /// <summary>
        /// Админ -> Сервер: запрос списка файлов журнала сервера
        /// </summary>
        AdminGetLogFiles = 48,
        /// <summary>
        /// Сервер -> Админ: список файлов журнала сервера
        /// </summary>
        AdminLogFilesResponse = 49,
        /// <summary>
        /// Админ -> Сервер: запрос содержимого файла журнала сервера
        /// </summary>
        AdminGetLogContent = 50,
        /// <summary>
        /// Сервер -> Админ: содержимое файла журнала сервера
        /// </summary>
        AdminLogContentResponse = 51,

        /// <summary>
        /// Админ -> Сервер: добавить пользователя в чат
        /// </summary>
        AdminAddChatMember = 52,
        /// <summary>
        /// Админ -> Сервер: исключить пользователя из чата
        /// </summary>
        AdminRemoveChatMember = 53,
        /// <summary>
        /// Сервер -> Админ/Клиент: актуальный список участников чата
        /// </summary>
        ChatMembersResponse = 54,

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
