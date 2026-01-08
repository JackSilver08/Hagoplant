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

            base.OnModelCreating(modelBuilder);
        }
    }
}
