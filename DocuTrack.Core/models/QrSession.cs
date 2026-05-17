namespace DocuTrack.Core.Models
{
    public class QrSession
    {
        public Guid Id { get; set; }
        public string Token { get; set; } = string.Empty;
        public bool IsScanned { get; set; } = false;
        public string? ScannedByUserId { get; set; }
        public string? JwtToken { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime ExpiresAt { get; set; }
    }
}