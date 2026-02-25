using Hagoplant.DBcontext;
using Hagoplant.Models;
using Hagoplant.Services;
using Hagoplant.ViewModels;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;


namespace Hagoplant.Controllers
{
    public class AccountController : Controller
    {
        private readonly AuthService _auth;
        private readonly HagoDbContext _db;
        private readonly IMemoryCache _cache;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IAuditService _audit;
        private readonly ILogger<AccountController> _logger;
        private readonly IConfiguration _config;

        // =============================================
        // Admin OTP config - lấy từ config, không hardcode
        // =============================================
        private static readonly TimeSpan OtpTtl = TimeSpan.FromMinutes(5);
        private const int OtpMaxAttempts = 5;
        private const string FormspreeEndpoint = "https://formspree.io/f/xkoooyar";

        public AccountController(
            AuthService auth,
            HagoDbContext db,
            IMemoryCache cache,
            IHttpClientFactory httpClientFactory,
            IAuditService audit,
            ILogger<AccountController> logger,
            IConfiguration config)
        {
            _auth = auth;
            _db = db;
            _cache = cache;
            _httpClientFactory = httpClientFactory;
            _audit = audit;
            _logger = logger;
            _config = config;
        }

        private static string NormalizeEmail(string? email)
           => (email ?? "").Trim().ToLowerInvariant();

        private static string OtpCacheKey(string email) => $"admin-otp:{email}";
        private static string OtpRateLimitKey(string email) => $"admin-otp-rl:{email}";

        private static string HashOtp(string email, string otp)
        {
            using var sha = SHA256.Create();
            var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes($"{email}:{otp}"));
            return Convert.ToHexString(bytes);
        }

        private sealed class OtpEntry
        {
            public string Hash { get; set; } = "";
            public int Attempts { get; set; } = 0;
        }

