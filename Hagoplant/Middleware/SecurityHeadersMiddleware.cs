namespace Hagoplant.Middleware
{
    /// <summary>
    /// Security Headers Middleware
    /// Thêm các HTTP security headers vào mọi response
    /// </summary>
    public class SecurityHeadersMiddleware
    {
        private readonly RequestDelegate _next;

        public SecurityHeadersMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var headers = context.Response.Headers;

            // Ngăn chặn clickjacking
            headers["X-Frame-Options"] = "SAMEORIGIN";

            // Ngăn chặn MIME sniffing
            headers["X-Content-Type-Options"] = "nosniff";

            // XSS Protection (legacy browser)
            headers["X-XSS-Protection"] = "1; mode=block";

            // Referrer Policy
            headers["Referrer-Policy"] = "strict-origin-when-cross-origin";

            // Permissions Policy (disable không cần thiết)
            headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=(), payment=()";

            // Content Security Policy (Đã nới lỏng để hỗ trợ tính năng bên thứ 3 và Dev tools)
            headers["Content-Security-Policy"] =
                "default-src 'self' * 'unsafe-inline' 'unsafe-eval' data: blob: ws: wss:; " +
                "script-src 'self' * 'unsafe-inline' 'unsafe-eval' data: blob:; " +
                "style-src 'self' * 'unsafe-inline' data:; " +
                "font-src 'self' * data:; " +
                "img-src 'self' * data: blob:; " +
                "connect-src 'self' * 'unsafe-inline' data: blob: ws: wss:; " +
                "frame-ancestors 'self'; " +
                "base-uri 'self' *; " +
                "form-action 'self' *;";

            // Remove server info
            headers.Remove("Server");
            headers.Remove("X-Powered-By");

            await _next(context);
        }
    }

    // Extension method
    public static class SecurityHeadersMiddlewareExtensions
    {
        public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app)
            => app.UseMiddleware<SecurityHeadersMiddleware>();
    }
}
