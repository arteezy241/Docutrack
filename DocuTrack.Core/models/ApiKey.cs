namespace DocuTrack.Core.Models
{
    public class ApiKey
    {
        public Guid Id { get; set; }
        public string Key { get; set; } = string.Empty;
        public Guid UserId { get; set; }
        public string Name { get; set; } = "Default";
        public bool IsActive { get; set; } = true;
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset? LastUsedAt { get; set; }
    }
}