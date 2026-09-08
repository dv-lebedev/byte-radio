using Microsoft.AspNetCore.Mvc;
using System.Net.WebSockets;

namespace ByteRadio.StreamingGatewayService.Controllers;

[ApiController]
[Route("[controller]")]
public class LiveStreamListenerController : ControllerBase
{
    private readonly WebSocketConnectionManager _connectionManager;
    private readonly ILogger<LiveStreamListenerController> _logger;

    public LiveStreamListenerController(
        WebSocketConnectionManager connectionManager,
        ILogger<LiveStreamListenerController> logger)
    {
        _connectionManager = connectionManager;
        _logger = logger;
    }

    [HttpGet("connect")]
    public async Task<IActionResult> ConnectAsync()
    {
        if (!HttpContext.WebSockets.IsWebSocketRequest)
        {
            return BadRequest("WebSocket request expected.");
        }

        WebSocket webSocket = await HttpContext.WebSockets.AcceptWebSocketAsync();
        var connectionId = _connectionManager.AddSocket(webSocket);

        try
        {
            var buffer = new byte[1024];
            WebSocketReceiveResult result;

            do
            {
                result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), HttpContext.RequestAborted);
            }
            while (result.MessageType != WebSocketMessageType.Close);

            await Task.Delay(Timeout.Infinite);

        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "WebSocket {ConnectionId} encountered an error and will be removed.", connectionId);
        }
        finally
        {
            await _connectionManager.RemoveSocketAsync(connectionId);
        }

        webSocket.Dispose();
        return new EmptyResult();
    }
}
