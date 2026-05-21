using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DocuTrack.Core.Models;
using DocuTrack.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
namespace DocuTrack.Api.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/audit")]
    [Produces("application/json")]
    public class AuditController : ControllerBase
    {
        private readonly DocuTrackDbContext _db;

        public AuditController(DocuTrackDbContext db)
        {
            _db = db;
        }

        /// <summary>
        /// Gets the full audit log of all document movements.
        /// </summary>
        [HttpGet]
        [ProducesResponseType(typeof(IEnumerable<RoutingEvent>), StatusCodes.Status200OK)]
        public async Task<ActionResult<IEnumerable<RoutingEvent>>> GetAll()
        {
            var logs = await _db.RoutingEvents
                .OrderByDescending(r => r.Timestamp)
                .AsNoTracking()
                .ToListAsync();

            return Ok(logs);
        }

        /// <summary>
        /// Gets the audit log for a specific document.
        /// </summary>
        [HttpGet("document/{documentId:guid}")]
        [ProducesResponseType(typeof(IEnumerable<RoutingEvent>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<IEnumerable<RoutingEvent>>> GetByDocument(Guid documentId)
        {
            var docExists = await _db.Documents.AnyAsync(d => d.Id == documentId);
            if (!docExists) return NotFound(new { error = "Document not found." });

            var logs = await _db.RoutingEvents
                .Where(r => r.DocumentId == documentId)
                .OrderByDescending(r => r.Timestamp)
                .AsNoTracking()
                .ToListAsync();

            return Ok(logs);
        }

        /// <summary>
        /// Gets the audit log for a specific user.
        /// </summary>
        [HttpGet("user/{userId:guid}")]
        [ProducesResponseType(typeof(IEnumerable<RoutingEvent>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<IEnumerable<RoutingEvent>>> GetByUser(Guid userId)
        {
            var userExists = await _db.Users.AnyAsync(u => u.Id == userId);
            if (!userExists) return NotFound(new { error = "User not found." });

            var logs = await _db.RoutingEvents
                .Where(r => r.FromUserId == userId || r.ToUserId == userId)
                .OrderByDescending(r => r.Timestamp)
                .AsNoTracking()
                .ToListAsync();

            return Ok(logs);
        }
        /// <summary>
        /// Gets system audit logs — Admin only.
        /// </summary>
        [HttpGet("system")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GetSystemLogs(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50,
            [FromQuery] string? action = null,
            [FromQuery] string? userId = null)
        {
            var role = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;
            if (role != "Admin") return Forbid();

            var query = _db.AuditLogs.AsQueryable();

            if (!string.IsNullOrEmpty(action))
                query = query.Where(a => a.Action == action);

            if (!string.IsNullOrEmpty(userId) && Guid.TryParse(userId, out var uid))
                query = query.Where(a => a.UserId == uid);

            var total = await query.CountAsync();

            var logs = await query
                .OrderByDescending(a => a.Timestamp)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(a => new
                {
                    a.Id,
                    a.UserId,
                    a.UserEmail,
                    a.Action,
                    a.ResourceType,
                    a.ResourceId,
                    a.Details,
                    a.IpAddress,
                    a.Timestamp,
                })
                .ToListAsync();

            return Ok(new { total, page, pageSize, logs });
        }
    }
}