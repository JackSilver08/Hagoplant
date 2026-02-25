using Hagoplant.DBcontext;
using Hagoplant.Models;
using System.Security.Claims;

namespace Hagoplant.Services
{
    /// <summary>
    /// Audit Logging Service - ghi lại mọi hành động quan trọng vào DB
    /// Thread-safe, không throw exception để tránh ảnh hưởng business flow
    /// </summary>
    public class AuditService : IAuditService
    {
        private readonly HagoDbContext _db;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILogger<AuditService> _logger;

        public AuditService(
            HagoDbContext db,
            IHttpContextAccessor httpContextAccessor,
            ILogger<AuditService> logger)
        {
            _db = db;
            _httpContextAccessor = httpContextAccessor;
            _logger = logger;
        }

        public async Task LogAsync(
            string action,
            string? entityType = null,
            string? entityId = null,
            string? details = null,
            string result = AuditResult.Success,
            string? errorMessage = null,
            Guid? userId = null,
            string? userEmail = null)
        {
            try
            {
                var ctx = _httpContextAccessor.HttpContext;

                // Auto-detect từ HttpContext nếu không truyền vào
                var resolvedUserId = userId ?? GetCurrentUserId(ctx);
                var resolvedEmail = userEmail ?? GetCurrentUserEmail(ctx);

                var log = new AuditLog
                {
                    Id = Guid.NewGuid(),
                    UserId = resolvedUserId,
                    UserEmail = resolvedEmail,
                    Action = action,
                    EntityType = entityType,
                    EntityId = entityId,
                    Details = details,
                    IpAddress = GetClientIp(),
                    UserAgent = GetUserAgent(),
                    Result = result,
                    ErrorMessage = errorMessage,
                    CreatedAt = DateTimeOffset.UtcNow
                };

                _db.AuditLogs.Add(log);
                await _db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                // Audit log KHÔNG ĐƯỢC làm hỏng business flow
                _logger.LogError(ex, "Failed to write audit log. Action={Action}", action);
            }
        }

        public string? GetClientIp()
        {
            var ctx = _httpContextAccessor.HttpContext;
            if (ctx == null) return null;

            // Hỗ trợ X-Forwarded-For (proxy/load balancer)
            var forwardedFor = ctx.Request.Headers["X-Forwarded-For"].FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(forwardedFor))
            {
                // Lấy IP đầu tiên (client thực)
                return forwardedFor.Split(',')[0].Trim();
            }

            // Fallback: Real IP (Nginx)
            var realIp = ctx.Request.Headers["X-Real-IP"].FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(realIp))
                return realIp;

            return ctx.Connection.RemoteIpAddress?.ToString();
        }

        public string? GetUserAgent()
        {
            return _httpContextAccessor.HttpContext?
                .Request.Headers["User-Agent"].FirstOrDefault();
        }

        private static Guid? GetCurrentUserId(HttpContext? ctx)
        {
            var raw = ctx?.User?.FindFirstValue(ClaimTypes.NameIdentifier);
            return Guid.TryParse(raw, out var id) ? id : null;
        }

        private static string? GetCurrentUserEmail(HttpContext? ctx)
        {
            return ctx?.User?.FindFirstValue(ClaimTypes.Email);
        }
    }
}
