using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using RGRTU.Messenger.Client.Models;

namespace RGRTU.Messenger.Client.Views
{
    public partial class ChatInfoWindow : Window
    {
        private readonly List<AttachmentGroupViewModel> _groups;

        public ChatInfoWindow(IEnumerable<ChatMemberViewModel> members, IEnumerable<ChatMessageViewModel> messages)
        {
            InitializeComponent();

            var memberList = members
                .Where(m => m.IsInChat)
                .OrderBy(m => m.DisplayName)
                .Select(m => new ChatInfoMemberViewModel
                {
                    DisplayName = m.DisplayName,
                    OnlineStatusText = m.OnlineStatusText
                })
                .ToList();
            MembersDataGrid.ItemsSource = memberList;

            var attachments = messages
                .Where(m => m.HasAttachment)
                .Select(m => new AttachmentItemViewModel
                {
                    FileName = m.AttachmentFileName ?? "file",
                    SizeBytes = m.AttachmentFileSize,
                    Base64 = m.AttachmentBase64 ?? string.Empty,
                    SenderName = m.SenderName,
                    Timestamp = m.Timestamp
                })
                .ToList();

            _groups = new List<AttachmentGroupViewModel>
            {
                BuildGroup("Изображения", attachments, IsImage),
                BuildGroup("Видео", attachments, IsVideo),
                BuildGroup("Файлы", attachments, f => !IsImage(f) && !IsVideo(f))
            };

            AttachmentGroupsListBox.ItemsSource = _groups;
        }

        private static AttachmentGroupViewModel BuildGroup(string title, List<AttachmentItemViewModel> all, Func<string, bool> predicate)
        {
            var items = all.Where(a => predicate(a.FileName)).OrderByDescending(a => a.Timestamp).ToList();
            return new AttachmentGroupViewModel { Title = title, Items = items };
        }

        private static bool IsImage(string fileName)
        {
            string ext = Path.GetExtension(fileName).ToLowerInvariant();
            return ext is ".png" or ".jpg" or ".jpeg" or ".gif" or ".bmp" or ".webp";
        }

        private static bool IsVideo(string fileName)
        {
            string ext = Path.GetExtension(fileName).ToLowerInvariant();
            return ext is ".mp4" or ".avi" or ".mkv" or ".mov" or ".wmv" or ".webm";
        }

        private void AttachmentGroup_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn || btn.Tag is not AttachmentGroupViewModel group) return;
            var window = new ChatAttachmentsWindow(group.Title, group.Items) { Owner = this };
            window.ShowDialog();
        }
    }

    public class ChatInfoMemberViewModel
    {
        public string DisplayName { get; set; } = string.Empty;
        public string OnlineStatusText { get; set; } = string.Empty;
    }

    public class AttachmentGroupViewModel
    {
        public string Title { get; set; } = string.Empty;
        public List<AttachmentItemViewModel> Items { get; set; } = new();
        public string CountText => Items.Count.ToString();
    }

    public class AttachmentItemViewModel
    {
        public string FileName { get; set; } = string.Empty;
        public long SizeBytes { get; set; }
        public string Base64 { get; set; } = string.Empty;
        public string SenderName { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }

        public string SizeText => FormatSize(SizeBytes);
        public string InfoText => $"{SenderName} • {Timestamp:dd.MM.yyyy HH:mm} • {SizeText}";

        private static string FormatSize(long bytes)
        {
            if (bytes < 1024) return $"{bytes} Б";
            if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} КБ";
            return $"{bytes / (1024.0 * 1024.0):F1} МБ";
        }
    }
}