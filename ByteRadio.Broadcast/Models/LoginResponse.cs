using System.Text.Json.Serialization;

namespace ByteRadio.Broadcast.Models;

public class LoginResponse
{
    [JsonPropertyName("accessToken")]
    public string Token { get; set; } = string.Empty;

    [JsonPropertyName("error")]
    public string? Error { get; set; }
}