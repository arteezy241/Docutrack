using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebPush;
using DocuTrack.Infrastructure.Data;
using System.Text.Json;

namespace DocuTrack.Api.Controllers
{
    [ApiController]
    [Route("api/push")]
    [Produces("application/json")]
    public class PushController : ControllerBase
    {
        private readonly DocuTrackDbContext _db;
        private readonly IConfiguration _config;

        public PushController(DocuTrackDbContext db, IConfiguration config)
        {
            _db = db;
            _config = config;
        }

        public class SubscribeDto
        {
            public string Endpoint { get; set; } = string.Empty;
            public string P256dh { get; set; } = string.Empty;
            public string Auth { get; set; } = string.Empty;
        }

        /// <summary>
        /// Saves a push subscription.
        /// </summary>
        [HttpPost("subscribe")]
        [ProducesResponseType(StatusCodes.Status201Created)]
        public async Task<IActionResult> Subscribe(SubscribeDto dto)
        {
            var existing = await _db.PushSubscriptions
                .FirstOrDefaultAsync(p => p.Endpoint == dto.Endpoint);

            if (existing != null) return Ok(new { message = "Already subscribed." });

            var sub = new DocuTrack.Core.Models.PushSubscription
            {
                Id = Guid.NewGuid(),
                Endpoint = dto.Endpoint,
                P256dh = dto.P256dh,
                Auth = dto.Auth,
                CreatedAt = DateTime.UtcNow
            };

            _db.PushSubscriptions.Add(sub);
            await _db.SaveChangesAsync();

            return Created("", new { message = "Subscribed successfully." });
        }

        /// <summary>
        /// Sends a push notification to all subscribers.
        /// </summary>
        [HttpPost("send")]
        public async Task<IActionResult> Send([FromBody] JsonElement body)
        {
            var title = body.TryGetProperty("title", out var t) ? t.GetString() : "DocuTrack";
            var message = body.TryGetProperty("message", out var m) ? m.GetString() : "You have a new notification.";

            var publicKey = _config["Vapid:PublicKey"];
            var privateKey = _config["Vapid:PrivateKey"];
            var subject = _config["Vapid:Subject"];

            var client = new WebPushClient();
            client.SetVapidDetails(subject, publicKey, privateKey);

            var payload = JsonSerializer.Serialize(new { title, message });

            var subscriptions = await _db.PushSubscriptions.ToListAsync();
            var failed = new List<Guid>();
            var errors = new List<string>();

            foreach (var sub in subscriptions)
            {
                try
                {
                    var pushSub = new WebPush.PushSubscription(sub.Endpoint, sub.P256dh, sub.Auth);
                    await client.SendNotificationAsync(pushSub, payload);
                }
                catch (Exception ex)
                {
                    errors.Add(ex.Message);
                    failed.Add(sub.Id);
                }
            }

            //if (failed.Any())
           // {
            //    var dead = _db.PushSubscriptions.Where(p => failed.Contains(p.Id));
             //   _db.PushSubscriptions.RemoveRange(dead);
             //   await _db.SaveChangesAsync();
          //  }

            return Ok(new { sent = subscriptions.Count - failed.Count, errors });
        }

        /// <summary>
        /// Returns the VAPID public key for the frontend.
        /// </summary>
        [HttpGet("publickey")]
        public IActionResult GetPublicKey()
        {
            return Ok(new { publicKey = _config["Vapid:PublicKey"] });
        }
        /// <summary>
        /// Clears all push subscriptions.
        /// </summary>
        [HttpDelete("subscriptions")]
        public async Task<IActionResult> ClearSubscriptions()
        {
            _db.PushSubscriptions.RemoveRange(_db.PushSubscriptions);
            await _db.SaveChangesAsync();
            return Ok(new { message = "Cleared." });
        }
        /// <summary>
        /// Lists all push subscriptions.
        /// </summary>
        [HttpGet("subscriptions")]
        public async Task<IActionResult> GetSubscriptions()
        {
            var subs = await _db.PushSubscriptions.ToListAsync();
            return Ok(subs);
        }
        public class SendEmailDto
        {
            public string ToEmail { get; set; } = string.Empty;
            public string Subject { get; set; } = string.Empty;
            public string Body { get; set; } = string.Empty;
        }

        /// <summary>
        /// Sends an email notification.
        /// </summary>
        [HttpPost("email")]
        public async Task<IActionResult> SendEmail(
            [FromBody] SendEmailDto dto,
            [FromServices] DocuTrack.Api.Services.EmailService emailService)
        {
            if (string.IsNullOrEmpty(dto.ToEmail))
                return BadRequest(new { error = "Email is required." });

            try
            {
                await emailService.SendEmailAsync(dto.ToEmail, dto.Subject, dto.Body);
                return Ok(new { message = "Email sent successfully." });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }
    }


}