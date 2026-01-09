using Microsoft.EntityFrameworkCore;
using Hagoplant.Models;

namespace Hagoplant.DBcontext
{
    public class HagoDbContext : DbContext
    {
        public HagoDbContext(DbContextOptions<HagoDbContext> options)
            : base(options)
        {
        }

        // =========================
        // DB SETS
        // =========================
        public DbSet<User> Users => Set<User>();
        public DbSet<Product> Products => Set<Product>();
        public DbSet<BlogPost> BlogPosts => Set<BlogPost>();
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // =========================
            // DEFAULT SCHEMA
            // =========================
            modelBuilder.HasDefaultSchema("hago");

            // =========================
            // TABLE MAPPING
            // =========================
            modelBuilder.Entity<User>().ToTable("users");
            modelBuilder.Entity<Product>().ToTable("products");
            modelBuilder.Entity<BlogPost>().ToTable("blog_posts");

            // =========================
            // PRODUCT CONFIG
            // =========================
            modelBuilder.Entity<Product>(entity =>
            {
                entity.HasKey(p => p.Id);

                entity.Property(p => p.Price)
                      .HasColumnType("numeric(12,2)");

                entity.Property(p => p.SalePrice)
                      .HasColumnType("numeric(12,2)");

                entity.Property(p => p.CreatedAt)
                      .HasDefaultValueSql("NOW()");
            });

            // =========================
            // BLOG POST CONFIG
            // =========================
            modelBuilder.Entity<BlogPost>(entity =>
            {
                entity.HasKey(x => x.Id);

                // uuid default: gen_random_uuid()
                entity.Property(x => x.Id)
                      .HasDefaultValueSql("gen_random_uuid()");

                // text columns (không bắt buộc set TypeName nếu bạn đã dùng [Column(TypeName="text")] trong Model)
                entity.Property(x => x.Title).HasColumnType("text").IsRequired();
                entity.Property(x => x.Slug).HasColumnType("text").IsRequired();
                entity.Property(x => x.Excerpt).HasColumnType("text");
                entity.Property(x => x.ContentHtml).HasColumnType("text").IsRequired();
                entity.Property(x => x.CoverImageUrl).HasColumnType("text");
                entity.Property(x => x.Status).HasColumnType("text").IsRequired();

                // timestamptz defaults
                entity.Property(x => x.CreatedAt)
                      .HasDefaultValueSql("now()")
                      .IsRequired();

                entity.Property(x => x.UpdatedAt)
                      .HasDefaultValueSql("now()")
                      .IsRequired();

                // Default status (nếu DB đã set default thì có thể bỏ)
                entity.Property(x => x.Status)
                      .HasDefaultValue("draft");

                // Indexes
                entity.HasIndex(x => x.Slug).IsUnique();
                entity.HasIndex(x => new { x.Status, x.PublishedAt });

                // FK (tuỳ bạn có muốn cấu hình luôn không)
                // Nếu bảng users có PK là Guid và bạn muốn ràng buộc:
                // entity.HasOne<User>()
                //       .WithMany()
                //       .HasForeignKey(x => x.AuthorUserId)
                //       .OnDelete(DeleteBehavior.SetNull);
            });

            base.OnModelCreating(modelBuilder);
        }
    }
}
