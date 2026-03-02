using backend.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace backend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AnalyticsController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public AnalyticsController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet("dashboard-stats")]
    public async Task<IActionResult> GetDashboardStats()
    {
        // Auth removed — query all records (no userId filter)
        var totalCampaigns = await _context.Campaigns.CountAsync();
        var totalMessages = await _context.Messages.CountAsync();
        var messagesSent = await _context.Messages.Where(m => m.Status == "Sent").CountAsync();
        var messagesDelivered = await _context.Messages.Where(m => m.Status == "Delivered").CountAsync();
        var messagesRead = await _context.Messages.Where(m => m.Status == "Read").CountAsync();
        var messagesFailed = await _context.Messages.Where(m => m.Status == "Failed").CountAsync();

        return Ok(new
        {
            TotalCampaigns = totalCampaigns,
            TotalMessages = totalMessages,
            Sent = messagesSent,
            Delivered = messagesDelivered,
            Read = messagesRead,
            Failed = messagesFailed
        });
    }

    [HttpGet("campaigns")]
    public async Task<IActionResult> GetCampaignHistory()
    {
        // Auth removed — return all campaigns
        var campaigns = await _context.Campaigns
            .OrderByDescending(c => c.CreatedAt)
            .Select(c => new
            {
                c.Id,
                c.Name,
                c.Status,
                c.TotalMessages,
                c.SuccessfullySent,
                c.FailedToSend,
                c.CreatedAt
            })
            .ToListAsync();

        return Ok(campaigns);
    }
}
