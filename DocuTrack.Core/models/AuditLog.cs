namespace DocuTrack.Core.Models
{
    public class AuditLog
    {
        public Guid Id { get; set; }
        public Guid? UserId { get; set; }
        public string? UserEmail { get; set; }
        public string Action { get; set; } = string.Empty;
        public string? ResourceType { get; set; }
        public string? ResourceId { get; set; }
        public string? Details { get; set; }
        public string? IpAddress { get; set; }
        public DateTimeOffset Timestamp { get; set; }
    }
}