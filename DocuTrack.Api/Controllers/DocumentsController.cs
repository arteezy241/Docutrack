using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DocuTrack.Core.Models;
using DocuTrack.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;

namespace DocuTrack.Api.Controllers
{
    /// <summary>
    /// Manage documents.
    /// </summary>
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    public class DocumentsController : ControllerBase
    {
        private readonly DocuTrackDbContext _db;

        public DocumentsController(DocuTrackDbContext db)
        {
            _db = db;
        }

        /// <summary>
        /// Lists all documents.
        /// </summary>
        /// <returns>All documents.</returns>
        [HttpGet]
        [ProducesResponseType(typeof(IEnumerable<Document>), StatusCodes.Status200OK)]
        public async Task<ActionResult<IEnumerable<Document>>> GetAll()
        {
            var docs = await _db.Documents
                .Include(d => d.Owner)
                .Include(d => d.RoutingHistory)
                .AsNoTracking()
                .ToListAsync();
            return Ok(docs);
        }

        // GET: /api/documents/{id}
        /// <summary>
        /// Gets a single document by id.
        /// </summary>
        /// <param name="id">Document id.</param>
        [HttpGet("{id:guid}")]
        [ProducesResponseType(typeof(Document), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<Document>> GetOne(Guid id)
        {
            var doc = await _db.Documents
                .Include(d => d.RoutingHistory)
                .Include(d => d.Owner)
                .AsNoTracking()
                .FirstOrDefaultAsync(d => d.Id == id);

            if (doc == null) return NotFound();

            return Ok(doc);
        }

        public class CreateDocumentDto
        {
            public string? Title { get; set; }
            public string? Content { get; set; }
            public Guid? OwnerId { get; set; }
        }

        /// <summary>
        /// Creates a new document.
        /// </summary>
        /// <param name="dto">Document create payload.</param>
        [HttpPost]
        [ProducesResponseType(typeof(Document), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<Document>> Create(CreateDocumentDto dto)
        {
            if (dto.OwnerId.HasValue)
            {
                var ownerExists = await _db.Users.AnyAsync(u => u.Id == dto.OwnerId.Value);
                if (!ownerExists)
                    return BadRequest(new { error = "Owner not found. Please reference an existing user." });
            }

            var doc = new Document
            {
                Id = Guid.NewGuid(),
                Title = dto.Title,
                Content = dto.Content,
                OwnerId = dto.OwnerId,
                Status = DocumentStatus.Draft,
                CreatedAt = DateTime.UtcNow
            };

            _db.Documents.Add(doc);

            try
            {
                await _db.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                return BadRequest(new { error = "Failed to save document due to a data integrity error." });
            }

            await _db.Entry(doc).Reference(d => d.Owner).LoadAsync();
            return CreatedAtAction(nameof(GetOne), new { id = doc.Id }, doc);
        }
        public class UpdateStatusDto
        {
            public DocumentStatus Status { get; set; }
        }

        /// <summary>
        /// Updates the status of a document. A routing event will be recorded.
        /// </summary>
        /// <param name="id">Document id.</param>
        /// <param name="dto">New status value.</param>
        [HttpPatch("{id:guid}/status")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UpdateStatus(Guid id, UpdateStatusDto dto)
        {
            var doc = await _db.Documents.FindAsync(id);
            if (doc == null) return NotFound();

            doc.Status = dto.Status;
            doc.UpdatedAt = DateTime.UtcNow;

            // add a routing event recording the status change
            var routingEvent = new RoutingEvent
            {
                Id = Guid.NewGuid(),
                DocumentId = doc.Id,
                FromUserId = null,
                ToUserId = null,
                Timestamp = DateTime.UtcNow,
                Note = null,
                StatusAfter = dto.Status
            };

            _db.RoutingEvents.Add(routingEvent);

            // persist change and the routing event
            await _db.SaveChangesAsync();

            return NoContent();
        }
    }
}
