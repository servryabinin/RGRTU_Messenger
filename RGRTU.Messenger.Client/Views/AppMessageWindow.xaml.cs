using System.Windows;

namespace RGRTU.Messenger.Client.Views
{
    /// <summary>
    /// Стилизованное модальное окно с сообщением и кнопкой «Ок».
    /// </summary>
    public partial class AppMessageWindow : Window
    {
        public AppMessageWindow(string title, string message)
        {
            InitializeComponent();
            Title = title;
            TitleTextBlock.Text = title;
            MessageTextBlock.Text = message;
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }
    }
}
