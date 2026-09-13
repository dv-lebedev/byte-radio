using ByteRadio.LiveStreamIngestService.Controllers;
using ByteRadio.LiveStreamIngestService.Services;
using ByteRadio.Messaging;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Serilog;
using System.Diagnostics;
using System.Text;

namespace ByteRadio.LiveStreamIngestService;

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

        builder.Services.AddControllers();
        builder.Services.AddProblemDetails();

        builder.Services.AddSingleton<IAuthService, MockAuthService>();

        builder.Services.AddSingleton<LiveStreamProviderController>();
        builder.Services.AddSingleton<LiveStreamSourceManager>();

        builder.Services.AddSingleton<LocalTestRabbitMqOptionsProvider>();
        builder.Services.AddSingleton<IRabbitMqOptionsProvider>(sp => sp.GetRequiredService<LocalTestRabbitMqOptionsProvider>());
        
        builder.Services.AddSingleton<IRabbitMqPublisher, RabbitMqPublisher>();

        builder.Services.AddHealthChecks()
            .AddCheck("self", () => HealthCheckResult.Healthy(), tags: new[] { "live" });

        builder.Services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new OpenApiInfo { Title = "LiveStreamIngestService", Version = "v1" });
        });

        var jwtKey = builder.Configuration["Jwt:Key"] ?? "supersecretkeythatmustbeatleast32characterslong!";
        var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "ByteRadioApi";
        var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "ByteRadioClient";

        builder.Services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,

                ValidIssuer = jwtIssuer,
                ValidAudience = jwtAudience,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),

                ClockSkew = TimeSpan.Zero
            };
        });

        builder.Services.AddAuthorization();

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
        app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "LiveStreamIngestService v1"));

        app.UseHttpsRedirection();

        app.UseWebSockets();

        app.UseAuthentication();
        app.UseAuthorization();

        app.MapControllers();
        app.MapHealthChecks("/health");
        app.MapHealthChecks("/health/live", new()
        {
            Predicate = check => check.Tags.Contains("live")
        });

        app.MapGet("/", () => Results.Ok());

        app.Run();
    }

    private static void ConfigureLogging()
    {
        Serilog.Debugging.SelfLog.Enable(msg => Debug.WriteLine(msg));

        Log.Logger = new LoggerConfiguration()
            .Enrich.FromLogContext()
            .MinimumLevel.Debug()
            .MinimumLevel.Override("Microsoft", Serilog.Events.LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.AspNetCore", Serilog.Events.LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.Hosting.Lifetime", Serilog.Events.LogEventLevel.Information)
            .MinimumLevel.Override("System", Serilog.Events.LogEventLevel.Warning)
            .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss.fff} {Level:u3}] [s:{SessionId}][m:{MeetId}][p:{SpeakerId}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();
    }

    private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        Log.Error("UnhandledException: {unhandledExc} isTerminating:{isTerminating}", e.ExceptionObject, e.IsTerminating);
        Log.CloseAndFlush();
    }
}