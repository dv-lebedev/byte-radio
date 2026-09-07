using System.Net.WebSockets;

namespace ByteRadio.LiveStreamIngestService;

public class LiveStreamSourceManager
{
    private readonly object _sync = new();
    private LiveStreamSourceItem? _item;

    public async Task HandleLiveStreamSourceAsync(WebSocket webSocket, Serilog.ILogger log)
    {
        var newItem = new LiveStreamSourceItem(webSocket, log);
        LiveStreamSourceItem? oldItem;

        lock (_sync)
        {
            oldItem = _item;
            _item = newItem;
        }

        oldItem?.Dispose();

        await newItem.RunAsync();
    }
}

public class LiveStreamSourceItem : IDisposable
{
    private readonly WebSocket _webSocket;
    private readonly Serilog.ILogger _logger;
    private readonly CancellationTokenSource _cts;
    private readonly CancellationToken _ct;
    private int _disposed;

    public LiveStreamSourceItem(WebSocket webSocket, Serilog.ILogger logger)
    {
        _webSocket = webSocket;
        _logger = logger;
        _cts = new CancellationTokenSource();
        _ct = _cts.Token;
    }

    public async Task RunAsync()
    {
        try
        {
            var buffer = new byte[16 * 1024];
            using var messageStream = new MemoryStream();

            WebSocketReceiveResult result;
            do
            {
                result = await _webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), _ct);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    if (_webSocket.State == WebSocketState.CloseReceived)
                    {
                        await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
                    }

                    return;
                }

                if (result.Count > 0)
                {
                    await messageStream.WriteAsync(buffer.AsMemory(0, result.Count), _ct);
                }
            }
            while (!result.EndOfMessage);

            var data = messageStream.ToArray();

            if (result.MessageType == WebSocketMessageType.Text)
            {
                var text = System.Text.Encoding.UTF8.GetString(data);
                _logger.Debug("Received live stream source text message: {Message}", text);
            }
            else if (result.MessageType == WebSocketMessageType.Binary && data is not null)
            {
                SendToQueue(data);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.Debug("Live stream source item run loop canceled.");
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Live stream source item run loop encountered an error.");
        }
        finally
        {
            if (_webSocket.State == WebSocketState.Open)
            {
                await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
            }
            _webSocket.Dispose();
            _logger.Debug("Live stream source item run loop finished.");
        }
    }

    private void SendToQueue(byte[] data)
    {

    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        _cts.Cancel();
        _cts.Dispose();
    }
}