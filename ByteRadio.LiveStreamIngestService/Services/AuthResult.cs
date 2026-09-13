namespace ByteRadio.LiveStreamIngestService.Services;

public class AuthResult
{
    public bool Success { get; init; }
    public string? AccessToken { get; init; }
    public string? Error { get; init; }
}