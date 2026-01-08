using Hagoplant.DBcontext;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Hagoplant.Controllers
{
    public class ProductController : Controller
    {
        private readonly HagoDbContext _context;

        public ProductController(HagoDbContext context)
        {
            _context = context;
        }

        // GET: /Product/Detail/{slug}
        [HttpGet("san-pham/{slug}")]
        public async Task<IActionResult> Detail(string slug)
        {
            if (string.IsNullOrEmpty(slug))
                return NotFound();

            var product = await _context.Products
                .FirstOrDefaultAsync(p => p.Slug == slug && p.IsActive);

            if (product == null)
                return NotFound();

            return View(product);
        }
    }
}
