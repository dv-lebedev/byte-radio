using ByteRadio.Broadcast.ViewModels;
using ByteRadio.Broadcast.Views;
using System.Windows;

namespace ByteRadio.Broadcast
{
    public partial class LoginWindow : Window
    {
        public LoginWindow(LoginView loginView, LoginViewModel loginViewModel)
        {
            InitializeComponent();
            Content = loginView;
            loginViewModel.OnLoginSuccess += (_) => this.Close();
        }
    }
}
