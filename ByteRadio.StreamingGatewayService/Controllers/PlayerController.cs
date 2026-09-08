using Microsoft.AspNetCore.Mvc;

namespace ByteRadio.StreamingGatewayService.Controllers;

[ApiController]
[Route("[controller]")]
public class PlayerController : ControllerBase
{
    private readonly IWebHostEnvironment _webHostEnvironment;

    public PlayerController(IWebHostEnvironment webHostEnvironment)
    {
        _webHostEnvironment = webHostEnvironment;
    }

    [HttpGet]
    public async Task<ContentResult> Index()
    {
        var filePath = Path.Combine(_webHostEnvironment.WebRootPath, "player", "index.html");
        var html = await System.IO.File.ReadAllTextAsync(filePath);

        return Content(html, "text/html");
    }
}