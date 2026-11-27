using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;
using RGRTU.Messenger.Client.Services;
using RGRTU.Messenger.Shared.Models.DTO;

namespace RGRTU.Messenger.Client.Models
{
    public class ChatMessageViewModel : INotifyPropertyChanged
    {
        private string _text = string.Empty;
        private bool _isEdited;
        private bool _isSelected;
        private bool _isSelectionMode;
        private double _highlightOpacity;
        private bool _isMuted = true;
        private bool _isUploading;
        private double _uploadProgress;

        private ImageSource? _imageSource;
        private bool _imageSourceResolved;
        private string? _localPath;
        private bool _localPathResolved;

        public event PropertyChangedEventHandler? PropertyChanged;

        public string MessageId { get; init; } = string.Empty;
        public int SenderId { get; init; }
        public string SenderName { get; init; } = string.Empty;
        public DateTime Timestamp { get; init; }
        public bool IsOwn { get; init; }

        public string? AttachmentFileName { get; init; }
        public long AttachmentFileSize { get; init; }
        public string? AttachmentBase64 { get; init; }
        public bool IsSticker { get; init; }

        public string? ReplyToMessageId { get; init; }
        public string? ReplyToSenderName { get; init; }
        public string? ReplyToText { get; init; }

        public string Text
        {
            get => _text;
            set { if (_text == value) return; _text = value; OnPropertyChanged(); OnPropertyChanged(nameof(TextVisibility)); }
        }

        public bool IsEdited
        {
            get => _isEdited;
            set { if (_isEdited == value) return; _isEdited = value; OnPropertyChanged(); OnPropertyChanged(nameof(EditedVisibility)); }
        }

        public bool IsSelected
        {
            get => _isSelected;
            set { if (_isSelected == value) return; _isSelected = value; OnPropertyChanged(); OnPropertyChanged(nameof(SelectionMark)); }
        }

        public bool IsSelectionMode
        {
            get => _isSelectionMode;
            set { if (_isSelectionMode == value) return; _isSelectionMode = value; OnPropertyChanged(); OnPropertyChanged(nameof(SelectionVisibility)); }
        }

        public double HighlightOpacity
        {
            get => _highlightOpacity;
            set { if (Math.Abs(_highlightOpacity - value) < 0.001) return; _highlightOpacity = value; OnPropertyChanged(); }
        }

        public bool IsMuted
        {
            get => _isMuted;
            set
            {
                if (_isMuted == value) return;
                _isMuted = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(MuteIcon));
            }
        }

        public string MuteIcon => IsMuted ? "🔇" : "🔊";

        /// <summary>
        /// Файл ещё передаётся на сервер (показывается только отправителю).
        /// </summary>
        public bool IsUploading
        {
            get => _isUploading;
            set
            {
                if (_isUploading == value) return;
                _isUploading = value;

                if (!value)
                {
                    // После завершения передачи нужно заново прочитать локальные
                    // превью (во время загрузки они намеренно не создавались).
                    _imageSourceResolved = false;
                    _imageSource = null;
                    _localPathResolved = false;
                    _localPath = null;
                    OnPropertyChanged(nameof(ImageSource));
                    OnPropertyChanged(nameof(VideoSourcePath));
                }

                OnPropertyChanged();
                OnPropertyChanged(nameof(UploadingVisibility));
                OnPropertyChanged(nameof(AttachmentVisibility));
                OnPropertyChanged(nameof(ImageVisibility));
                OnPropertyChanged(nameof(VideoVisibility));
                OnPropertyChanged(nameof(FileVisibility));
            }
        }

        public double UploadProgress
        {
            get => _uploadProgress;
            set
            {
                double clamped = Math.Clamp(value, 0, 100);
                if (Math.Abs(_uploadProgress - clamped) < 0.01) return;
                _uploadProgress = clamped;
                OnPropertyChanged();
                OnPropertyChanged(nameof(UploadProgressText));
            }
        }

