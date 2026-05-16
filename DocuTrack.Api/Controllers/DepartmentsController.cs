using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DocuTrack.Core.Models;
using DocuTrack.Infrastructure.Data;

namespace DocuTrack.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    public class DepartmentsController : ControllerBase
    {
        private readonly DocuTrackDbContext _db;

        public DepartmentsController(DocuTrackDbContext db)
        {
            _db = db;
        }

        public class CreateDepartmentDto
        {
            public string? Name { get; set; }
            public string? Description { get; set; }
        }

        /// <summary>
        /// Lists all departments.
        /// </summary>
        [HttpGet]
        [ProducesResponseType(typeof(IEnumerable<Department>), StatusCodes.Status200OK)]
        public async Task<ActionResult<IEnumerable<Department>>> GetAll()
        {
            var departments = await _db.Departments
                .Include(d => d.Users)
                .AsNoTracking()
                .ToListAsync();
            return Ok(departments);
        }

        /// <summary>
        /// Gets a single department by id.
        /// </summary>
        [HttpGet("{id:guid}")]
        [ProducesResponseType(typeof(Department), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<Department>> GetOne(Guid id)
        {
            var dept = await _db.Departments.AsNoTracking().FirstOrDefaultAsync(d => d.Id == id);
            if (dept == null) return NotFound();
            return Ok(dept);
        }

        /// <summary>
        /// Creates a new department.
        /// </summary>
        [HttpPost]
        [ProducesResponseType(typeof(Department), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<Department>> Create(CreateDepartmentDto dto)
        {
            var dept = new Department
            {
                Id = Guid.NewGuid(),
                Name = dto.Name,
                Description = dto.Description
            };

            _db.Departments.Add(dept);
            await _db.SaveChangesAsync();

            return CreatedAtAction(nameof(GetOne), new { id = dept.Id }, dept);
        }

        /// <summary>
        /// Deletes a department.
        /// </summary>
        [HttpDelete("{id:guid}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Delete(Guid id)
        {
            var dept = await _db.Departments.FindAsync(id);
            if (dept == null) return NotFound();

            _db.Departments.Remove(dept);
            await _db.SaveChangesAsync();

            return NoContent();
        }
    }
}