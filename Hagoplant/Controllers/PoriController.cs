using Microsoft.AspNetCore.Mvc;

public class PoriController : Controller
{
    public IActionResult Policy()
    {
        return View();
    }

    public IActionResult Privacy()
    {
        return View();
    }
}
