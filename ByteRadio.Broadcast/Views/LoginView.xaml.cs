using ByteRadio.Broadcast.ViewModels;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Core;
using System.Security;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ByteRadio.Broadcast.Views;

public partial class LoginView : UserControl
{
    private readonly LoginViewModel _vm;
    private readonly ILogger<LoginView> _logger;

    public LoginView(LoginViewModel vm, ILogger<LoginView> logger)
    {
        InitializeComponent();
        _vm = vm;
        _logger = logger;
        DataContext = _vm;
        PasswordBox.PasswordChanged += OnPasswordChanged;
        _vm.OnLoginSuccess += (_, __) => ClearPasswordBox();
    }

    private void OnPasswordChanged(object sender, RoutedEventArgs e)
    {
        try
        {
            var box = (PasswordBox)sender;

            var secure = new SecureString();
            foreach (char c in box.Password)
                secure.AppendChar(c);

            secure.MakeReadOnly();

            _vm.UpdatePassword(secure);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating password in ViewModel.");
        }
    }

    private void PasswordBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        try
        {
            if (e.Key == Key.Enter)
            {
                _vm.LoginCommand?.Execute(null);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing login command on Enter key press.");
        }
    }

    private void ClearPasswordBox()
    {
        try
        {
            PasswordBox.Clear();
            PasswordBox.SecurePassword?.Dispose();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to clear password box after login success.");
        }
    }
}