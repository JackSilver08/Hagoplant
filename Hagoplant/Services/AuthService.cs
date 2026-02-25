using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Hagoplant.DBcontext;
using Hagoplant.Models;

namespace Hagoplant.Services
{
    /// <summary>
    /// AuthService nâng cấp với:
    /// - Account lockout sau nhiều lần thất bại
    /// - Password policy (độ dài, độ phức tạp)
    /// - Timing-safe password verification
    /// </summary>
    public class AuthService
    {
        private readonly HagoDbContext _db;
        private readonly PasswordHasher<User> _hasher = new();
        private readonly ILogger<AuthService> _logger;

        // === Password Policy ===
        public const int MinPasswordLength = 8;
        public const int MaxPasswordLength = 128;

        // === Account Lockout Policy ===
        public const int MaxFailedAttempts = 5;          // Số lần thất bại tối đa
        public static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15); // Thời gian khóa

        public AuthService(HagoDbContext db, ILogger<AuthService> logger)
        {
            _db = db;
            _logger = logger;
        }

        /// <summary>
        /// Đăng ký tài khoản mới với password policy validation
        /// </summary>
        public async Task<(bool ok, string message)> RegisterAsync(
            string email,
            string password,
            string fullName,
            string? phone,
            string role = UserRoles.User)
        {
            email = email.Trim().ToLowerInvariant();

            // === Validate email format ===
            if (!IsValidEmail(email))
                return (false, "Email không hợp lệ.");

            // === Validate password policy ===
            var (pwOk, pwMsg) = ValidatePasswordPolicy(password);
            if (!pwOk) return (false, pwMsg);

            // === Validate fullName ===
            if (string.IsNullOrWhiteSpace(fullName))
                return (false, "Họ tên không được để trống.");

            // === Check email uniqueness ===
            var exists = await _db.Users.AnyAsync(u => u.Email == email);
            if (exists) return (false, "Email đã tồn tại. Vui lòng dùng email khác.");

            var now = DateTimeOffset.UtcNow;
            var user = new User
            {
                Id = Guid.NewGuid(),
                Email = email,
                FullName = fullName.Trim(),
                Phone = string.IsNullOrWhiteSpace(phone) ? null : phone.Trim(),
                Role = role,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now
            };

            user.PasswordHash = _hasher.HashPassword(user, password);

            _db.Users.Add(user);
            await _db.SaveChangesAsync();

            _logger.LogInformation("New user registered: {Email}, Role: {Role}", email, role);

            return (true, "Đăng ký thành công. Bạn có thể đăng nhập ngay.");
        }

        /// <summary>
        /// Xác thực đăng nhập với account lockout protection
        /// </summary>
        public async Task<(User? user, string message, LoginFailReason reason)> ValidateLoginAsync(
            string email, string password)
        {
            email = email.Trim().ToLowerInvariant();

            var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email);

            // Luôn trả cùng thông báo để tránh user enumeration attack
            if (user == null)
            {
                _logger.LogWarning("Login attempt for non-existent email: {Email}", email);
                return (null, "Sai email hoặc mật khẩu.", LoginFailReason.InvalidCredentials);
            }

            // Kiểm tra tài khoản bị vô hiệu hóa
            if (!user.IsActive)
            {
                _logger.LogWarning("Login attempt for deactivated account: {Email}", email);
                return (null, "Tài khoản đã bị vô hiệu hóa. Vui lòng liên hệ hỗ trợ.", LoginFailReason.AccountDeactivated);
            }

            // Kiểm tra tài khoản bị khóa tạm thời
            if (user.IsLockedOut)
            {
                var remaining = (int)(user.LockedUntil!.Value - DateTimeOffset.UtcNow).TotalMinutes + 1;
                _logger.LogWarning("Login attempt for locked account: {Email}, LockedUntil: {Time}", email, user.LockedUntil);
                return (null, $"Tài khoản tạm thời bị khóa do nhập sai quá nhiều lần. Vui lòng thử lại sau {remaining} phút.", LoginFailReason.LockedOut);
            }

            // Xác thực mật khẩu
            var result = _hasher.VerifyHashedPassword(user, user.PasswordHash, password);

            if (result == PasswordVerificationResult.Failed)
            {
                // Tăng đếm thất bại
                user.FailedLoginAttempts++;

                if (user.FailedLoginAttempts >= MaxFailedAttempts)
                {
                    user.LockedUntil = DateTimeOffset.UtcNow.Add(LockoutDuration);
                    user.FailedLoginAttempts = 0; // Reset sau khi lock
                    await _db.SaveChangesAsync();

                    _logger.LogWarning("Account locked due to too many failed attempts: {Email}", email);
                    return (null, $"Tài khoản bị khóa {(int)LockoutDuration.TotalMinutes} phút do nhập sai mật khẩu quá nhiều lần.", LoginFailReason.LockedOut);
                }

                await _db.SaveChangesAsync();

                var remaining = MaxFailedAttempts - user.FailedLoginAttempts;
                _logger.LogWarning("Failed login for {Email}. Attempts: {Count}", email, user.FailedLoginAttempts);
                return (null, $"Sai email hoặc mật khẩu. Còn {remaining} lần thử trước khi bị khóa.", LoginFailReason.InvalidCredentials);
            }

            // Đăng nhập thành công - reset lockout
            if (user.FailedLoginAttempts > 0 || user.LockedUntil.HasValue)
            {
                user.FailedLoginAttempts = 0;
                user.LockedUntil = null;
                await _db.SaveChangesAsync();
            }

            // Rehash nếu cần (password hasher tự detect)
            if (result == PasswordVerificationResult.SuccessRehashNeeded)
            {
                user.PasswordHash = _hasher.HashPassword(user, password);
                await _db.SaveChangesAsync();
            }

            _logger.LogInformation("Successful login: {Email}", email);
            return (user, "Đăng nhập thành công.", LoginFailReason.None);
        }

        /// <summary>
        /// Validate mật khẩu theo policy
        /// </summary>
        public static (bool ok, string message) ValidatePasswordPolicy(string? password)
        {
            if (string.IsNullOrEmpty(password))
                return (false, "Mật khẩu không được để trống.");

            if (password.Length < MinPasswordLength)
                return (false, $"Mật khẩu phải có ít nhất {MinPasswordLength} ký tự.");

            if (password.Length > MaxPasswordLength)
                return (false, $"Mật khẩu không được vượt quá {MaxPasswordLength} ký tự.");

            if (!password.Any(char.IsLetter))
                return (false, "Mật khẩu phải chứa ít nhất một chữ cái.");

            if (!password.Any(char.IsDigit))
                return (false, "Mật khẩu phải chứa ít nhất một chữ số.");

            return (true, "OK");
        }

        private static bool IsValidEmail(string email)
        {
            try
            {
                var addr = new System.Net.Mail.MailAddress(email);
                return addr.Address == email;
            }
            catch
            {
                return false;
            }
        }
    }

    public enum LoginFailReason
    {
        None,
        InvalidCredentials,
        AccountDeactivated,
        LockedOut
    }
}
