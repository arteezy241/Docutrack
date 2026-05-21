using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DocuTrack.Core.Models;
using DocuTrack.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;

namespace DocuTrack.Api.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    public class UsersController : ControllerBase
    {
        private readonly DocuTrackDbContext _db;

        public UsersController(DocuTrackDbContext db)
        {
            _db = db;
        }

        public class CreateUserDto
        {
            public string? Username { get; set; }
            public string? Email { get; set; }
            public string? FullName { get; set; }
        }

        /// <summary>
        /// Creates a new user.
        /// </summary>
        [HttpPost]
        [ProducesResponseType(typeof(User), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<User>> Create(CreateUserDto dto)
        {
            var user = new User
            {
                Id = Guid.NewGuid(),
                Username = dto.Username,
                Email = dto.Email,
                FullName = dto.FullName
            };

            _db.Users.Add(user);
            await _db.SaveChangesAsync();

            return CreatedAtAction(nameof(GetOne), new { id = user.Id }, user);
        }

        /// <summary>
        /// Gets a single user by id.
        /// </summary>
        [HttpGet("{id:guid}")]
        [ProducesResponseType(typeof(User), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<User>> GetOne(Guid id)
        {
            var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id);
            if (user == null) return NotFound();
            return Ok(user);
        }

        /// <summary>
        /// Lists all users.
        /// </summary>
        [HttpGet]
        [ProducesResponseType(typeof(IEnumerable<User>), StatusCodes.Status200OK)]
        public async Task<ActionResult<IEnumerable<User>>> GetAll()
        {
            var users = await _db.Users.AsNoTracking().ToListAsync();
            return Ok(users);
        }
        /// <summary>
        /// Deletes a user.
        /// </summary>
        [HttpDelete("{id:guid}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Delete(Guid id)
        {
            var currentUserId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (currentUserId == id.ToString())
                return BadRequest(new { error = "You cannot deactivate your own account." });
            
            var user = await _db.Users.FindAsync(id);
            if (user == null) return NotFound();

            // Soft delete — deactivate instead of removing
            user.IsActive = false;
            await _db.SaveChangesAsync();

            return NoContent();
        }
        /// <summary>
        /// Reactivates a deactivated user.
        /// </summary>
        [HttpPatch("{id:guid}/reactivate")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Reactivate(Guid id)
        {
            var user = await _db.Users.FindAsync(id);
            if (user == null) return NotFound();

            user.IsActive = true;
            await _db.SaveChangesAsync();

            return NoContent();
        }
        /// <summary>
        /// Assigns a user to a department.
        /// </summary>
        [HttpPatch("{id:guid}/department")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> AssignDepartment(Guid id, [FromBody] AssignDepartmentDto dto)
        {
            var user = await _db.Users.FindAsync(id);
            if (user == null) return NotFound();
            user.DepartmentId = dto.DepartmentId;
            await _db.SaveChangesAsync();
            return NoContent();
        }

        public class AssignDepartmentDto
        {
            public Guid? DepartmentId { get; set; }
        }
        /// <summary>
        /// Updates the phone number for a user.
        /// </summary>
        [HttpPatch("{id:guid}/phone")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UpdatePhone(Guid id, [FromBody] UpdatePhoneDto dto)
        {
            var user = await _db.Users.FindAsync(id);
            if (user == null) return NotFound();
            user.PhoneNumber = dto.PhoneNumber;
            await _db.SaveChangesAsync();
            return NoContent();
        }

        public class UpdatePhoneDto
        {
            public string? PhoneNumber { get; set; }
        }
    }
}