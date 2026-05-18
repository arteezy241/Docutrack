namespace DocuTrack.Core.Models
{
    public class TrustedDevice
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public User? User { get; set; }
        public string DeviceToken { get; set; } = string.Empty;
        public string DeviceName { get; set; } = "Unknown Device";
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset LastUsedAt { get; set; }
    }
}