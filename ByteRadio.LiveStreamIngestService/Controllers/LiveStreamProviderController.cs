using ByteRadio.LiveStreamIngestService;
using Microsoft.AspNetCore.Mvc;
using System.Net.WebSockets;

namespace ByteRadio.TrackPublisherService.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class LiveStreamProviderController(
        LiveStreamSourceManager streamSourceManager,
        ILogger<LiveStreamProviderController> logger) : ControllerBase
    {
        private readonly LiveStreamSourceManager _streamSourceManager = streamSourceManager;
        private readonly ILogger<LiveStreamProviderController> _logger = logger;

        [HttpGet("connect")]
        public async Task<IActionResult> ConnectLiveStreamSource()
        {
            if (!HttpContext.WebSockets.IsWebSocketRequest)
            {
                return BadRequest("WebSocket request expected.");
            }

            _logger.LogDebug("Received request to connect live stream source from {RemoteIpAddress}", HttpContext.Connection.RemoteIpAddress);

            using WebSocket webSocket = await HttpContext.WebSockets.AcceptWebSocketAsync();
            await _streamSourceManager.HandleLiveStreamSourceAsync(webSocket, _logger);
            return Ok();
        }
    }
}