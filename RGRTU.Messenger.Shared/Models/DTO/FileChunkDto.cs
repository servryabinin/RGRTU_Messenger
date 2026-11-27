using System;
using System.Collections.Generic;
using System.Text;

namespace RGRTU.Messenger.Shared.Models.DTO
{
    /// <summary>
    /// Класс DTO для передачи информации о чанке файла.
    /// </summary>
    public class FileChunkDto
    {
        /// <summary>
        /// Уникальный ID текущей передачи
        /// </summary>
        public string FileId { get; set; }

        /// <summary>
        /// Сам кусок файла
        /// </summary>
        public byte[] Data { get; set; }

        /// <summary>
        /// Смещение (чтобы собрать файл в правильном порядке)
        /// </summary>
        public long Offset { get; set; }

        /// <summary>
        /// Является ли этот кусок последним.
        /// </summary>
        public bool IsLast { get; set; }
    }
}
