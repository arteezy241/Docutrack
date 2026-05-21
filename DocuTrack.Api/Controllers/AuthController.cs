
using DocuTrack.Core.Models;
using DocuTrack.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.ComponentModel.DataAnnotations;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;



namespace DocuTrack.Api.Controllers
{
    [ApiController]
    [Route("api/auth")]
    [Produces("application/json")]
    public class AuthController : ControllerBase
    {
        private readonly DocuTrackDbContext _db;
        private readonly IConfiguration _config;
        private readonly Services.EmailService _email;

        public AuthController(DocuTrackDbContext db, IConfiguration config, Services.EmailService email)
        {
            _db = db;
            _config = config;
            _email = email;
        }

        public class RegisterDto
        {
            [Required(ErrorMessage = "Full name is required.")]
            [StringLength(100, MinimumLength = 2, ErrorMessage = "Full name must be between 2 and 100 characters.")]
            public string FullName { get; set; } = string.Empty;

            [Required(ErrorMessage = "Username is required.")]
            [StringLength(50, MinimumLength = 3, ErrorMessage = "Username must be between 3 and 50 characters.")]
            [RegularExpression(@"^[a-zA-Z0-9_]+$", ErrorMessage = "Username can only contain letters, numbers, and underscores.")]
            public string Username { get; set; } = string.Empty;

            [Required(ErrorMessage = "Email is required.")]
            [EmailAddress(ErrorMessage = "Invalid email format.")]
            [StringLength(200, ErrorMessage = "Email must not exceed 200 characters.")]
            public string Email { get; set; } = string.Empty;

            [Required(ErrorMessage = "Password is required.")]
            [StringLength(100, MinimumLength = 8, ErrorMessage = "Password must be at least 8 characters.")]
            [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d).+$",
                ErrorMessage = "Password must contain at least one uppercase letter, one lowercase letter, and one number.")]
            public string Password { get; set; } = string.Empty;

