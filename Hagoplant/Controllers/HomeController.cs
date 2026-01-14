using Hagoplant.DBcontext;
using Hagoplant.Models;
using Hagoplant.Models.ViewModels;
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
        [HttpGet("/Blog")]
        [HttpGet("/Home/Blog")]
        public async Task<IActionResult> Blog(bool all = false)
        {
            var q = _db.BlogPosts.AsNoTracking();

            if (!all)
            {
                // Chỉ lấy bài published (không phân biệt hoa/thường)
                q = q.Where(x => x.Status != null && EF.Functions.ILike(x.Status, BlogPostStatuses.Published));
            }

            var posts = await q
     .OrderByDescending(x => x.PublishedAt ?? x.CreatedAt)
     .Select(x => new BlogPost
     {
         Id = x.Id,
         Title = x.Title,
         Slug = x.Slug,
         Excerpt = x.Excerpt,
         ContentHtml = x.ContentHtml,
         CoverImageUrl = x.CoverImageUrl,
         AuthorUserId = x.AuthorUserId,
         Status = x.Status,
         PublishedAt = x.PublishedAt,
         CreatedAt = x.CreatedAt,
         UpdatedAt = x.UpdatedAt,
         ViewCount = x.ViewCount
     })
     .ToListAsync();


            Console.WriteLine($"[Home/Blog] all={all}, count={posts.Count}");

            return View(new HomeIndexVm { BlogPosts = posts });
        }

        // =============================
        // HIỂN THỊ GIỎ HÀNG
        // =============================
        [HttpGet]
        public async Task<IActionResult> Cart()
        {
            var vm = await BuildCartVmAsync();
            return View(vm);
        }

        // =============================
        // THÊM SẢN PHẨM VÀO GIỎ
        // =============================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Add(Guid productId, int quantity = 1)
        {
            if (quantity < 1) quantity = 1;

            var cartId = GetCartId();

            // đảm bảo có cart
            var cartExists = await _db.Carts.AnyAsync(c => c.Id == cartId);
            if (!cartExists)
            {
                _db.Carts.Add(new Cart
                {
                    Id = cartId,
                    CreatedAt = DateTimeOffset.UtcNow,
                    UpdatedAt = DateTimeOffset.UtcNow
                });
                await _db.SaveChangesAsync();
            }

            var item = await _db.CartItems
                .FirstOrDefaultAsync(x => x.CartId == cartId && x.ProductId == productId);

            if (item != null)
            {
                item.Quantity += quantity;
                item.UpdatedAt = DateTimeOffset.UtcNow;
            }
            else
            {
                var product = await _db.Products.AsNoTracking()
                    .FirstOrDefaultAsync(p => p.Id == productId);

                if (product == null) return NotFound();

                _db.CartItems.Add(new CartItem
                {
                    Id = Guid.NewGuid(),
                    CartId = cartId,
                    ProductId = productId,
                    Quantity = quantity,
                    UnitPriceSnapshot = product.SalePrice ?? product.Price,
                    CreatedAt = DateTimeOffset.UtcNow,
                    UpdatedAt = DateTimeOffset.UtcNow
                });
            }

            await _db.SaveChangesAsync();

            // ✅ Nếu là AJAX thì trả JSON để JS đọc res.json()
            var isAjax = Request.Headers["X-Requested-With"] == "XMLHttpRequest";
            if (isAjax)
            {
                // Count theo tổng quantity (đúng nghĩa badge)
                var count = await _db.CartItems
                    .Where(x => x.CartId == cartId)
                    .SumAsync(x => (int?)x.Quantity) ?? 0;

                return Json(new { ok = true, count });
            }

            // ✅ Submit bình thường thì redirect như cũ
            return RedirectToAction(nameof(Cart));
        }


        // =============================
        // CẬP NHẬT SỐ LƯỢNG
        // =============================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateQuantity(Guid productId, int quantity)
        {
            if (quantity < 1) quantity = 1;

            var cartId = GetCartId();

            var item = await _db.CartItems
                .FirstOrDefaultAsync(x => x.CartId == cartId && x.ProductId == productId);

            if (item == null) return NotFound();

            item.Quantity = quantity;
            item.UpdatedAt = DateTimeOffset.UtcNow;

            await _db.SaveChangesAsync();
            return RedirectToAction(nameof(Cart));
        }


        // =============================
        // XÓA 1 SẢN PHẨM
        // =============================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Remove(Guid productId)
        {
            var cartId = GetCartId();
            var item = await _db.CartItems
                .FirstOrDefaultAsync(x => x.CartId == cartId && x.ProductId == productId);

            if (item != null)
            {
                _db.CartItems.Remove(item);
                await _db.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Index));
        }

        // =============================
        // XÓA TOÀN BỘ GIỎ HÀNG
        // =============================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Clear()
        {
            var cartId = GetCartId();
            var items = await _db.CartItems.Where(x => x.CartId == cartId).ToListAsync();

            if (items.Any())
            {
                _db.CartItems.RemoveRange(items);
                await _db.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Index));
        }

        // =============================
        // TIẾN HÀNH THANH TOÁN (GIẢ LẬP)
        // =============================
        [HttpGet]
        public async Task<IActionResult> Checkout()
        {
            var vm = await BuildCartVmAsync();
            if (vm.IsEmpty)
            {
                TempData["Toast.Message"] = "Giỏ hàng trống, vui lòng chọn sản phẩm trước khi thanh toán.";
                return RedirectToAction(nameof(Index));
            }

            // Ở đây bạn có thể điều hướng tới trang thanh toán thật
            return View(vm);
        }

        // =============================
        // HÀM PHỤ TRỢ
        // =============================
        private Guid GetCartId()
        {
            const string key = "CartId";

            var existingStr = HttpContext.Session.GetString(key);
            if (Guid.TryParse(existingStr, out var existing))
                return existing;

            var newId = Guid.NewGuid();
            HttpContext.Session.SetString(key, newId.ToString());
            return newId;
        }


        private async Task<CartVm> BuildCartVmAsync()
        {
            var cartId = GetCartId();
            var et = _db.Model.FindEntityType(typeof(BlogPost))!;
            Console.WriteLine("BlogPost mapped table: " + et.GetTableName());
            Console.WriteLine("BlogPost props: " + string.Join(", ", et.GetProperties().Select(p => p.Name)));
            Console.WriteLine("BlogPost columns: " + string.Join(", ", et.GetProperties().Select(p => p.GetColumnName())));

            var items = await _db.CartItems
                .AsNoTracking()
                .Where(ci => ci.CartId == cartId)
                .Include(ci => ci.Product)
                .Select(ci => new CartItemVm
                {
                    CartItemId = ci.Id,
                    CartId = ci.CartId,
                    ProductId = ci.ProductId,

                    Name = ci.Product!.Name,
                    Slug = ci.Product!.Slug,
                    ImageUrl = ci.Product!.ImageUrl,

                    Quantity = ci.Quantity,
                    UnitPriceSnapshot = ci.UnitPriceSnapshot,

                    CurrentPrice = ci.Product!.Price,
                    CurrentSalePrice = ci.Product!.SalePrice,
                    IsActive = ci.Product!.IsActive
                })
                .ToListAsync();

            return new CartVm
            {
                CartId = cartId,
                Items = items,
                DiscountAmount = 0
            };
        }


    }
}
