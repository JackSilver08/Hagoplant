using Hagoplant.DBcontext;
using Hagoplant.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Hagoplant.Controllers
{
    public class HomeController : Controller
    {
        private readonly HagoDbContext _db;

        public HomeController(HagoDbContext db)
        {
            _db = db;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var featured = await _db.Products
                .AsNoTracking()
                .Where(p => p.IsActive && p.IsFeatured)
                .OrderByDescending(p => p.CreatedAt)
                .Take(12)
                .ToListAsync();

            if (featured.Count == 0)
            {
                featured = await _db.Products
                    .AsNoTracking()
                    .Where(p => p.IsActive)
                    .OrderByDescending(p => p.CreatedAt)
                    .Take(12)
                    .ToListAsync();
            }

            var vm = new HomeIndexVm
            {
                FeaturedProducts = featured
            };

            return View(vm);
        }

        public IActionResult About() => View();
        public IActionResult Contact() => View();
    }
}
