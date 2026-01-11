using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Hagoplant.Models
{
    [Table("vouchers", Schema = "hago")]
    public class Voucher
    {
        [Key]
        [Column("id")]
        public Guid Id { get; set; }

        [Required]
        [Column("code")]
        public string Code { get; set; } = string.Empty;

        [Column("name")]
        public string? Name { get; set; }

        [Column("discount_type")]
        public string? DiscountType { get; set; }

        [Precision(12, 2)]
        [Column("discount_value")]
        public decimal DiscountValue { get; set; }

        [Precision(12, 2)]
        [Column("min_order_amount")]
        public decimal? MinOrderAmount { get; set; }

        [Precision(12, 2)]
        [Column("max_discount_amount")]
        public decimal? MaxDiscountAmount { get; set; }

        [Column("start_at")]
        public DateTimeOffset? StartAt { get; set; }

        [Column("end_at")]
        public DateTimeOffset? EndAt { get; set; }

        [Column("usage_limit_total")]
        public int? UsageLimitTotal { get; set; }

        [Column("usage_limit_per_user")]
        public int? UsageLimitPerUser { get; set; }

        [Column("used_count")]
        public int? UsedCount { get; set; }

        [Column("is_active")]
        public bool IsActive { get; set; }

        [Column("created_at")]
        public DateTimeOffset CreatedAt { get; set; }

        // Navigation
        public ICollection<Order> Orders { get; set; } = new List<Order>();
    }
}
