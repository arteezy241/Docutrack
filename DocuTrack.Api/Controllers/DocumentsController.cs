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
using DocuTrack.Api.Services;

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
        private readonly FileService _fileService;

        public DocumentsController(DocuTrackDbContext db, FileService fileService)
        {
            _db = db;
            _fileService = fileService;
        }

        /// <summary>
        /// Lists all documents.
        /// </summary>
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

        /// <summary>
        /// Gets a single document by id.
        /// </summary>
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

            try { await _db.SaveChangesAsync(); }
            catch (DbUpdateException)
            {
                return BadRequest(new { error = "Failed to save document due to a data integrity error." });
            }

            await _db.Entry(doc).Reference(d => d.Owner).LoadAsync();
            return CreatedAtAction(nameof(GetOne), new { id = doc.Id }, doc);
        }

        /// <summary>
        /// Upload a file to an existing document.
        /// </summary>
        [HttpPost("{id:guid}/upload")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> UploadFile(Guid id, IFormFile file)
        {
            var doc = await _db.Documents.FindAsync(id);
            if (doc == null) return NotFound();

            if (file == null || file.Length == 0)
                return BadRequest(new { error = "No file provided." });

            // only allow PDF and DOCX
            var allowedTypes = new[] {
                "application/pdf",
                "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                "application/msword"
            };
            if (!allowedTypes.Contains(file.ContentType))
                return BadRequest(new { error = "Only PDF and DOCX files are allowed." });

            // max 10MB
            if (file.Length > 10 * 1024 * 1024)
                return BadRequest(new { error = "File size must be under 10MB." });

            try
            {
                // delete old file if exists
                if (!string.IsNullOrEmpty(doc.FileUrl))
                    await _fileService.DeleteFileAsync(doc.FileUrl);

                var fileUrl = await _fileService.UploadFileAsync(file);
                doc.FileUrl = fileUrl;
                doc.FileName = file.FileName;
                doc.UpdatedAt = DateTime.UtcNow;

                await _db.SaveChangesAsync();

                return Ok(new { fileUrl = doc.FileUrl, fileName = doc.FileName });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        /// <summary>
        /// Delete the file attached to a document.
        /// </summary>
        [HttpDelete("{id:guid}/file")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DeleteFile(Guid id)
        {
            var doc = await _db.Documents.FindAsync(id);
            if (doc == null) return NotFound();

            if (!string.IsNullOrEmpty(doc.FileUrl))
                await _fileService.DeleteFileAsync(doc.FileUrl);

            doc.FileUrl = null;
            doc.FileName = null;
            doc.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();
            return Ok(new { message = "File deleted." });
        }
        /// <summary>
        /// Delete a document.
        /// </summary>
        [HttpDelete("{id:guid}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Delete(Guid id)
        {
            var doc = await _db.Documents.FindAsync(id);
            if (doc == null) return NotFound();

            if (!string.IsNullOrEmpty(doc.FileUrl))
                await _fileService.DeleteFileAsync(doc.FileUrl);

            _db.Documents.Remove(doc);
            await _db.SaveChangesAsync();

            return NoContent();
        }
        public class UpdateStatusDto
        {
            public DocumentStatus Status { get; set; }
        }

        /// <summary>
        /// Updates the status of a document.
        /// </summary>
        [HttpPatch("{id:guid}/status")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UpdateStatus(Guid id, UpdateStatusDto dto)
        {
            var doc = await _db.Documents.FindAsync(id);
            if (doc == null) return NotFound();

            doc.Status = dto.Status;
            doc.UpdatedAt = DateTime.UtcNow;

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
            await _db.SaveChangesAsync();

            return NoContent();
        }
    }
}