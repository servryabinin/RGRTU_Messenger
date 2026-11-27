using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using RGRTU.Messenger.Client.Models;
using RGRTU.Messenger.Client.Network;

namespace RGRTU.Messenger.Client.Views
{
    public partial class ChatInfoWindow : Window
    {
        private readonly MessengerClient _client;
        private readonly ObservableCollection<ChatMemberViewModel> _members;
        private readonly List<AttachmentGroupViewModel> _groups;

        public ChatInfoWindow(
            MessengerClient client,
            ObservableCollection<ChatMemberViewModel> members,
            IEnumerable<ChatMessageViewModel> messages)
        {
            InitializeComponent();

            _client = client;
            _members = members;

            bool canManage = members.Any(m => m.CanManage);
            MemberActionColumn.Visibility = canManage ? Visibility.Visible : Visibility.Collapsed;

            MembersDataGrid.ItemsSource = _members;

            var attachments = messages
                .Where(m => m.HasAttachment && !m.IsUploading)
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

        private void ChatInfoWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Таблица всегда должна открываться с самого верха, а не с середины.
            var scrollViewer = FindVisualChild<ScrollViewer>(MembersDataGrid);
            scrollViewer?.ScrollToTop();

            if (_members.Count > 0)
                MembersDataGrid.ScrollIntoView(_members[0]);
        }

        private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T typed)
                    return typed;

                var nested = FindVisualChild<T>(child);
                if (nested != null)
                    return nested;
            }

            return null;
        }

        private async void ChatMemberActionButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn || btn.Tag is not int userId) return;

            var member = _members.FirstOrDefault(m => m.Id == userId);
            if (member == null) return;

            if (member.IsInChat)
                await _client.RemoveChatMemberAsync(userId);
            else if (!member.IsChatInvitedByAdmin)
                await _client.AddChatMemberAsync(userId);
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
        public string InfoText => $"{SenderName} • {Timestamp:HH:mm} • {SizeText}";

        private static string FormatSize(long bytes)
        {
            if (bytes < 1024) return $"{bytes} Б";
            if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} КБ";
            return $"{bytes / (1024.0 * 1024.0):F1} МБ";
        }
    }
}