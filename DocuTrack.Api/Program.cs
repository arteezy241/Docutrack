
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
        // Convert Railway PostgreSQL URL to Npgsql format
        var uri = new Uri(connectionString);
        var userInfo = uri.UserInfo.Split(':');
        connectionString = $"Host={uri.Host};Port={uri.Port};Database={uri.AbsolutePath.TrimStart('/')};Username={userInfo[0]};Password={userInfo[1]};SSL Mode=Require;Trust Server Certificate=true";
        options.UseNpgsql(connectionString);
    }
    else
    {
        options.UseSqlite(connectionString);
    }
});

var app = builder.Build();
// Auto-create database on startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<DocuTrack.Infrastructure.Data.DocuTrackDbContext>();
    db.Database.EnsureCreated();
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
app.UseCors("DevCorsPolicy");

app.MapControllers();

app.Run();
