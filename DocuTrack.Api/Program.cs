using Microsoft.EntityFrameworkCore;
using AspNetCoreRateLimit;

var builder = WebApplication.CreateBuilder(args);

// OpenAPI
builder.Services.AddOpenApi();

// CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins(
                "https://docutrack-frontend-gray.vercel.app",
                "https://mheku.fyi",
                "https://www.mheku.fyi",
                "http://localhost:5173"
            )
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

// Rate Limiting
builder.Services.AddMemoryCache();
builder.Services.Configure<IpRateLimitOptions>(options =>
{
    options.EnableEndpointRateLimiting = true;
    options.StackBlockedRequests = false;
    options.HttpStatusCode = 429;
    options.RealIpHeader = "X-Real-IP";
    options.GeneralRules = new List<RateLimitRule>
    {
        // Global rule — max 100 requests per minute per IP
        new RateLimitRule
        {
            Endpoint = "*",
            Period = "1m",
            Limit = 100,
        },
        // Auth endpoints — stricter limit to prevent brute force
        new RateLimitRule
        {
            Endpoint = "POST:/api/auth/login",
            Period = "1m",
            Limit = 10,
        },
        new RateLimitRule
        {
            Endpoint = "POST:/api/auth/register",
            Period = "1m",
            Limit = 5,
        },
        new RateLimitRule
        {
            Endpoint = "POST:/api/auth/verify-device",
            Period = "1m",
            Limit = 10,
        },
        new RateLimitRule
        {
            Endpoint = "POST:/api/auth/google",
            Period = "1m",
            Limit = 10,
        },
        // OTP resend — very strict
        new RateLimitRule
        {
            Endpoint = "POST:/api/auth/resend-otp",
            Period = "5m",
            Limit = 3,
        },
    };
});
builder.Services.AddSingleton<IIpPolicyStore, MemoryCacheIpPolicyStore>();
builder.Services.AddSingleton<IRateLimitCounterStore, MemoryCacheRateLimitCounterStore>();
builder.Services.AddSingleton<IProcessingStrategy, AsyncKeyLockProcessingStrategy>();
builder.Services.AddSingleton<IRateLimitConfiguration, RateLimitConfiguration>();
builder.Services.AddInMemoryRateLimiting();

builder.Services.AddSingleton<DocuTrack.Api.Services.FileService>();

// Email Service
builder.Services.AddSingleton<DocuTrack.Api.Services.EmailService>();

// JWT Authentication
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidAudience = builder.Configuration["Jwt:Audience"],
        IssuerSigningKey = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(
            System.Text.Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!))
    };
});

builder.Services.AddAuthorization();

builder.Services.AddControllers()
    .AddJsonOptions(opts =>
    {
        opts.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
    });

// Database
builder.Services.AddDbContext<DocuTrack.Infrastructure.Data.DocuTrackDbContext>(options =>
{
    var connectionString = Environment.GetEnvironmentVariable("DATABASE_URL")
        ?? "Data Source=docutrack.db";

    if (connectionString.StartsWith("postgresql://") || connectionString.StartsWith("postgres://"))
    {
        var uri = new Uri(connectionString);
        var userInfo = uri.UserInfo.Split(':');
        connectionString = $"Host={uri.Host};Port={uri.Port};Database={uri.AbsolutePath.TrimStart('/')};Username={userInfo[0]};Password={userInfo[1]};SSL Mode=Require;Trust Server Certificate=true";
        options.UseNpgsql(connectionString);
    }
    else
    {
        options.UseSqlite(connectionString);
    }

    options.ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
});

var app = builder.Build();
// Global exception handler — no stack traces exposed to users
app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        context.Response.StatusCode = 500;
        context.Response.ContentType = "application/json";

        var exceptionFeature = context.Features
            .Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>();

        if (exceptionFeature != null)
        {
            // Log the real error (visible in Railway logs)
            Console.WriteLine($"[UNHANDLED ERROR] {exceptionFeature.Error}");

            // Return safe generic message to client
            await context.Response.WriteAsync(
                System.Text.Json.JsonSerializer.Serialize(new
                {
                    error = "An unexpected error occurred. Please try again.",
                    statusCode = 500
                })
            );
        }
    });
});

