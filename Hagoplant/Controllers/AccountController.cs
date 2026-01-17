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
using System.Net.Http;
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

        private const string AdminEmail = "hagotreevn@gmail.com";
        private const string FormspreeEndpoint = "https://formspree.io/f/xkoooyar";
        private static readonly TimeSpan OtpTtl = TimeSpan.FromMinutes(5);
        private const int OtpMaxAttempts = 5;
        public AccountController(AuthService auth, HagoDbContext db, IMemoryCache cache, IHttpClientFactory httpClientFactory)
        {
            _auth = auth;
            _db = db;
            _cache = cache;
            _httpClientFactory = httpClientFactory;
        }

        private static string NormalizeEmail(string? email)
           => (email ?? "").Trim().ToLowerInvariant();

        private static string OtpCacheKey(string email) => $"admin-otp:{email}";

        private static string HashOtp(string email, string otp)
        {
            // hash theo email để giảm rủi ro reuse
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
            string OtpVerified)  // Từ hidden field ở frontend
        {
            // 1. Kiểm tra mật khẩu khớp
            if (password != confirmPassword)
            {
                TempData["Toast.Ok"] = "0";
                TempData["Toast.Message"] = "Mật khẩu xác nhận không khớp.";
                return RedirectToAction("Index", "Home");
            }

            // 2. Kiểm tra đã verify OTP chưa
            if (OtpVerified != "1")
            {
                TempData["Toast.Ok"] = "0";
                TempData["Toast.Message"] = "Vui lòng xác nhận mã OTP trước khi đăng ký.";
                return RedirectToAction("Index", "Home");
            }

            // 3. Chuẩn hóa email
            email = (email ?? "").Trim().ToLowerInvariant();
            if (string.IsNullOrEmpty(email))
            {
                TempData["Toast.Ok"] = "0";
                TempData["Toast.Message"] = "Email không hợp lệ.";
                return RedirectToAction("Index", "Home");
            }

            // 4. Kiểm tra email đã tồn tại chưa
            if (await _db.Users.AnyAsync(u => u.Email == email))
            {
                TempData["Toast.Ok"] = "0";
                TempData["Toast.Message"] = "Email này đã được đăng ký.";
                return RedirectToAction("Index", "Home");
            }

            // 5. Tạo tài khoản
            var (ok, message) = await _auth.RegisterAsync(email, password, fullName, phone);
            TempData["Toast.Ok"] = ok ? "1" : "0";
            TempData["Toast.Message"] = message;
            return RedirectToAction("Index", "Home");
        }


        // ===================== ADMIN OTP: SEND =====================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SendAdminOtp(string email, string password)
        {
            var normalizedEmail = NormalizeEmail(email);

            if (normalizedEmail != AdminEmail)
                return Forbid();

            // Chỉ kiểm tra user tồn tại
            var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == normalizedEmail);
            if (user == null)
            {
                return BadRequest(new { ok = false, message = "Tài khoản admin không tồn tại." });
            }


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

            // Gửi OTP qua Formspree (server -> Formspree)
            var client = _httpClientFactory.CreateClient();

            var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                // Field "email" và "message" theo đúng form bạn đưa
                // Lưu ý: Formspree sẽ gửi về email đã cấu hình trong dashboard của form đó
                ["email"] = AdminEmail,
                ["message"] = $"[HagoTree Admin OTP] Mã OTP: {otp} (hết hạn sau 5 phút)."
            });

            var resp = await client.PostAsync(FormspreeEndpoint, content);
            if (!resp.IsSuccessStatusCode)
            {
                _cache.Remove(OtpCacheKey(normalizedEmail));
                return StatusCode((int)resp.StatusCode, new { ok = false, message = "Gửi OTP thất bại (Formspree)." });
            }

            return Json(new { ok = true, message = "OTP đã được gửi về email admin. Vui lòng kiểm tra hộp thư (kể cả spam)." });
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(string email, string password, bool rememberMe, string? otp)
        {
            var normalizedEmail = NormalizeEmail(email);

            var (user, message) = await _auth.ValidateLoginAsync(normalizedEmail, password);
            if (user == null)
            {
                TempData["Toast.Ok"] = "0";
                TempData["Toast.Message"] = message ?? "Email hoặc mật khẩu không đúng.";
                TempData["OpenLoginModal"] = "1";
                TempData["LoginEmail"] = normalizedEmail;
                return RedirectToAction("Index", "Home");
            }

            // Nếu là admin -> bắt buộc OTP
            if (normalizedEmail == AdminEmail)
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
                    TempData["Toast.Message"] = "OTP đã hết hạn hoặc chưa được gửi. Vui lòng bấm “Gửi OTP”.";
                    TempData["OpenLoginModal"] = "1";
                    TempData["LoginEmail"] = normalizedEmail;
                    return RedirectToAction("Index", "Home");
                }

                entry.Attempts++;
                if (entry.Attempts > OtpMaxAttempts)
                {
                    _cache.Remove(OtpCacheKey(normalizedEmail));
                    TempData["Toast.Ok"] = "0";
                    TempData["Toast.Message"] = "Bạn đã nhập sai OTP quá số lần cho phép. Vui lòng gửi OTP mới.";
                    TempData["OpenLoginModal"] = "1";
                    TempData["LoginEmail"] = normalizedEmail;
                    return RedirectToAction("Index", "Home");
                }

                var inputHash = HashOtp(normalizedEmail, otp.Trim());
                if (!string.Equals(entry.Hash, inputHash, StringComparison.OrdinalIgnoreCase))
                {
                    // giữ TTL như cũ
                    _cache.Set(OtpCacheKey(normalizedEmail), entry, new MemoryCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = OtpTtl
                    });

                    TempData["Toast.Ok"] = "0";
                    TempData["Toast.Message"] = "OTP không đúng.";
                    TempData["OpenLoginModal"] = "1";
                    TempData["LoginEmail"] = normalizedEmail;
                    return RedirectToAction("Index", "Home");
                }

                // OTP đúng -> consume
                _cache.Remove(OtpCacheKey(normalizedEmail));
            }

            var isAdmin = normalizedEmail == AdminEmail;

            await SignInAppAsync(user, rememberMe, isAdmin);

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
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            TempData["Toast.Ok"] = "1";
            TempData["Toast.Message"] = "Bạn đã đăng xuất.";

            return RedirectToAction("Index", "Home");
        }

        // =========================
        // Helper: Sign-in app cookie
        // =========================
        private async Task SignInAppAsync(User user, bool rememberMe, bool isAdmin = false)
        {
            var claims = new List<Claim>
    {
        new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
        new Claim(ClaimTypes.Name, user.FullName ?? user.Email),
        new Claim(ClaimTypes.Email, user.Email),
    };

            if (isAdmin)
                claims.Add(new Claim(ClaimTypes.Role, "Admin"));

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



        // ==========================================================
        // NEW: PROFILE MODAL (GET partial) + UPDATE PROFILE (POST)
        // ==========================================================

        private Guid? GetCurrentUserId()
        {
            var raw = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return Guid.TryParse(raw, out var id) ? id : null;
        }

        // GET: /Account/ProfileModal  (load vào modal)
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

            u.FullName = string.IsNullOrWhiteSpace(vm.FullName) ? null : vm.FullName.Trim();
            u.Phone = string.IsNullOrWhiteSpace(vm.Phone) ? null : vm.Phone.Trim();

            await _db.SaveChangesAsync();

            // Cập nhật lại cookie claims để header đổi ngay (User.Identity.Name)
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
    };

            if (user.Email == AdminEmail)
            {
                claims.Add(new Claim(ClaimTypes.Role, "Admin"));
            }

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                principal,
                props);
        }

    }
}
