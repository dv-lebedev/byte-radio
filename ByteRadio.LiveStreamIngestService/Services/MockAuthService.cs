using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace ByteRadio.LiveStreamIngestService.Services;

public class MockAuthService : IAuthService
{
    private static readonly Dictionary<string, string> Users = new()
    {
        { "test", "test" },
    };

    private readonly string _jwtKey;
    private const string Issuer = "MyApi";
    private const string Audience = "MyClient";

    public MockAuthService(IConfiguration config)
    {
        _jwtKey = config["Jwt:Key"]
               ?? "supersecretkeythatmustbeatleast32characterslong!";
    }

    public AuthResult Authenticate(string username, string password)
    {
        if (!Users.TryGetValue(username, out var storedPassword) || storedPassword != password)
            return new AuthResult { Success = false, Error = "Invalid credentials" };

        var claims = new[]
        {
            new Claim(ClaimTypes.Name, username),
            new Claim(ClaimTypes.Role, "User")
        };

        var creds = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtKey)),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: Issuer,
            audience: Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(15),
            signingCredentials: creds);

        var tokenString = new JwtSecurityTokenHandler().WriteToken(token);

        return new AuthResult { Success = true, AccessToken = tokenString };
    }
}
