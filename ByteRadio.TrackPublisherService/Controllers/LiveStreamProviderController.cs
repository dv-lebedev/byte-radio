using ByteRadio.LiveStreamIngestService;
using Microsoft.AspNetCore.Mvc;
using Serilog;
using System.Net.WebSockets;

namespace ByteRadio.TrackPublisherService.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class LiveStreamProviderController(LiveStreamSourceManager streamSourceManager) : ControllerBase
    {
        private readonly LiveStreamSourceManager _streamSourceManager = streamSourceManager;

        [HttpGet("connect")]
        public async Task<IActionResult> ConnectLiveStreamSource()
        {
            if (HttpContext.WebSockets.IsWebSocketRequest)
            {
                using WebSocket webSocket = await HttpContext.WebSockets.AcceptWebSocketAsync();
                var logger = Log.ForContext("ForConnection", Guid.NewGuid());
                await _streamSourceManager.HandleLiveStreamSourceAsync(webSocket, logger);
                return Ok();
            }
            else
            {
                return BadRequest("WebSocket request expected.");
            }
        }
    }
}
