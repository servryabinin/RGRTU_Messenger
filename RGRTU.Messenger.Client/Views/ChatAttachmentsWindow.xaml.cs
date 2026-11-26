using System;
using System.Collections.Generic;
using System.IO;
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
            AttachmentsListBox.ItemsSource = items;
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
}