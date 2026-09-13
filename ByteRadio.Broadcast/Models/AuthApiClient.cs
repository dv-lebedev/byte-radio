using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;

namespace ByteRadio.Broadcast.Models;

public class AuthApiClient
{
    private readonly HttpClient _http;

    public AuthApiClient(HttpClient http) => _http = http;

    public async Task<LoginResponse> LoginAsync(string username, string password, CancellationToken ct = default)
    {
        const string endpoint = "https://localhost:5011/api/auth/login";

        var payload = new { username, password };
        var response = await _http.PostAsJsonAsync(endpoint, payload, ct);
        var json = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            var body = JsonSerializer.Deserialize<LoginResponse>(json);
            return new LoginResponse
            {
                Error = body?.Error ?? $"HTTP {response.StatusCode}: {response.ReasonPhrase}"
            };
        }

        return JsonSerializer.Deserialize<LoginResponse>(json) ?? new LoginResponse { Error = "Пустой ответ от сервера" };
    }
}