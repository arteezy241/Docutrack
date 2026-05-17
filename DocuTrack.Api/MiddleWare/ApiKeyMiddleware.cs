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

            // set user identity so [Authorize] endpoints accept API key auth
            var claims = new[]
            {
        new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, apiKey.UserId.ToString()),
        new System.Security.Claims.Claim("ApiKeyAuth", "true")
    };
            var identity = new System.Security.Claims.ClaimsIdentity(claims, "ApiKey");
            var principal = new System.Security.Claims.ClaimsPrincipal(identity);
            context.User = principal;

            await _next(context);
        }
    }
}