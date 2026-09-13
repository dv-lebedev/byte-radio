using ByteRadio.LiveStreamIngestService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ByteRadio.LiveStreamIngestService.Controllers;

public record LoginRequest(string Username, string Password);

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IConfiguration config, IAuthService authService)
    {
        _authService = authService;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public IActionResult Login([FromBody] LoginRequest request)
    {
        var result = _authService.Authenticate(request.Username, request.Password);

        if (!result.Success)
            return Unauthorized(new { Message = result.Error });

        return Ok(new
        {
            AccessToken = result.AccessToken,
            TokenType = "Bearer",
            ExpiresIn = 15 * 60
        });
    }
}
