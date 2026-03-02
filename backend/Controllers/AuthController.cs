using backend.Models.DTOs;
using backend.Services;
using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    [HttpPost("request-otp")]
    public async Task<IActionResult> RequestOtp([FromBody] LoginDto request)
    {
        if (string.IsNullOrWhiteSpace(request.WhatsAppNumber))
            return BadRequest(new { Message = "WhatsApp number is required." });

        var success = await _authService.RequestOtpAsync(request.WhatsAppNumber);
        
        if (success)
            return Ok(new { Message = "OTP sent successfully." });
            
        return StatusCode(500, new { Message = "Failed to send OTP." });
    }

    [HttpPost("verify-otp")]
    public async Task<IActionResult> VerifyOtp([FromBody] VerifyOtpDto request)
    {
        if (string.IsNullOrWhiteSpace(request.WhatsAppNumber) || string.IsNullOrWhiteSpace(request.Otp))
            return BadRequest(new { Message = "WhatsApp number and OTP are required." });

        var token = await _authService.VerifyOtpAndLoginAsync(request.WhatsAppNumber, request.Otp);

        if (token == null)
            return Unauthorized(new { Message = "Invalid OTP." });

        return Ok(new 
        { 
            Message = "Login successful.",
            Token = token 
        });
    }
}
