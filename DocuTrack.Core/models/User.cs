namespace DocuTrack.Core.Models
{
    public class User
    {
        public Guid Id { get; set; }
        public string? Username { get; set; }
        public string? Email { get; set; }
        public string? FullName { get; set; }
        public Guid? DepartmentId { get; set; }
        public Department? Department { get; set; }

        // Auth fields
        public string? PasswordHash { get; set; }
        public bool IsTwoFactorEnabled { get; set; } = false;
        public string? Role { get; set; } = "Staff";
        public bool IsEmailVerified { get; set; } = false;
        public string? EmailVerificationOtp { get; set; }
        public DateTime? OtpExpiry { get; set; }
        public string? QrLoginToken { get; set; }
        public DateTime? QrLoginExpiry { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; }
    }
}