using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Hagoplant.DBcontext;
using Hagoplant.Models;

namespace Hagoplant.Services
{
    public class AuthService
    {
        private readonly HagoDbContext _db;
        private readonly PasswordHasher<User> _hasher = new();

        public AuthService(HagoDbContext db)
        {
            _db = db;
        }

        public async Task<(bool ok, string message)> RegisterAsync(string email, string password, string fullName, string? phone)
        {
            email = email.Trim();

            // citext: so sánh = vẫn ok; nếu bạn muốn chắc chắn, có thể normalize ToLower()
            var exists = await _db.Users.AnyAsync(u => u.Email == email);
            if (exists) return (false, "Email đã tồn tại. Vui lòng dùng email khác.");

            var user = new User
            {
                Id = Guid.NewGuid(),
                Email = email,
                FullName = fullName.Trim(),
                Phone = string.IsNullOrWhiteSpace(phone) ? null : phone.Trim(),
                IsActive = true,
                CreatedAt = DateTimeOffset.UtcNow
            };

            user.PasswordHash = _hasher.HashPassword(user, password);

            _db.Users.Add(user);
            await _db.SaveChangesAsync();

            return (true, "Đăng ký thành công. Bạn có thể đăng nhập ngay.");
        }

        public async Task<(User? user, string message)> ValidateLoginAsync(string email, string password)
        {
            email = email.Trim();

            var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email);
            if (user == null) return (null, "Sai email hoặc mật khẩu.");

            if (!user.IsActive) return (null, "Tài khoản đã bị khóa. Vui lòng liên hệ hỗ trợ.");

            var result = _hasher.VerifyHashedPassword(user, user.PasswordHash, password);
            if (result == PasswordVerificationResult.Failed)
                return (null, "Sai email hoặc mật khẩu.");

            return (user, "Đăng nhập thành công.");
        }
    }
}
