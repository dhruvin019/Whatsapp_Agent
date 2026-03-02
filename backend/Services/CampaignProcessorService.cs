using Hangfire;
using backend.Data;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace backend.Services;

public interface ICampaignProcessorService
{
    Task ProcessCampaignAsync(int campaignId);
    Task ProcessSingleMessageAsync(int messageId);
}

public class CampaignProcessorService : ICampaignProcessorService
{
    private readonly ApplicationDbContext _context;
    private readonly IWhatsAppService _whatsAppService;
    private readonly ILogger<CampaignProcessorService> _logger;
    private readonly IBackgroundJobClient _backgroundJobClient;

    public CampaignProcessorService(
        ApplicationDbContext context, 
        IWhatsAppService whatsAppService, 
        ILogger<CampaignProcessorService> logger,
        IBackgroundJobClient backgroundJobClient)
    {
        _context = context;
        _whatsAppService = whatsAppService;
        _logger = logger;
        _backgroundJobClient = backgroundJobClient;
    }

    /// <summary>
    /// This job picks up a campaign, finds all pending messages, and queues them individually
    /// with a slight delay between each to avoid hitting WhatsApp rate limits.
    /// </summary>
    public async Task ProcessCampaignAsync(int campaignId)
    {
        var campaign = await _context.Campaigns.FindAsync(campaignId);
        if (campaign == null) return;

        campaign.Status = "Processing";
        await _context.SaveChangesAsync();

        var messages = await _context.Messages
            .Where(m => m.CampaignId == campaignId && m.Status == "Pending")
            .ToListAsync();

        _logger.LogInformation($"Starting to enqueue {messages.Count} messages for Campaign {campaignId}");

        // Rate limiting strategy: Delay each message slightly (e.g., 5 messages per second = 200ms delay)
        DateTimeOffset scheduledTime = DateTimeOffset.UtcNow;
        int delayMilliseconds = 200; 

        foreach (var message in messages)
        {
            _backgroundJobClient.Schedule<ICampaignProcessorService>(
                service => service.ProcessSingleMessageAsync(message.Id),
                scheduledTime
            );

            scheduledTime = scheduledTime.AddMilliseconds(delayMilliseconds);
        }
    }

    /// <summary>
    /// This job processes a single message, calling the WhatsApp API.
    /// </summary>
    public async Task ProcessSingleMessageAsync(int messageId)
    {
        var message = await _context.Messages.Include(m => m.Campaign).FirstOrDefaultAsync(m => m.Id == messageId);
        if (message == null || message.Status != "Pending") return;

        try
        {
            // Parse variables
            var variables = new Dictionary<string, string>();
            if (!string.IsNullOrEmpty(message.VariablesJson))
            {
                variables = JsonConvert.DeserializeObject<Dictionary<string, string>>(message.VariablesJson) ?? new Dictionary<string, string>();
            }

            // Map variables to a dynamic message string since Whapi accepts native text format
            var messageBody = message.TemplateName ?? "Automated Campaign Message";
            
            if (variables.Any())
            {
                messageBody += "\n";
                foreach(var kvp in variables) 
                {
                    messageBody += $"\n- {kvp.Key}: {kvp.Value}";
                }
            }

            // Call API
            var success = await _whatsAppService.SendTextMessageAsync(
                message.PhoneNumber,
                messageBody
            );

            // Update Database statuses proactively (Webhook will update later to Delivered/Read)
            if (success)
            {
                message.Status = "Sent";
                message.SentAt = DateTime.UtcNow;
                if (message.Campaign != null) message.Campaign.SuccessfullySent++;
            }
            else
            {
                message.Status = "Failed";
                message.ErrorMessage = "WhatsApp API returned failure.";
                if (message.Campaign != null) message.Campaign.FailedToSend++;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error processing message {messageId}");
            message.Status = "Failed";
            message.ErrorMessage = ex.Message;
            if (message.Campaign != null) message.Campaign.FailedToSend++;
        }

        await _context.SaveChangesAsync();

        // Check if Campaign is complete
        await CheckAndUpdateCampaignStatusAsync(message.CampaignId);
    }

    private async Task CheckAndUpdateCampaignStatusAsync(int campaignId)
    {
        var campaign = await _context.Campaigns.FindAsync(campaignId);
        if (campaign == null) return;

        var pendingCount = await _context.Messages.CountAsync(m => m.CampaignId == campaignId && m.Status == "Pending");
        
        if (pendingCount == 0 && campaign.Status != "Completed")
        {
            campaign.Status = "Completed";
            await _context.SaveChangesAsync();
            _logger.LogInformation($"Campaign {campaignId} has finished processing.");
        }
    }
}
