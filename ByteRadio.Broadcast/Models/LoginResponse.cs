using System.Text.Json.Serialization;

namespace ByteRadio.Broadcast.Models;

public class LoginResponse
{
    [JsonPropertyName("token")]
    public string Token { get; set; } = string.Empty;

    [JsonPropertyName("error")]
    public string? Error { get; set; }
}