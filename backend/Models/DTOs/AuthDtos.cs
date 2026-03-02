namespace backend.Models.DTOs;

public class LoginDto
{
    public string WhatsAppNumber { get; set; } = string.Empty;
}

public class VerifyOtpDto
{
    public string WhatsAppNumber { get; set; } = string.Empty;
    public string Otp { get; set; } = string.Empty;
}
