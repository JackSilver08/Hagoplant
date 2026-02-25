using System.Net;
using System.Text.Json;

namespace Hagoplant.Middleware
{
    /// <summary>
    /// Global Exception Handler Middleware
    /// Bắt mọi unhandled exception, log chi tiết, trả về response phù hợp
    /// Không để lộ stack trace ra ngoài ở production
    /// </summary>
    public class GlobalExceptionMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<GlobalExceptionMiddleware> _logger;
        private readonly IWebHostEnvironment _env;

        public GlobalExceptionMiddleware(
            RequestDelegate next,
            ILogger<GlobalExceptionMiddleware> logger,
            IWebHostEnvironment env)
        {
            _next = next;
            _logger = logger;
            _env = env;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                await HandleExceptionAsync(context, ex);
            }
        }

        private async Task HandleExceptionAsync(HttpContext context, Exception ex)
        {
            // Log chi tiết exception với correlation ID
            var correlationId = context.TraceIdentifier;
            _logger.LogError(ex,
                "Unhandled exception. CorrelationId: {CorrelationId}, Path: {Path}, Method: {Method}",
                correlationId,
                context.Request.Path,
                context.Request.Method);

            // Nếu đã bắt đầu response, không thể sửa status code
            if (context.Response.HasStarted)
            {
                _logger.LogWarning("Response already started, cannot modify headers");
                return;
            }

            context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;

            // API request → trả JSON
            if (IsApiRequest(context))
            {
                context.Response.ContentType = "application/json";
                var response = new
                {
                    ok = false,
                    message = "Đã xảy ra lỗi trong quá trình xử lý. Vui lòng thử lại sau.",
                    correlationId,
                    // Chỉ hiển thị detail ở Development
                    detail = _env.IsDevelopment() ? ex.Message : null,
                    stackTrace = _env.IsDevelopment() ? ex.StackTrace : null
                };
                await context.Response.WriteAsync(JsonSerializer.Serialize(response));
            }
            else
            {
                // Web request → redirect tới trang lỗi
                context.Response.Redirect($"/Home/Error?code=500");
            }
        }

        private static bool IsApiRequest(HttpContext context)
        {
            var acceptHeader = context.Request.Headers["Accept"].FirstOrDefault() ?? "";
            var contentType = context.Request.ContentType ?? "";
            var path = context.Request.Path.Value ?? "";

            return acceptHeader.Contains("application/json")
                || contentType.Contains("application/json")
                || path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase)
                || (context.Request.Headers["X-Requested-With"] == "XMLHttpRequest");
        }
    }

    public static class GlobalExceptionMiddlewareExtensions
    {
        public static IApplicationBuilder UseGlobalExceptionHandler(this IApplicationBuilder app)
            => app.UseMiddleware<GlobalExceptionMiddleware>();
    }
}
