using Hagoplant.DBcontext;
using Hagoplant.Models;
using Hagoplant.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Security.Claims;
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
                    .ToListAsync(),

                BlogPosts = await _db.BlogPosts.AsNoTracking()
                    .OrderByDescending(b => b.CreatedAt)
                    .ToListAsync(),

                  // THÊM KHỐI NÀY
                Users = await _db.Users.AsNoTracking()
                    .OrderByDescending(u => u.CreatedAt)   // nếu User không có CreatedAt thì đổi field khác
                    .ToListAsync()
            };


            Console.WriteLine($"[Admin/Index] Products={vm.Products.Count}, BlogPosts={vm.BlogPosts.Count}");
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


        // =========================
        // BLOG CRUD
        // =========================

        // THÊM BLOG
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateBlogPost(
            [Bind("Title,Slug,Excerpt,ContentHtml,CoverImageUrl,Status,PublishedAt")] BlogPost input)
        {
            await ValidateBlogPostAsync(input, currentId: null);

            if (!ModelState.IsValid)
            {
                TempData["Toast.Ok"] = "0";
                TempData["Toast.Message"] = "Dữ liệu bài viết không hợp lệ.";
                return RedirectToAction(nameof(Index));
            }

            var now = DateTimeOffset.UtcNow;

            var status = NormalizeStatus(input.Status);

            var entity = new BlogPost
            {
                Id = Guid.NewGuid(),
                Title = input.Title.Trim(),
                Slug = input.Slug.Trim(),
                Excerpt = string.IsNullOrWhiteSpace(input.Excerpt) ? null : input.Excerpt.Trim(),
                ContentHtml = input.ContentHtml.Trim(),
                CoverImageUrl = string.IsNullOrWhiteSpace(input.CoverImageUrl) ? null : input.CoverImageUrl.Trim(),
                Status = status,

                // published_at:
                // - Nếu published thì set now (hoặc dùng input.PublishedAt nếu bạn muốn truyền từ client)
                // - Nếu draft/archived thì để null
                PublishedAt = status == "published" ? (input.PublishedAt ?? now) : null,

                CreatedAt = now,
                UpdatedAt = now
            };

            // (Tuỳ chọn) gán author_user_id nếu bạn có userId dạng Guid trong claims NameIdentifier
            var authorId = TryGetCurrentUserId();
            if (authorId.HasValue)
                entity.AuthorUserId = authorId.Value;

            _db.BlogPosts.Add(entity);
            await _db.SaveChangesAsync();

            TempData["Toast.Ok"] = "1";
            TempData["Toast.Message"] = "Đã tạo bài viết blog.";
            return RedirectToAction(nameof(Index));
        }

        // SỬA BLOG
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateBlogPost(
            Guid id,
            [Bind("Title,Slug,Excerpt,ContentHtml,CoverImageUrl,Status")] BlogPost input)
        {
            var entity = await _db.BlogPosts.FirstOrDefaultAsync(x => x.Id == id);
            if (entity == null)
            {
                TempData["Toast.Ok"] = "0";
                TempData["Toast.Message"] = "Không tìm thấy bài viết.";
                return RedirectToAction(nameof(Index));
            }

            await ValidateBlogPostAsync(input, currentId: id);

            if (!ModelState.IsValid)
            {
                TempData["Toast.Ok"] = "0";
                TempData["Toast.Message"] = "Dữ liệu cập nhật bài viết không hợp lệ.";
                return RedirectToAction(nameof(Index));
            }

            var now = DateTimeOffset.UtcNow;
            var newStatus = NormalizeStatus(input.Status);

            entity.Title = input.Title.Trim();
            entity.Slug = input.Slug.Trim();
            entity.Excerpt = string.IsNullOrWhiteSpace(input.Excerpt) ? null : input.Excerpt.Trim();
            entity.ContentHtml = input.ContentHtml.Trim();

            // Cho phép xoá ảnh bìa: nếu input.CoverImageUrl rỗng => set null
            entity.CoverImageUrl = string.IsNullOrWhiteSpace(input.CoverImageUrl) ? null : input.CoverImageUrl.Trim();

            // Xử lý published_at theo status:
            // - published: nếu trước đó chưa có PublishedAt thì set now
            // - draft: set PublishedAt = null
            // - archived: giữ PublishedAt nếu đã từng publish (không bắt buộc)
            if (newStatus == "published")
            {
                entity.PublishedAt ??= now;
            }
            else if (newStatus == "draft")
            {
                entity.PublishedAt = null;
            }
            // archived => giữ nguyên PublishedAt

            entity.Status = newStatus;
            entity.UpdatedAt = now;

            await _db.SaveChangesAsync();

            TempData["Toast.Ok"] = "1";
            TempData["Toast.Message"] = "Đã cập nhật bài viết blog.";
            return RedirectToAction(nameof(Index));
        }

        // XÓA BLOG
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteBlogPost(Guid id)
        {
            var entity = await _db.BlogPosts.FirstOrDefaultAsync(x => x.Id == id);
            if (entity == null)
            {
                TempData["Toast.Ok"] = "0";
                TempData["Toast.Message"] = "Không tìm thấy bài viết.";
                return RedirectToAction(nameof(Index));
            }

            _db.BlogPosts.Remove(entity);
            await _db.SaveChangesAsync();

            TempData["Toast.Ok"] = "1";
            TempData["Toast.Message"] = "Đã xóa bài viết blog.";
            return RedirectToAction(nameof(Index));
        }

        // =========================
        // BLOG VALIDATION HELPERS
        // =========================
        private async Task ValidateBlogPostAsync(BlogPost input, Guid? currentId)
        {
            if (string.IsNullOrWhiteSpace(input.Title))
                ModelState.AddModelError(nameof(BlogPost.Title), "Tiêu đề là bắt buộc.");

            if (string.IsNullOrWhiteSpace(input.Slug) && !string.IsNullOrWhiteSpace(input.Title))
                input.Slug = Slugify(input.Title);

            if (string.IsNullOrWhiteSpace(input.Slug))
                ModelState.AddModelError(nameof(BlogPost.Slug), "Slug là bắt buộc.");

            if (string.IsNullOrWhiteSpace(input.ContentHtml))
                ModelState.AddModelError(nameof(BlogPost.ContentHtml), "Nội dung bài viết là bắt buộc.");

            // status check
            var st = NormalizeStatus(input.Status);
            if (string.IsNullOrWhiteSpace(st))
                ModelState.AddModelError(nameof(BlogPost.Status), "Trạng thái là bắt buộc.");

            var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "draft", "published", "archived"
    };
            if (!allowed.Contains(st))
                ModelState.AddModelError(nameof(BlogPost.Status), "Trạng thái không hợp lệ (draft/published/archived).");

            // slug unique
            if (!string.IsNullOrWhiteSpace(input.Slug))
            {
                var slug = input.Slug.Trim();
                var exists = await _db.BlogPosts.AnyAsync(b =>
                    b.Slug == slug && (!currentId.HasValue || b.Id != currentId.Value));

                if (exists)
                    ModelState.AddModelError(nameof(BlogPost.Slug), "Slug đã tồn tại. Vui lòng chọn slug khác.");
            }
        }

        private static string NormalizeStatus(string? s)
        {
            return (s ?? "").Trim().ToLowerInvariant();
        }

        private Guid? TryGetCurrentUserId()
        {
            // Nếu bạn có lưu Guid userId trong ClaimTypes.NameIdentifier
            var raw = User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            return Guid.TryParse(raw, out var g) ? g : null;
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteUser(Guid id)
        {
            // Chặn tự xóa chính mình
            var currentIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (Guid.TryParse(currentIdStr, out var currentId) && currentId == id)
            {
                TempData["Toast.Ok"] = "0";
                TempData["Toast.Message"] = "Không thể xóa tài khoản đang đăng nhập.";
                return RedirectToAction(nameof(Index));
            }

            var user = await _db.Users.FirstOrDefaultAsync(x => x.Id == id);
            if (user == null)
            {
                TempData["Toast.Ok"] = "0";
                TempData["Toast.Message"] = "Người dùng không tồn tại.";
                return RedirectToAction(nameof(Index));
            }

            _db.Users.Remove(user);

            try
            {
                await _db.SaveChangesAsync();
                TempData["Toast.Ok"] = "1";
                TempData["Toast.Message"] = "Đã xóa người dùng.";
            }
            catch (DbUpdateException)
            {
                TempData["Toast.Ok"] = "0";
                TempData["Toast.Message"] = "Không thể xóa do có dữ liệu liên quan (FK). Nên dùng soft delete hoặc khóa tài khoản.";
            }

            return RedirectToAction(nameof(Index));
        }
    }
}
