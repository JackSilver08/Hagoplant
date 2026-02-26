using Hagoplant.DBcontext;
using Hagoplant.Models;
using Hagoplant.Services;
using Hagoplant.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Security.Claims;
using System.Text;
using System.Text.RegularExpressions;

namespace Hagoplant.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly HagoDbContext _db;
        private readonly IAuditService _audit;
        private readonly ILogger<AdminController> _logger;

        public AdminController(HagoDbContext db, IAuditService audit, ILogger<AdminController> logger)
        {
            _db = db;
            _audit = audit;
            _logger = logger;
        }

        // ========================
        // DASHBOARD
        // ========================
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var products = await _db.Products.AsNoTracking()
                .OrderByDescending(p => p.CreatedAt)
                .Take(10)
                .ToListAsync();

            var blogPosts = await _db.BlogPosts.AsNoTracking()
                .OrderByDescending(b => b.CreatedAt)
                .Take(10)
                .ToListAsync();

            var users = await _db.Users
                .AsNoTracking()
                .OrderByDescending(u => u.CreatedAt)
                .Take(10)
                .ToListAsync();

            var orders = await _db.Orders.AsNoTracking()
                .OrderByDescending(o => o.CreatedAt)
                .Take(10)
                .ToListAsync();

            var recentLogs = await _db.AuditLogs.AsNoTracking()
                .OrderByDescending(a => a.CreatedAt)
                .Take(20)
                .ToListAsync();

            // Statistics
            var totalUsers = await _db.Users.CountAsync();
            var totalOrders = await _db.Orders.CountAsync();
            var pendingOrders = await _db.Orders.CountAsync(o =>
                o.Status == OrderStatuses.Pending || o.Status == OrderStatuses.AwaitingPayment);
            var totalRevenue = await _db.Orders
                .Where(o => o.Status == OrderStatuses.Paid ||
                           o.Status == OrderStatuses.Processing ||
                           o.Status == OrderStatuses.Shipping ||
                           o.Status == OrderStatuses.Completed)
                .SumAsync(o => (decimal?)o.TotalAmount) ?? 0;
            var totalProducts = await _db.Products.CountAsync();
            var activeProducts = await _db.Products.CountAsync(p => p.IsActive);

            // Chart 1: Revenue last 6 months
            var sixMonthsAgo = DateTimeOffset.UtcNow.AddMonths(-6);
            var recentSales = await _db.Orders
                .Where(o => o.CreatedAt >= sixMonthsAgo &&
                            (o.Status == OrderStatuses.Paid ||
                             o.Status == OrderStatuses.Processing ||
                             o.Status == OrderStatuses.Shipping ||
                             o.Status == OrderStatuses.Completed))
                .GroupBy(o => new { o.CreatedAt.Year, o.CreatedAt.Month })
                .Select(g => new { 
                    g.Key.Year, 
                    g.Key.Month, 
                    Total = g.Sum(o => (decimal?)o.TotalAmount) ?? 0 
                })
                .ToListAsync();

            var revLabels = new List<string>();
            var revData = new List<decimal>();
            for (int i = 5; i >= 0; i--)
            {
                var dt = DateTime.Now.AddMonths(-i);
                revLabels.Add($"T{dt.Month}/{dt.Year}");
                var mtSales = recentSales.FirstOrDefault(x => x.Year == dt.Year && x.Month == dt.Month);
                revData.Add(mtSales?.Total ?? 0);
            }

            // Chart 2: Product statuses (Active vs Inactive)
            var pLabels = new List<string> { "Đang bán", "Ngừng bán" };
            var pData = new List<int> { activeProducts, totalProducts - activeProducts };

            var vm = new AdminDashboardVm
            {
                Products = products,
                BlogPosts = blogPosts,
                Users = users,
                Orders = orders,
                RecentAuditLogs = recentLogs,
                TotalUsers = totalUsers,
                TotalOrders = totalOrders,
                PendingOrders = pendingOrders,
                TotalRevenue = totalRevenue,
                TotalProducts = totalProducts,
                ActiveProducts = activeProducts,
                RevenueLabels = revLabels,
                RevenueData = revData,
                ProductDistributionLabels = pLabels,
                ProductDistributionData = pData
            };

            return View(vm);
        }

        // ========================
        // PRODUCT CRUD
        // ========================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateProduct(
            [Bind("Name,Slug,Description,Price,SalePrice,ImageUrl,IsActive,IsFeatured")] Product input)
        {
            await ValidateProductAsync(input, currentId: null);

            if (!ModelState.IsValid)
            {
                // Log chi tiết lỗi để debug trên server
                var errors = ModelState
                    .Where(e => e.Value?.Errors.Count > 0)
                    .Select(e => $"{e.Key}: {string.Join(", ", e.Value!.Errors.Select(x => x.ErrorMessage))}")
                    .ToList();
                _logger.LogWarning("CreateProduct ModelState invalid. Errors: {Errors}", string.Join(" | ", errors));

                // Hiển thị lỗi cụ thể để admin biết sửa gì
                var errorMsg = string.Join(" | ", ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage));

                TempData["Toast.Ok"] = "0";
                TempData["Toast.Message"] = string.IsNullOrWhiteSpace(errorMsg)
                    ? "Dữ liệu sản phẩm không hợp lệ."
                    : errorMsg;
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

            await _audit.LogAsync(
                AuditActions.ProductCreated,
                entityType: "Product",
                entityId: entity.Id.ToString(),
                details: $"Product created: {entity.Name} (slug: {entity.Slug})",
                result: AuditResult.Success);

            TempData["Toast.Ok"] = "1";
            TempData["Toast.Message"] = "Đã thêm sản phẩm.";
            return RedirectToAction(nameof(Index));
        }

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

            var oldValues = $"Name={entity.Name}, Price={entity.Price}";

            entity.Name = input.Name.Trim();
            entity.Slug = input.Slug.Trim();
            entity.Description = input.Description?.Trim();
            entity.Price = input.Price;
            entity.SalePrice = input.SalePrice;

            var newUrl = input.ImageUrl?.Trim();
            if (!string.IsNullOrWhiteSpace(newUrl))
                entity.ImageUrl = newUrl;

            entity.IsActive = input.IsActive;
            entity.IsFeatured = input.IsFeatured;

            await _db.SaveChangesAsync();

            await _audit.LogAsync(
                AuditActions.ProductUpdated,
                entityType: "Product",
                entityId: entity.Id.ToString(),
                details: $"Product updated. Old: {oldValues}, New: Name={entity.Name}, Price={entity.Price}",
                result: AuditResult.Success);

            TempData["Toast.Ok"] = "1";
            TempData["Toast.Message"] = "Đã cập nhật sản phẩm.";
            return RedirectToAction(nameof(Index));
        }

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

            // Soft delete: đánh dấu IsActive = false thay vì xóa thật
            // Tránh vỡ OrderHistory khi sản phẩm đã có trong đơn hàng
            entity.IsActive = false;
            await _db.SaveChangesAsync();

            await _audit.LogAsync(
                AuditActions.ProductDeleted,
                entityType: "Product",
                entityId: entity.Id.ToString(),
                details: $"Product soft-deleted: {entity.Name}",
                result: AuditResult.Success);

            TempData["Toast.Ok"] = "1";
            TempData["Toast.Message"] = "Đã ẩn sản phẩm (soft delete để bảo toàn lịch sử đơn hàng).";
            return RedirectToAction(nameof(Index));
        }

        // ========================
        // USER MANAGEMENT
        // ========================

        [HttpGet]
        public async Task<IActionResult> Users(
            int page = 1,
            string? search = null,
            string? role = null,
            bool? isActive = null)
        {
            const int pageSize = 20;

            var query = _db.Users.AsNoTracking().AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(u =>
                    EF.Functions.ILike(u.Email, $"%{search}%") ||
                    (u.FullName != null && EF.Functions.ILike(u.FullName, $"%{search}%")));
            }

            if (!string.IsNullOrWhiteSpace(role))
                query = query.Where(u => u.Role == role);

            if (isActive.HasValue)
                query = query.Where(u => u.IsActive == isActive.Value);

            var totalCount = await query.CountAsync();

            var users = await query
                .OrderByDescending(u => u.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var vm = new AdminUserListVm
            {
                Users = users,
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount,
                SearchQuery = search,
                RoleFilter = role,
                IsActiveFilter = isActive
            };

            return View(vm);
        }

        /// <summary>Thay đổi Role người dùng - chỉ Admin mới có quyền</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangeUserRole(Guid userId, string newRole)
        {
            // Validate role
            if (!UserRoles.AllRoles.Contains(newRole))
            {
                TempData["Toast.Ok"] = "0";
                TempData["Toast.Message"] = "Role không hợp lệ.";
                return RedirectToAction(nameof(Users));
            }

            // Chặn tự thay đổi role của mình
            var currentUserId = TryGetCurrentUserId();
            if (currentUserId == userId)
            {
                TempData["Toast.Ok"] = "0";
                TempData["Toast.Message"] = "Không thể thay đổi role của chính mình.";
                return RedirectToAction(nameof(Users));
            }

            var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (user == null)
            {
                TempData["Toast.Ok"] = "0";
                TempData["Toast.Message"] = "Người dùng không tồn tại.";
                return RedirectToAction(nameof(Users));
            }

            var oldRole = user.Role;
            user.Role = newRole;
            user.UpdatedAt = DateTimeOffset.UtcNow;
            await _db.SaveChangesAsync();

            await _audit.LogAsync(
                AuditActions.UserRoleChanged,
                entityType: "User",
                entityId: userId.ToString(),
                details: $"Role changed for {user.Email}: {oldRole} → {newRole}",
                result: AuditResult.Success);

            TempData["Toast.Ok"] = "1";
            TempData["Toast.Message"] = $"Đã thay đổi role của {user.Email} thành {newRole}.";
            return RedirectToAction(nameof(Users));
        }

        /// <summary>Kích hoạt/vô hiệu hóa tài khoản (thay thế xóa thật)</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleUserActive(Guid id)
        {
            var currentIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (Guid.TryParse(currentIdStr, out var currentId) && currentId == id)
            {
                TempData["Toast.Ok"] = "0";
                TempData["Toast.Message"] = "Không thể vô hiệu hóa tài khoản đang đăng nhập.";
                return RedirectToAction(nameof(Users));
            }

            var user = await _db.Users.FirstOrDefaultAsync(x => x.Id == id);
            if (user == null)
            {
                TempData["Toast.Ok"] = "0";
                TempData["Toast.Message"] = "Người dùng không tồn tại.";
                return RedirectToAction(nameof(Users));
            }

            user.IsActive = !user.IsActive;
            user.UpdatedAt = DateTimeOffset.UtcNow;
            await _db.SaveChangesAsync();

            var action = user.IsActive ? AuditActions.UserActivated : AuditActions.UserDeactivated;
            await _audit.LogAsync(
                action,
                entityType: "User",
                entityId: id.ToString(),
                details: $"User {user.Email} {(user.IsActive ? "activated" : "deactivated")}",
                result: AuditResult.Success);

            TempData["Toast.Ok"] = "1";
            TempData["Toast.Message"] = user.IsActive
                ? $"Đã kích hoạt tài khoản {user.Email}."
                : $"Đã vô hiệu hóa tài khoản {user.Email}.";
            return RedirectToAction(nameof(Users));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteUser(Guid id)
        {
            var currentIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (Guid.TryParse(currentIdStr, out var currentId) && currentId == id)
            {
                TempData["Toast.Ok"] = "0";
                TempData["Toast.Message"] = "Không thể xóa tài khoản đang đăng nhập.";
                return RedirectToAction(nameof(Users));
            }

            var user = await _db.Users.FirstOrDefaultAsync(x => x.Id == id);
            if (user == null)
            {
                TempData["Toast.Ok"] = "0";
                TempData["Toast.Message"] = "Người dùng không tồn tại.";
                return RedirectToAction(nameof(Users));
            }

            _db.Users.Remove(user);

            try
            {
                await _db.SaveChangesAsync();

                await _audit.LogAsync(
                    AuditActions.UserDeleted,
                    entityType: "User",
                    entityId: id.ToString(),
                    details: $"User permanently deleted: {user.Email}",
                    result: AuditResult.Success);

                TempData["Toast.Ok"] = "1";
                TempData["Toast.Message"] = "Đã xóa người dùng.";
            }
            catch (DbUpdateException)
            {
                TempData["Toast.Ok"] = "0";
                TempData["Toast.Message"] = "Không thể xóa do có dữ liệu liên quan. Nên dùng 'Vô hiệu hóa' thay thế.";
            }

            return RedirectToAction(nameof(Users));
        }

        // ========================
        // ORDER MANAGEMENT (State Machine)
        // ========================

        [HttpGet]
        public async Task<IActionResult> Orders(
            int page = 1,
            string? status = null,
            string? search = null)
        {
            const int pageSize = 20;

            var query = _db.Orders.AsNoTracking()
                .Include(o => o.Payments)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(status))
                query = query.Where(o => o.Status == status);

            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(o =>
                    (o.OrderNumber != null && EF.Functions.ILike(o.OrderNumber, $"%{search}%")) ||
                    (o.Email != null && EF.Functions.ILike(o.Email, $"%{search}%")) ||
                    (o.CustomerName != null && EF.Functions.ILike(o.CustomerName, $"%{search}%")));
            }

            var totalCount = await query.CountAsync();

            var orders = await query
                .OrderByDescending(o => o.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var vm = new AdminOrderListVm
            {
                Orders = orders,
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount,
                StatusFilter = status,
                SearchQuery = search
            };

            return View(vm);
        }

        /// <summary>Cập nhật trạng thái đơn hàng theo State Machine</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateOrderStatus(UpdateOrderStatusVm vm)
        {
            var order = await _db.Orders.FirstOrDefaultAsync(o => o.Id == vm.OrderId);
            if (order == null)
            {
                TempData["Toast.Ok"] = "0";
                TempData["Toast.Message"] = "Không tìm thấy đơn hàng.";
                return RedirectToAction(nameof(Orders));
            }

            // Validate state transition
            if (!OrderStatuses.CanTransition(order.Status, vm.NewStatus))
            {
                TempData["Toast.Ok"] = "0";
                TempData["Toast.Message"] = $"Không thể chuyển từ '{OrderStatuses.GetDisplayName(order.Status)}' sang '{OrderStatuses.GetDisplayName(vm.NewStatus)}'.";
                return RedirectToAction(nameof(Orders));
            }

            var oldStatus = order.Status;
            var now = DateTimeOffset.UtcNow;
            var adminUserId = TryGetCurrentUserId();

            order.Status = vm.NewStatus;
            order.UpdatedAt = now;

            if (!string.IsNullOrWhiteSpace(vm.AdminNotes))
                order.AdminNotes = vm.AdminNotes;

            // Set timestamp theo trạng thái
            switch (vm.NewStatus)
            {
                case OrderStatuses.Paid:
                    order.ConfirmedAt = now;
                    order.ConfirmedByUserId = adminUserId;
                    break;
                case OrderStatuses.Shipping:
                    order.ShippedAt = now;
                    break;
                case OrderStatuses.Completed:
                    order.CompletedAt = now;
                    break;
                case OrderStatuses.Cancelled:
                    order.CancelledAt = now;
                    order.CancelReason = vm.CancelReason ?? "Admin hủy";
                    break;
                case OrderStatuses.Refunded:
                    order.RefundedAt = now;
                    break;
            }

            await _db.SaveChangesAsync();

            await _audit.LogAsync(
                AuditActions.OrderStatusChanged,
                entityType: "Order",
                entityId: vm.OrderId.ToString(),
                details: $"Order status changed: {oldStatus} → {vm.NewStatus}. Order: {order.OrderNumber}. Notes: {vm.AdminNotes}",
                result: AuditResult.Success);

            TempData["Toast.Ok"] = "1";
            TempData["Toast.Message"] = $"Đã cập nhật trạng thái đơn hàng thành '{OrderStatuses.GetDisplayName(vm.NewStatus)}'.";
            return RedirectToAction(nameof(Orders));
        }

        // ========================
        // AUDIT LOG VIEW
        // ========================

        [HttpGet]
        public async Task<IActionResult> AuditLogs(
            int page = 1,
            string? action = null,
            string? userEmail = null,
            DateTime? from = null,
            DateTime? to = null)
        {
            const int pageSize = 50;

            var query = _db.AuditLogs.AsNoTracking().AsQueryable();

            if (!string.IsNullOrWhiteSpace(action))
                query = query.Where(a => a.Action == action);

            if (!string.IsNullOrWhiteSpace(userEmail))
                query = query.Where(a => a.UserEmail != null && EF.Functions.ILike(a.UserEmail, $"%{userEmail}%"));

            if (from.HasValue)
                query = query.Where(a => a.CreatedAt >= new DateTimeOffset(from.Value, TimeSpan.Zero));

            if (to.HasValue)
                query = query.Where(a => a.CreatedAt <= new DateTimeOffset(to.Value.AddDays(1), TimeSpan.Zero));

            var totalCount = await query.CountAsync();
            var logs = await query
                .OrderByDescending(a => a.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            ViewBag.Logs = logs;
            ViewBag.TotalCount = totalCount;
            ViewBag.Page = page;
            ViewBag.PageSize = pageSize;
            ViewBag.TotalPages = (int)Math.Ceiling((double)totalCount / pageSize);
            ViewBag.ActionFilter = action;
            ViewBag.UserEmailFilter = userEmail;

            return View();
        }

        // ========================
        // BLOG CRUD
        // ========================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateBlogPost(
        [Bind("Title,Slug,Excerpt,ContentHtml,CoverImageUrl,Status,PublishedAt")] BlogPost input)
        {
            input.Title = (input.Title ?? "").Trim();
            input.Slug = (input.Slug ?? "").Trim();
            input.Excerpt = string.IsNullOrWhiteSpace(input.Excerpt) ? null : input.Excerpt.Trim();
            input.ContentHtml = (input.ContentHtml ?? "").Trim();
            input.CoverImageUrl = string.IsNullOrWhiteSpace(input.CoverImageUrl) ? null : input.CoverImageUrl.Trim();

            if (string.IsNullOrWhiteSpace(input.Slug) && !string.IsNullOrWhiteSpace(input.Title))
                input.Slug = Slugify(input.Title);

            input.Status = NormalizeStatus(input.Status);

            await ValidateBlogPostAsync(input, currentId: null);

            if (!ModelState.IsValid)
            {
                TempData["Toast.Ok"] = "0";
                TempData["Toast.Message"] = "Dữ liệu bài viết không hợp lệ.";
                return RedirectToAction(nameof(Index));
            }

            var now = DateTimeOffset.UtcNow;
            DateTimeOffset? publishedAt = input.Status == BlogPostStatuses.Published
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
                AuthorUserId = TryGetCurrentUserId()
            };

            _db.BlogPosts.Add(entity);
            await _db.SaveChangesAsync();

            await _audit.LogAsync(
                AuditActions.BlogPostCreated,
                entityType: "BlogPost",
                entityId: entity.Id.ToString(),
                details: $"Blog post created: {entity.Title}",
                result: AuditResult.Success);

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

            if (newStatus == BlogPostStatuses.Draft)
            {
                await _db.BlogPosts
                    .Where(b => b.Id == id)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(b => b.PublishedAt, (DateTimeOffset?)null));
            }
            else if (newStatus == BlogPostStatuses.Published)
            {
                await _db.BlogPosts
                    .Where(b => b.Id == id && b.PublishedAt == null)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(b => b.PublishedAt, now));
            }

            await _audit.LogAsync(
                AuditActions.BlogPostUpdated,
                entityType: "BlogPost",
                entityId: id.ToString(),
                details: $"Blog post updated. New status: {newStatus}",
                result: AuditResult.Success);

            TempData["Toast.Ok"] = "1";
            TempData["Toast.Message"] = "Đã cập nhật bài viết blog.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteBlogPost(Guid id)
        {
            var post = await _db.BlogPosts.FirstOrDefaultAsync(b => b.Id == id);
            if (post != null)
            {
                await _audit.LogAsync(
                    AuditActions.BlogPostDeleted,
                    entityType: "BlogPost",
                    entityId: id.ToString(),
                    details: $"Blog post deleted: {post.Title}",
                    result: AuditResult.Success);
            }

            var rows = await _db.BlogPosts
                .Where(x => x.Id == id)
                .ExecuteDeleteAsync();

            TempData["Toast.Ok"] = rows > 0 ? "1" : "0";
            TempData["Toast.Message"] = rows > 0 ? "Đã xóa bài viết blog." : "Không tìm thấy bài viết.";
            return RedirectToAction(nameof(Index));
        }

        // ========================
        // HELPER METHODS
        // ========================

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

            var st = NormalizeStatus(input.Status);
            var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "draft", "published", "archived" };
            if (!allowed.Contains(st))
                ModelState.AddModelError(nameof(BlogPost.Status), "Trạng thái không hợp lệ.");

            if (!string.IsNullOrWhiteSpace(input.Slug))
            {
                var slug = input.Slug.Trim();
                var exists = await _db.BlogPosts.AnyAsync(b =>
                    b.Slug == slug && (!currentId.HasValue || b.Id != currentId.Value));
                if (exists)
                    ModelState.AddModelError(nameof(BlogPost.Slug), "Slug đã tồn tại.");
            }
        }

        private static string NormalizeStatus(string? s) => (s ?? "").Trim().ToLowerInvariant();

        private Guid? TryGetCurrentUserId()
        {
            var raw = User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return Guid.TryParse(raw, out var g) ? g : null;
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