        public Visibility UploadingVisibility => IsUploading ? Visibility.Visible : Visibility.Collapsed;
        public string UploadProgressText => $"Загрузка {UploadProgress:0}%";

        public string TimeText => Timestamp.ToString("HH:mm");
        public HorizontalAlignment Alignment => IsOwn ? HorizontalAlignment.Right : HorizontalAlignment.Left;
        public Visibility SenderVisibility => IsOwn ? Visibility.Collapsed : Visibility.Visible;
        public Visibility TextVisibility => string.IsNullOrWhiteSpace(Text) ? Visibility.Collapsed : Visibility.Visible;
        public Visibility AttachmentVisibility => HasAttachment && !IsUploading ? Visibility.Visible : Visibility.Collapsed;
        public Visibility ReplyVisibility => string.IsNullOrWhiteSpace(ReplyToMessageId) ? Visibility.Collapsed : Visibility.Visible;
        public Visibility EditedVisibility => IsEdited ? Visibility.Visible : Visibility.Collapsed;
        public Visibility SelectionVisibility => IsSelectionMode && IsOwn ? Visibility.Visible : Visibility.Collapsed;
        public string SelectionMark => IsSelected ? "✔" : "";

        public bool HasAttachment => !string.IsNullOrWhiteSpace(AttachmentFileName) && !string.IsNullOrWhiteSpace(AttachmentBase64);
        public bool IsImageAttachment => HasAttachment && MediaHelper.IsImage(AttachmentFileName);
        public bool IsVideoAttachment => HasAttachment && MediaHelper.IsVideo(AttachmentFileName);
        public bool IsFileAttachment => HasAttachment && !IsImageAttachment && !IsVideoAttachment;

        public Visibility ImageVisibility => !IsUploading && IsImageAttachment ? Visibility.Visible : Visibility.Collapsed;
        public Visibility VideoVisibility => !IsUploading && IsVideoAttachment ? Visibility.Visible : Visibility.Collapsed;
        public Visibility FileVisibility => !IsUploading && IsFileAttachment ? Visibility.Visible : Visibility.Collapsed;

        /// <summary>
        /// Локальный временный файл вложения — нужен для MediaElement и внешнего проигрывателя.
        /// </summary>
        public string? LocalPath
        {
            get
            {
                if (IsUploading)
                    return null;

                if (!_localPathResolved)
                {
                    _localPathResolved = true;
                    if (IsImageAttachment || IsVideoAttachment)
                    {
                        _localPath = MediaHelper.TryGetLocalPath(
                            AttachmentBase64, AttachmentFileName ?? "file", MessageId);
                    }
                }

                return _localPath;
            }
        }

        /// <summary>
        /// Путь к видеофайлу. Для не-видео возвращает null, чтобы MediaElement
        /// не пытался открывать изображения и обычные файлы.
        /// </summary>
        public string? VideoSourcePath => IsVideoAttachment ? LocalPath : null;

        public ImageSource? ImageSource
        {
            get
            {
                if (IsUploading)
                    return null;

                if (!_imageSourceResolved)
                {
                    _imageSourceResolved = true;
                    if (IsImageAttachment)
                        _imageSource = MediaHelper.TryLoadImage(AttachmentBase64);
                }

                return _imageSource;
            }
        }

        public string AttachmentSizeText => HasAttachment ? FormatSize(AttachmentFileSize) : string.Empty;
        public string AttachmentDisplay => HasAttachment ? $"📎 {AttachmentFileName}" : string.Empty;
        public string ReplyPreview => string.IsNullOrWhiteSpace(ReplyToText) ? "" : (ReplyToText.Length > 60 ? ReplyToText[..60] + "…" : ReplyToText);

        /// <summary>
        /// Обновляет прогресс передачи файла (0–100).
        /// </summary>
        public void SetUploadProgress(double percent) => UploadProgress = percent;

