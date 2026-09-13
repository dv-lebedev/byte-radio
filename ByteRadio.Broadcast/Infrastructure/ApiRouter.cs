using Microsoft.Extensions.Options;

namespace ByteRadio.Broadcast.Infrastructure;

public interface IApiRouter
{
    Uri Login { get; }
    Uri WebSocket { get; }
}

public class ApiRouter : IApiRouter
{
    private readonly ApiSettings _cfg;

    public ApiRouter(IOptions<ApiSettings> options)
    {
        _cfg = options.Value;
    }

    private Uri Combine(string baseUri, string path) => new(new Uri(baseUri), path);

    public Uri Login => Combine(_cfg.BaseHttp, _cfg.Endpoints.Login);
    public Uri WebSocket
    {
        get
        {
            var httpUri = Combine(_cfg.BaseHttp, _cfg.Endpoints.WebSocket);
            return new UriBuilder(httpUri)
            {
                Scheme = _cfg.BaseWs.StartsWith("wss") ? "wss" : "ws",
            }.Uri;
        }
    }
}
