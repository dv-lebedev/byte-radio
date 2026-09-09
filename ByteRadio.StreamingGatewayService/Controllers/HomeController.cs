using Microsoft.AspNetCore.Mvc;

namespace ByteRadio.StreamingGatewayService.Controllers
{
    [Route("/")]
    public class HomeController : Controller
    {
        [HttpGet]
        public IActionResult Index()
        {
            return View();
        }
    }
}