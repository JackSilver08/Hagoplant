using Hagoplant.DBcontext;
using Hagoplant.Middleware;
using Hagoplant.Models;
using Hagoplant.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;
using System.Threading.RateLimiting;

// =============================================
// Configure Serilog TRƯỚC khi build app
// =============================================
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .Enrich.WithMachineName()
    .Enrich.WithThreadId()
    .WriteTo.Console(outputTemplate:
        "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}")
    .WriteTo.File(
        path: "logs/hagoplant-.log",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 30,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

try
{
    Log.Information("Starting Hagoplant application...");

    var builder = WebApplication.CreateBuilder(args);

    // Sử dụng Serilog thay vì default logging
    builder.Host.UseSerilog();

    // =============================================
    // MVC
    // =============================================
    builder.Services.AddControllersWithViews(options =>
    {
        // Global CSRF protection (áp dụng cho mọi POST)
        options.Filters.Add(new Microsoft.AspNetCore.Mvc.AutoValidateAntiforgeryTokenAttribute());
    });

    // =============================================
    // DATABASE
    // =============================================
    var cs = builder.Configuration.GetConnectionString("DefaultConnection");
    if (string.IsNullOrWhiteSpace(cs))
        throw new InvalidOperationException(
            "Missing connection string 'DefaultConnection'. Check appsettings.json or environment variables.");

    builder.Services.AddDbContext<HagoDbContext>(options =>
        options.UseNpgsql(cs));

    // =============================================
    // SERVICES
    // =============================================
    builder.Services.Configure<PayOsOptions>(
        builder.Configuration.GetSection("PayOs"));

    builder.Services.AddHttpClient<PayOsClient>();
    builder.Services.AddHttpClient();

    // AuthService
    builder.Services.AddScoped<AuthService>();

    // AuditService (Scoped để dùng được DbContext)
    builder.Services.AddHttpContextAccessor();
    builder.Services.AddScoped<IAuditService, AuditService>();

    // MemoryCache (cho OTP)
    builder.Services.AddMemoryCache();

    // =============================================
    // RATE LIMITING (ASP.NET Core built-in - .NET 7+)
    // =============================================
    builder.Services.AddRateLimiter(options =>
    {
        // Rate limit cho login endpoint: 10 requests/phút/IP
        options.AddFixedWindowLimiter("LoginRateLimit", opt =>
        {
            opt.Window = TimeSpan.FromMinutes(1);
            opt.PermitLimit = 10;
            opt.QueueLimit = 0;
        });

        // Rate limit cho OTP endpoint: 5 requests/5 phút/IP
        options.AddFixedWindowLimiter("OtpRateLimit", opt =>
        {
            opt.Window = TimeSpan.FromMinutes(5);
            opt.PermitLimit = 5;
            opt.QueueLimit = 0;
        });

        // Rate limit toàn cục: 200 requests/phút/IP
        options.AddFixedWindowLimiter("GlobalRateLimit", opt =>
        {
            opt.Window = TimeSpan.FromMinutes(1);
            opt.PermitLimit = 200;
            opt.QueueLimit = 0;
        });

        options.OnRejected = async (context, cancellationToken) =>
        {
            context.HttpContext.Response.StatusCode = 429;
            await context.HttpContext.Response.WriteAsync(
                "Quá nhiều yêu cầu. Vui lòng thử lại sau.", cancellationToken);
        };
    });

    // =============================================
    // SESSION (cho CartId)
    // =============================================
    builder.Services.AddDistributedMemoryCache();
    builder.Services.AddSession(options =>
    {
        options.Cookie.Name = "Hago.Session";
        options.Cookie.HttpOnly = true;
        options.Cookie.IsEssential = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always; // Chỉ gửi qua HTTPS
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.IdleTimeout = TimeSpan.FromDays(7);
    });

    // =============================================
    // AUTHENTICATION
    // =============================================
    builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/";
        options.LogoutPath = "/Account/Logout";
        options.AccessDeniedPath = "/Home/AccessDenied";
        options.Cookie.Name = "Hago.Auth";
        options.Cookie.HttpOnly = true;               // Chặn JS đọc cookie
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always; // Chỉ HTTPS
        options.Cookie.SameSite = SameSiteMode.Lax;   // Chống CSRF
        options.SlidingExpiration = true;
        options.ExpireTimeSpan = TimeSpan.FromDays(7);
    });

    builder.Services.AddAuthorization();

    // =============================================
    // ANTIFORGERY
    // =============================================
    builder.Services.AddAntiforgery(options =>
    {
        options.Cookie.Name = "Hago.CSRF";
        options.Cookie.HttpOnly = false;    // Angular/React cần đọc
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.Cookie.SameSite = SameSiteMode.Lax;
    });

    // =============================================
    // BUILD APP
    // =============================================
    var app = builder.Build();

    // =============================================
    // Auto-migrate + seed admin user
    // =============================================
    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<HagoDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

        try
        {
            // Auto-migrate (an toàn khi có pending migrations)
            await db.Database.MigrateAsync();
            logger.LogInformation("Database migration completed.");

            // Seed admin user nếu chưa có
            await SeedAdminAsync(db, builder.Configuration, logger);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during database migration/seeding");
        }
    }

    // =============================================
    // MIDDLEWARE PIPELINE
    // =============================================

    // Global Exception Handler (PHẢI ĐẶT ĐẦU TIÊN)
    app.UseGlobalExceptionHandler();

    // Forward headers khi chạy sau proxy/IIS/Nginx
    app.UseForwardedHeaders(new ForwardedHeadersOptions
    {
        ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
    });

    // Error handling
    if (!app.Environment.IsDevelopment())
    {
        app.UseExceptionHandler("/Home/Error");
        app.UseHsts();
    }

    // ✅ HTTPS Redirection - BẬT LẠI (đây là lỗi bảo mật nghiêm trọng khi tắt)
    // Chú ý: Nếu chạy sau reverse proxy (Nginx/IIS), proxy sẽ xử lý SSL,
    // app chạy HTTP nội bộ → Uncomment dòng này chỉ khi app expose HTTPS trực tiếp
    // app.UseHttpsRedirection();

    // Security Headers
    app.UseSecurityHeaders();

    app.UseStaticFiles();
    app.UseRouting();

    // Rate Limiting (trước Auth để block sớm)
    app.UseRateLimiter();

    // Session
    app.UseSession();

    // Auth middleware
    app.UseAuthentication();
    app.UseAuthorization();

    // Serilog request logging
    app.UseSerilogRequestLogging(opts =>
    {
        opts.MessageTemplate = "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms";
    });

    // =============================================
    // ROUTING
    // =============================================
    app.MapControllerRoute(
        name: "default",
        pattern: "{controller=Home}/{action=Index}/{id?}");

    // PayOS Webhook route (explicit)
    app.MapControllerRoute(
        name: "payos_webhook",
        pattern: "payments/webhook/payos",
        defaults: new { controller = "Payments", action = "PayOsWebhook" });

    Log.Information("Hagoplant started successfully.");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
