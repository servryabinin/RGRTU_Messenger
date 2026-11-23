using System.Windows;
using RGRTU.Messenger.Client.Configuration;
using RGRTU.Messenger.Client.Security;
using RGRTU.Messenger.Shared.Models.DTO.Admin;

namespace RGRTU.Messenger.Client.Views
{
    /// <summary>
    /// Диалог создания или редактирования пользователя в админской панели.
    /// </summary>
    public partial class UserEditWindow : Window
    {
        private const int MinPasswordLength = 4;

        private readonly AdminUserDto? _existingUser;

        /// <summary>
        /// Запрос на создание, заполняется при нажатии "Сохранить" в режиме добавления.
        /// </summary>
        public AdminCreateUserRequestDto? CreateRequest { get; private set; }

        /// <summary>
        /// Запрос на обновление, заполняется в режиме редактирования.
        /// </summary>
        public AdminUpdateUserRequestDto? UpdateRequest { get; private set; }

        public UserEditWindow(AdminUserDto? existingUser)
        {
            InitializeComponent();
            _existingUser = existingUser;

            if (existingUser is null)
            {
                Title = "Добавить пользователя";
                TitleTextBlock.Text = "Новый пользователь";
            }
            else
            {
                Title = "Редактировать пользователя";
                TitleTextBlock.Text = "Редактирование";
                LoginTextBox.Text = existingUser.Login;
                DisplayNameTextBox.Text = existingUser.DisplayName;
                RoleComboBox.SelectedIndex = existingUser.Role == AdminUserRole.Admin ? 1 : 0;

                PasswordLabelTextBlock.Text = "Новый пароль";
                ConfirmPasswordLabelTextBlock.Text = "Подтверждение";
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            string login = LoginTextBox.Text.Trim();
            string displayName = string.IsNullOrWhiteSpace(DisplayNameTextBox.Text)
                ? login
                : DisplayNameTextBox.Text.Trim();

            if (login.Length < ClientConfig.MinLoginLength)
            {
                ShowError($"Логин должен быть не короче {ClientConfig.MinLoginLength} символов");
                return;
            }

            string password = PasswordBox.Password;
            string confirm = ConfirmPasswordBox.Password;

            if (_existingUser is null)
            {
                if (password.Length < MinPasswordLength)
                {
                    ShowError($"Пароль должен быть не короче {MinPasswordLength} символов");
                    return;
                }

                if (password != confirm)
                {
                    ShowError("Пароли не совпадают");
                    return;
                }

                CreateRequest = new AdminCreateUserRequestDto
                {
                    Login = login,
                    DisplayName = displayName,
                    PasswordHash = ClientPasswordHasher.Sha256Hex(password),
                    Role = GetSelectedRole()
                };
            }
            else
            {
                string passwordHash = string.Empty;

                if (!string.IsNullOrEmpty(password) || !string.IsNullOrEmpty(confirm))
                {
                    if (password.Length < MinPasswordLength)
                    {
                        ShowError($"Пароль должен быть не короче {MinPasswordLength} символов");
                        return;
                    }

                    if (password != confirm)
                    {
                        ShowError("Пароли не совпадают");
                        return;
                    }

                    passwordHash = ClientPasswordHasher.Sha256Hex(password);
                }

                UpdateRequest = new AdminUpdateUserRequestDto
                {
                    Id = _existingUser.Id,
                    Login = login,
                    DisplayName = displayName,
                    PasswordHash = passwordHash,
                    Role = GetSelectedRole()
                };
            }

            DialogResult = true;
            Close();
        }

        private AdminUserRole GetSelectedRole()
        {
            return RoleComboBox.SelectedIndex == 1 ? AdminUserRole.Admin : AdminUserRole.User;
        }

        private void ShowError(string message)
        {
            ErrorTextBlock.Text = message;
        }
    }
}
