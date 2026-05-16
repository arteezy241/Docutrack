using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using DocuTrack.Core.Models;
using DocuTrack.Infrastructure.Data;

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
            public string FullName { get; set; } = string.Empty;
            public string Username { get; set; } = string.Empty;
            public string Email { get; set; } = string.Empty;
            public string Password { get; set; } = string.Empty;
            public string Role { get; set; } = "Staff";
        }

        public class LoginDto
        {
            public string Email { get; set; } = string.Empty;
            public string Password { get; set; } = string.Empty;
        }

        public class VerifyOtpDto
        {
            public string Email { get; set; } = string.Empty;
            public string Otp { get; set; } = string.Empty;
        }

        public class QrLoginDto
        {
            public string Token { get; set; } = string.Empty;
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
                return Unauthorized(new { error = "Invalid credentials." });

            if (!user.IsActive)
                return Unauthorized(new { error = "Account is disabled." });

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
        /// Generate a QR login token.
        /// </summary>
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

        public class ResendOtpDto
        {
            public string Email { get; set; } = string.Empty;
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
    }
}