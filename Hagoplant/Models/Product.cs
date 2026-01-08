using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Hagoplant.Models
{
    [Table("products", Schema = "hago")]
    public class Product
    {
        [Key]
        [Column("id")]
        public Guid Id { get; set; }

        [Required]
        [Column("name")]
        public string Name { get; set; } = string.Empty;

        [Required]
        [Column("slug")]
        public string Slug { get; set; } = string.Empty;

        [Column("description")]
        public string? Description { get; set; }

        [Column("price", TypeName = "numeric(12,2)")]
        public decimal Price { get; set; }

        [Column("sale_price", TypeName = "numeric(12,2)")]
        public decimal? SalePrice { get; set; }

        [Column("image_url")]
        public string? ImageUrl { get; set; }

        [Column("is_active")]
        public bool IsActive { get; set; } = true;

        [Column("is_featured")]
        public bool IsFeatured { get; set; } = false;

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }
    }
}

