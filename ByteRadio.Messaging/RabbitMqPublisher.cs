using RabbitMQ.Client;
using Serilog;

namespace ByteRadio.Messaging;

public sealed class RabbitMqPublisher : IRabbitMqPublisher, IAsyncDisposable
{
    private readonly IRabbitMqOptionsProvider _optionsProvider;
    private RabbitMqOptions? _options;
    private readonly Serilog.ILogger _logger = Log.ForContext<RabbitMqPublisher>();
    private readonly SemaphoreSlim _initLock = new(1, 1);

    private IConnection? _connection;
    private IChannel? _channel;
    private readonly BasicProperties _properties = new()
    {
        Persistent = true
    };

    public RabbitMqPublisher(IRabbitMqOptionsProvider optionsProvider)
    {
        _optionsProvider = optionsProvider;
        _ = GetOrCreateChannelAsync(CancellationToken.None);
    }

    public async Task PublishAsync(byte[] data, CancellationToken cancellationToken = default)
    {
        var channel = await GetOrCreateChannelAsync(cancellationToken);

        await channel.BasicPublishAsync(
            exchange: string.Empty,
            routingKey: _options!.QueueName,
            mandatory: false,
            basicProperties: _properties,
            body: data,
            cancellationToken: cancellationToken);
    }

    private async Task<IChannel> GetOrCreateChannelAsync(CancellationToken cancellationToken)
    {
        if (_channel is { IsOpen: true })
        {
            return _channel;
        }

        await _initLock.WaitAsync(cancellationToken);
        try
        {
            if (_channel is { IsOpen: true })
            {
                return _channel;
            }

            _options = await _optionsProvider.RequestOptions();

            var factory = new ConnectionFactory
            {
                HostName = _options.HostName,
                Port = _options.Port,
                UserName = _options.UserName,
                Password = _options.Password
            };

            _connection = await factory.CreateConnectionAsync(cancellationToken);
            _channel = await _connection.CreateChannelAsync(cancellationToken: cancellationToken);

            await _channel.QueueDeclareAsync(
                queue: _options.QueueName,
                durable: true,
                exclusive: false,
                autoDelete: false,
                cancellationToken: cancellationToken);

            _logger.Information("RabbitMQ channel established for queue {QueueName}.", _options.QueueName);

            return _channel;
        }
        finally
        {
            _initLock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_channel is not null)
        {
            await _channel.CloseAsync();
            _channel.Dispose();
        }

        if (_connection is not null)
        {
            await _connection.CloseAsync();
            _connection.Dispose();
        }

        _initLock.Dispose();
    }
}