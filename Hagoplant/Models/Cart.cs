using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Hagoplant.Models
{
    [Table("carts", Schema = "hago")]
    public class Cart
    {
        [Key]
        [Column("id")]
        public Guid Id { get; set; }

        [Column("user_id")]
        public Guid? UserId { get; set; }

        [Column("guest_token")]
        public Guid? GuestToken { get; set; }

        [Column("created_at")]
        public DateTimeOffset CreatedAt { get; set; }

        [Column("updated_at")]
        public DateTimeOffset UpdatedAt { get; set; }

        // Navigation
        public ICollection<CartItem> Items { get; set; } = new List<CartItem>();
    }
}
