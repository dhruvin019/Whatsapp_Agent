namespace backend.Services;

public interface IWhatsAppService
{
    Task<bool> SendTextMessageAsync(string phoneNumber, string message);
}
