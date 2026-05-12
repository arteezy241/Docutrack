
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
builder.Services.AddControllers()
    .AddJsonOptions(opts =>
    {
        // prevent possible reference cycles when returning entities with navigation props
        opts.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
    });

// Register DbContext (use same SQLite file as design-time factory)
builder.Services.AddDbContext<DocuTrack.Infrastructure.Data.DocuTrackDbContext>(options =>
{
    options.UseSqlite("Data Source=docutrack.db");
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

app.UseStaticFiles();
app.UseCors("DevCorsPolicy");

app.MapControllers();

app.Run();
