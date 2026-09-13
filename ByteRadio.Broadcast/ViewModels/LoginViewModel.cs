using ByteRadio.Broadcast.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using System.Net.Http;
using System.Security;

namespace ByteRadio.Broadcast.ViewModels;

public partial class LoginViewModel : ObservableObject
{
    private readonly ILogger<LoginViewModel> _logger;
    private readonly AuthApiClient _api;
    private readonly ISessionData _sessionData;
    private SecureString? _securePassword;
    private CancellationTokenSource? _cts;

    [ObservableProperty]
    private string _username = string.Empty;

    [ObservableProperty]
    private string _statusText = "Enter login and password";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LoginCommand))]
    private bool _isBusy;

    [ObservableProperty]
    private bool _hasError = false;

    public event EventHandler OnLoginSuccess;

    public LoginViewModel(AuthApiClient api, ISessionData sessionData, ILogger<LoginViewModel> logger)
    {
        _logger = logger;
        _api = api ?? throw new ArgumentNullException(nameof(api));
        _sessionData = sessionData ?? throw new ArgumentNullException(nameof(sessionData));
    }

    public void UpdatePassword(SecureString password) => _securePassword = password;

    private string GetPlainPassword()
    {
        if (_securePassword == null || _securePassword.Length == 0)
            return string.Empty;

        var ptr = System.Runtime.InteropServices.Marshal.SecureStringToBSTR(_securePassword);
        try
        {
            return System.Runtime.InteropServices.Marshal.PtrToStringBSTR(ptr) ?? string.Empty;
        }
        finally
        {
            System.Runtime.InteropServices.Marshal.ZeroFreeBSTR(ptr);
        }
    }

    [RelayCommand(CanExecute = nameof(CanLogin))]
    private async Task LoginAsync()
    {
        _logger.LogDebug("LoginAsync");

        HasError = false;
        IsBusy = true;
        StatusText = "Connecting...";

        _cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));

        try
        {
            var plainPassword = GetPlainPassword();
            if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrEmpty(plainPassword))
            {
                StatusText = "Login and/or password can not be empty.";
                HasError = true;
                return;
            }

            var result = await _api.LoginAsync(Username, plainPassword, _cts.Token);

            if (!string.IsNullOrEmpty(result.Error))
            {
                StatusText = $"Error: {result.Error}";
                HasError = true;
                return;
            }

            if (!string.IsNullOrEmpty(result.Token))
            {
                StatusText = "Login completed successfully.";
                _logger.LogDebug("Login completed successfully.");
                
                _sessionData.Token = result.Token;
                OnLoginSuccess?.Invoke(this, EventArgs.Empty);
            }
            else
            {
                StatusText = "The server returned an empty token.";
                HasError = true;
            }
        }
        catch (Exception ex)
        {
            if (ex is TaskCanceledException)
            {
                StatusText = "Connection lost. Please check your network.";
            }
            else if (ex is OperationCanceledException)
            {
                StatusText = "Timeout: the server didn’t respond within 15 seconds. Please check your connection.";
            }
            else if (ex is HttpRequestException)
            {
                StatusText = "No connection to the server.";
            }
            else
            {
                StatusText = "Unexpected error.";
                _logger.LogError(ex, "LoginAsync error");
            }
            HasError = true;
        }
        finally
        {
            IsBusy = false;
            _cts?.Dispose();
            _cts = null;
        }
    }

    private bool CanLogin() => !IsBusy;
}