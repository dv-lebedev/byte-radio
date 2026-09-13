using ByteRadio.Broadcast.WpfClient.ViewModels;
using ByteRadio.Broadcast.WpfClient.Views;
using System.Windows;

namespace ByteRadio.Broadcast.WpfClient
{
    public partial class LoginWindow : Window
    {
        public LoginWindow(LoginView loginView, LoginViewModel loginViewModel)
        {
            InitializeComponent();
            Content = loginView;
            loginViewModel.OnLoginSuccess += (_, __) => this.Close();
        }
    }
}