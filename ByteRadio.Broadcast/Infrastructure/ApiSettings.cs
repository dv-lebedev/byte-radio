namespace ByteRadio.Broadcast.Infrastructure;

public class ApiSettings
{
    public ApiEndpoints Endpoints { get; set; } = new();
    public string BaseHttp { get; set; } = string.Empty;
    public string BaseWs { get; set; } = string.Empty;
}