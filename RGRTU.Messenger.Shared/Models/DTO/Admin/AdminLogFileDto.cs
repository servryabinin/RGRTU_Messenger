using System;

namespace RGRTU.Messenger.Shared.Models.DTO.Admin
{
    /// <summary>
    /// Метаданные файла журнала, расположенного на сервере.
    /// </summary>
    public class AdminLogFileDto
    {
        public string Name { get; set; } = string.Empty;
        public DateTime Date { get; set; }
        public long SizeBytes { get; set; }
    }
}
