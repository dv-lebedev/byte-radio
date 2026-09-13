using ByteRadio.Broadcast.Infrastructure;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;

namespace ByteRadio.Broadcast.Models;

public class AuthApiClient
{
    private readonly IApiRouter _apiRouter;

    public AuthApiClient(IApiRouter apiRouter)
    {
        _apiRouter = apiRouter;
    }

    public async Task<LoginResponse> LoginAsync(string username, string password, CancellationToken ct = default)
    {
        var client = new HttpClient();
        var payload = new { username, password };
        var response = await client.PostAsJsonAsync(_apiRouter.Login, payload, ct);
        var json = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            var body = JsonSerializer.Deserialize<LoginResponse>(json);
            return new LoginResponse
            {
                Error = body?.Error ?? $"HTTP {response.StatusCode}: {response.ReasonPhrase}"
            };
        }

        return JsonSerializer.Deserialize<LoginResponse>(json) 
            ?? new LoginResponse { Error = "Empty response from server" };
    }
}