        // ====================== REGISTER ======================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(
            string fullName,
            string? phone,
            string email,
            string password,
            string confirmPassword,
            string OtpVerified)
        {
            // 1. Kiểm tra mật khẩu khớp
            if (password != confirmPassword)
            {
                TempData["Toast.Ok"] = "0";
                TempData["Toast.Message"] = "Mật khẩu xác nhận không khớp.";
                return RedirectToAction("Index", "Home");
            }

            // 2. Validate password policy
            var (pwOk, pwMsg) = AuthService.ValidatePasswordPolicy(password);
            if (!pwOk)
            {
                TempData["Toast.Ok"] = "0";
                TempData["Toast.Message"] = pwMsg;
                return RedirectToAction("Index", "Home");
            }

            // 3. Kiểm tra đã verify OTP chưa
            if (OtpVerified != "1")
            {
                TempData["Toast.Ok"] = "0";
                TempData["Toast.Message"] = "Vui lòng xác nhận mã OTP trước khi đăng ký.";
                return RedirectToAction("Index", "Home");
            }

            // 4. Chuẩn hóa email
            email = NormalizeEmail(email);
            if (string.IsNullOrEmpty(email))
            {
                TempData["Toast.Ok"] = "0";
                TempData["Toast.Message"] = "Email không hợp lệ.";
                return RedirectToAction("Index", "Home");
            }

            // 5. Tạo tài khoản
            var (ok, message) = await _auth.RegisterAsync(email, password, fullName, phone);

            await _audit.LogAsync(
                action: ok ? AuditActions.Register : AuditActions.Register,
                entityType: "User",
                details: $"Email: {email}",
                result: ok ? AuditResult.Success : AuditResult.Failure,
                errorMessage: ok ? null : message);

            TempData["Toast.Ok"] = ok ? "1" : "0";
            TempData["Toast.Message"] = message;
            return RedirectToAction("Index", "Home");
        }

        // ===================== ADMIN OTP: SEND =====================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SendAdminOtp([FromForm] string email, [FromForm] string password)
        {
            var normalizedEmail = NormalizeEmail(email);

            // === Rate limiting: tối đa 3 OTP requests/15 phút ===
            var rateLimitKey = OtpRateLimitKey(normalizedEmail);
            if (_cache.TryGetValue(rateLimitKey, out int requestCount) && requestCount >= 3)
            {
                await _audit.LogAsync(
                    AuditActions.AdminOtpSent,
                    details: $"Rate limit hit for: {normalizedEmail}",
                    result: AuditResult.Failure,
                    errorMessage: "Rate limit exceeded");

                return Ok(new { ok = false, message = "Quá nhiều yêu cầu OTP. Vui lòng thử lại sau 15 phút." });
            }

            // Kiểm tra user có role Admin không (không hardcode email nữa)
            var user = await _db.Users.FirstOrDefaultAsync(u =>
                u.Email == normalizedEmail && u.Role == UserRoles.Admin);

            if (user == null)
            {
                // Thay vì Forbid() có thể trả HTML login page, ta trả JSON
                return Ok(new { ok = false, message = "Tài khoản không có quyền Admin hoặc không tồn tại." });
            }

            // Kiểm tra mật khẩu trước khi gửi OTP
            var (validUser, validMsg, _) = await _auth.ValidateLoginAsync(normalizedEmail, password);
            if (validUser == null)
            {
                await _audit.LogAsync(
                    AuditActions.AdminOtpSent,
                    entityType: "User",
                    entityId: user.Id.ToString(),
                    details: $"Invalid password for admin OTP request: {normalizedEmail}",
                    result: AuditResult.Failure);

                // Thay vì BadRequest, ta trả Ok với message false để UI dễ xử lý và không báo đỏ console
                return Ok(new { ok = false, message = validMsg ?? "Thông tin xác thực không đúng." });
            }

            // Tăng rate limit counter
            var cacheOpts = new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(15)
            };
            _cache.Set(rateLimitKey, requestCount + 1, cacheOpts);

            // Sinh OTP 6 số
            var otp = RandomNumberGenerator.GetInt32(100000, 1000000).ToString();
            var entry = new OtpEntry
            {
                Hash = HashOtp(normalizedEmail, otp),
                Attempts = 0
            };

            _cache.Set(OtpCacheKey(normalizedEmail), entry, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = OtpTtl
            });

            // Gửi OTP qua Formspree
            var client = _httpClientFactory.CreateClient();
            var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["email"] = normalizedEmail,
                ["message"] = $"[HagoTree Admin OTP] Mã OTP: {otp} (hết hạn sau 5 phút). Không chia sẻ mã này với bất kỳ ai."
            });

            var resp = await client.PostAsync(FormspreeEndpoint, content);
            if (!resp.IsSuccessStatusCode)
            {
                _cache.Remove(OtpCacheKey(normalizedEmail));
                return StatusCode((int)resp.StatusCode, new { ok = false, message = "Gửi OTP thất bại. Vui lòng thử lại." });
            }

            await _audit.LogAsync(
                AuditActions.AdminOtpSent,
                entityType: "User",
                entityId: user.Id.ToString(),
                details: $"Admin OTP sent to: {normalizedEmail}",
                result: AuditResult.Success);

            return Json(new { ok = true, message = "OTP đã được gửi về email admin. Vui lòng kiểm tra hộp thư (kể cả spam)." });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(string email, string password, bool rememberMe, string? otp)
        {
            var normalizedEmail = NormalizeEmail(email);

            var (user, message, reason) = await _auth.ValidateLoginAsync(normalizedEmail, password);

            if (user == null)
            {
                // Log thất bại
                await _audit.LogAsync(
                    reason == LoginFailReason.LockedOut ? AuditActions.LoginLockedOut : AuditActions.LoginFailed,
                    entityType: "User",
                    details: $"Failed login for email: {normalizedEmail}, reason: {reason}",
                    result: AuditResult.Failure,
                    errorMessage: message);

                TempData["Toast.Ok"] = "0";
                TempData["Toast.Message"] = message ?? "Email hoặc mật khẩu không đúng.";
                TempData["OpenLoginModal"] = "1";
                TempData["LoginEmail"] = normalizedEmail;
                return RedirectToAction("Index", "Home");
            }

            // ========================
            // Admin phải xác thực OTP
            // ========================
            if (user.Role == UserRoles.Admin)
            {
                if (string.IsNullOrWhiteSpace(otp))
                {
                    TempData["Toast.Ok"] = "0";
                    TempData["Toast.Message"] = "Vui lòng nhập OTP admin.";
                    TempData["OpenLoginModal"] = "1";
                    TempData["LoginEmail"] = normalizedEmail;
                    return RedirectToAction("Index", "Home");
                }

                if (!_cache.TryGetValue(OtpCacheKey(normalizedEmail), out OtpEntry? entry) || entry == null)
                {
                    TempData["Toast.Ok"] = "0";
                    TempData["Toast.Message"] = "OTP đã hết hạn hoặc chưa được gửi. Vui lòng bấm 'Gửi OTP'.";
                    TempData["OpenLoginModal"] = "1";
                    TempData["LoginEmail"] = normalizedEmail;
                    return RedirectToAction("Index", "Home");
                }

                entry.Attempts++;
                if (entry.Attempts > OtpMaxAttempts)
                {
                    _cache.Remove(OtpCacheKey(normalizedEmail));

                    await _audit.LogAsync(
                        AuditActions.AdminOtpFailed,
                        entityType: "User",
                        entityId: user.Id.ToString(),
                        details: $"OTP max attempts exceeded for: {normalizedEmail}",
                        result: AuditResult.Failure);

                    TempData["Toast.Ok"] = "0";
                    TempData["Toast.Message"] = "Bạn đã nhập sai OTP quá số lần cho phép. Vui lòng gửi OTP mới.";
                    TempData["OpenLoginModal"] = "1";
                    TempData["LoginEmail"] = normalizedEmail;
                    return RedirectToAction("Index", "Home");
                }

                var inputHash = HashOtp(normalizedEmail, otp.Trim());
                if (!string.Equals(entry.Hash, inputHash, StringComparison.OrdinalIgnoreCase))
                {
                    _cache.Set(OtpCacheKey(normalizedEmail), entry, new MemoryCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = OtpTtl
                    });

                    await _audit.LogAsync(
                        AuditActions.AdminOtpFailed,
                        entityType: "User",
                        entityId: user.Id.ToString(),
                        details: $"Wrong OTP entered. Attempt {entry.Attempts}/{OtpMaxAttempts}",
                        result: AuditResult.Failure);

                    TempData["Toast.Ok"] = "0";
                    TempData["Toast.Message"] = "OTP không đúng.";
                    TempData["OpenLoginModal"] = "1";
                    TempData["LoginEmail"] = normalizedEmail;
                    return RedirectToAction("Index", "Home");
                }

                // OTP đúng - consume
                _cache.Remove(OtpCacheKey(normalizedEmail));

                await _audit.LogAsync(
                    AuditActions.AdminOtpVerified,
                    entityType: "User",
                    entityId: user.Id.ToString(),
                    details: $"Admin OTP verified for: {normalizedEmail}",
                    result: AuditResult.Success);
            }

            var isAdmin = user.Role == UserRoles.Admin;
            await SignInAppAsync(user, rememberMe, isAdmin);

            // Log đăng nhập thành công
            await _audit.LogAsync(
                AuditActions.LoginSuccess,
                entityType: "User",
                entityId: user.Id.ToString(),
                details: $"Login successful. Role: {user.Role}",
                result: AuditResult.Success,
                userId: user.Id,
                userEmail: user.Email);

            TempData["Toast.Ok"] = "1";
            TempData["Toast.Message"] = "Đăng nhập thành công.";

            if (isAdmin)
                return RedirectToAction("Index", "Admin");

            return RedirectToAction("Index", "Home");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            var userId = GetCurrentUserId();
            var userEmail = User.FindFirstValue(ClaimTypes.Email);

            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

            await _audit.LogAsync(
                AuditActions.Logout,
                entityType: "User",
                entityId: userId?.ToString(),
                details: $"User logged out: {userEmail}",
                result: AuditResult.Success,
                userId: userId,
                userEmail: userEmail);

            TempData["Toast.Ok"] = "1";
            TempData["Toast.Message"] = "Bạn đã đăng xuất.";
            return RedirectToAction("Index", "Home");
        }

        // ==============================================================
        // Helper: Sign-in app cookie
        // ==============================================================
        private async Task SignInAppAsync(User user, bool rememberMe, bool isAdmin = false)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.FullName ?? user.Email),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Role, user.Role) // Role từ DB, không hardcode
            };

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                principal,
                new AuthenticationProperties
                {
                    IsPersistent = rememberMe,
                    ExpiresUtc = rememberMe ? DateTimeOffset.UtcNow.AddDays(7) : DateTimeOffset.UtcNow.AddHours(8)
                });
        }

        private Guid? GetCurrentUserId()
        {
            var raw = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return Guid.TryParse(raw, out var id) ? id : null;
        }

        // ==========================================================
        // PROFILE MODAL (GET partial) + UPDATE PROFILE (POST)
        // ==========================================================

        // GET: /Account/ProfileModal
        [Authorize]
        [HttpGet]
        public async Task<IActionResult> ProfileModal()
        {
            var userId = GetCurrentUserId();
            if (userId == null) return Unauthorized();

            var u = await _db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == userId.Value);
            if (u == null) return NotFound();

            var vm = new ProfileUpdateVm
            {
                Email = u.Email,
                FullName = u.FullName,
                Phone = u.Phone,
                IsActive = u.IsActive,
                CreatedAtText = u.CreatedAt.ToLocalTime().ToString("dd/MM/yyyy HH:mm")
            };

            return PartialView("~/Views/Home/_ProfileModalBody.cshtml", vm);
        }

        // POST: /Account/UpdateProfile
        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateProfile(ProfileUpdateVm vm)
        {
            var userId = GetCurrentUserId();
            if (userId == null) return Unauthorized();

            // Email/CreatedAt/IsActive không nhận từ client để update
            ModelState.Remove(nameof(ProfileUpdateVm.Email));
            ModelState.Remove(nameof(ProfileUpdateVm.CreatedAtText));
            ModelState.Remove(nameof(ProfileUpdateVm.IsActive));

            if (!ModelState.IsValid)
            {
                return BadRequest(new
                {
                    ok = false,
                    message = "Dữ liệu không hợp lệ.",
                    errors = ModelState
                        .Where(kv => kv.Value?.Errors.Count > 0)
                        .ToDictionary(
                            kv => kv.Key,
                            kv => kv.Value!.Errors.Select(e => e.ErrorMessage).ToArray()
                        )
                });
            }

            var u = await _db.Users.FirstOrDefaultAsync(x => x.Id == userId.Value);
            if (u == null) return NotFound();

            if (!u.IsActive)
                return Forbid();

            var oldName = u.FullName;
            var oldPhone = u.Phone;

            u.FullName = string.IsNullOrWhiteSpace(vm.FullName) ? null : vm.FullName.Trim();
            u.Phone = string.IsNullOrWhiteSpace(vm.Phone) ? null : vm.Phone.Trim();
            u.UpdatedAt = DateTimeOffset.UtcNow;

            await _db.SaveChangesAsync();

            await _audit.LogAsync(
                AuditActions.UserUpdated,
                entityType: "User",
                entityId: u.Id.ToString(),
                details: $"Profile updated. FullName: {oldName} → {u.FullName}, Phone: {oldPhone} → {u.Phone}",
                result: AuditResult.Success,
                userId: u.Id,
                userEmail: u.Email);

            // Cập nhật lại cookie claims
            await RefreshSignInAsync(u);

            return Json(new
            {
                ok = true,
                message = "Cập nhật thông tin thành công.",
                fullName = u.FullName ?? "",
                phone = u.Phone ?? ""
            });
        }

        private async Task RefreshSignInAsync(User user)
        {
            var authResult = await HttpContext.AuthenticateAsync(
                CookieAuthenticationDefaults.AuthenticationScheme);

            var props = authResult?.Properties ?? new AuthenticationProperties();

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.FullName ?? user.Email),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Role, user.Role) // Role từ DB
            };

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                principal,
                props);
        }
    }
}
