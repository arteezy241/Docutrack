using Microsoft.EntityFrameworkCore;
using DocuTrack.Infrastructure.Data;

namespace DocuTrack.Api.Middleware
{
    public class ApiKeyMiddleware
    {
        private readonly RequestDelegate _next;
        private static readonly string[] _excludedPaths = new[]
        {
            "/api/auth/login",
            "/api/auth/register",
            "/api/auth/verify-otp",
            "/api/auth/resend-otp",
            "/api/auth/qr-session",
            "/api/auth/qr-login",
            "/api/auth/qr-generate",
            "/openapi",
            "/docs",
            "/mobile",
        };

        public ApiKeyMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context, DocuTrackDbContext db)
        {
            // skip excluded paths
            var path = context.Request.Path.Value ?? "";
            if (_excludedPaths.Any(p => path.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
            {
                await _next(context);
                return;
            }

            // skip if already authenticated via JWT
            if (context.User.Identity?.IsAuthenticated == true)
            {
                await _next(context);
                return;
            }

            // check for API key header
            if (!context.Request.Headers.TryGetValue("X-API-Key", out var apiKeyValue))
            {
                await _next(context);
                return;
            }

            var key = apiKeyValue.ToString();
            var apiKey = await db.ApiKeys
                .FirstOrDefaultAsync(k => k.Key == key && k.IsActive);

            if (apiKey == null)
            {
                context.Response.StatusCode = 401;
                await context.Response.WriteAsJsonAsync(new { error = "Invalid or inactive API key" });
                return;
            }

            // update last used
            apiKey.LastUsedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync();

            await _next(context);
        }
    }
}