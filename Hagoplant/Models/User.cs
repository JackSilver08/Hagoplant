using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Hagoplant.Models
{
    [Table("users", Schema = "hago")]
    public class User
    {
        [Key]
        [Column("id")]
        public Guid Id { get; set; }

        // citext trong PostgreSQL (email không phân biệt hoa thường)
        [Required]
        [Column("email")]
        public string Email { get; set; } = default!;

        [Required]
        [Column("password_hash")]
        public string PasswordHash { get; set; } = default!;

        [Column("full_name")]
        public string? FullName { get; set; }

        [Column("phone")]
        public string? Phone { get; set; }

        [Column("is_active")]
        public bool IsActive { get; set; } = true;

        /// <summary>
        /// Role: "User" | "Admin" | "Manager" | "Staff"
        /// Thay thế hard-code admin email
        /// </summary>
        [Column("role")]
        public string Role { get; set; } = UserRoles.User;

        /// <summary>Số lần đăng nhập thất bại liên tiếp</summary>
        [Column("failed_login_attempts")]
        public int FailedLoginAttempts { get; set; } = 0;

        /// <summary>Thời điểm tài khoản bị khóa tạm thời (null = không bị khóa)</summary>
        [Column("locked_until")]
        public DateTimeOffset? LockedUntil { get; set; }

        // timestamptz -> DateTimeOffset để tránh lỗi Kind=Unspecified
        [Column("created_at")]
        public DateTimeOffset CreatedAt { get; set; }

        [Column("updated_at")]
        public DateTimeOffset UpdatedAt { get; set; }

        // Navigation
        public ICollection<Cart> Carts { get; set; } = new List<Cart>();
        public ICollection<Order> Orders { get; set; } = new List<Order>();

        // Orders confirmed by this user (confirmed_by_user_id)
        public ICollection<Order> OrdersConfirmed { get; set; } = new List<Order>();

        public ICollection<BlogPost> BlogPosts { get; set; } = new List<BlogPost>();
        public ICollection<AuditLog> AuditLogs { get; set; } = new List<AuditLog>();

        // Helper
        public bool IsLockedOut => LockedUntil.HasValue && LockedUntil.Value > DateTimeOffset.UtcNow;
        public bool IsAdmin => Role == UserRoles.Admin;
    }

    /// <summary>Constanst cho Role - tránh magic string</summary>
    public static class UserRoles
    {
        public const string Admin = "Admin";
        public const string Manager = "Manager";
        public const string Staff = "Staff";
        public const string User = "User";

        public static readonly string[] AllRoles = { Admin, Manager, Staff, User };
    }
}
