using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace ByteRadio.Messaging;

public sealed class RabbitMqConsumerService : BackgroundService
{
    private readonly IRabbitMqOptionsProvider _optionsProvider;
    private readonly IMessageBroadcaster _broadcaster;
    private readonly ILogger<RabbitMqConsumerService> _logger;

    private RabbitMqOptions? _options;
    private IConnection? _connection;
    private IChannel? _channel;

    public RabbitMqConsumerService(
        IRabbitMqOptionsProvider optionsProvider,
        IMessageBroadcaster broadcaster,
        ILogger<RabbitMqConsumerService> logger)
    {
        _optionsProvider = optionsProvider;
        _broadcaster = broadcaster;
        _logger = logger;
    }

    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ConnectAndConsumeAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // expected on shutdown
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to connect/consume from RabbitMQ at {HostName}:{Port}. Retrying in {Delay}.",
                    _options.HostName, _options.Port, RetryDelay);
            }

            CleanUpConnection();

            try
            {
                await Task.Delay(RetryDelay, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task ConnectAndConsumeAsync(CancellationToken stoppingToken)
    {
        _options = await _optionsProvider.RequestOptions();

        var factory = new ConnectionFactory
        {
            HostName = _options.HostName,
            Port = _options.Port,
            UserName = _options.UserName,
            Password = _options.Password
        };

        _connection = await factory.CreateConnectionAsync(stoppingToken);
        _channel = await _connection.CreateChannelAsync(cancellationToken: stoppingToken);

        await _channel.QueueDeclareAsync(
            queue: _options.QueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            cancellationToken: stoppingToken);

        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.ReceivedAsync += async (_, ea) =>
        {
            try
            {
                var data = ea.Body.ToArray();
                await _broadcaster.BroadcastAsync(data, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing message from queue {QueueName}.", _options.QueueName);
            }
        };

        await _channel.BasicConsumeAsync(
            queue: _options.QueueName,
            autoAck: true,
            consumer: consumer,
            cancellationToken: stoppingToken);

        _logger.LogInformation("Subscribed to RabbitMQ queue {QueueName} on {HostName}:{Port}.", _options.QueueName, _options.HostName, _options.Port);

        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    private void CleanUpConnection()
    {
        try
        {
            _channel?.Dispose();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error while disposing RabbitMQ channel during cleanup.");
        }

        try
        {
            _connection?.Dispose();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error while disposing RabbitMQ connection during cleanup.");
        }

        _channel = null;
        _connection = null;
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (_channel is not null)
            {
                await _channel.CloseAsync(cancellationToken);
                await _channel.DisposeAsync();
            }

            if (_connection is not null)
            {
                await _connection.CloseAsync(cancellationToken);
                await _connection.DisposeAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error while closing RabbitMQ connection/channel during shutdown.");
        }

        await base.StopAsync(cancellationToken);
    }
}