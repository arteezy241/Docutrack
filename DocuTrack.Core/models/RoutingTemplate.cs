namespace DocuTrack.Core.Models
{
    public class RoutingTemplate
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string StepsJson { get; set; } = "[]"; // JSON array of { userId, note }
        public Guid CreatedById { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public bool IsActive { get; set; } = true;
    }
}