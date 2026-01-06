using Microsoft.EntityFrameworkCore;
using Hagoplant.Models;

namespace Hagoplant.DBcontext
{
    public class HagoDbContext : DbContext
    {
        public HagoDbContext(DbContextOptions<HagoDbContext> options) : base(options) { }

        public DbSet<User> Users => Set<User>();
   
     


        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // IMPORTANT: toàn bộ bảng mặc định nằm trong schema hago
            modelBuilder.HasDefaultSchema("hago");

            // Nếu tên bảng trong DB đúng là "users" và "user_external_logins"
            modelBuilder.Entity<User>().ToTable("users");
          

            base.OnModelCreating(modelBuilder);
        }
    }
}