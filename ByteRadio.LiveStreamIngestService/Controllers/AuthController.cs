using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace ByteRadio.LiveStreamIngestService.Controllers;

public record LoginRequest(string Username, string Password);

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly string _jwtKey;
    private readonly string _issuer;
    private readonly string _audience;

    public AuthController(IConfiguration config)
    {
        _jwtKey = config["Jwt:Key"] ?? "supersecretkeythatmustbeatleast32characterslong!";
        _issuer = config["Jwt:Issuer"] ?? "MyApi";
        _audience = config["Jwt:Audience"] ?? "MyClient";
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public IActionResult Login([FromBody] LoginRequest request)
    {
        // Проверка учётных данных (в реальности — по БД)
        if (request.Username != "admin" || request.Password != "password")
            return Unauthorized(new { Message = "Invalid credentials" });

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(ClaimTypes.Name, request.Username),
            new Claim(ClaimTypes.Role, "User")
        };

        var token = new JwtSecurityToken(
            issuer: _issuer,
            audience: _audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(15),
            signingCredentials: creds
        );

        var handler = new JwtSecurityTokenHandler();
        var tokenString = handler.WriteToken(token);

        System.Diagnostics.Debug.WriteLine("auth: >> " + tokenString);

        return Ok(new
        {
            AccessToken = tokenString,
            TokenType = "Bearer",
            ExpiresIn = 15 * 60
        });
    }
}
