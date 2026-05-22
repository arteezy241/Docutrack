using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DocuTrack.Core.Models;
using DocuTrack.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;

namespace DocuTrack.Api.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/colleges")]
    [Produces("application/json")]
    public class CollegesController : ControllerBase
    {
        private readonly DocuTrackDbContext _db;

        public CollegesController(DocuTrackDbContext db)
        {
            _db = db;
        }

        /// <summary>
        /// Get all colleges with their departments.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var colleges = await _db.Colleges
                .Where(c => c.IsActive)
                .Include(c => c.Departments)
                .OrderBy(c => c.Name)
                .Select(c => new
                {
                    c.Id,
                    c.Name,
                    c.Code,
                    c.Description,
                    c.IsActive,
                    c.CreatedAt,
                    Departments = c.Departments.Select(d => new
                    {
                        d.Id,
                        d.Name,
                        d.Description,
                    })
                })
                .ToListAsync();

            return Ok(colleges);
        }

        /// <summary>
        /// Create a new college — Admin only.
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateCollegeDto dto)
        {
            var role = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;
            if (role != "Admin") return Forbid();

            var college = new College
            {
                Id = Guid.NewGuid(),
                Name = dto.Name,
                Code = dto.Code,
                Description = dto.Description,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
            };

            _db.Colleges.Add(college);
            await _db.SaveChangesAsync();

            return Ok(college);
        }

        /// <summary>
        /// Update a college — Admin only.
        /// </summary>
        [HttpPatch("{id:guid}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] CreateCollegeDto dto)
        {
            var role = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;
            if (role != "Admin") return Forbid();

            var college = await _db.Colleges.FindAsync(id);
            if (college == null) return NotFound();

            college.Name = dto.Name;
            college.Code = dto.Code;
            college.Description = dto.Description;
            await _db.SaveChangesAsync();

            return Ok(college);
        }

        /// <summary>
        /// Delete a college — Admin only.
        /// </summary>
        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var role = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;
            if (role != "Admin") return Forbid();

            var college = await _db.Colleges.FindAsync(id);
            if (college == null) return NotFound();

            // Soft delete
            college.IsActive = false;
            await _db.SaveChangesAsync();

            return NoContent();
        }

        /// <summary>
        /// Seed LPU-Cavite colleges — Admin only, run once.
        /// </summary>
        [HttpPost("seed")]
        public async Task<IActionResult> Seed()
        {
            var role = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;
            if (role != "Admin") return Forbid();

            if (await _db.Colleges.AnyAsync())
                return BadRequest(new { error = "Colleges already seeded." });

            var colleges = new[]
            {
                new College { Id = Guid.NewGuid(), Name = "College of Allied Medical Science", Code = "CAMS", IsActive = true, CreatedAt = DateTime.UtcNow },
                new College { Id = Guid.NewGuid(), Name = "College of Arts and Sciences", Code = "CAS", IsActive = true, CreatedAt = DateTime.UtcNow },
                new College { Id = Guid.NewGuid(), Name = "College of Business Administration", Code = "CBA", IsActive = true, CreatedAt = DateTime.UtcNow },
                new College { Id = Guid.NewGuid(), Name = "College of Fine Arts and Design", Code = "CFAD", IsActive = true, CreatedAt = DateTime.UtcNow },
                new College { Id = Guid.NewGuid(), Name = "College of International Tourism and Hospitality Management", Code = "CITHM", IsActive = true, CreatedAt = DateTime.UtcNow },
                new College { Id = Guid.NewGuid(), Name = "College of Nursing", Code = "CON", IsActive = true, CreatedAt = DateTime.UtcNow },
                new College { Id = Guid.NewGuid(), Name = "College of Engineering and Architecture", Code = "CEA", IsActive = true, CreatedAt = DateTime.UtcNow },
                new College { Id = Guid.NewGuid(), Name = "International High School", Code = "IS", IsActive = true, CreatedAt = DateTime.UtcNow },
                new College { Id = Guid.NewGuid(), Name = "Graduate School", Code = "GS", IsActive = true, CreatedAt = DateTime.UtcNow },
            };

            _db.Colleges.AddRange(colleges);
            await _db.SaveChangesAsync();

            return Ok(new { message = "LPU-Cavite colleges seeded.", count = colleges.Length });
        }
    }

    public class CreateCollegeDto
    {
        [System.ComponentModel.DataAnnotations.Required]
        [System.ComponentModel.DataAnnotations.StringLength(200, MinimumLength = 2)]
        public string Name { get; set; } = string.Empty;

        [System.ComponentModel.DataAnnotations.Required]
        [System.ComponentModel.DataAnnotations.StringLength(20, MinimumLength = 2)]
        public string Code { get; set; } = string.Empty;

        public string? Description { get; set; }
    }
}