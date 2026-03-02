using backend.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json.Linq;

namespace backend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class WebhookController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly ILogger<WebhookController> _logger;

    public WebhookController(ApplicationDbContext context, IConfiguration configuration, ILogger<WebhookController> logger)
    {
        _context = context;
        _configuration = configuration;
        _logger = logger;
    }

    // GET /api/webhook - Used by Meta to verify the webhook URL
    [HttpGet]
    public IActionResult VerifyWebhook(
        [FromQuery(Name = "hub.mode")] string mode,
        [FromQuery(Name = "hub.verify_token")] string token,
        [FromQuery(Name = "hub.challenge")] string challenge)
    {
        var verifyToken = _configuration["WhatsAppApiSettings:WebhookVerifyToken"];

        if (mode == "subscribe" && token == verifyToken)
        {
            _logger.LogInformation("Webhook verified successfully.");
            return Ok(challenge);
        }

        _logger.LogWarning("Webhook verification failed.");
        return Forbid();
    }

    // POST /api/webhook - Receives status updates from Meta
    [HttpPost]
    public async Task<IActionResult> ReceiveMessageStatus([FromBody] JObject payload)
    {
        _logger.LogInformation($"Received webhook payload: {payload}");

        try
        {
            var entries = payload["entry"] as JArray;
            if (entries == null) return Ok();

            foreach (var entry in entries)
            {
                var changes = entry["changes"] as JArray;
                if (changes == null) continue;

                foreach (var change in changes)
                {
                    var value = change["value"];
                    if (value == null) continue;

                    var statuses = value["statuses"] as JArray;
                    if (statuses != null)
                    {
                        await ProcessStatusesAsync(statuses);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing webhook payload.");
        }

        // Always return 200 OK so Meta doesn't retry
        return Ok();
    }

    private async Task ProcessStatusesAsync(JArray statuses)
    {
        foreach (var status in statuses)
        {
            var statusStr = status["status"]?.ToString();
            var wamid = status["id"]?.ToString();

            if (!string.IsNullOrEmpty(statusStr) && !string.IsNullOrEmpty(wamid))
            {
                // Find message in DB
                var message = await _context.Messages.FirstOrDefaultAsync(m => m.WhatsAppMessageId == wamid);
                
                if (message != null)
                {
                    message.Status = char.ToUpper(statusStr[0]) + statusStr.Substring(1); // Capitalize: sent -> Sent

                    // Handle failures
                    if (statusStr == "failed")
                    {
                        var errors = status["errors"] as JArray;
                        if (errors != null && errors.Count > 0)
                        {
                            message.ErrorMessage = errors[0]["title"]?.ToString();
                        }
                    }

                    await _context.SaveChangesAsync();
                    _logger.LogInformation($"Updated message {message.Id} status to {message.Status}");
                    
                    // We can also trigger SignalR/WebSockets here to update the frontend in real-time
                }
            }
        }
    }
}
