using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Hagoplant.Models
{
    /// <summary>
    /// Audit log - ghi lại mọi hành động quan trọng
    /// (admin actions, auth events, payment events)
    /// </summary>
    [Table("audit_logs", Schema = "hago")]
    public class AuditLog
    {
        [Key]
        [Column("id")]
        public Guid Id { get; set; }

        /// <summary>Người dùng thực hiện hành động (null = anonymous/system)</summary>
        [Column("user_id")]
        public Guid? UserId { get; set; }

        /// <summary>Email của người dùng (lưu snapshot phòng trường hợp user bị xóa)</summary>
        [Column("user_email")]
        public string? UserEmail { get; set; }

        /// <summary>Loại hành động: Login, Logout, CreateProduct, UpdateOrder, ...</summary>
        [Required]
        [Column("action")]
        public string Action { get; set; } = default!;

        /// <summary>Entity bị tác động: User, Product, Order, Payment, ...</summary>
        [Column("entity_type")]
        public string? EntityType { get; set; }

        /// <summary>ID của entity bị tác động</summary>
        [Column("entity_id")]
        public string? EntityId { get; set; }

        /// <summary>Thông tin chi tiết (JSON hoặc text)</summary>
        [Column("details")]
        public string? Details { get; set; }

        /// <summary>Địa chỉ IP của người dùng</summary>
        [Column("ip_address")]
        public string? IpAddress { get; set; }

        /// <summary>User-Agent của trình duyệt</summary>
        [Column("user_agent")]
        public string? UserAgent { get; set; }

        /// <summary>Kết quả: Success | Failure | Warning</summary>
        [Column("result")]
        public string Result { get; set; } = AuditResult.Success;

        /// <summary>Thông báo lỗi nếu có</summary>
        [Column("error_message")]
        public string? ErrorMessage { get; set; }

        [Column("created_at")]
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

        // Navigation
        [ForeignKey(nameof(UserId))]
        public User? User { get; set; }
    }

    public static class AuditActions
    {
        // Auth
        public const string LoginSuccess = "LoginSuccess";
        public const string LoginFailed = "LoginFailed";
        public const string LoginLockedOut = "LoginLockedOut";
        public const string Logout = "Logout";
        public const string Register = "Register";
        public const string AdminOtpSent = "AdminOtpSent";
        public const string AdminOtpVerified = "AdminOtpVerified";
        public const string AdminOtpFailed = "AdminOtpFailed";

        // User management
        public const string UserCreated = "UserCreated";
        public const string UserUpdated = "UserUpdated";
        public const string UserDeleted = "UserDeleted";
        public const string UserRoleChanged = "UserRoleChanged";
        public const string UserActivated = "UserActivated";
        public const string UserDeactivated = "UserDeactivated";

        // Product
        public const string ProductCreated = "ProductCreated";
        public const string ProductUpdated = "ProductUpdated";
        public const string ProductDeleted = "ProductDeleted";

        // Order
        public const string OrderCreated = "OrderCreated";
        public const string OrderStatusChanged = "OrderStatusChanged";
        public const string OrderCancelled = "OrderCancelled";
        public const string OrderConfirmed = "OrderConfirmed";

        // Payment
        public const string PaymentCreated = "PaymentCreated";
        public const string PaymentWebhookReceived = "PaymentWebhookReceived";
        public const string PaymentWebhookFailed = "PaymentWebhookFailed";
        public const string PaymentCompleted = "PaymentCompleted";

        // Blog
        public const string BlogPostCreated = "BlogPostCreated";
        public const string BlogPostUpdated = "BlogPostUpdated";
        public const string BlogPostDeleted = "BlogPostDeleted";

        public static readonly string[] AllActions = {
            LoginSuccess, LoginFailed, LoginLockedOut, Logout, Register, AdminOtpSent, AdminOtpVerified, AdminOtpFailed,
            UserCreated, UserUpdated, UserDeleted, UserRoleChanged, UserActivated, UserDeactivated,
            ProductCreated, ProductUpdated, ProductDeleted,
            OrderCreated, OrderStatusChanged, OrderCancelled, OrderConfirmed,
            PaymentCreated, PaymentWebhookReceived, PaymentWebhookFailed, PaymentCompleted,
            BlogPostCreated, BlogPostUpdated, BlogPostDeleted
        };
    }

    public static class AuditResult
    {
        public const string Success = "Success";
        public const string Failure = "Failure";
        public const string Warning = "Warning";
    }
}
