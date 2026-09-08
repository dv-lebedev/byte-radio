using System.Collections.Concurrent;
using System.Net.WebSockets;

namespace ByteRadio.StreamingGatewayService;

public class WebSocketConnectionManager
{
    private readonly ConcurrentDictionary<Guid, WebSocket> _sockets = new();
    private readonly ILogger<WebSocketConnectionManager> _logger;

    public WebSocketConnectionManager(ILogger<WebSocketConnectionManager> logger)
    {
        _logger = logger;
    }

    public Guid AddSocket(WebSocket socket)
    {
        var id = Guid.NewGuid();
        _sockets[id] = socket;
        _logger.LogInformation("WebSocket {ConnectionId} connected. Active connections: {Count}", id, _sockets.Count);
        return id;
    }

    public async Task RemoveSocketAsync(Guid id)
    {
        if (!_sockets.TryRemove(id, out var socket))
        {
            return;
        }

        _logger.LogInformation("WebSocket {ConnectionId} removed. Active connections: {Count}", id, _sockets.Count);

        try
        {
            if (socket.State == WebSocketState.Open || socket.State == WebSocketState.CloseReceived)
            {
                await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error closing WebSocket {ConnectionId} during removal.", id);
        }
        finally
        {
            socket.Dispose();
        }
    }

    public async Task BroadcastAsync(byte[] data, CancellationToken cancellationToken = default)
    {
        foreach (var (id, socket) in _sockets)
        {
            if (socket.State != WebSocketState.Open)
            {
                await RemoveSocketAsync(id);
                continue;
            }

            try
            {
                await socket.SendAsync(
                    new ArraySegment<byte>(data),
                    WebSocketMessageType.Binary,
                    endOfMessage: true,
                    cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to send data to WebSocket {ConnectionId}. Removing connection.", id);
                await RemoveSocketAsync(id);
            }
        }
    }
}
