using Microsoft.AspNetCore.Mvc;

namespace Hagoplant.Controllers
{
    public class AdminController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
