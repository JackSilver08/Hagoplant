using Hagoplant.DBcontext;
using Hagoplant.Models;
using Hagoplant.Services;
using Hagoplant.ViewModels;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;


namespace Hagoplant.Controllers
{
    public class AccountController : Controller
    {
       
        private readonly AuthService _auth;
        private readonly HagoDbContext _db;

        public AccountController(AuthService auth, HagoDbContext db)
        {
            _auth = auth;
            _db = db;
         
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

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(string email, string password, bool rememberMe)
        {
            var (user, message) = await _auth.ValidateLoginAsync(email, password);

            if (user == null)
            {
                TempData["Toast.Ok"] = "0";
                TempData["Toast.Message"] = message;
                return RedirectToAction("Index", "Home");
            }

            await SignInAppAsync(user, rememberMe);

            TempData["Toast.Ok"] = "1";
            TempData["Toast.Message"] = "Đăng nhập thành công.";

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
        private async Task SignInAppAsync(User user, bool rememberMe)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.FullName ?? user.Email),
                new Claim(ClaimTypes.Email, user.Email),
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
            // Lấy properties hiện tại (IsPersistent/ExpiresUtc) để giữ nguyên rememberMe
            var authResult = await HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            var props = authResult?.Properties ?? new AuthenticationProperties();

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.FullName ?? user.Email),
                new Claim(ClaimTypes.Email, user.Email),
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
