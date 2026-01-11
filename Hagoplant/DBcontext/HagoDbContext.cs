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
            modelBuilder.Entity<BlogPost>().ToTable("blog_posts");
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
            // BLOG POST CONFIG
            // =========================
            modelBuilder.Entity<BlogPost>(entity =>
            {
                entity.HasKey(x => x.Id);

                entity.Property(x => x.Id)
                      .HasDefaultValueSql("gen_random_uuid()");

                entity.Property(x => x.Title).HasColumnType("text").IsRequired();
                entity.Property(x => x.Slug).HasColumnType("text").IsRequired();
                entity.Property(x => x.ContentHtml).HasColumnType("text").IsRequired();
                entity.Property(x => x.Excerpt).HasColumnType("text");
                entity.Property(x => x.CoverImageUrl).HasColumnType("text");
                entity.Property(x => x.Status).HasColumnType("text").HasDefaultValue("draft");

                entity.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
                entity.Property(x => x.UpdatedAt).HasDefaultValueSql("now()");

                entity.HasIndex(x => x.Slug).IsUnique();
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

                // 2 FK tới users -> phải cấu hình tường minh
                entity.HasOne(o => o.User)
                      .WithMany()
                      .HasForeignKey(o => o.UserId)
                      .OnDelete(DeleteBehavior.SetNull);

                entity.HasOne(o => o.ConfirmedByUser)
                      .WithMany()
                      .HasForeignKey(o => o.ConfirmedByUserId)
                      .OnDelete(DeleteBehavior.SetNull);

                entity.HasOne(o => o.Voucher)
                      .WithMany()
                      .HasForeignKey(o => o.VoucherId)
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

            base.OnModelCreating(modelBuilder);
        }
    }
}
