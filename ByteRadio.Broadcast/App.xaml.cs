using ByteRadio.Broadcast.ViewModels;
using ByteRadio.Broadcast.Views;
using Microsoft.Extensions.Logging;
using Serilog;
using System.Windows;

namespace ByteRadio.Broadcast;

public partial class App : Application
{
    private MainWindowViewModel? _viewModel;

    public static ILoggerFactory LoggerFactory { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        Log.Logger = new LoggerConfiguration()
            .Enrich.FromLogContext()
            .MinimumLevel.Debug()
            .WriteTo.Console()
            .CreateLogger();

        LoggerFactory = new LoggerFactory().AddSerilog(Log.Logger);

        base.OnStartup(e);

        var broadcaster = new AudioBroadcaster(LoggerFactory);
        _viewModel = new MainWindowViewModel(broadcaster);
        var view = new MainView(_viewModel);
        var mainWindow = new MainWindow();
        mainWindow.Content = view;
        mainWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _ = _viewModel?.DisposeAsync();
        Log.CloseAndFlush();
        base.OnExit(e);
    }
}