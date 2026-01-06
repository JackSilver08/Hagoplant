using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Hagoplant.DBcontext;
using Hagoplant.Models;
using Hagoplant.Services;


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
    }
}
