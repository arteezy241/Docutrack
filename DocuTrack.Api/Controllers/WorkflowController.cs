using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DocuTrack.Core.Models;
using DocuTrack.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;

namespace DocuTrack.Api.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/workflow")]
    [Produces("application/json")]
    public class WorkflowController : ControllerBase
    {
        private readonly DocuTrackDbContext _db;

        public WorkflowController(DocuTrackDbContext db)
        {
            _db = db;
        }

        public class CreateWorkflowRuleDto
        {
            public string Name { get; set; } = string.Empty;
            public DocumentStatus TriggerStatus { get; set; }
            public Guid? AssignToUserId { get; set; }
            public DocumentStatus NextStatus { get; set; }
            public string? Note { get; set; }
        }

        /// <summary>
        /// Gets all workflow rules.
        /// </summary>
        [HttpGet]
        [ProducesResponseType(typeof(IEnumerable<WorkflowRule>), StatusCodes.Status200OK)]
        public async Task<ActionResult<IEnumerable<WorkflowRule>>> GetAll()
        {
            var rules = await _db.WorkflowRules
                .Include(w => w.AssignToUser)
                .AsNoTracking()
                .ToListAsync();
            return Ok(rules);
        }

        /// <summary>
        /// Creates a new workflow rule.
        /// </summary>
        [HttpPost]
        [ProducesResponseType(typeof(WorkflowRule), StatusCodes.Status201Created)]
        public async Task<ActionResult<WorkflowRule>> Create(CreateWorkflowRuleDto dto)
        {
            var rule = new WorkflowRule
            {
                Id = Guid.NewGuid(),
                Name = dto.Name,
                TriggerStatus = dto.TriggerStatus,
                AssignToUserId = dto.AssignToUserId,
                NextStatus = dto.NextStatus,
                Note = dto.Note,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            _db.WorkflowRules.Add(rule);
            await _db.SaveChangesAsync();

            await _db.Entry(rule).Reference(r => r.AssignToUser).LoadAsync();
            return CreatedAtAction(nameof(GetAll), new { id = rule.Id }, rule);
        }

        /// <summary>
        /// Toggles a workflow rule on or off.
        /// </summary>
        [HttpPatch("{id:guid}/toggle")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Toggle(Guid id)
        {
            var rule = await _db.WorkflowRules.FindAsync(id);
            if (rule == null) return NotFound();
            rule.IsActive = !rule.IsActive;
            await _db.SaveChangesAsync();
            return NoContent();
        }

        /// <summary>
        /// Deletes a workflow rule.
        /// </summary>
        [HttpDelete("{id:guid}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Delete(Guid id)
        {
            var rule = await _db.WorkflowRules.FindAsync(id);
            if (rule == null) return NotFound();
            _db.WorkflowRules.Remove(rule);
            await _db.SaveChangesAsync();
            return NoContent();
        }

        /// <summary>
        /// Manually triggers the workflow engine for a document.
        /// </summary>
        [HttpPost("trigger/{documentId:guid}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Trigger(Guid documentId)
        {
            var doc = await _db.Documents.FindAsync(documentId);
            if (doc == null) return NotFound();

            var matchingRules = await _db.WorkflowRules
                .Where(r => r.IsActive && r.TriggerStatus == doc.Status)
                .ToListAsync();

            if (!matchingRules.Any())
                return Ok(new { message = "No matching workflow rules found.", triggered = 0 });

            int triggered = 0;
            foreach (var rule in matchingRules)
            {
                var routingEvent = new RoutingEvent
                {
                    Id = Guid.NewGuid(),
                    DocumentId = doc.Id,
                    FromUserId = null,
                    ToUserId = rule.AssignToUserId,
                    Timestamp = DateTime.UtcNow,
                    Note = rule.Note ?? $"Auto-routed by workflow: {rule.Name}",
                    StatusAfter = rule.NextStatus
                };

                doc.Status = rule.NextStatus;
                doc.UpdatedAt = DateTime.UtcNow;

                _db.RoutingEvents.Add(routingEvent);
                triggered++;
            }

            await _db.SaveChangesAsync();
            return Ok(new { message = $"Workflow triggered {triggered} rule(s).", triggered });
        }
    }
}