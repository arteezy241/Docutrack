using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DocuTrack.Core.Models;
using DocuTrack.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;

namespace DocuTrack.Api.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/Documents/{documentId:guid}/routing")]
    [Produces("application/json")]
    public class RoutingController : ControllerBase
    {
        private readonly DocuTrackDbContext _db;

        public RoutingController(DocuTrackDbContext db)
        {
            _db = db;
        }

        public class RouteDocumentDto
        {
            public Guid? FromUserId { get; set; }
            public Guid? ToUserId { get; set; }
            public string? Note { get; set; }
            public DocumentStatus StatusAfter { get; set; }
        }

        /// <summary>
        /// Routes a document to another user and updates its status.
        /// </summary>
        [HttpPost]
        [ProducesResponseType(typeof(RoutingEvent), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<RoutingEvent>> Route(Guid documentId, RouteDocumentDto dto)
        {
            var doc = await _db.Documents.FindAsync(documentId);
            if (doc == null) return NotFound(new { error = "Document not found." });

            if (dto.ToUserId.HasValue)
            {
                var toUserExists = await _db.Users.AnyAsync(u => u.Id == dto.ToUserId.Value);
                if (!toUserExists)
                    return BadRequest(new { error = "ToUser not found." });
            }

            var routingEvent = new RoutingEvent
            {
                Id = Guid.NewGuid(),
                DocumentId = documentId,
                FromUserId = dto.FromUserId,
                ToUserId = dto.ToUserId,
                Timestamp = DateTime.UtcNow,
                Note = dto.Note,
                StatusAfter = dto.StatusAfter
            };

            doc.Status = dto.StatusAfter;
            doc.UpdatedAt = DateTime.UtcNow;

            _db.RoutingEvents.Add(routingEvent);
            await _db.SaveChangesAsync();

            return CreatedAtAction(nameof(GetHistory), new { documentId }, routingEvent);
        }

        /// <summary>
        /// Gets the full routing history of a document.
        /// </summary>
        [HttpGet]
        [ProducesResponseType(typeof(IEnumerable<RoutingEvent>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<IEnumerable<RoutingEvent>>> GetHistory(Guid documentId)
        {
            var docExists = await _db.Documents.AnyAsync(d => d.Id == documentId);
            if (!docExists) return NotFound(new { error = "Document not found." });

            var history = await _db.RoutingEvents
                .Where(r => r.DocumentId == documentId)
                .OrderBy(r => r.Timestamp)
                .AsNoTracking()
                .ToListAsync();

            return Ok(history);
        }
        /// <summary>
        /// Approve a routed document.
        /// </summary>
        [HttpPatch("{eventId:guid}/approve")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> Approve(Guid documentId, Guid eventId)
        {
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

            var routingEvent = await _db.RoutingEvents
                .FirstOrDefaultAsync(r => r.Id == eventId && r.DocumentId == documentId);

            if (routingEvent == null) return NotFound(new { error = "Routing event not found." });

            // only the recipient can approve
            if (routingEvent.ToUserId?.ToString() != userId)
                return Forbid();

            var doc = await _db.Documents.FindAsync(documentId);
            if (doc == null) return NotFound(new { error = "Document not found." });

            routingEvent.StatusAfter = DocumentStatus.Approved;
            doc.Status = DocumentStatus.Approved;
            doc.UpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc);

            // create a new routing event recording the approval
            var approvalEvent = new RoutingEvent
            {
                Id = Guid.NewGuid(),
                DocumentId = documentId,
                FromUserId = routingEvent.ToUserId,
                ToUserId = routingEvent.FromUserId,
                Timestamp = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc),
                Note = "Document approved",
                StatusAfter = DocumentStatus.Approved,
            };

            _db.RoutingEvents.Add(approvalEvent);
            await _db.SaveChangesAsync();

            return Ok(new { message = "Document approved.", status = "Approved" });
        }

        /// <summary>
        /// Reject a routed document.
        /// </summary>
        [HttpPatch("{eventId:guid}/reject")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> Reject(Guid documentId, Guid eventId, [FromBody] RejectDto dto)
        {
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

            var routingEvent = await _db.RoutingEvents
                .FirstOrDefaultAsync(r => r.Id == eventId && r.DocumentId == documentId);

            if (routingEvent == null) return NotFound(new { error = "Routing event not found." });

            // only the recipient can reject
            if (routingEvent.ToUserId?.ToString() != userId)
                return Forbid();

            var doc = await _db.Documents.FindAsync(documentId);
            if (doc == null) return NotFound(new { error = "Document not found." });

            routingEvent.StatusAfter = DocumentStatus.Rejected;
            doc.Status = DocumentStatus.Rejected;
            doc.UpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc);

            var rejectionEvent = new RoutingEvent
            {
                Id = Guid.NewGuid(),
                DocumentId = documentId,
                FromUserId = routingEvent.ToUserId,
                ToUserId = routingEvent.FromUserId,
                Timestamp = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc),
                Note = dto?.Reason ?? "Document rejected",
                StatusAfter = DocumentStatus.Rejected,
            };

            _db.RoutingEvents.Add(rejectionEvent);
            await _db.SaveChangesAsync();

            return Ok(new { message = "Document rejected.", status = "Rejected" });
        }

        public class RejectDto
        {
            public string? Reason { get; set; }
        }
    }
}