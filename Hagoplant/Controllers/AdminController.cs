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
            Console.WriteLine("A: load products");
            var products = await _db.Products.AsNoTracking()
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();

            Console.WriteLine("B: load blogposts");
            var blogPosts = await _db.BlogPosts.AsNoTracking()
       .OrderByDescending(b => b.CreatedAt)
       .Select(b => new BlogPost
       {
           Id = b.Id,
           Title = b.Title,
           Slug = b.Slug,
           Excerpt = b.Excerpt,
           ContentHtml = b.ContentHtml,
           CoverImageUrl = b.CoverImageUrl,
           AuthorUserId = b.AuthorUserId,
           Status = b.Status,
           PublishedAt = b.PublishedAt,
           CreatedAt = b.CreatedAt,
           UpdatedAt = b.UpdatedAt,
           ViewCount = b.ViewCount
       })
       .ToListAsync();



            Console.WriteLine("C: load users");
            var users = await _db.Users
                .AsNoTracking()
                .OrderByDescending(u => u.CreatedAt)
                .Select(u => new User
                {
                    Id = u.Id,
                    Email = u.Email,
                    FullName = u.FullName,
                    Phone = u.Phone,
                    IsActive = u.IsActive,
                    CreatedAt = u.CreatedAt
                })
                .ToListAsync();


            var vm = new AdminDashboardVm
            {
                Products = products,
                BlogPosts = blogPosts,
                Users = users
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


        // =========================
        // BLOG CRUD
        // =========================

        // THÊM BLOG
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateBlogPost(
        [Bind("Title,Slug,Excerpt,ContentHtml,CoverImageUrl,Status,PublishedAt")] BlogPost input)
        {
            // Chuẩn hoá input sớm để validation dùng đúng
            input.Title = (input.Title ?? "").Trim();
            input.Slug = (input.Slug ?? "").Trim();
            input.Excerpt = string.IsNullOrWhiteSpace(input.Excerpt) ? null : input.Excerpt.Trim();
            input.ContentHtml = (input.ContentHtml ?? "").Trim();
            input.CoverImageUrl = string.IsNullOrWhiteSpace(input.CoverImageUrl) ? null : input.CoverImageUrl.Trim();

            // Nếu chưa có slug thì tự tạo từ title
            if (string.IsNullOrWhiteSpace(input.Slug) && !string.IsNullOrWhiteSpace(input.Title))
                input.Slug = Slugify(input.Title);

            // status chuẩn hoá
            input.Status = NormalizeStatus(input.Status);

            await ValidateBlogPostAsync(input, currentId: null);

            if (!ModelState.IsValid)
            {
                TempData["Toast.Ok"] = "0";
                TempData["Toast.Message"] = "Dữ liệu bài viết không hợp lệ.";
                return RedirectToAction(nameof(Index));
            }

            var now = DateTimeOffset.UtcNow;

            // PublishedAt theo rule:
            // - published: nếu không truyền thì set now
            // - draft/archived: null
            DateTimeOffset? publishedAt =
                input.Status == BlogPostStatuses.Published
                    ? (input.PublishedAt ?? now)
                    : null;

            var entity = new BlogPost
            {
                Id = Guid.NewGuid(),
                Title = input.Title,
                Slug = input.Slug,
                Excerpt = input.Excerpt,
                ContentHtml = input.ContentHtml,
                CoverImageUrl = input.CoverImageUrl,
                Status = input.Status,
                PublishedAt = publishedAt,
                CreatedAt = now,
                UpdatedAt = now,
                AuthorUserId = TryGetCurrentUserId() // null nếu chưa login/claim không phải Guid
            };

            _db.BlogPosts.Add(entity);
            await _db.SaveChangesAsync();

            TempData["Toast.Ok"] = "1";
            TempData["Toast.Message"] = "Đã tạo bài viết blog.";
            return RedirectToAction(nameof(Index));
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateBlogPost(
            Guid id,
            [Bind("Title,Slug,Excerpt,ContentHtml,CoverImageUrl,Status")] BlogPost input)
        {
            await ValidateBlogPostAsync(input, currentId: id);

            if (!ModelState.IsValid)
            {
                TempData["Toast.Ok"] = "0";
                TempData["Toast.Message"] = "Dữ liệu cập nhật bài viết không hợp lệ.";
                return RedirectToAction(nameof(Index));
            }

            var now = DateTimeOffset.UtcNow;
            var newStatus = NormalizeStatus(input.Status);

            // 1) Update các field cơ bản trước
            var rows = await _db.BlogPosts
                .Where(b => b.Id == id)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(b => b.Title, (input.Title ?? "").Trim())
                    .SetProperty(b => b.Slug, (input.Slug ?? "").Trim())
                    .SetProperty(b => b.Excerpt, string.IsNullOrWhiteSpace(input.Excerpt) ? null : input.Excerpt.Trim())
                    .SetProperty(b => b.ContentHtml, (input.ContentHtml ?? "").Trim())
                    .SetProperty(b => b.CoverImageUrl, string.IsNullOrWhiteSpace(input.CoverImageUrl) ? null : input.CoverImageUrl.Trim())
                    .SetProperty(b => b.Status, newStatus)
                    .SetProperty(b => b.UpdatedAt, now)
                );

            if (rows == 0)
            {
                TempData["Toast.Ok"] = "0";
                TempData["Toast.Message"] = "Không tìm thấy bài viết.";
                return RedirectToAction(nameof(Index));
            }

            // 2) Update PublishedAt theo status (tách ra cho dễ, tránh expression phức tạp)
            if (newStatus == BlogPostStatuses.Draft)
            {
                await _db.BlogPosts
                    .Where(b => b.Id == id)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(b => b.PublishedAt, (DateTimeOffset?)null)
                    );
            }
            else if (newStatus == BlogPostStatuses.Published)
            {
                // Chỉ set now nếu PublishedAt đang null
                await _db.BlogPosts
                    .Where(b => b.Id == id && b.PublishedAt == null)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(b => b.PublishedAt, now)
                    );
            }
            // archived: không đụng PublishedAt

            TempData["Toast.Ok"] = "1";
            TempData["Toast.Message"] = "Đã cập nhật bài viết blog.";
            return RedirectToAction(nameof(Index));
        }




        // XÓA BLOG
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteBlogPost(Guid id)
        {
            var rows = await _db.BlogPosts
                .Where(x => x.Id == id)
                .ExecuteDeleteAsync();

            TempData["Toast.Ok"] = rows > 0 ? "1" : "0";
            TempData["Toast.Message"] = rows > 0 ? "Đã xóa bài viết blog." : "Không tìm thấy bài viết.";
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
