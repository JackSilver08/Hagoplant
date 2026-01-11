using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Hagoplant.Models
{
    [Table("cart_items", Schema = "hago")]
    public class CartItem
    {
        [Key]
        [Column("id")]
        public Guid Id { get; set; }

        [Column("cart_id")]
        public Guid CartId { get; set; }

        [Column("product_id")]
        public Guid ProductId { get; set; }

        [Column("quantity")]
        public int Quantity { get; set; }

        [Precision(12, 2)]
        [Column("unit_price_snapshot")]
        public decimal UnitPriceSnapshot { get; set; }

        [Column("created_at")]
        public DateTimeOffset CreatedAt { get; set; }

        [Column("updated_at")]
        public DateTimeOffset UpdatedAt { get; set; }

        // Navigation (khai báo rõ để EF không tự suy diễn quan hệ thứ 2)
        public Cart? Cart { get; set; }
        public Product? Product { get; set; }
    }
}
