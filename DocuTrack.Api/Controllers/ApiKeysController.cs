using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DocuTrack.Infrastructure.Data;

namespace DocuTrack.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ApiKeysController : ControllerBase
    {
        private readonly DocuTrackDbContext _db;

        public ApiKeysController(DocuTrackDbContext db)
        {
            _db = db;
        }

        [HttpGet]
        public async Task<IActionResult> GetMyKeys()
        {
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (userId == null) return Unauthorized();

            var keys = await _db.ApiKeys
                .Where(k => k.UserId == Guid.Parse(userId))
                .OrderByDescending(k => k.CreatedAt)
                .ToListAsync();

            return Ok(keys.Select(k => new
            {
                k.Id,
                k.Name,
                k.Key,
                k.IsActive,
                k.CreatedAt,
                k.LastUsedAt
            }));
        }

        [HttpPost]
        public async Task<IActionResult> CreateKey([FromBody] CreateApiKeyRequest request)
        {
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (userId == null) return Unauthorized();

            var apiKey = new DocuTrack.Core.Models.ApiKey
            {
                Id = Guid.NewGuid(),
                Key = $"dt_{Guid.NewGuid():N}{Guid.NewGuid():N}",
                UserId = Guid.Parse(userId),
                Name = request.Name ?? "Default",
                IsActive = true,
                CreatedAt = DateTimeOffset.UtcNow,
            };

            _db.ApiKeys.Add(apiKey);
            await _db.SaveChangesAsync();

            return Ok(new
            {
                apiKey.Id,
                apiKey.Name,
                apiKey.Key,
                apiKey.IsActive,
                apiKey.CreatedAt
            });
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> RevokeKey(Guid id)
        {
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (userId == null) return Unauthorized();

            var key = await _db.ApiKeys
                .FirstOrDefaultAsync(k => k.Id == id && k.UserId == Guid.Parse(userId));

            if (key == null) return NotFound();

            _db.ApiKeys.Remove(key);
            await _db.SaveChangesAsync();

            return Ok(new { message = "API key revoked" });
        }

        [HttpPatch("{id}/toggle")]
        public async Task<IActionResult> ToggleKey(Guid id)
        {
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (userId == null) return Unauthorized();

            var key = await _db.ApiKeys
                .FirstOrDefaultAsync(k => k.Id == id && k.UserId == Guid.Parse(userId));

            if (key == null) return NotFound();

            key.IsActive = !key.IsActive;
            await _db.SaveChangesAsync();

            return Ok(new { key.IsActive });
        }
    }

    public class CreateApiKeyRequest
    {
        public string? Name { get; set; }
    }
}