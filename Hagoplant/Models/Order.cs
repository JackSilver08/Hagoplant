using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

namespace Hagoplant.Models
{
    [Table("orders", Schema = "hago")]
    public class Order
    {
        [Key]
        [Column("id")]
        public Guid Id { get; set; }

        [Column("order_number")]
        public string? OrderNumber { get; set; }

        [Column("user_id")]
        public Guid? UserId { get; set; }

        [Column("customer_name")]
        public string? CustomerName { get; set; }

        [Column("phone")]
        public string? Phone { get; set; }

        [Column("email")]
        public string? Email { get; set; }

        // jsonb
        [Column("shipping_address", TypeName = "jsonb")]
        public JsonDocument? ShippingAddress { get; set; }

        [Column("status")]
        public string? Status { get; set; }

        [Column("payment_method")]
        public string? PaymentMethod { get; set; }

        [Column("payment_status")]
        public string? PaymentStatus { get; set; }

        [Precision(12, 2)]
        [Column("subtotal")]
        public decimal Subtotal { get; set; }

        [Precision(12, 2)]
        [Column("discount_amount")]
        public decimal DiscountAmount { get; set; }

        [Precision(12, 2)]
        [Column("total_amount")]
        public decimal TotalAmount { get; set; }

        [Column("voucher_id")]
        public Guid? VoucherId { get; set; }

        [Column("voucher_code_snapshot")]
        public string? VoucherCodeSnapshot { get; set; }

        // jsonb
        [Column("items_json", TypeName = "jsonb")]
        public JsonDocument? ItemsJson { get; set; }

        [Column("confirmed_at")]
        public DateTimeOffset? ConfirmedAt { get; set; }

        [Column("confirmed_by_user_id")]
        public Guid? ConfirmedByUserId { get; set; }

        [Column("created_at")]
        public DateTimeOffset CreatedAt { get; set; }

        // =========================
        // NAVIGATION (PHẢI CHỈ RÕ FK)
        // =========================

        [ForeignKey(nameof(UserId))]
        public User? User { get; set; }

        [ForeignKey(nameof(ConfirmedByUserId))]
        public User? ConfirmedByUser { get; set; }

        [ForeignKey(nameof(VoucherId))]
        public Voucher? Voucher { get; set; }

        public ICollection<Payment> Payments { get; set; } = new List<Payment>();
    }
}
