using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// OpenAPI
builder.Services.AddOpenApi();

// CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("DevCorsPolicy", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

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
app.UseCors("DevCorsPolicy");
app.UseDefaultFiles();
app.UseStaticFiles();
app.MapOpenApi();
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