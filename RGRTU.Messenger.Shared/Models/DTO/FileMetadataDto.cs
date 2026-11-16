using System;
using System.Collections.Generic;
using System.Text;

namespace RGRTU.Messenger.Shared.Models.DTO
{
    /// <summary>
    /// Класс DTO для передачи метаданных файла.
    /// </summary>
    public class FileMetadataDto
    {
        /// <summary>
        /// Имя файла
        /// </summary>
        public string FileName { get; set; }

        /// <summary>
        /// Размер файла в байтах
        /// </summary>
        public long FileSize { get; set; }

        /// <summary>
        /// Расширение файла
        /// </summary>
        public string FileExtension { get; set; }

        /// <summary>
        /// ID отправителя файла
        /// </summary>
        public int SenderId { get; set; }
    }
}
