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
        public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

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
            modelBuilder.Entity<AuditLog>().ToTable("audit_logs");

            // =========================
            // USER CONFIG
            // =========================
            modelBuilder.Entity<User>(entity =>
            {
                entity.HasKey(u => u.Id);
                entity.Property(u => u.Email).HasColumnType("citext").IsRequired();

                // Unique index cho email (quan trọng!)
                entity.HasIndex(u => u.Email).IsUnique().HasDatabaseName("ix_users_email_unique");

                entity.Property(u => u.FullName).HasColumnType("text");
                entity.Property(u => u.Role).HasColumnType("text").HasDefaultValue(UserRoles.User).IsRequired();
                entity.Property(u => u.IsActive).HasDefaultValue(true);
                entity.Property(u => u.FailedLoginAttempts).HasDefaultValue(0);
                entity.Property(u => u.CreatedAt).HasDefaultValueSql("now()");
                entity.Property(u => u.UpdatedAt).HasDefaultValueSql("now()");
            });

            // =========================
            // PRODUCT CONFIG
            // =========================
            modelBuilder.Entity<Product>(entity =>
            {
                entity.HasKey(p => p.Id);
                entity.Property(p => p.Name).HasColumnType("text").IsRequired();
                entity.Property(p => p.Slug).HasColumnType("text").IsRequired();

                // Unique index cho slug
                entity.HasIndex(p => p.Slug).IsUnique().HasDatabaseName("ix_products_slug_unique");

                entity.Property(p => p.ImageUrl).HasColumnType("text");
                entity.Property(p => p.Description).HasColumnType("text");
                entity.Property(p => p.Price).HasColumnType("numeric(12,2)");
                entity.Property(p => p.SalePrice).HasColumnType("numeric(12,2)");
                entity.Property(p => p.IsActive).HasDefaultValue(true);
                entity.Property(p => p.CreatedAt).HasDefaultValueSql("now()");
            });

            // =========================
            // BLOG POST CONFIG
            // =========================
            modelBuilder.Entity<BlogPost>(entity =>
            {
                entity.ToTable("blog_posts", "hago");
                entity.HasKey(x => x.Id);

                entity.Property(x => x.Id).HasColumnName("id");
                entity.Property(x => x.Title).HasColumnName("title").HasColumnType("text").IsRequired();
                entity.Property(x => x.Slug).HasColumnName("slug").HasColumnType("text").IsRequired();
                entity.Property(x => x.Excerpt).HasColumnName("excerpt").HasColumnType("text");
                entity.Property(x => x.ContentHtml).HasColumnName("content_html").HasColumnType("text").IsRequired();
                entity.Property(x => x.CoverImageUrl).HasColumnName("cover_image_url").HasColumnType("text");
                entity.Property(x => x.AuthorUserId).HasColumnName("author_user_id");
                entity.Property(x => x.Status).HasColumnName("status").HasColumnType("text").IsRequired();
                entity.Property(x => x.PublishedAt).HasColumnName("published_at");
                entity.Property(x => x.ViewCount).HasColumnName("view_count");
                entity.Property(x => x.CreatedAt).HasColumnName("created_at");
                entity.Property(x => x.UpdatedAt).HasColumnName("updated_at");

                entity.HasIndex(x => x.Slug).IsUnique();

                entity.HasOne(x => x.AuthorUser)
                      .WithMany(u => u.BlogPosts)
                      .HasForeignKey(x => x.AuthorUserId)
                      .HasConstraintName("fk_blog_posts_author_user_id")
                      .OnDelete(DeleteBehavior.SetNull);
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
            // ORDER CONFIG - State Machine
            // =========================
            modelBuilder.Entity<Order>(entity =>
            {
                entity.HasKey(o => o.Id);

                // Unique index cho OrderNumber
                entity.HasIndex(o => o.OrderNumber)
                      .IsUnique()
                      .HasFilter("order_number IS NOT NULL")
                      .HasDatabaseName("ix_orders_order_number_unique");

                entity.Property(o => o.Status).HasDefaultValue(OrderStatuses.Pending);
                entity.Property(o => o.CreatedAt).HasDefaultValueSql("now()");
                entity.Property(o => o.UpdatedAt).HasDefaultValueSql("now()");

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

                // Unique index cho TransactionRef để tránh duplicate payment
                entity.HasIndex(p => p.TransactionRef)
                      .IsUnique()
                      .HasFilter("transaction_ref IS NOT NULL")
                      .HasDatabaseName("ix_payments_transaction_ref_unique");

                entity.HasOne(p => p.Order)
                      .WithMany(o => o.Payments)
                      .HasForeignKey(p => p.OrderId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // =========================
            // AUDIT LOG CONFIG
            // =========================
            modelBuilder.Entity<AuditLog>(entity =>
            {
                entity.HasKey(a => a.Id);
                entity.Property(a => a.Action).HasColumnType("text").IsRequired();
                entity.Property(a => a.EntityType).HasColumnType("text");
                entity.Property(a => a.EntityId).HasColumnType("text");
                entity.Property(a => a.Details).HasColumnType("text");
                entity.Property(a => a.IpAddress).HasColumnType("varchar(45)"); // IPv4 + IPv6
                entity.Property(a => a.UserAgent).HasColumnType("text");
                entity.Property(a => a.Result).HasColumnType("text").HasDefaultValue(AuditResult.Success);
                entity.Property(a => a.ErrorMessage).HasColumnType("text");
                entity.Property(a => a.CreatedAt).HasDefaultValueSql("now()");

                // Index để query nhanh theo user và thời gian
                entity.HasIndex(a => new { a.UserId, a.CreatedAt })
                      .HasDatabaseName("ix_audit_logs_user_created");
                entity.HasIndex(a => new { a.Action, a.CreatedAt })
                      .HasDatabaseName("ix_audit_logs_action_created");

                entity.HasOne(a => a.User)
                      .WithMany(u => u.AuditLogs)
                      .HasForeignKey(a => a.UserId)
                      .HasConstraintName("fk_audit_logs_user_id")
                      .OnDelete(DeleteBehavior.SetNull);
            });
        }
    }
}
