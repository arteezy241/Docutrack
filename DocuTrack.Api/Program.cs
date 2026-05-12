
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

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    // Using Swashbuckle (UseSwagger + UseSwaggerUI) instead of Microsoft.AspNetCore.OpenApi

    // enable Swagger UI at /swagger
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "DocuTrack API v1");
        c.RoutePrefix = "swagger"; // serve UI at /swagger
    });

    // Enable permissive CORS in development
    app.UseCors("DevCorsPolicy");

    // redirect /swagger to the UI index for convenience
    app.MapGet("/swagger", ctx =>
    {
        ctx.Response.Redirect("/swagger/index.html", false);
        return System.Threading.Tasks.Task.CompletedTask;
    });
}

app.UseHttpsRedirection();

app.UseStaticFiles();
app.UseCors("DevCorsPolicy");

app.MapControllers();

app.Run();
