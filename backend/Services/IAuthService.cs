namespace backend.Services;

public interface IAuthService
{
    Task<bool> RequestOtpAsync(string phoneNumber);
    Task<string?> VerifyOtpAndLoginAsync(string phoneNumber, string otp);
}
