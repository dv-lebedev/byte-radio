using ByteRadio.Broadcast.Models;
using ByteRadio.Broadcast.ViewModels;
using ByteRadio.Broadcast.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using System.Windows;

namespace ByteRadio.Broadcast;

public partial class App : Application
{
    private readonly IHost _host;

    public App()
    {
        _host = Host.CreateDefaultBuilder()
               .UseSerilog((ctx, cfg) =>
               {
                   string template = "{Timestamp:HH:mm:ss} [{Level}] ({SourceContext}) {Message}{NewLine}{Exception}";

                   cfg.MinimumLevel.Debug()
                      .WriteTo.File("logs/log.txt", rollingInterval: RollingInterval.Day, outputTemplate: template)
                      .WriteTo.Console(outputTemplate: template)
                      .WriteTo.Debug(outputTemplate: template);
               })
               .ConfigureServices((ctx, services) =>
               {
                   services.AddSingleton<AuthApiClient>(c => new AuthApiClient(new System.Net.Http.HttpClient()));
                   services.AddSingleton<AudioBroadcaster>();

                   services.AddSingleton<MainWindowViewModel>();
                   services.AddSingleton<MainView>();
                   services.AddSingleton<MainWindow>();

                   services.AddSingleton<LoginViewModel>();
                   services.AddSingleton<LoginView>();
                   services.AddSingleton<LoginWindow>();
               })
               .Build();
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        await _host.StartAsync();
        base.OnStartup(e);

        var services = _host.Services;

        // HttpClient для API
        //services.AddHttpClient<AuthApiClient>(client =>
        //{
        //    // Базовый URL можно задать тут
        //    client.BaseAddress = new System.Uri("https://localhost:5011/");
        //    client.Timeout = TimeSpan.FromSeconds(15);
        //});

        var vm = services.GetRequiredService<LoginViewModel>();
        vm.OnLoginSuccess += token =>
        {
            var mainWindow = services.GetRequiredService<MainWindow>();
            mainWindow.Show();
        };

        var loginWindow = services.GetRequiredService<LoginWindow>();
        loginWindow.Show();
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        await _host.Services
            .GetRequiredService<MainWindowViewModel>()
            .DisposeAsync();

        await _host.StopAsync();

        Log.CloseAndFlush();
        base.OnExit(e);
    }
}