// Auto-migrate on startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<DocuTrack.Infrastructure.Data.DocuTrackDbContext>();
    var conn = db.Database.GetDbConnection();
    conn.Open();
    using var cmd = conn.CreateCommand();

    var alterCommands = new[]
    {
        "ALTER TABLE \"Users\" ADD COLUMN IF NOT EXISTS \"PasswordHash\" text",
        "ALTER TABLE \"Users\" ADD COLUMN IF NOT EXISTS \"Role\" text",
        "ALTER TABLE \"Users\" ADD COLUMN IF NOT EXISTS \"IsEmailVerified\" boolean NOT NULL DEFAULT false",
        "ALTER TABLE \"Users\" ADD COLUMN IF NOT EXISTS \"EmailVerificationOtp\" text",
        "ALTER TABLE \"Users\" ADD COLUMN IF NOT EXISTS \"OtpExpiry\" timestamp with time zone",
        "ALTER TABLE \"Users\" ADD COLUMN IF NOT EXISTS \"QrLoginToken\" text",
        "ALTER TABLE \"Users\" ADD COLUMN IF NOT EXISTS \"QrLoginExpiry\" timestamp with time zone",
        "ALTER TABLE \"Users\" ADD COLUMN IF NOT EXISTS \"IsActive\" boolean NOT NULL DEFAULT true",
        "ALTER TABLE \"Users\" ADD COLUMN IF NOT EXISTS \"CreatedAt\" timestamp with time zone NOT NULL DEFAULT now()",
        "ALTER TABLE \"TrustedDevices\" ADD COLUMN IF NOT EXISTS \"DeviceToken\" text NOT NULL DEFAULT ''",
        "ALTER TABLE \"Users\" ADD COLUMN IF NOT EXISTS \"PhoneNumber\" text",
        "ALTER TABLE \"Users\" ADD COLUMN IF NOT EXISTS \"IsPhoneVerified\" boolean NOT NULL DEFAULT false",
        "ALTER TABLE \"Users\" ADD COLUMN IF NOT EXISTS \"PhoneOtp\" text",
        "ALTER TABLE \"Users\" ADD COLUMN IF NOT EXISTS \"PhoneOtpExpiry\" timestamp with time zone",
        "ALTER TABLE \"Users\" ADD COLUMN IF NOT EXISTS \"TwoFactorMethod\" text",
        "ALTER TABLE \"Users\" ADD COLUMN IF NOT EXISTS \"PasswordResetOtp\" text",
        "ALTER TABLE \"Documents\" ADD COLUMN IF NOT EXISTS \"DueDate\" timestamp with time zone",
        "CREATE TABLE IF NOT EXISTS \"RoutingTemplates\" (\"Id\" uuid NOT NULL DEFAULT gen_random_uuid(), \"Name\" text NOT NULL, \"Description\" text, \"StepsJson\" text NOT NULL DEFAULT '[]', \"CreatedById\" uuid NOT NULL, \"IsActive\" boolean NOT NULL DEFAULT true, \"CreatedAt\" timestamptz NOT NULL DEFAULT now(), CONSTRAINT \"PK_RoutingTemplates\" PRIMARY KEY (\"Id\"))",
        "ALTER TABLE \"Users\" ADD COLUMN IF NOT EXISTS \"PasswordResetOtpExpiry\" timestamp with time zone",
        "CREATE TABLE IF NOT EXISTS \"Colleges\" (\"Id\" uuid NOT NULL DEFAULT gen_random_uuid(), \"Name\" text NOT NULL, \"Code\" text NOT NULL, \"Description\" text, \"IsActive\" boolean NOT NULL DEFAULT true, \"CreatedAt\" timestamptz NOT NULL DEFAULT now(), CONSTRAINT \"PK_Colleges\" PRIMARY KEY (\"Id\"))",
        "ALTER TABLE \"Departments\" ADD COLUMN IF NOT EXISTS \"CollegeId\" uuid REFERENCES \"Colleges\"(\"Id\")",
        "CREATE TABLE IF NOT EXISTS \"AuditLogs\" (\"Id\" uuid NOT NULL DEFAULT gen_random_uuid(), \"UserId\" uuid, \"UserEmail\" text, \"Action\" text NOT NULL, \"ResourceType\" text, \"ResourceId\" text, \"Details\" text, \"IpAddress\" text, \"Timestamp\" timestamptz NOT NULL DEFAULT now(), CONSTRAINT \"PK_AuditLogs\" PRIMARY KEY (\"Id\"))",
    };

    foreach (var sql in alterCommands)
    {
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    conn.Close();
    try { db.Database.Migrate(); } catch { }
}

app.Urls.Add("http://0.0.0.0:" + (Environment.GetEnvironmentVariable("PORT") ?? "8080"));

// Middleware pipeline — ORDER MATTERS
app.UseCors("AllowFrontend");
app.UseIpRateLimiting();         // ← rate limiting before everything else
app.UseDefaultFiles();
app.UseStaticFiles();
app.MapOpenApi();
app.UseMiddleware<DocuTrack.Api.Middleware.ApiKeyMiddleware>();
app.UseAuthentication();
app.UseAuthorization();

// Swagger UI
var docsHtml = """
<!DOCTYPE html>
<html>
<head>
    <title>DocuTrack API</title>
    <meta charset="utf-8"/>
    <link rel="stylesheet" type="text/css" href="https://unpkg.com/swagger-ui-dist@5/swagger-ui.css">
</head>
<body>
<div id="swagger-ui"></div>
<script src="https://unpkg.com/swagger-ui-dist@5/swagger-ui-bundle.js"></script>
<script>
    SwaggerUIBundle({
    url: "/openapi/v1.json",
    dom_id: '#swagger-ui',
    presets: [SwaggerUIBundle.presets.apis, SwaggerUIBundle.SwaggerUIStandalonePreset],
    layout: "BaseLayout",
    requestInterceptor: (request) => {
        request.url = request.url.replace('http://', 'https://');
        return request;
    }
})
</script>
</body>
</html>
""";

app.MapGet("/docs", () => Results.Content(docsHtml, "text/html"));
app.MapGet("/mobile", async context => context.Response.Redirect("/mobile.html", false));
app.MapControllers();

app.Run();