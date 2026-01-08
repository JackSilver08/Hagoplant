using Hagoplant.DBcontext;
using Hagoplant.Models;
using Hagoplant.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Hagoplant.Controllers
{
    public class AdminController : Controller
    {
        private readonly HagoDbContext _db;

        public AdminController(HagoDbContext db)
        {
            _db = db;
        }

        // HIỆN SẢN PHẨM (load về Admin/Index)
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var vm = new AdminDashboardVm
            {
                Products = await _db.Products.AsNoTracking()
                    .OrderByDescending(p => p.CreatedAt)
                    .ToListAsync()
            };

            return View(vm);
        }

        // THÊM SẢN PHẨM
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateProduct(
            [Bind("Name,Slug,Description,Price,SalePrice,ImageUrl,IsActive,IsFeatured")] Product input)
        {
            await ValidateProductAsync(input, currentId: null);

            if (!ModelState.IsValid)
            {
                TempData["Toast.Ok"] = "0";
                TempData["Toast.Message"] = "Dữ liệu sản phẩm không hợp lệ.";
                return RedirectToAction(nameof(Index));
            }

            var entity = new Product
            {
                Id = Guid.NewGuid(),
                Name = input.Name.Trim(),
                Slug = input.Slug.Trim(),
                Description = input.Description?.Trim(),
                Price = input.Price,
                SalePrice = input.SalePrice,
                ImageUrl = input.ImageUrl?.Trim(),
                IsActive = input.IsActive,
                IsFeatured = input.IsFeatured,
                CreatedAt = DateTime.UtcNow
            };

            _db.Products.Add(entity);
            await _db.SaveChangesAsync();

            TempData["Toast.Ok"] = "1";
            TempData["Toast.Message"] = "Đã thêm sản phẩm.";
            return RedirectToAction(nameof(Index));
        }

        // SỬA SẢN PHẨM
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateProduct(
            Guid id,
            [Bind("Name,Slug,Description,Price,SalePrice,ImageUrl,IsActive,IsFeatured")] Product input)
        {
            var entity = await _db.Products.FirstOrDefaultAsync(p => p.Id == id);
            if (entity == null)
            {
                TempData["Toast.Ok"] = "0";
                TempData["Toast.Message"] = "Không tìm thấy sản phẩm.";
                return RedirectToAction(nameof(Index));
            }

            await ValidateProductAsync(input, currentId: id);

            if (!ModelState.IsValid)
            {
                TempData["Toast.Ok"] = "0";
                TempData["Toast.Message"] = "Dữ liệu cập nhật không hợp lệ.";
                return RedirectToAction(nameof(Index));
            }

            entity.Name = input.Name.Trim();
            entity.Slug = input.Slug.Trim();
            entity.Description = input.Description?.Trim();
            entity.Price = input.Price;
            entity.SalePrice = input.SalePrice;
            var newUrl = input.ImageUrl?.Trim();

            // Chỉ set khi có URL mới (tức là đã upload Cloudinary)
            if (!string.IsNullOrWhiteSpace(newUrl))
            {
                entity.ImageUrl = newUrl;
            }
            entity.IsActive = input.IsActive;
            entity.IsFeatured = input.IsFeatured;

            await _db.SaveChangesAsync();

            TempData["Toast.Ok"] = "1";
            TempData["Toast.Message"] = "Đã cập nhật sản phẩm.";
            return RedirectToAction(nameof(Index));
        }

        // XÓA SẢN PHẨM
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteProduct(Guid id)
        {
            var entity = await _db.Products.FirstOrDefaultAsync(p => p.Id == id);
            if (entity == null)
            {
                TempData["Toast.Ok"] = "0";
                TempData["Toast.Message"] = "Không tìm thấy sản phẩm.";
                return RedirectToAction(nameof(Index));
            }

            _db.Products.Remove(entity);
            await _db.SaveChangesAsync();

            TempData["Toast.Ok"] = "1";
            TempData["Toast.Message"] = "Đã xóa sản phẩm.";
            return RedirectToAction(nameof(Index));
        }

        private async Task ValidateProductAsync(Product input, Guid? currentId)
        {
            if (string.IsNullOrWhiteSpace(input.Name))
                ModelState.AddModelError(nameof(Product.Name), "Tên sản phẩm là bắt buộc.");

            if (string.IsNullOrWhiteSpace(input.Slug) && !string.IsNullOrWhiteSpace(input.Name))
                input.Slug = Slugify(input.Name);

            if (string.IsNullOrWhiteSpace(input.Slug))
                ModelState.AddModelError(nameof(Product.Slug), "Slug là bắt buộc.");

            if (input.Price < 0)
                ModelState.AddModelError(nameof(Product.Price), "Giá không hợp lệ.");

            if (input.SalePrice.HasValue && input.SalePrice.Value < 0)
                ModelState.AddModelError(nameof(Product.SalePrice), "Giá sale không hợp lệ.");

            if (input.SalePrice.HasValue && input.SalePrice.Value > input.Price)
                ModelState.AddModelError(nameof(Product.SalePrice), "Giá sale phải <= giá gốc.");

            if (!string.IsNullOrWhiteSpace(input.Slug))
            {
                var slug = input.Slug.Trim();
                var exists = await _db.Products.AnyAsync(p =>
                    p.Slug == slug && (!currentId.HasValue || p.Id != currentId.Value));

                if (exists)
                    ModelState.AddModelError(nameof(Product.Slug), "Slug đã tồn tại. Vui lòng chọn slug khác.");
            }
        }

        private static string Slugify(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return string.Empty;

            string normalized = text.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder();

            foreach (var c in normalized)
            {
                var uc = CharUnicodeInfo.GetUnicodeCategory(c);
                if (uc != UnicodeCategory.NonSpacingMark) sb.Append(c);
            }

            var noDiacritics = sb.ToString()
                .Normalize(NormalizationForm.FormC)
                .Replace('đ', 'd');

            noDiacritics = Regex.Replace(noDiacritics, @"[^a-z0-9\s-]", "");
            noDiacritics = Regex.Replace(noDiacritics, @"\s+", "-");
            noDiacritics = Regex.Replace(noDiacritics, @"-+", "-").Trim('-');

            return noDiacritics;
        }

      

    }
}