// =============================================
// Seed Admin User (helper)
// =============================================
static async Task SeedAdminAsync(
    HagoDbContext db,
    IConfiguration config,
    Microsoft.Extensions.Logging.ILogger logger)
{
    // Lấy admin email từ config (không hardcode)
    var adminEmail = config["AdminSettings:Email"];
    var adminPassword = config["AdminSettings:Password"];

    if (string.IsNullOrWhiteSpace(adminEmail) || string.IsNullOrWhiteSpace(adminPassword))
    {
        logger.LogWarning("AdminSettings:Email or AdminSettings:Password not configured. Skipping admin seed.");
        return;
    }

    adminEmail = adminEmail.Trim().ToLowerInvariant();

    var adminUser = await db.Users.FirstOrDefaultAsync(u =>
        u.Email == adminEmail && u.Role == UserRoles.Admin);

    var hasher = new Microsoft.AspNetCore.Identity.PasswordHasher<Hagoplant.Models.User>();

    if (adminUser == null)
    {
        adminUser = new Hagoplant.Models.User
        {
            Id = Guid.NewGuid(),
            Email = adminEmail,
            FullName = "Administrator",
            Role = UserRoles.Admin,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        adminUser.PasswordHash = hasher.HashPassword(adminUser, adminPassword);

        db.Users.Add(adminUser);
        await db.SaveChangesAsync();

        logger.LogInformation("Admin user seeded: {Email}", adminEmail);
    }
    else
    {
        var verifyResult = hasher.VerifyHashedPassword(adminUser, adminUser.PasswordHash, adminPassword);
        if (verifyResult == Microsoft.AspNetCore.Identity.PasswordVerificationResult.Failed)
        {
            adminUser.PasswordHash = hasher.HashPassword(adminUser, adminPassword);
            db.Users.Update(adminUser);
            await db.SaveChangesAsync();
            logger.LogInformation("Admin user password updated: {Email}", adminEmail);
        }
    }
}
