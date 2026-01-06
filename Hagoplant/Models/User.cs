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

        // timestamptz -> DateTimeOffset để tránh lỗi Kind=Unspecified
        [Column("created_at")]
        public DateTimeOffset CreatedAt { get; set; }
    }
}
