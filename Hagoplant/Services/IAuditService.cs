using Hagoplant.Models;

namespace Hagoplant.Services
{
    /// <summary>
    /// Interface cho Audit Logging Service
    /// </summary>
    public interface IAuditService
    {
        /// <summary>Ghi log hành động</summary>
        Task LogAsync(
            string action,
            string? entityType = null,
            string? entityId = null,
            string? details = null,
            string result = AuditResult.Success,
            string? errorMessage = null,
            Guid? userId = null,
            string? userEmail = null);

        /// <summary>Lấy IP từ HttpContext (hỗ trợ proxy)</summary>
        string? GetClientIp();

        /// <summary>Lấy User-Agent từ HttpContext</summary>
        string? GetUserAgent();
    }
}
