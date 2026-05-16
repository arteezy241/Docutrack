
using Microsoft.EntityFrameworkCore;
DocuTrack.Api.VapidKeys.Generate();

var builder = WebApplication.CreateBuilder(args);


// Add services to the container.
builder.Services.AddSwaggerGen();

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

    try
    {
        db.Database.Migrate();
    }
    catch (Exception)
    {
        // Tables already exist — add missing columns manually
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

        // Mark pending migrations as applied
        foreach (var migration in db.Database.GetPendingMigrations().ToList())
        {
            cmd.CommandText = $"INSERT INTO \"__EFMigrationsHistory\" (\"MigrationId\", \"ProductVersion\") VALUES ('{migration}', '10.0.0') ON CONFLICT DO NOTHING";
            cmd.ExecuteNonQuery();
        }

        conn.Close();
    }
}
app.Urls.Add("http://0.0.0.0:" + (Environment.GetEnvironmentVariable("PORT") ?? "8080"));

// Configure the HTTP request pipeline.
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "DocuTrack API v1");
    c.RoutePrefix = "swagger";
});
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

app.MapControllers();

app.Run();
// DocuTrack v2 - JWT Auth
