using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace RGRTU.Messenger.Client.Services
{
    /// <summary>
    /// Работа с вложениями-медиа: распознавание типа, декодирование изображений,
    /// временные файлы для проигрывания видео и открытие во внешнем приложении.
    /// </summary>
    public static class MediaHelper
    {
        public static bool IsImage(string? fileName)
        {
            string ext = Path.GetExtension(fileName ?? string.Empty).ToLowerInvariant();
            return ext is ".png" or ".jpg" or ".jpeg" or ".gif" or ".bmp" or ".webp" or ".tif" or ".tiff";
        }

        public static bool IsVideo(string? fileName)
        {
            string ext = Path.GetExtension(fileName ?? string.Empty).ToLowerInvariant();
            return ext is ".mp4" or ".avi" or ".mkv" or ".mov" or ".wmv" or ".webm" or ".m4v" or ".mpg" or ".mpeg";
        }

        public static ImageSource? TryLoadImage(string? base64)
        {
            if (string.IsNullOrWhiteSpace(base64))
                return null;

            try
            {
                byte[] bytes = Convert.FromBase64String(base64);
                using var stream = new MemoryStream(bytes);
                var image = new BitmapImage();
                image.BeginInit();
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.StreamSource = stream;
                image.EndInit();
                image.Freeze();
                return image;
            }
            catch
            {
                return null;
            }
        }

        public static string? TryGetLocalPath(string? base64, string fileName, string messageId)
        {
            if (string.IsNullOrWhiteSpace(base64))
                return null;

            try
            {
                string directory = Path.Combine(Path.GetTempPath(), "RGRTU_Messenger", "media");
                Directory.CreateDirectory(directory);

                string safeName = Path.GetFileName(fileName);
                foreach (char invalid in Path.GetInvalidFileNameChars())
                    safeName = safeName.Replace(invalid, '_');
                if (string.IsNullOrWhiteSpace(safeName))
                    safeName = "file";

                string prefix = string.IsNullOrWhiteSpace(messageId)
                    ? Guid.NewGuid().ToString("N")
                    : messageId;
                string path = Path.Combine(directory, $"{prefix}_{safeName}");

                if (!File.Exists(path))
                    File.WriteAllBytes(path, Convert.FromBase64String(base64));

                return path;
            }
            catch
            {
                return null;
            }
        }

        public static void OpenExternally(string? path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                return;

            try
            {
                Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            }
            catch (Win32Exception)
            {
                // Для файлов без сопоставленного приложения показываем стандартный
                // диалог Windows «Открыть с помощью».
                try
                {
                    Process.Start("rundll32.exe", $"shell32.dll,OpenAs_RunDLL \"{path}\"");
                }
                catch
                {
                    // Игнорируем: пользователь может открыть файл вручную.
                }
            }
            catch
            {
                // Игнорируем ошибки запуска внешнего приложения.
            }
        }
    }
}