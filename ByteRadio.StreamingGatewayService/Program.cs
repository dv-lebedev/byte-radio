using ByteRadio.Messaging;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.OpenApi;
using Serilog;
using System.Diagnostics;

namespace ByteRadio.StreamingGatewayService;

public class Program
{
    public static void Main(string[] args)
    {
        ConfigureLogging();
        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

        try
        {
            Log.Information("Starting up");
            BuildAndRunWebApplication(args);
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Application start-up failed");
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }

    private static void BuildAndRunWebApplication(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Host.UseSerilog();

        builder.WebHost.ConfigureKestrel(options =>
        {
            options.ConfigureEndpointDefaults(listenOptions =>
            {
                listenOptions.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http1;
            });
        });

        builder.Services.AddControllers();
        builder.Services.AddProblemDetails();

        builder.Services.AddSingleton<WebSocketConnectionManager>();
        builder.Services.AddSingleton<IMessageBroadcaster>(sp => sp.GetRequiredService<WebSocketConnectionManager>());

        builder.Services.AddSingleton<LocalTestRabbitMqOptionsProvider>();
        builder.Services.AddSingleton<IRabbitMqOptionsProvider>(sp => sp.GetRequiredService<LocalTestRabbitMqOptionsProvider>());

        builder.Services.AddHostedService<RabbitMqConsumerService>();

        builder.Services.AddCors(options =>
        {
            options.AddDefaultPolicy(policy =>
            {
                policy.AllowAnyOrigin()
                      .AllowAnyMethod()
                      .AllowAnyHeader();
            });
        });

        builder.Services.AddHealthChecks()
            .AddCheck("self", () => HealthCheckResult.Healthy(), tags: new[] { "live" });

        builder.Services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new OpenApiInfo { Title = "StreamingGatewayService", Version = "v1" });
        });

        var app = builder.Build();

        app.UseExceptionHandler(exceptionHandlerApp =>
        {
            exceptionHandlerApp.Run(async context =>
            {
                var exceptionFeature = context.Features.Get<IExceptionHandlerFeature>();
                var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();

                if (exceptionFeature?.Error is not null)
                {
                    logger.LogError(exceptionFeature.Error, "Unhandled request exception");
                }

                context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                await Results.Problem(
                    title: "An unexpected error occurred.",
                    statusCode: StatusCodes.Status500InternalServerError)
                    .ExecuteAsync(context);
            });
        });

        app.UseSwagger();
        app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "StreamingGatewayService v1"));

        app.UseHttpsRedirection();

        app.UseCors(policy => policy
            .AllowAnyOrigin()
            .AllowAnyMethod()
            .AllowAnyHeader()
        );

        app.UseWebSockets();

        app.UseAuthorization();

        app.MapControllers();
        app.MapHealthChecks("/health");
        app.MapHealthChecks("/health/live", new()
        {
            Predicate = check => check.Tags.Contains("live")
        });

        app.Run();
    }

    private static void ConfigureLogging()
    {
        Serilog.Debugging.SelfLog.Enable(msg => Debug.WriteLine(msg));

        Log.Logger = new LoggerConfiguration()
            .Enrich.FromLogContext()
            .MinimumLevel.Verbose()
            .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss.fff} {Level:u3}] [s:{SessionId}][m:{MeetId}][p:{SpeakerId}] {Message:lj}{NewLine}{Exception}")
            .MinimumLevel.Debug()
            .CreateLogger();
    }

    private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        Log.Error("UnhandledException: {unhandledExc} isTerminating:{isTerminating}", e.ExceptionObject, e.IsTerminating);
        Log.CloseAndFlush();
    }
}