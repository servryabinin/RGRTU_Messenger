using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace RGRTU.Messenger.Client.Views
{
    public partial class ChatAttachmentsWindow : Window
    {
        public ChatAttachmentsWindow(string title, IEnumerable<AttachmentItemViewModel> items)
        {
            InitializeComponent();
            Title = title;
            TitleTextBlock.Text = title;
            AttachmentsListBox.ItemsSource = BuildRows(items);
        }

        private static List<AttachmentRowViewModel> BuildRows(IEnumerable<AttachmentItemViewModel> items)
        {
            var rows = new List<AttachmentRowViewModel>();

            foreach (var group in items
                         .OrderByDescending(i => i.Timestamp)
                         .GroupBy(i => i.Timestamp.Date))
            {
                rows.Add(new AttachmentRowViewModel
                {
                    IsHeader = true,
                    DateText = FormatDate(group.Key)
                });

                foreach (var item in group.OrderByDescending(i => i.Timestamp))
                {
                    rows.Add(new AttachmentRowViewModel
                    {
                        IsHeader = false,
                        Item = item
                    });
                }
            }

            return rows;
        }

        private static string FormatDate(DateTime date)
        {
            var today = DateTime.Today;
            if (date == today) return "Сегодня";
            if (date == today.AddDays(-1)) return "Вчера";
            return date.ToString("d MMMM yyyy", CultureInfo.GetCultureInfo("ru-RU"));
        }

        private void SaveAttachment_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn || btn.Tag is not AttachmentItemViewModel item) return;

            var dialog = new SaveFileDialog
            {
                FileName = item.FileName,
                Title = "Сохранить файл"
            };

            if (dialog.ShowDialog(this) == true)
            {
                try
                {
                    byte[] bytes = Convert.FromBase64String(item.Base64);
                    File.WriteAllBytes(dialog.FileName, bytes);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Не удалось сохранить файл: {ex.Message}", "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }

    public class AttachmentRowViewModel
    {
        public bool IsHeader { get; init; }
        public string DateText { get; init; } = string.Empty;
        public AttachmentItemViewModel? Item { get; init; }

        public Visibility HeaderVisibility => IsHeader ? Visibility.Visible : Visibility.Collapsed;
        public Visibility ItemVisibility => IsHeader ? Visibility.Collapsed : Visibility.Visible;
    }
}