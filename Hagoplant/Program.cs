using Hagoplant.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.EntityFrameworkCore;
using Hagoplant.DBcontext;


var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();


// =========================
// DATABASE
// =========================
var cs = builder.Configuration.GetConnectionString("DefaultConnection");

if (string.IsNullOrWhiteSpace(cs))
{
    throw new InvalidOperationException(
        "Missing connection string 'DefaultConnection'. Check appsettings.json"
    );
}

builder.Services.AddDbContext<HagoDbContext>(options =>
{
    options.UseNpgsql(cs);
});

builder.Services.AddScoped<AuthService>();



// =========================
// AUTHENTICATION
// =========================
builder.Services
    .AddAuthentication(options =>
    {
        // Cookie chính của app
        options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;

      
    })

    // Cookie đăng nhập của hệ thống
    .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, options =>
    {
        options.LoginPath = "/";
        options.LogoutPath = "/Account/Logout";
        options.AccessDeniedPath = "/";
        options.Cookie.Name = "Hago.Auth";
        options.SlidingExpiration = true;
        options.ExpireTimeSpan = TimeSpan.FromDays(7);
    });

builder.Services.AddAuthorization();


// =========================
// PIPELINE
// =========================
var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

// ⚠️ THỨ TỰ BẮT BUỘC
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