        /// <summary>
        /// Завершает передачу файла и показывает обычное превью вложения.
        /// </summary>
        public void CompleteUpload()
        {
            UploadProgress = 100;
            IsUploading = false;
        }

        public Brush BubbleBrush { get; init; } = Brushes.White;
        public Brush BorderBrush { get; init; } = Brushes.LightGray;
        public Brush SenderBrush { get; init; } = Brushes.SteelBlue;

        public static ChatMessageViewModel Create(ChatMessageDto dto, int currentUserId)
        {
            bool isOwn = dto.SenderId == currentUserId;
            var (bubble, border, sender) = GetPalette(dto.SenderId);
            return new ChatMessageViewModel
            {
                MessageId = dto.MessageId,
                SenderId = dto.SenderId,
                SenderName = dto.SenderName,
                _text = dto.Text ?? string.Empty,
                Timestamp = dto.Timestamp,
                IsOwn = isOwn,
                AttachmentFileName = dto.AttachmentFileName,
                AttachmentFileSize = dto.AttachmentFileSize,
                AttachmentBase64 = dto.AttachmentBase64,
                IsSticker = dto.IsSticker,
                _isEdited = dto.IsEdited,
                ReplyToMessageId = dto.ReplyToMessageId,
                ReplyToSenderName = dto.ReplyToSenderName,
                ReplyToText = dto.ReplyToText,
                BubbleBrush = isOwn ? new LinearGradientBrush(Color.FromRgb(219, 234, 254), Color.FromRgb(191, 219, 254), 45) : bubble,
                BorderBrush = isOwn ? new SolidColorBrush(Color.FromRgb(147, 197, 253)) : border,
                SenderBrush = sender
            };
        }

        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        private static string FormatSize(long bytes)
        {
            if (bytes < 1024) return $"{bytes} Б";
            if (bytes < 1024 * 1024) return $"{bytes / 1024d:0.#} КБ";
            return $"{bytes / (1024d * 1024d):0.#} МБ";
        }

        private static (Brush bubble, Brush border, Brush sender) GetPalette(int userId)
        {
            // Deterministic varied palette per user.
            var palettes = new[]
            {
                (Color.FromRgb(236, 253, 245), Color.FromRgb(167, 243, 208), Color.FromRgb(5, 150, 105)),
                (Color.FromRgb(255, 247, 237), Color.FromRgb(253, 186, 116), Color.FromRgb(234, 88, 12)),
                (Color.FromRgb(245, 243, 255), Color.FromRgb(196, 181, 253), Color.FromRgb(124, 58, 237)),
                (Color.FromRgb(254, 242, 242), Color.FromRgb(252, 165, 165), Color.FromRgb(220, 38, 38)),
                (Color.FromRgb(240, 249, 255), Color.FromRgb(125, 211, 252), Color.FromRgb(2, 132, 199)),
                (Color.FromRgb(253, 244, 255), Color.FromRgb(240, 171, 252), Color.FromRgb(192, 38, 211)),
                (Color.FromRgb(254, 252, 232), Color.FromRgb(253, 224, 71), Color.FromRgb(161, 98, 7)),
                (Color.FromRgb(240, 253, 250), Color.FromRgb(94, 234, 212), Color.FromRgb(13, 148, 136)),
            };

            int idx = Math.Abs(userId * 31) % palettes.Length;
            var p = palettes[idx];
            // Add slight gradient for non-monotone look.
            var alt = palettes[(idx + 3) % palettes.Length].Item1;
            var bubble = new LinearGradientBrush(p.Item1, Color.FromRgb(
                (byte)((p.Item1.R + alt.R) / 2),
                (byte)((p.Item1.G + alt.G) / 2),
                (byte)((p.Item1.B + alt.B) / 2)), 135);
            return (bubble, new SolidColorBrush(p.Item2), new SolidColorBrush(p.Item3));
        }
    }
}