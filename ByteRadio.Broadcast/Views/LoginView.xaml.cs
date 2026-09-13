using ByteRadio.Broadcast.ViewModels;
using System.Security;
using System.Windows;
using System.Windows.Controls;

namespace ByteRadio.Broadcast.Views;

public partial class LoginView : UserControl
{
    private readonly LoginViewModel _vm;

    public LoginView(LoginViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = _vm;
        PasswordBox.PasswordChanged += OnPasswordChanged;
        _vm.OnLoginSuccess += (_, __) =>
        {
            PasswordBox.Clear();
            PasswordBox.SecurePassword.Dispose();
        };
    }

    private void OnPasswordChanged(object sender, RoutedEventArgs e)
    {
        var box = (PasswordBox)sender;

        var secure = new SecureString();
        foreach (char c in box.Password)
            secure.AppendChar(c);

        secure.MakeReadOnly();

        _vm.UpdatePassword(secure);
        box.SecurePassword.Copy();
    }
}