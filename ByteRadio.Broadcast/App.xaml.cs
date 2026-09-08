using ByteRadio.Broadcast.ViewModels;
using Microsoft.Extensions.Logging;
using Serilog;
using System.Windows;

namespace ByteRadio.Broadcast;

public partial class App : Application
{
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
        var viewModel = new MainWindowViewModel(broadcaster);
        var mainWindow = new MainWindow(viewModel);
        mainWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Log.CloseAndFlush();
        base.OnExit(e);
    }
}