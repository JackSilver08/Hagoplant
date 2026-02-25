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

        /// <summary>
        /// Order status state machine:
        /// PENDING → AWAITING_PAYMENT → PAID → PROCESSING → SHIPPING → COMPLETED
        ///                           ↘ CANCELLED (từ bất kỳ trạng thái nào)
        ///                                              ↘ REFUNDED (từ PAID/PROCESSING)
        /// </summary>
        [Column("status")]
        public string Status { get; set; } = OrderStatuses.Pending;

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

        // jsonb - lưu snapshot items tại thời điểm đặt hàng
        [Column("items_json", TypeName = "jsonb")]
        public JsonDocument? ItemsJson { get; set; }

        [Column("confirmed_at")]
        public DateTimeOffset? ConfirmedAt { get; set; }

        [Column("confirmed_by_user_id")]
        public Guid? ConfirmedByUserId { get; set; }

        [Column("shipped_at")]
        public DateTimeOffset? ShippedAt { get; set; }

        [Column("completed_at")]
        public DateTimeOffset? CompletedAt { get; set; }

        [Column("cancelled_at")]
        public DateTimeOffset? CancelledAt { get; set; }

        [Column("cancel_reason")]
        public string? CancelReason { get; set; }

        [Column("refunded_at")]
        public DateTimeOffset? RefundedAt { get; set; }

        [Column("admin_notes")]
        public string? AdminNotes { get; set; }

        [Column("created_at")]
        public DateTimeOffset CreatedAt { get; set; }

        [Column("updated_at")]
        public DateTimeOffset UpdatedAt { get; set; }

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

    /// <summary>
    /// Trạng thái đơn hàng - State Machine đầy đủ
    /// </summary>
    public static class OrderStatuses
    {
        public const string Pending = "PENDING";               // Vừa tạo, chưa thanh toán
        public const string AwaitingPayment = "AWAITING_PAYMENT"; // Đang chờ thanh toán
        public const string Paid = "PAID";                     // Đã thanh toán thành công
        public const string Processing = "PROCESSING";         // Đang xử lý/chuẩn bị hàng
        public const string Shipping = "SHIPPING";             // Đang giao hàng
        public const string Completed = "COMPLETED";           // Giao hàng thành công
        public const string Cancelled = "CANCELLED";           // Đã hủy
        public const string Refunded = "REFUNDED";             // Đã hoàn tiền

        // Alias (backward compat)
        public const string Confirmed = "PAID"; // Cho tương thích với code cũ

        public static readonly string[] AllStatuses =
        {
            Pending, AwaitingPayment, Paid, Processing, Shipping, Completed, Cancelled, Refunded
        };

        /// <summary>Kiểm tra chuyển trạng thái hợp lệ</summary>
        public static bool CanTransition(string from, string to)
        {
            return (from, to) switch
            {
                (Pending, AwaitingPayment) => true,
                (Pending, Cancelled) => true,
                (AwaitingPayment, Paid) => true,
                (AwaitingPayment, Cancelled) => true,
                (Paid, Processing) => true,
                (Paid, Refunded) => true,
                (Paid, Cancelled) => true,
                (Processing, Shipping) => true,
                (Processing, Refunded) => true,
                (Processing, Cancelled) => true,
                (Shipping, Completed) => true,
                (Shipping, Refunded) => true,
                _ => false
            };
        }

        public static string GetDisplayName(string status) => status switch
        {
            Pending => "Chờ xử lý",
            AwaitingPayment => "Chờ thanh toán",
            Paid => "Đã thanh toán",
            Processing => "Đang xử lý",
            Shipping => "Đang giao hàng",
            Completed => "Hoàn thành",
            Cancelled => "Đã hủy",
            Refunded => "Đã hoàn tiền",
            _ => status
        };

        public static string GetBadgeClass(string status) => status switch
        {
            Pending => "badge-warning",
            AwaitingPayment => "badge-info",
            Paid => "badge-success",
            Processing => "badge-primary",
            Shipping => "badge-info",
            Completed => "badge-success",
            Cancelled => "badge-danger",
            Refunded => "badge-secondary",
            _ => "badge-secondary"
        };
    }

    public static class PaymentStatuses
    {
        public const string Unpaid = "UNPAID";
        public const string Pending = "PENDING";
        public const string Paid = "PAID";
        public const string Failed = "FAILED";
        public const string Refunded = "REFUNDED";
        public const string Cancelled = "CANCELLED";
    }
}
