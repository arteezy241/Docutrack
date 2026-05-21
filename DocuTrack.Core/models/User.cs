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
        public string? TwoFactorMethod { get; set; } = "email"; // "email" or "sms"
        public string? Role { get; set; } = "Staff";
        public string? PasswordResetOtp { get; set; }
        public DateTime? PasswordResetOtpExpiry { get; set; }
        // Email verification
        public bool IsEmailVerified { get; set; } = false;
        public string? EmailVerificationOtp { get; set; }
        public DateTime? OtpExpiry { get; set; }

        // Phone
        public string? PhoneNumber { get; set; }
        public bool IsPhoneVerified { get; set; } = false;
        public string? PhoneOtp { get; set; }
        public DateTime? PhoneOtpExpiry { get; set; }

        // QR login
        public string? QrLoginToken { get; set; }
        public DateTime? QrLoginExpiry { get; set; }

        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; }
    }
}