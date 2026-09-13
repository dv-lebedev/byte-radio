using System.Windows;
using System.Windows.Threading;
using ByteRadio.Broadcast.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Serilog;

namespace ByteRadio.Broadcast.ViewModels;

public partial class MainWindowViewModel : ObservableObject, IAsyncDisposable
{
    private readonly AudioBroadcaster _broadcaster;
    private readonly ILogger _logger;
    private readonly DispatcherTimer _transferTimer;

    [ObservableProperty]
    private string _url = "ws://localhost:5000/LiveStreamProvider/connect"; // TODO: Make this configurable

    [ObservableProperty]
    private string _status = "Idle";

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    [ObservableProperty]
    private double _transferredMegabytes;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartCommand))]
    [NotifyCanExecuteChangedFor(nameof(StopCommand))]
    private bool _isRunning;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartCommand))]
    private bool _isStarting;

    public MainWindowViewModel(AudioBroadcaster broadcaster, ILogger logger)
    {
        _logger = logger;
        _broadcaster = broadcaster;
        _broadcaster.StatusChanged += OnStatusChanged;
        _broadcaster.ErrorOccurred += OnErrorOccurred;

        _transferTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _transferTimer.Tick += (_, _) => UpdateTransferredMegabytes();
    }

    private void UpdateTransferredMegabytes()
    {
        TransferredMegabytes = Math.Round(_broadcaster.TotalBytesSent / (1024d * 1024d), 2);
    }

    private bool CanStart() => !IsRunning && !IsStarting;

    private bool CanStop() => IsRunning;

    [RelayCommand(CanExecute = nameof(CanStart))]
    private async Task StartAsync()
    {
        ErrorMessage = string.Empty;
        IsStarting = true;
        TransferredMegabytes = 0;

        try
        {
            await _broadcaster.StartAsync(Url);
            IsRunning = true;
            _transferTimer.Start();
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error in StartAsync");
            ErrorMessage = ex.Message;
            Status = "Failed to start";
        }
        finally
        {
            IsStarting = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanStop))]
    private async Task StopAsync()
    {
        try
        {
            await _broadcaster.StopAsync();
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error in StopAsync");
            ErrorMessage = ex.Message;
        }
        finally
        {
            _transferTimer.Stop();
            UpdateTransferredMegabytes();
            IsRunning = false;
        }
    }

    private void OnStatusChanged(object? sender, string status)
    {
        Application.Current.Dispatcher.Invoke(() => Status = status);
    }

    private void OnErrorOccurred(object? sender, Exception ex)
    {
        Application.Current.Dispatcher.Invoke(() => ErrorMessage = ex.Message);
    }

    public async ValueTask DisposeAsync()
    {
        _transferTimer.Stop();
        _broadcaster.StatusChanged -= OnStatusChanged;
        _broadcaster.ErrorOccurred -= OnErrorOccurred;
        await _broadcaster.DisposeAsync();
    }
}