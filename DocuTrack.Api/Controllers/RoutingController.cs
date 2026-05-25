using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DocuTrack.Core.Models;
using DocuTrack.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using DocuTrack.Api.Services;
using System.Text.Json;
using WebPush;

namespace DocuTrack.Api.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/Documents/{documentId:guid}/routing")]
    [Produces("application/json")]
    public class RoutingController : ControllerBase
    {
        private readonly DocuTrackDbContext _db;
        private readonly EmailService _email;
        private readonly IConfiguration _config;

        public RoutingController(DocuTrackDbContext db, EmailService email, IConfiguration config)
        {
            _db = db;
            _email = email;
            _config = config;
        }

        public class RouteDocumentDto
        {
            public Guid? FromUserId { get; set; }
            public Guid? ToUserId { get; set; }
            public string? Note { get; set; }
            public DocumentStatus StatusAfter { get; set; }
        }

        private async Task SendPush(User user, string title, string message)
        {
            try
            {
                var publicKey = _config["Vapid:PublicKey"];
                var privateKey = _config["Vapid:PrivateKey"];
                var subject = _config["Vapid:Subject"];

                var pushClient = new WebPushClient();
                pushClient.SetVapidDetails(subject, publicKey, privateKey);

                var payload = JsonSerializer.Serialize(new { title, message });

                var subscriptions = await _db.PushSubscriptions.ToListAsync();
                foreach (var sub in subscriptions)
                {
                    try
                    {
                        var pushSub = new WebPush.PushSubscription(sub.Endpoint, sub.P256dh, sub.Auth);
                        await pushClient.SendNotificationAsync(pushSub, payload);
                    }
                    catch { /* dead subscription, ignore */ }
                }
            }
            catch { /* push failure should not break the request */ }
        }

        private async Task SendEmailAndPush(Guid? userId, string subject, string emailBody, string pushTitle, string pushMessage)
        {
            if (!userId.HasValue) return;
            var user = await _db.Users.FindAsync(userId.Value);
            if (user == null || string.IsNullOrEmpty(user.Email)) return;

            try { await _email.SendEmailAsync(user.Email, subject, emailBody); } catch { }
            await SendPush(user, pushTitle, pushMessage);
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
            await LogAudit("DOCUMENT_ROUTED", "Document", documentId.ToString(), $"Routed to {dto.ToUserId}");
            // notify recipient
            await SendEmailAndPush(
                dto.ToUserId,
                "DocuTrack — Document Routed to You",
                $"<h2>Document Routed to You</h2><p>The document <strong>{doc.Title}</strong> has been routed to you for review.</p>{(string.IsNullOrEmpty(dto.Note) ? "" : $"<p>Note: {dto.Note}</p>")}<p>Log in to DocuTrack to review it.</p>",
                "Document Routed to You",
                $"{doc.Title} has been routed to you for review."
            );

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

            if (routingEvent.ToUserId?.ToString() != userId)
                return Forbid();

            var doc = await _db.Documents.FindAsync(documentId);
            if (doc == null) return NotFound(new { error = "Document not found." });

            routingEvent.StatusAfter = DocumentStatus.Approved;
            doc.Status = DocumentStatus.Approved;
            doc.UpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc);

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
            await LogAudit("DOCUMENT_APPROVED", "Document", documentId.ToString());
            // notify original sender
            await SendEmailAndPush(
                routingEvent.FromUserId,
                "DocuTrack — Document Approved",
                $"<h2>Document Approved</h2><p>Your document <strong>{doc.Title}</strong> has been approved.</p><p>Log in to DocuTrack to view it.</p>",
                "Document Approved",
                $"{doc.Title} has been approved."
            );

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
            await LogAudit("DOCUMENT_REJECTED", "Document", documentId.ToString(), dto?.Reason);
            // notify original sender
            await SendEmailAndPush(
                routingEvent.FromUserId,
                "DocuTrack — Document Rejected",
                $"<h2>Document Rejected</h2><p>Your document <strong>{doc.Title}</strong> has been rejected.</p>{(string.IsNullOrEmpty(dto?.Reason) ? "" : $"<p>Reason: {dto.Reason}</p>")}<p>Log in to DocuTrack to view it.</p>",
                "Document Rejected",
                $"{doc.Title} has been rejected.{(string.IsNullOrEmpty(dto?.Reason) ? "" : " Reason: " + dto.Reason)}"
            );

            return Ok(new { message = "Document rejected.", status = "Rejected" });
        }

        public class RejectDto
        {
            public string? Reason { get; set; }
        }
        private async Task LogAudit(string action, string? resourceType = null,
    string? resourceId = null, string? details = null, Guid? userId = null, string? userEmail = null)
        {
            try
            {
                _db.AuditLogs.Add(new DocuTrack.Core.Models.AuditLog
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    UserEmail = userEmail,
                    Action = action,
                    ResourceType = resourceType,
                    ResourceId = resourceId,
                    Details = details,
                    IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
                    Timestamp = DateTimeOffset.UtcNow,
                });
                await _db.SaveChangesAsync();
            }
            catch { }
        }
        /// <summary>
        /// Get all routing events pending for the current user.
        /// </summary>
        [Authorize]
        [HttpGet("/api/routing/pending")]
        public async Task<IActionResult> GetPendingApprovals()
        {
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (userId == null) return Unauthorized();

            var pending = await _db.RoutingEvents
                 .Include(r => r.Document)
                 .Where(r => r.ToUserId == Guid.Parse(userId) &&
                             r.StatusAfter != DocuTrack.Core.Models.DocumentStatus.Approved &&
                            r.StatusAfter != DocuTrack.Core.Models.DocumentStatus.Rejected)
                .OrderByDescending(r => r.Timestamp)
                .Select(r => new
                {
                    r.Id,
                    r.DocumentId,
                    r.FromUserId,
                    r.ToUserId,
                    DocTitle = r.Document != null ? r.Document.Title : null,
                    r.Note,
                    r.Timestamp,
                    r.StatusAfter,
                    FromUser = r.FromUserId != null ? _db.Users
                        .Where(u => u.Id == r.FromUserId)
                        .Select(u => new { u.Id, u.FullName, u.Username, u.Email })
                        .FirstOrDefault() : null,
                    Document = r.Document != null ? new { r.Document.Id, r.Document.Title, r.Document.Status } : null,
                })
                .ToListAsync();

            return Ok(pending);
        }
    }
}