            [StringLength(20, ErrorMessage = "Role must not exceed 20 characters.")]
            public string Role { get; set; } = "Staff";
        }

        public class LoginDto
        {
            [Required(ErrorMessage = "Email is required.")]
            [EmailAddress(ErrorMessage = "Invalid email format.")]
            [StringLength(200, ErrorMessage = "Email must not exceed 200 characters.")]
            public string Email { get; set; } = string.Empty;

            [Required(ErrorMessage = "Password is required.")]
            [StringLength(100, MinimumLength = 1, ErrorMessage = "Password is required.")]

            public string Password { get; set; } = string.Empty;
        }

        public class VerifyOtpDto
        {
            [Required(ErrorMessage = "Email is required.")]
            [EmailAddress(ErrorMessage = "Invalid email format.")]
            public string Email { get; set; } = string.Empty;

            [Required(ErrorMessage = "OTP is required.")]
            [StringLength(6, MinimumLength = 6, ErrorMessage = "OTP must be exactly 6 digits.")]
            [RegularExpression(@"^\d{6}$", ErrorMessage = "OTP must be exactly 6 digits.")]
            public string Otp { get; set; } = string.Empty;
        }

        public class VerifyDeviceDto
        {
            [Required(ErrorMessage = "Email is required.")]
            [EmailAddress(ErrorMessage = "Invalid email format.")]
            public string Email { get; set; } = string.Empty;

            [Required(ErrorMessage = "OTP is required.")]
            [StringLength(6, MinimumLength = 6, ErrorMessage = "OTP must be exactly 6 digits.")]
            [RegularExpression(@"^\d{6}$", ErrorMessage = "OTP must be exactly 6 digits.")]
            public string Otp { get; set; } = string.Empty;

            public string? DeviceName { get; set; }
        }

        public class ResendOtpDto
        {
            [Required(ErrorMessage = "Email is required.")]
            [EmailAddress(ErrorMessage = "Invalid email format.")]
            public string Email { get; set; } = string.Empty;
        }
        public class ForgotPasswordDto
        {
            [Required(ErrorMessage = "Email is required.")]
            [EmailAddress(ErrorMessage = "Invalid email format.")]
            public string Email { get; set; } = string.Empty;
        }

        public class ResetPasswordDto
        {
            [Required(ErrorMessage = "Email is required.")]
            [EmailAddress(ErrorMessage = "Invalid email format.")]
            public string Email { get; set; } = string.Empty;

            [Required(ErrorMessage = "OTP is required.")]
            [StringLength(6, MinimumLength = 6, ErrorMessage = "OTP must be exactly 6 digits.")]
            public string Otp { get; set; } = string.Empty;

            [Required(ErrorMessage = "New password is required.")]
            [StringLength(100, MinimumLength = 8, ErrorMessage = "Password must be at least 8 characters.")]
            [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d).+$",
                ErrorMessage = "Password must contain uppercase, lowercase, and a number.")]
            public string NewPassword { get; set; } = string.Empty;
        }

        /// <summary>
        /// Sends a password reset OTP to the user's email.
        /// </summary>
        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordDto dto)
        {
            var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == dto.Email);

            // Always return success even if email not found (prevent email enumeration)
            if (user == null)
                return Ok(new { message = "If that email exists, a reset code has been sent." });

            if (!user.IsActive)
                return Ok(new { message = "If that email exists, a reset code has been sent." });

            var otp = new Random().Next(100000, 999999).ToString();
            user.PasswordResetOtp = otp;
            user.PasswordResetOtpExpiry = DateTime.UtcNow.AddMinutes(10);
            await _db.SaveChangesAsync();

            await _email.SendEmailAsync(user.Email!, "DocuTrack — Password Reset",
                $@"<h2>Password Reset Request</h2>
        <p>You requested to reset your DocuTrack password.</p>
        <h1 style='color:#4F46E5;letter-spacing:8px'>{otp}</h1>
        <p>This code expires in 10 minutes.</p>
        <p>If you did not request this, please ignore this email and secure your account.</p>");

            return Ok(new { message = "If that email exists, a reset code has been sent." });
        }

        /// <summary>
        /// Resets the user's password using the OTP sent to their email.
        /// </summary>
        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordDto dto)
        {
            var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == dto.Email);
            if (user == null)
                return BadRequest(new { error = "Invalid request." });

            if (user.PasswordResetOtp != dto.Otp)
                return BadRequest(new { error = "Invalid or expired reset code." });

            if (user.PasswordResetOtpExpiry < DateTime.UtcNow)
                return BadRequest(new { error = "Reset code has expired. Please request a new one." });

            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.NewPassword);
            user.PasswordResetOtp = null;
            user.PasswordResetOtpExpiry = null;

            // Invalidate all trusted devices for security
            var trustedDevices = await _db.TrustedDevices
                .Where(d => d.UserId == user.Id)
                .ToListAsync();
            _db.TrustedDevices.RemoveRange(trustedDevices);

            await _db.SaveChangesAsync();
            await LogAudit("PASSWORD_RESET", "User", user.Id.ToString(), null, user.Id, user.Email);
            return Ok(new { message = "Password reset successfully. Please log in with your new password." });
        }
        public class QrLoginDto
        {
            public string Token { get; set; } = string.Empty;
        }
        [HttpPost("google")]
        public async Task<IActionResult> GoogleLogin([FromBody] GoogleLoginRequest request)
        {
            try
            {
                var payload = await Google.Apis.Auth.GoogleJsonWebSignature.ValidateAsync(request.IdToken);

                var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == payload.Email);
                if (user == null)
                {
                    user = new User
                    {
                        Id = Guid.NewGuid(),
                        Email = payload.Email,
                        FullName = payload.Name,
                        Username = payload.Email.Split('@')[0],
                        Role = "Staff",
                        IsEmailVerified = true,
                        IsActive = true,
                        CreatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc),
                        PasswordHash = BCrypt.Net.BCrypt.HashPassword(Guid.NewGuid().ToString()),
                    };
                    _db.Users.Add(user);
                    await _db.SaveChangesAsync();
                }

                // check 2FA
                if (user.IsTwoFactorEnabled)
                {
                    var deviceToken = Request.Headers["X-Device-Token"].ToString();
                    if (!string.IsNullOrEmpty(deviceToken))
                    {
                        var trusted = await _db.TrustedDevices
                            .FirstOrDefaultAsync(d => d.DeviceToken == deviceToken && d.UserId == user.Id);
                        if (trusted != null)
                        {
                            trusted.LastUsedAt = DateTimeOffset.UtcNow;
                            await _db.SaveChangesAsync();
                            
                            var token = GenerateJwt(user);
                            return Ok(new
                            {
                                token,
                                requiresOtp = false,
                                user = new { id = user.Id, fullName = user.FullName, username = user.Username, email = user.Email, role = user.Role, departmentId = user.DepartmentId, isTwoFactorEnabled = user.IsTwoFactorEnabled }
                            });
                        }
                    }

                    // untrusted device — send OTP
                    var otp = new Random().Next(100000, 999999).ToString();
                    user.EmailVerificationOtp = otp;
                    user.OtpExpiry = DateTime.UtcNow.AddMinutes(10);
                    await _db.SaveChangesAsync();

                    await _email.SendEmailAsync(user.Email!, "DocuTrack - New Device Login",
                        $"<h2>New Device Login Detected</h2><p>Someone is trying to sign in to your account from a new device.</p><h1 style='color:#4F46E5;letter-spacing:8px'>{otp}</h1><p>This code expires in 10 minutes. If this wasn't you, please secure your account.</p>");

                    return Ok(new { requiresOtp = true, email = user.Email });
                }

                // 2FA disabled — normal login
                var jwt = GenerateJwt(user);
                await LogAudit("LOGIN_SUCCESS", "User", user.Id.ToString(), null, user.Id, user.Email);
                return Ok(new
                {
                    token = jwt,
                    requiresOtp = false,
                    user = new { id = user.Id, fullName = user.FullName, username = user.Username, email = user.Email, role = user.Role, departmentId = user.DepartmentId, isTwoFactorEnabled = user.IsTwoFactorEnabled }
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] GoogleLogin: {ex}");
                return BadRequest(new { error = "Google sign-in failed. Please try again." });
            }
        }

        public class GoogleLoginRequest
        {
            public string IdToken { get; set; } = string.Empty;
        }


        /// <summary>
        /// Register a new user.
        /// </summary>
        [HttpPost("register")]
        public async Task<IActionResult> Register(RegisterDto dto)
        {
            if (await _db.Users.AnyAsync(u => u.Email == dto.Email))
                return BadRequest(new { error = "Email already registered." });

            if (await _db.Users.AnyAsync(u => u.Username == dto.Username))
                return BadRequest(new { error = "Username already taken." });

            var otp = new Random().Next(100000, 999999).ToString();

            var user = new User
            {
                Id = Guid.NewGuid(),
                FullName = dto.FullName,
                Username = dto.Username,
                Email = dto.Email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
                Role = dto.Role,
                IsEmailVerified = false,
                EmailVerificationOtp = otp,
                OtpExpiry = DateTime.UtcNow.AddMinutes(10),
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            _db.Users.Add(user);
            await _db.SaveChangesAsync();

            // Send OTP email
            await _email.SendEmailAsync(dto.Email, "DocuTrack - Verify Your Email",
                $"<h2>Welcome to DocuTrack!</h2><p>Your verification code is:</p><h1 style='color:#4F46E5;letter-spacing:8px'>{otp}</h1><p>This code expires in 10 minutes.</p>");

            return Ok(new { message = "Registration successful. Check your email for the verification OTP." });
        }

        /// <summary>
        /// Verify email with OTP.
        /// </summary>
        [HttpPost("verify-otp")]
        public async Task<IActionResult> VerifyOtp(VerifyOtpDto dto)
        {
            var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == dto.Email);
            if (user == null) return NotFound(new { error = "User not found." });

            if (user.IsEmailVerified)
                return BadRequest(new { error = "Email already verified." });

            if (user.EmailVerificationOtp != dto.Otp)
                return BadRequest(new { error = "Invalid OTP." });

            if (user.OtpExpiry < DateTime.UtcNow)
                return BadRequest(new { error = "OTP has expired. Please register again." });

            user.IsEmailVerified = true;
            user.EmailVerificationOtp = null;
            user.OtpExpiry = null;
            await _db.SaveChangesAsync();

            return Ok(new { message = "Email verified successfully. You can now log in." });
        }

        /// <summary>
        /// Reset password for existing unverified users (temp).
        /// </summary>
        [HttpPost("reset-for-old-user")]
        public async Task<IActionResult> ResetOldUser([FromBody] RegisterDto dto)
        {
            var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == dto.Email);
            if (user == null) return NotFound();

            var otp = new Random().Next(100000, 999999).ToString();
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password);
            user.IsEmailVerified = false;
            user.EmailVerificationOtp = otp;
            user.OtpExpiry = DateTime.UtcNow.AddMinutes(10);
            user.IsActive = true;
            user.CreatedAt = DateTime.UtcNow;
            user.Role = dto.Role;
            user.FullName = dto.FullName;
            user.Username = dto.Username;

            await _db.SaveChangesAsync();

            await _email.SendEmailAsync(dto.Email, "DocuTrack - Verify Your Email",
                $"<h2>Account Reset!</h2><p>Your verification code is:</p><h1 style='color:#4F46E5;letter-spacing:8px'>{otp}</h1><p>This code expires in 10 minutes.</p>");

            return Ok(new { message = "Account reset. Check your email for OTP." });
        }

        /// <summary>
        /// Login with email and password.
        /// </summary>
        [HttpPost("login")]
        public async Task<IActionResult> Login(LoginDto dto)
        {
            var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == dto.Email);
            if (user == null) return Unauthorized(new { error = "Invalid credentials." });

            if (!user.IsEmailVerified)
                return Unauthorized(new { error = "Email not verified. Please verify your email first." });

            if (!BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash))
            {
                await LogAudit("LOGIN_FAILED", "User", user.Id.ToString(), $"Wrong password for {dto.Email}", user.Id, user.Email);
                return Unauthorized(new { error = "Invalid credentials." });
            }


            if (!user.IsActive)
                return Unauthorized(new { error = "Account is disabled." });

            // check 2FA
            if (user.IsTwoFactorEnabled)
            {
                var deviceToken = Request.Headers["X-Device-Token"].ToString();
                if (!string.IsNullOrEmpty(deviceToken))
                {
                    var trusted = await _db.TrustedDevices
                        .FirstOrDefaultAsync(d => d.DeviceToken == deviceToken && d.UserId == user.Id);
                    if (trusted != null)
                    {
                        trusted.LastUsedAt = DateTimeOffset.UtcNow;
                        await _db.SaveChangesAsync();
                        var token = GenerateJwt(user);
                        return Ok(new
                        {
                            token,
                            requiresOtp = false,
                            user = new { id = user.Id, fullName = user.FullName, username = user.Username, email = user.Email, role = user.Role, departmentId = user.DepartmentId, isTwoFactorEnabled = user.IsTwoFactorEnabled }
                        });
                    }
                }

                // untrusted device — send OTP
                var otp = new Random().Next(100000, 999999).ToString();
                user.EmailVerificationOtp = otp;
                user.OtpExpiry = DateTime.UtcNow.AddMinutes(10);
                await _db.SaveChangesAsync();

                await _email.SendEmailAsync(user.Email!, "DocuTrack - New Device Login",
                    $"<h2>New Device Login Detected</h2><p>Someone is trying to sign in to your account from a new device.</p><h1 style='color:#4F46E5;letter-spacing:8px'>{otp}</h1><p>This code expires in 10 minutes. If this wasn't you, please secure your account.</p>");

                return Ok(new { requiresOtp = true, email = user.Email });
            }

            // 2FA disabled — normal login
            var jwt = GenerateJwt(user);
            return Ok(new
            {
                token = jwt,
                requiresOtp = false,
                user = new { id = user.Id, fullName = user.FullName, username = user.Username, email = user.Email, role = user.Role, departmentId = user.DepartmentId, isTwoFactorEnabled = user.IsTwoFactorEnabled }
            });
        }

        [HttpPost("verify-device")]
        public async Task<IActionResult> VerifyDevice([FromBody] VerifyDeviceDto dto)
        {
            var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == dto.Email);
            if (user == null) return Unauthorized(new { error = "User not found." });

            if (user.EmailVerificationOtp != dto.Otp)
                return BadRequest(new { error = "Invalid OTP." });

            if (user.OtpExpiry < DateTime.UtcNow)
                return BadRequest(new { error = "OTP expired." });

            // clear OTP
            user.EmailVerificationOtp = null;
            user.OtpExpiry = null;

            // create trusted device
            var deviceToken = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
            var device = new TrustedDevice
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                DeviceToken = deviceToken,
                DeviceName = ParseDeviceName(Request.Headers["User-Agent"].ToString()),
                CreatedAt = DateTimeOffset.UtcNow,
                LastUsedAt = DateTimeOffset.UtcNow,
            };

            _db.TrustedDevices.Add(device);
            await _db.SaveChangesAsync();

            var token = GenerateJwt(user);
            return Ok(new
            {
                token,
                deviceToken,
                user = new { id = user.Id, fullName = user.FullName, username = user.Username, email = user.Email, role = user.Role, departmentId = user.DepartmentId, isTwoFactorEnabled = user.IsTwoFactorEnabled }
            });
        }



        [Authorize]
        [HttpGet("trusted-devices")]
        public async Task<IActionResult> GetTrustedDevices()
        {
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (userId == null) return Unauthorized();

            var devices = await _db.TrustedDevices
                .Where(d => d.UserId == Guid.Parse(userId))
                .OrderByDescending(d => d.LastUsedAt)
                .ToListAsync();

            return Ok(devices.Select(d => new
            {
                d.Id,
                d.DeviceName,
                d.DeviceToken,
                d.CreatedAt,
                d.LastUsedAt,
            }));
        }
        [Authorize]
        [HttpPost("trust-device")]
        public async Task<IActionResult> TrustDevice()
        {
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (userId == null) return Unauthorized();

            var deviceToken = Request.Headers["X-Device-Token"].ToString();

            // check if already trusted
            if (!string.IsNullOrEmpty(deviceToken))
            {
                var existing = await _db.TrustedDevices
                    .FirstOrDefaultAsync(d => d.DeviceToken == deviceToken && d.UserId == Guid.Parse(userId));
                if (existing != null)
                {
                    existing.LastUsedAt = DateTimeOffset.UtcNow;
                    await _db.SaveChangesAsync();
                    return Ok(new { deviceToken = existing.DeviceToken, message = "Already trusted." });
                }
            }

            // create new trusted device
            var newDeviceToken = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
            var device = new TrustedDevice
            {
                Id = Guid.NewGuid(),
                UserId = Guid.Parse(userId),
                DeviceToken = newDeviceToken,
                DeviceName = ParseDeviceName(Request.Headers["User-Agent"].ToString()),
                CreatedAt = DateTimeOffset.UtcNow,
                LastUsedAt = DateTimeOffset.UtcNow,
            };

            _db.TrustedDevices.Add(device);
            await _db.SaveChangesAsync();

            return Ok(new { deviceToken = newDeviceToken, message = "Device trusted." });
        }
        [Authorize]
        [HttpDelete("trusted-devices/{id:guid}")]
        public async Task<IActionResult> RemoveTrustedDevice(Guid id)
        {
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (userId == null) return Unauthorized();

            var device = await _db.TrustedDevices
                .FirstOrDefaultAsync(d => d.Id == id && d.UserId == Guid.Parse(userId));

            if (device == null) return NotFound();

            _db.TrustedDevices.Remove(device);
            await _db.SaveChangesAsync();

            return Ok(new { message = "Device removed." });
        }

        [Authorize]
        [HttpPatch("2fa/toggle")]
        public async Task<IActionResult> Toggle2FA()
        {
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (userId == null) return Unauthorized();

            var user = await _db.Users.FindAsync(Guid.Parse(userId));
            if (user == null) return NotFound();

            user.IsTwoFactorEnabled = !user.IsTwoFactorEnabled;
            await _db.SaveChangesAsync();
            await LogAudit("2FA_TOGGLED", "User", user.Id.ToString(), $"2FA {(user.IsTwoFactorEnabled ? "enabled" : "disabled")}", user.Id);
            return Ok(new { isTwoFactorEnabled = user.IsTwoFactorEnabled });
        }
        /// <summary>
        /// Generate a QR login token.
        /// </summary>
        /// 
        [Authorize]
        [HttpPost("qr-generate")]
        public async Task<IActionResult> GenerateQrToken([FromBody] Microsoft.AspNetCore.Mvc.ModelBinding.BindingInfo? _ = null)
        {
            var email = User.FindFirst(ClaimTypes.Email)?.Value;
            if (email == null) return Unauthorized();

            var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email);
            if (user == null) return NotFound();

            user.QrLoginToken = Guid.NewGuid().ToString();
            user.QrLoginExpiry = DateTime.UtcNow.AddMinutes(2);
            await _db.SaveChangesAsync();

            return Ok(new { qrToken = user.QrLoginToken });
        }

        /// <summary>
        /// Login via QR code token.
        /// </summary>
        [HttpPost("qr-login")]
        public async Task<IActionResult> QrLogin(QrLoginDto dto)
        {
            var user = await _db.Users.FirstOrDefaultAsync(u => u.QrLoginToken == dto.Token);
            if (user == null) return Unauthorized(new { error = "Invalid or expired QR token." });

            if (user.QrLoginExpiry < DateTime.UtcNow)
                return Unauthorized(new { error = "QR code expired. Please generate a new one." });

            // Invalidate token after use
            user.QrLoginToken = null;
            user.QrLoginExpiry = null;
            await _db.SaveChangesAsync();

            var token = GenerateJwt(user);

            return Ok(new
            {
                token,
                user = new
                {
                    user.Id,
                    user.FullName,
                    user.Username,
                    user.Email,
                    user.Role,
                    user.DepartmentId
                }
            });
        }

        /// <summary>
        /// Resend OTP to email.
        /// </summary>
        [HttpPost("resend-otp")]
        public async Task<IActionResult> ResendOtp([FromBody] ResendOtpDto dto)
        {
            var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == dto.Email);
            if (user == null) return NotFound(new { error = "User not found." });
            if (user.IsEmailVerified) return BadRequest(new { error = "Email already verified." });

            var otp = new Random().Next(100000, 999999).ToString();
            user.EmailVerificationOtp = otp;
            user.OtpExpiry = DateTime.UtcNow.AddMinutes(10);
            await _db.SaveChangesAsync();

            await _email.SendEmailAsync(dto.Email, "DocuTrack - New Verification Code",
                $"<h2>New Verification Code</h2><p>Your new OTP is:</p><h1 style='color:#4F46E5;letter-spacing:8px'>{otp}</h1><p>This code expires in 10 minutes.</p>");

            return Ok(new { message = "New OTP sent to your email." });
        }

        

        /// <summary>
        /// Creates a new QR session for login page display.
        /// </summary>
        [HttpPost("qr-session/create")]
        public async Task<IActionResult> CreateQrSession()
        {
            try
            {
                var now = DateTimeOffset.UtcNow;
                var expired = _db.QrSessions.Where(q => q.ExpiresAt < now);
                _db.QrSessions.RemoveRange(expired);

                var session = new DocuTrack.Core.Models.QrSession
                {
                    Id = Guid.NewGuid(),
                    Token = Guid.NewGuid().ToString(),
                    IsScanned = false,
                    CreatedAt = DateTimeOffset.UtcNow,
                    ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(2)
                };

                _db.QrSessions.Add(session);
                await _db.SaveChangesAsync();

                return Ok(new { token = session.Token, expiresAt = session.ExpiresAt });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"QR SESSION ERROR: {ex}");
                return StatusCode(500, new { error = ex.Message, detail = ex.InnerException?.Message });
            }
        }

        /// <summary>
        /// Poll to check if QR session was scanned.
        /// </summary>
        [HttpGet("qr-session/status/{token}")]
        public async Task<IActionResult> GetQrSessionStatus(string token)
        {
            try
            {
                var session = await _db.QrSessions.FirstOrDefaultAsync(q => q.Token == token);
                if (session == null) return NotFound(new { error = "Session not found." });
                if (session.ExpiresAt < DateTimeOffset.UtcNow) return BadRequest(new { error = "Session expired." });

                if (session.IsScanned && session.JwtToken != null)
                    return Ok(new { status = "confirmed", token = session.JwtToken });

                if (session.IsScanned)
                    return Ok(new { status = "scanned" });

                return Ok(new { status = "pending" });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"QR STATUS ERROR: {ex}");
                return StatusCode(500, new { error = ex.Message, detail = ex.InnerException?.Message });
            }
        }

        /// <summary>
        /// Confirm QR scan from mobile (requires auth).
        /// </summary>
        [Authorize]
        [HttpPost("qr-session/confirm/{token}")]
        public async Task<IActionResult> ConfirmQrSession(string token)
        {
            var session = await _db.QrSessions.FirstOrDefaultAsync(q => q.Token == token);
            if (session == null) return NotFound(new { error = "Session not found." });
            if (session.ExpiresAt < DateTime.UtcNow) return BadRequest(new { error = "Session expired." });

            var email = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value;
            var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email);
            if (user == null) return NotFound();

            session.IsScanned = true;
            session.ScannedByUserId = user.Id.ToString();
            session.JwtToken = GenerateJwt(user);
            await _db.SaveChangesAsync();

            return Ok(new { message = "QR session confirmed." });
        }
        private async Task LogAudit(string action, string? resourceType = null,
    string? resourceId = null, string? details = null, Guid? userId = null, string? userEmail = null)
        {
            try
            {
                var log = new DocuTrack.Core.Models.AuditLog
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
                };
                _db.AuditLogs.Add(log);
                await _db.SaveChangesAsync();
            }
            catch
            {
                // audit failure should never break the main flow
            }
        }

        private string GenerateJwt(User user)
        {
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"]!));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Email, user.Email!),
                new Claim(ClaimTypes.Name, user.FullName ?? user.Username!),
                new Claim(ClaimTypes.Role, user.Role ?? "Staff"),
            };

            var token = new JwtSecurityToken(
                issuer: _config["Jwt:Issuer"],
                audience: _config["Jwt:Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddHours(double.Parse(_config["Jwt:ExpiryHours"]!)),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
        private string ParseDeviceName(string? userAgent)
        {
            if (string.IsNullOrEmpty(userAgent)) return "Unknown Device";

            string browser = "Browser";
            string os = "Unknown OS";

            if (userAgent.Contains("Edg/")) browser = "Edge";
            else if (userAgent.Contains("Chrome")) browser = "Chrome";
            else if (userAgent.Contains("Firefox")) browser = "Firefox";
            else if (userAgent.Contains("Safari")) browser = "Safari";

            if (userAgent.Contains("Windows NT")) os = "Windows";
            else if (userAgent.Contains("Macintosh")) os = "macOS";
            else if (userAgent.Contains("Linux")) os = "Linux";
            else if (userAgent.Contains("Android")) os = "Android";
            else if (userAgent.Contains("iPhone") || userAgent.Contains("iPad")) os = "iOS";

            return $"{browser} on {os}";
        }

    }
}