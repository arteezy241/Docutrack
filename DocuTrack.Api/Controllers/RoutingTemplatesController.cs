using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DocuTrack.Core.Models;
using DocuTrack.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using System.Text.Json;

namespace DocuTrack.Api.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/routing-templates")]
    [Produces("application/json")]
    public class RoutingTemplatesController : ControllerBase
    {
        private readonly DocuTrackDbContext _db;

        public RoutingTemplatesController(DocuTrackDbContext db)
        {
            _db = db;
        }

        /// <summary>
        /// Get all active routing templates.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var templates = await _db.RoutingTemplates
                .Where(t => t.IsActive)
                .OrderBy(t => t.Name)
                .Select(t => new
                {
                    t.Id,
                    t.Name,
                    t.Description,
                    t.StepsJson,
                    t.CreatedById,
                    t.CreatedAt,
                })
                .ToListAsync();

            return Ok(templates);
        }

        /// <summary>
        /// Create a routing template.
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateRoutingTemplateDto dto)
        {
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (userId == null) return Unauthorized();

            var template = new RoutingTemplate
            {
                Id = Guid.NewGuid(),
                Name = dto.Name,
                Description = dto.Description,
                StepsJson = JsonSerializer.Serialize(dto.Steps),
                CreatedById = Guid.Parse(userId),
                CreatedAt = DateTime.UtcNow,
                IsActive = true,
            };

            _db.RoutingTemplates.Add(template);
            await _db.SaveChangesAsync();

            return Ok(new
            {
                template.Id,
                template.Name,
                template.Description,
                template.StepsJson,
                template.CreatedAt,
            });
        }

        /// <summary>
        /// Delete a routing template.
        /// </summary>
        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var template = await _db.RoutingTemplates.FindAsync(id);
            if (template == null) return NotFound();

            template.IsActive = false;
            await _db.SaveChangesAsync();

            return NoContent();
        }
    }

    public class RoutingTemplateStep
    {
        public string UserId { get; set; } = string.Empty;
        public string? Note { get; set; }
    }

    public class CreateRoutingTemplateDto
    {
        [System.ComponentModel.DataAnnotations.Required]
        [System.ComponentModel.DataAnnotations.StringLength(100, MinimumLength = 2)]
        public string Name { get; set; } = string.Empty;

        public string? Description { get; set; }

        [System.ComponentModel.DataAnnotations.Required]
        public List<RoutingTemplateStep> Steps { get; set; } = new();
    }
}