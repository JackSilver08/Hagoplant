// File: Models/BlogPost.cs
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Hagoplant.Models
{
    [Table("blog_posts", Schema = "hago")]
    public class BlogPost
    {
        [Key]
        [Column("id")]
        public Guid Id { get; set; }

        [Required]
        [Column("title", TypeName = "text")]
        public string Title { get; set; } = default!;

        [Required]
        [Column("slug", TypeName = "text")]
        public string Slug { get; set; } = default!;

        [Column("excerpt", TypeName = "text")]
        public string? Excerpt { get; set; }

        [Required]
        [Column("content_html", TypeName = "text")]
        public string ContentHtml { get; set; } = default!;

        [Column("cover_image_url", TypeName = "text")]
        public string? CoverImageUrl { get; set; }

        [Column("author_user_id")]
        public Guid? AuthorUserId { get; set; }

        [Required]
        [Column("status", TypeName = "text")]
        public string Status { get; set; } = "draft";

        [Column("published_at")]
        public DateTimeOffset? PublishedAt { get; set; }

        [Required]
        [Column("created_at")]
        public DateTimeOffset CreatedAt { get; set; }

        [Required]
        [Column("updated_at")]
        public DateTimeOffset UpdatedAt { get; set; }
        [Column("view_count")]
public int ViewCount { get; set; }


    }

    // (Tuỳ chọn) Hằng số status để dùng thống nhất trong code
    public static class BlogPostStatuses
    {
        public const string Draft = "draft";
        public const string Published = "published";
        public const string Archived = "archived";
    }
}
