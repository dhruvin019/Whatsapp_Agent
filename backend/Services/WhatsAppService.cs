using System.Net.Http.Headers;
using System.Text;
using Newtonsoft.Json;

namespace backend.Services;

public class WhatsAppService : IWhatsAppService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<WhatsAppService> _logger;

    public WhatsAppService(HttpClient httpClient, IConfiguration configuration, ILogger<WhatsAppService> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
        
        var settings = _configuration.GetSection("WhatsAppApiSettings");
        var token = settings["Token"];
        var apiUrl = settings["ApiUrl"] ?? "https://gate.whapi.cloud/";
        
        _httpClient.BaseAddress = new Uri(apiUrl);
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    public async Task<bool> SendTextMessageAsync(string phoneNumber, string message)
    {
        var endpoint = "messages/text";

        // Whapi Schema parameters for a text message
        var payload = new
        {
            typing_time = 0,
            to = phoneNumber,
            body = message
        };

        try
        {
            var content = new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync(endpoint, content);
            
            var responseContent = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation($"WhatsApp message sent successfully via Whapi: {responseContent}");
                return true;
            }
            else
            {
                _logger.LogError($"Failed to send Whapi message. Status: {response.StatusCode}, Details: {responseContent}");
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception occurred while sending Whapi message.");
            return false;
        }
    }
}
