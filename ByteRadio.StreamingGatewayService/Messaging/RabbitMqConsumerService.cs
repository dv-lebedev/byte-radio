using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Serilog;

namespace ByteRadio.StreamingGatewayService.Messaging;

public sealed class RabbitMqConsumerService : BackgroundService
{
    private readonly RabbitMqOptions _options;
    private readonly WebSocketConnectionManager _connectionManager;
    private readonly ILogger<RabbitMqConsumerService> _logger;

    private IConnection? _connection;
    private IChannel? _channel;

    public RabbitMqConsumerService(
        IOptions<RabbitMqOptions> options,
        WebSocketConnectionManager connectionManager,
        ILogger<RabbitMqConsumerService> logger)
    {
        _options = options.Value;
        _connectionManager = connectionManager;
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

                //TODO Serilog.Log.Debug("Received message from RabbitMQ queue {QueueName}: {MessageSize} bytes", _options.QueueName, data.Length);

                await _connectionManager.BroadcastAsync(data, stoppingToken);
                //await _channel.BasicAckAsync(ea.DeliveryTag, multiple: false, cancellationToken: stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing message from queue {QueueName}.", _options.QueueName);
                await _channel.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: false, cancellationToken: stoppingToken);
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
        catch
        {
            // ignore errors while cleaning up a broken channel
        }

        try
        {
            _connection?.Dispose();
        }
        catch
        {
            // ignore errors while cleaning up a broken connection
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
                _channel.Dispose();
            }

            if (_connection is not null)
            {
                await _connection.CloseAsync(cancellationToken);
                _connection.Dispose();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error while closing RabbitMQ connection/channel during shutdown.");
        }

        await base.StopAsync(cancellationToken);
    }
}
