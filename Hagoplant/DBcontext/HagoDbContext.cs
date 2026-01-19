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
        // DB SETS (BẢNG TRONG SCHEMA "hago")
        // =========================
        public DbSet<User> Users => Set<User>();
        public DbSet<Product> Products => Set<Product>();
        public DbSet<BlogPost> BlogPosts => Set<BlogPost>();
        public DbSet<Cart> Carts => Set<Cart>();
        public DbSet<CartItem> CartItems => Set<CartItem>();
        public DbSet<Order> Orders => Set<Order>();

        public DbSet<Payment> Payments => Set<Payment>();
        public DbSet<Voucher> Vouchers => Set<Voucher>();


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
           
            modelBuilder.Entity<Cart>().ToTable("carts");
            modelBuilder.Entity<CartItem>().ToTable("cart_items");
            modelBuilder.Entity<Order>().ToTable("orders");
            modelBuilder.Entity<Payment>().ToTable("payments");
            modelBuilder.Entity<Voucher>().ToTable("vouchers");

            // =========================
            // USER CONFIG
            // =========================
            modelBuilder.Entity<User>(entity =>
            {
                entity.HasKey(u => u.Id);
                entity.Property(u => u.Email).HasColumnType("citext");
                entity.Property(u => u.FullName).HasColumnType("text");
                entity.Property(u => u.IsActive).HasDefaultValue(true);
                entity.Property(u => u.CreatedAt).HasDefaultValueSql("now()");
            });

            // =========================
            // PRODUCT CONFIG
            // =========================
            modelBuilder.Entity<Product>(entity =>
            {
                entity.HasKey(p => p.Id);
                entity.Property(p => p.Name).HasColumnType("text").IsRequired();
                entity.Property(p => p.Slug).HasColumnType("text").IsRequired();
                entity.Property(p => p.ImageUrl).HasColumnType("text");
                entity.Property(p => p.Description).HasColumnType("text");

                entity.Property(p => p.Price).HasColumnType("numeric(12,2)");
                entity.Property(p => p.SalePrice).HasColumnType("numeric(12,2)");
                entity.Property(p => p.IsActive).HasDefaultValue(true);
                entity.Property(p => p.CreatedAt).HasDefaultValueSql("now()");
            });

            // =========================
            // BLOG POST CONFIG (FIX SHADOW UserId*)
            // =========================
            modelBuilder.Entity<BlogPost>(entity =>
            {
                entity.ToTable("blog_posts", "hago");

                entity.HasKey(x => x.Id);

                // --- Column mapping: explicit để EF không suy luận linh tinh ---
                entity.Property(x => x.Id).HasColumnName("id");
                entity.Property(x => x.Title).HasColumnName("title").HasColumnType("text").IsRequired();
                entity.Property(x => x.Slug).HasColumnName("slug").HasColumnType("text").IsRequired();
                entity.Property(x => x.Excerpt).HasColumnName("excerpt").HasColumnType("text");
                entity.Property(x => x.ContentHtml).HasColumnName("content_html").HasColumnType("text").IsRequired();
                entity.Property(x => x.CoverImageUrl).HasColumnName("cover_image_url").HasColumnType("text");

                entity.Property(x => x.AuthorUserId).HasColumnName("author_user_id"); // nullable Guid?
                entity.Property(x => x.Status).HasColumnName("status").HasColumnType("text").IsRequired();
                entity.Property(x => x.PublishedAt).HasColumnName("published_at");
                entity.Property(x => x.ViewCount).HasColumnName("view_count");

                entity.Property(x => x.CreatedAt).HasColumnName("created_at");
                entity.Property(x => x.UpdatedAt).HasColumnName("updated_at");

                entity.HasIndex(x => x.Slug).IsUnique();

                // --- Relationship: chỉ 1 cái duy nhất, đúng FK ---
                entity.HasOne(x => x.AuthorUser)
                      .WithMany(u => u.BlogPosts)
                      .HasForeignKey(x => x.AuthorUserId)
                      .HasConstraintName("fk_blog_posts_author_user_id") // optional, giúp debug
                      .OnDelete(DeleteBehavior.SetNull);

                // --- Optional: nếu bạn không muốn EF lazy-load/proxy sinh thêm gì đó ---
                // entity.Navigation(x => x.AuthorUser).AutoInclude(false);
            });


            // =========================
            // CART CONFIG
            // =========================
            modelBuilder.Entity<Cart>(entity =>
            {
                entity.HasKey(c => c.Id);

                entity.Property(c => c.CreatedAt).HasDefaultValueSql("now()");
                entity.Property(c => c.UpdatedAt).HasDefaultValueSql("now()");

                entity.HasMany(c => c.Items)
                      .WithOne(ci => ci.Cart)
                      .HasForeignKey(ci => ci.CartId)
                      .OnDelete(DeleteBehavior.Cascade);
            });


            // =========================
            // CART ITEM CONFIG
            // =========================
            modelBuilder.Entity<CartItem>(entity =>
            {
                entity.HasKey(ci => ci.Id);

                entity.Property(ci => ci.UnitPriceSnapshot).HasColumnType("numeric(12,2)");
                entity.Property(ci => ci.Quantity).HasDefaultValue(1);
                entity.Property(ci => ci.CreatedAt).HasDefaultValueSql("now()");
                entity.Property(ci => ci.UpdatedAt).HasDefaultValueSql("now()");

                entity.HasOne(ci => ci.Product)
                      .WithMany(p => p.CartItems)
                      .HasForeignKey(ci => ci.ProductId)
                      .OnDelete(DeleteBehavior.Restrict);
            });


            // =========================
            // ORDER CONFIG  (FIX LỖI ConfirmedByUser)
            // =========================
            modelBuilder.Entity<Order>(entity =>
            {
                entity.HasKey(o => o.Id);

                entity.HasOne(o => o.User)
                      .WithMany(u => u.Orders)
                      .HasForeignKey(o => o.UserId)
                      .HasConstraintName("fk_orders_user_id")
                      .OnDelete(DeleteBehavior.SetNull);

                entity.HasOne(o => o.ConfirmedByUser)
                      .WithMany(u => u.OrdersConfirmed)
                      .HasForeignKey(o => o.ConfirmedByUserId)
                      .HasConstraintName("fk_orders_confirmed_by_user_id")
                      .OnDelete(DeleteBehavior.SetNull);

                entity.HasOne(o => o.Voucher)
                      .WithMany(v => v.Orders)
                      .HasForeignKey(o => o.VoucherId)
                      .HasConstraintName("fk_orders_voucher_id")
                      .OnDelete(DeleteBehavior.SetNull);

                entity.Property(o => o.CreatedAt)
                      .HasDefaultValueSql("now()");
            });


            // =========================
            // PAYMENT CONFIG
            // =========================
            modelBuilder.Entity<Payment>(entity =>
            {
                entity.HasKey(p => p.Id);

                entity.Property(p => p.Provider).HasColumnType("text");
                entity.Property(p => p.Method).HasColumnType("text");
                entity.Property(p => p.Amount).HasColumnType("numeric(12,2)");
                entity.Property(p => p.Status).HasColumnType("text");
                entity.Property(p => p.CreatedAt).HasDefaultValueSql("now()");

                entity.HasOne(p => p.Order)
                      .WithMany(o => o.Payments)
                      .HasForeignKey(p => p.OrderId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

        
        }
    }
}
