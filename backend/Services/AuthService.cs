using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using backend.Data;
using backend.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace backend.Services;

public class AuthService : IAuthService
{
    private readonly ApplicationDbContext _context;
    private readonly IConfiguration _configuration;
    
    // In-memory store for OTPs (Demo purpose only. In production use Redis or DB with expiry)
    private static readonly Dictionary<string, string> _otpStore = new();

    public AuthService(ApplicationDbContext context, IConfiguration configuration)
    {
        _context = context;
        _configuration = configuration;
    }

    public async Task<bool> RequestOtpAsync(string phoneNumber)
    {
        // 1. Hardcode OTP for DEV testing, since actual SMS isn't hooked up yet
        var otp = "123456"; 
        
        // 2. Store it
        _otpStore[phoneNumber] = otp;
        
        // 3. For development, we'll just log it
        Console.WriteLine($"[DEV ONLY] OTP for {phoneNumber} is {otp}");
        
        return await Task.FromResult(true);
    }

    public async Task<string?> VerifyOtpAndLoginAsync(string phoneNumber, string otp)
    {
        // 1. Check if OTP is valid
        if (!_otpStore.TryGetValue(phoneNumber, out var storedOtp) || storedOtp != otp)
        {
            return null; // Invalid OTP
        }
        
        // 2. Remove OTP after verified
        _otpStore.Remove(phoneNumber);
        
        // 3. Find or Create User
        var user = await _context.Users.FirstOrDefaultAsync(u => u.WhatsAppNumber == phoneNumber);
        if (user == null)
        {
            user = new User 
            { 
                WhatsAppNumber = phoneNumber,
                IsVerified = true
            };
            _context.Users.Add(user);
            await _context.SaveChangesAsync();
        }

        // 4. Generate JWT
        return GenerateJwtToken(user);
    }

    private string GenerateJwtToken(User user)
    {
        var jwtSettings = _configuration.GetSection("JwtSettings");
        var secret = jwtSettings["Secret"];
        var expiryDays = int.Parse(jwtSettings["ExpirationInDays"] ?? "7");
        
        if (string.IsNullOrEmpty(secret) || secret.Length < 32)
            throw new InvalidOperationException("JWT Secret is not configured securely.");

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim("WhatsAppNumber", user.WhatsAppNumber),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var token = new JwtSecurityToken(
            issuer: null,
            audience: null,
            claims: claims,
            expires: DateTime.UtcNow.AddDays(expiryDays),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
