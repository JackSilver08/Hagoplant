using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;

namespace Hagoplant.Models.ViewModels
{
    public class CartVm
    {
        public Guid CartId { get; set; }
        public Guid? UserId { get; set; }
        public Guid? GuestToken { get; set; }

        public DateTimeOffset? CreatedAt { get; set; }
        public DateTimeOffset? UpdatedAt { get; set; }

        public List<CartItemVm> Items { get; set; } = new();

        [Display(Name = "Mã giảm giá")]
        [StringLength(50)]
        public string? VoucherCodeInput { get; set; }

        public AppliedVoucherVm? Voucher { get; set; }
        public string? VoucherMessage { get; set; }
        public bool VoucherAppliedOk { get; set; }

        public int TotalQuantity => Items?.Sum(x => x.Quantity) ?? 0;
        public decimal Subtotal => Items?.Sum(x => x.LineTotal) ?? 0m;

        public decimal DiscountAmount { get; set; }
        public decimal Total => Math.Max(0m, Subtotal - DiscountAmount);

        public bool IsEmpty => Items == null || Items.Count == 0;
    }

    public class CartItemVm
    {
        public Guid CartItemId { get; set; }
        public Guid CartId { get; set; }
        public Guid ProductId { get; set; }

        [Range(1, int.MaxValue)]
        public int Quantity { get; set; }

        public decimal UnitPriceSnapshot { get; set; }

        // Dùng đúng theo Cart.cshtml
        public string? Name { get; set; }
        public string? Slug { get; set; }
        public string? ImageUrl { get; set; }

        public decimal? CurrentPrice { get; set; }
        public decimal? CurrentSalePrice { get; set; }
        public bool? IsActive { get; set; }

        public decimal UnitPrice => UnitPriceSnapshot;
        public decimal LineTotal => UnitPrice * Quantity;

        // Alias tương thích nếu code cũ map ProductName/ProductSlug
        public string? ProductName { get => Name; set => Name = value; }
        public string? ProductSlug { get => Slug; set => Slug = value; }
    }
}
