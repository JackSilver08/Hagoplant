using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;
using Hagoplant.DBcontext;
using Hagoplant.Models;
using Hagoplant.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Hagoplant.Controllers
{
    [Authorize]
    public class OrderController : Controller
    {
        private readonly HagoDbContext _db;

        private static readonly JsonSerializerOptions _jsonOpts = new()
        {
            PropertyNameCaseInsensitive = true
        };

        public OrderController(HagoDbContext db)
        {
            _db = db;
        }

        [HttpGet]
        public async Task<IActionResult> History()
        {
            var userId = GetUserGuid();
            var email = GetUserEmail();

            // Query base
            var q = _db.Orders
                .AsNoTracking()
                .Include(o => o.Payments)
                .AsQueryable();

            // Ưu tiên theo UserId nếu có, fallback theo Email
            if (userId.HasValue)
            {
                q = q.Where(o => o.UserId == userId.Value);
            }
            else if (!string.IsNullOrWhiteSpace(email))
            {
                q = q.Where(o => o.Email != null && EF.Functions.ILike(o.Email, email));
            }
            else
            {
                // Không có key để lọc => trả view rỗng (hoặc redirect)
                return View(new OrderHistoryVm { Orders = new List<OrderCardVm>() });
            }

            var orders = await q
                .OrderByDescending(o => o.CreatedAt)
                .ToListAsync();

            var vm = new OrderHistoryVm
            {
                Orders = orders.Select(MapToCardVm).ToList()
            };

            return View("~/Views/Home/OrderHistory.cshtml", vm);

        }



        private Guid? GetUserGuid()
        {
            var raw = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return Guid.TryParse(raw, out var id) ? id : null;
        }

        private string? GetUserEmail()
        {
            // lấy email từ nhiều claim tuỳ hệ auth
            var email =
                User.FindFirstValue(ClaimTypes.Email)
                ?? User.FindFirstValue("email")
                ?? User.FindFirstValue(ClaimTypes.Upn)
                ?? User.FindFirstValue(ClaimTypes.Name);

            email = email?.Trim();
            return string.IsNullOrWhiteSpace(email) ? null : email;
        }

        private OrderCardVm MapToCardVm(Order o)
        {
            return new OrderCardVm
            {
                Id = o.Id,
                OrderNumber = o.OrderNumber ?? o.Id.ToString("N")[..8].ToUpperInvariant(),
                CreatedAt = o.CreatedAt,
                ConfirmedAt = o.ConfirmedAt,

                Status = o.Status,
                PaymentMethod = o.PaymentMethod,
                PaymentStatus = o.PaymentStatus,

                Subtotal = o.Subtotal,
                DiscountAmount = o.DiscountAmount,
                TotalAmount = o.TotalAmount,

                VoucherCodeSnapshot = o.VoucherCodeSnapshot,

                CustomerName = o.CustomerName,
                Phone = o.Phone,
                Email = o.Email,

                Shipping = ReadShipping(o.ShippingAddress),
                Items = ReadItems(o.ItemsJson),

                Payments = o.Payments?
                    .OrderByDescending(p => p.CreatedAt)
                    .Select(p => new PaymentVm
                    {
                        Provider = p.Provider,
                        Method = p.Method,
                        Amount = p.Amount,
                        Status = p.Status,
                        TransactionRef = p.TransactionRef,
                        PaidAt = p.PaidAt,
                        CreatedAt = p.CreatedAt
                    }).ToList() ?? new List<PaymentVm>()
            };
        }

        private ShippingAddressVm? ReadShipping(JsonDocument? doc)
        {
            if (doc == null) return null;
            try
            {
                return JsonSerializer.Deserialize<ShippingAddressVm>(doc.RootElement.GetRawText(), _jsonOpts);
            }
            catch
            {
                return null;
            }
        }

        private List<OrderItemVm> ReadItems(JsonDocument? doc)
        {
            if (doc == null) return new List<OrderItemVm>();
            var raw = doc.RootElement.GetRawText();

            try
            {
                var arr = JsonSerializer.Deserialize<List<OrderItemJson>>(raw, _jsonOpts);
                if (arr != null) return arr.Select(ToVm).ToList();
            }
            catch { }

            try
            {
                var wrap = JsonSerializer.Deserialize<OrderItemsWrapper>(raw, _jsonOpts);
                if (wrap?.Items != null) return wrap.Items.Select(ToVm).ToList();
            }
            catch { }

            return new List<OrderItemVm>();
        }

        private static OrderItemVm ToVm(OrderItemJson x) => new OrderItemVm
        {
            ProductId = x.ProductId,
            Name = x.Name,
            Slug = x.Slug,
            ImageUrl = x.ImageUrl,
            Quantity = x.Quantity,
            UnitPrice = x.UnitPriceSnapshot > 0 ? x.UnitPriceSnapshot : x.UnitPrice
        };

        private class OrderItemsWrapper
        {
            public List<OrderItemJson>? Items { get; set; }
        }

        private class OrderItemJson
        {
            public Guid? ProductId { get; set; }

            public string? Name { get; set; }
            public string? ProductName { get => Name; set => Name = value; }

            public string? Slug { get; set; }
            public string? ProductSlug { get => Slug; set => Slug = value; }

            public string? ImageUrl { get; set; }

            public int Quantity { get; set; }

            public decimal UnitPriceSnapshot { get; set; }
            public decimal UnitPrice { get; set; }
        }
    }
}
