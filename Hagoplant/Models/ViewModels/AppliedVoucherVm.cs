using System;

namespace Hagoplant.Models.ViewModels
{
    public class AppliedVoucherVm
    {
        public Guid VoucherId { get; set; }

        public string? Code { get; set; }
        public string? Name { get; set; }

        public string? DiscountType { get; set; }
        public decimal DiscountValue { get; set; }

        public decimal? MinOrderAmount { get; set; }
        public decimal? MaxDiscountAmount { get; set; }

        public DateTimeOffset? StartAt { get; set; }
        public DateTimeOffset? EndAt { get; set; }

        public int? UsageLimitTotal { get; set; }
        public int? UsageLimitPerUser { get; set; }
        public int? UsedCount { get; set; }

        public bool IsActive { get; set; }
        public DateTimeOffset? CreatedAt { get; set; }

        public decimal ComputeDiscount(decimal subtotal)
        {
            if (subtotal <= 0) return 0m;
            if (MinOrderAmount.HasValue && subtotal < MinOrderAmount.Value) return 0m;

            var type = (DiscountType ?? "").Trim().ToLowerInvariant();

            decimal discount;
            if (type == "percent" || type == "percentage")
                discount = subtotal * (DiscountValue / 100m);
            else
                discount = DiscountValue;

            if (MaxDiscountAmount.HasValue)
                discount = Math.Min(discount, MaxDiscountAmount.Value);

            discount = Math.Min(discount, subtotal);
            return Math.Max(0m, discount);
        }
    }
}
