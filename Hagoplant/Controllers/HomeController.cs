using Hagoplant.DBcontext;
using Hagoplant.Models;
using Hagoplant.Models.ViewModels;
using Hagoplant.Services;
using Hagoplant.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;

namespace Hagoplant.Controllers
{
    public class HomeController : Controller
    {
        private readonly HagoDbContext _db;
        private readonly PayOsClient _payOs;
        public HomeController(HagoDbContext db, PayOsClient payOs)
        {
            _db = db;
            _payOs = payOs;
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
        [Authorize]
        public async Task<IActionResult> Checkout()
        {
            var cart = await BuildCartVmAsync();
            if (cart.IsEmpty) return RedirectToAction(nameof(Index));

            var vm = new CheckoutPageVm
            {
                Cart = cart,
                Form = new CheckoutVm()
            };

            return View(vm);
        }

        private Guid? GetUserGuid()
        {
            var raw = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return Guid.TryParse(raw, out var id) ? id : null;
        }

        private string? GetUserEmail()
        {
            var email =
                User.FindFirstValue(ClaimTypes.Email)
                ?? User.FindFirstValue("email")
                ?? User.FindFirstValue(ClaimTypes.Upn)
                ?? User.Identity?.Name;

            email = email?.Trim();
            return string.IsNullOrWhiteSpace(email) ? null : email;
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

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> Checkout(CheckoutPageVm vm)
        {
            var cart = await BuildCartVmAsync();
            if (cart.IsEmpty) return RedirectToAction(nameof(Index));

            var total = cart.Total;
            var amountVnd = (int)decimal.Round(total, 0, MidpointRounding.AwayFromZero);

            // ✅ Lấy info từ claims (nếu đã đăng nhập)
            var claimUserId = GetUserGuid();
            var claimEmail = GetUserEmail();

            // ✅ Fallback email: claims > form
            var formEmail = vm.Form?.Email?.Trim();
            var finalEmail = !string.IsNullOrWhiteSpace(claimEmail) ? claimEmail : formEmail;

            // (khuyến nghị) nếu không có email thì chặn luôn vì History không tra được
            if (string.IsNullOrWhiteSpace(finalEmail))
            {
                ModelState.AddModelError("Form.Email", "Email là bắt buộc để tra cứu lịch sử đơn hàng.");
                vm.Cart = cart;                 // trả lại cart để View không lỗi
                return View(vm);                // trả về trang Checkout
            }

            var order = new Order
            {
                Id = Guid.NewGuid(),
                OrderNumber = $"HAGO-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}",

                // ✅ QUAN TRỌNG: gắn user
                UserId = claimUserId,

                CustomerName = vm.Form.CustomerName,
                Phone = vm.Form.Phone,

                // ✅ QUAN TRỌNG: email phải lấy finalEmail
                Email = finalEmail,

                ShippingAddress = JsonDocument.Parse(JsonSerializer.Serialize(vm.Form.ShippingAddress)),
                ItemsJson = JsonDocument.Parse(JsonSerializer.Serialize(cart.Items)),
                Subtotal = cart.Subtotal,
                DiscountAmount = cart.DiscountAmount,
                TotalAmount = cart.Total,
                Status = "PENDING",
                PaymentMethod = "BANK_QR",
                PaymentStatus = "UNPAID",
                CreatedAt = DateTimeOffset.UtcNow
            };

            _db.Orders.Add(order);

            var orderCode = int.Parse(DateTime.UtcNow.ToString("HHmmssfff"));
            var returnUrl = Url.Action("PayReturn", "Payments", new { orderId = order.Id }, Request.Scheme)!;
            var cancelUrl = Url.Action("PayCancel", "Payments", new { orderId = order.Id }, Request.Scheme)!;

            var pay = await _payOs.CreatePaymentAsync(
                orderCode: orderCode,
                amount: amountVnd,
                description: $"HAGO{orderCode}",
                cancelUrl: cancelUrl,
                returnUrl: returnUrl
            );

            var payment = new Payment
            {
                Id = Guid.NewGuid(),
                OrderId = order.Id,
                Provider = "payos",
                Method = "BANK_QR",
                Amount = amountVnd,
                Status = "PENDING",
                TransactionRef = pay.data.paymentLinkId,
                RawResponse = JsonDocument.Parse(JsonSerializer.Serialize(pay)),
                CreatedAt = DateTimeOffset.UtcNow
            };
            _db.Payments.Add(payment);

            await _db.SaveChangesAsync();

            return View("CheckoutPayQr", new CheckoutPayQrVm
            {
                OrderId = order.Id,
                OrderNumber = order.OrderNumber ?? "",
                Amount = amountVnd,
                QrPayload = pay.data.qrCode,
                CheckoutUrl = pay.data.checkoutUrl
            });
        }

        [HttpGet]
        public async Task<IActionResult> PaymentStatus(Guid orderId)
        {
            // 1) đọc payment mới nhất của order
            var payment = await _db.Payments
                .Include(p => p.Order)
                .Where(p => p.OrderId == orderId)
                .OrderByDescending(p => p.CreatedAt)
                .FirstOrDefaultAsync();

            if (payment == null)
                return NotFound(new { ok = false, message = "Payment not found" });

            // 2) Kểm tra qua PayOS nếu đang PENDING
            if (payment.Status == "PENDING" && payment.Provider == "payos" && !string.IsNullOrEmpty(payment.TransactionRef))
            {
                var payOsStatus = await _payOs.GetPaymentAsync(payment.TransactionRef);
                if (payOsStatus?.data != null && payOsStatus.data.status == "PAID")
                {
                    payment.Status = "PAID";
                    payment.PaidAt = DateTimeOffset.UtcNow;
                    if (payment.Order != null)
                    {
                        payment.Order.PaymentStatus = "PAID";
                        payment.Order.Status = "PAID";
                    }
                    await _db.SaveChangesAsync();
                }
            }

            // 3) trả status hiện tại trong DB
            return Json(new
            {
                ok = true,
                paymentStatus = payment.Status,
                paidAt = payment.PaidAt
            });
        }

    }
}
