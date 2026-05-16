
using Microsoft.EntityFrameworkCore;



var builder = WebApplication.CreateBuilder(args);




// Add services to the container.
builder.Services.AddOpenApi();

// CORS for development - allow any origin/header/method
builder.Services.AddCors(options =>
{
    options.AddPolicy("DevCorsPolicy", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// Add controllers
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
        // prevent possible reference cycles when returning entities with navigation props
        opts.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
    });

// Register DbContext (use same SQLite file as design-time factory)
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
// Auto-create database on startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<DocuTrack.Infrastructure.Data.DocuTrackDbContext>();
    var conn = db.Database.GetDbConnection();
    conn.Open();
    using var cmd = conn.CreateCommand();

    // Add missing auth columns if they don't exist
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

    // Now migrate
    try { db.Database.Migrate(); } catch { }
}
app.Urls.Add("http://0.0.0.0:" + (Environment.GetEnvironmentVariable("PORT") ?? "8080"));

// Configure the HTTP request pipeline.
app.MapOpenApi();
app.MapGet("/swagger", () => Results.Redirect("/swagger/index.html"));
app.UseStaticFiles();
app.UseCors("DevCorsPolicy");

if (app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

var defaultFilesOptions = new DefaultFilesOptions();
defaultFilesOptions.DefaultFileNames.Clear();
defaultFilesOptions.DefaultFileNames.Add("index.html");
app.UseDefaultFiles(defaultFilesOptions);
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.UseCors("DevCorsPolicy");


// DocuTrack v2 - JWT Auth

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
        layout: "BaseLayout"
    })
</script>
</body>
</html>
""";

app.MapGet("/docs", () => Results.Content(docsHtml, "text/html"));
app.MapControllers();

app.Run();