using Microsoft.AspNetCore.Mvc;

namespace ByteRadio.StreamingGatewayService.Controllers;

[ApiController]
[Route("/")]
public class IndexController : ControllerBase
{
    private readonly string _filePath;

    public IndexController(IWebHostEnvironment webHostEnvironment)
    {
        _filePath = Path.Combine(webHostEnvironment.WebRootPath, "index.html");
    }

    [HttpGet]
    public PhysicalFileResult Index()
    {
        return PhysicalFile(_filePath, "text/html");
    }
}
