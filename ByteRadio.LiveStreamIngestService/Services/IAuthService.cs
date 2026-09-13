namespace ByteRadio.LiveStreamIngestService.Services;

public interface IAuthService
{
    AuthResult Authenticate(string username, string password);
}