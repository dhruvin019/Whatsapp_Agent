using backend.Data;
using backend.Models;
using backend.Models.DTOs;
using backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using System.Security.Claims;
using Hangfire;

namespace backend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CampaignController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IExcelParserService _excelParserService;
    private readonly ILogger<CampaignController> _logger;

    public CampaignController(ApplicationDbContext context, IExcelParserService excelParserService, ILogger<CampaignController> logger)
    {
        _context = context;
        _excelParserService = excelParserService;
        _logger = logger;
    }

    [HttpPost("upload")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UploadCampaignFile(
        [FromForm] IFormFile file, 
        [FromForm] string campaignName, 
        [FromForm] string phoneColumnName,
        [FromForm] string templateName)
    {
        try
        {
            var userId = 1; // Auth removed — default user

            if (file == null || file.Length == 0)
                return BadRequest(new { Message = "No file uploaded" });

            if (string.IsNullOrEmpty(campaignName) || string.IsNullOrEmpty(phoneColumnName) || string.IsNullOrEmpty(templateName))
                return BadRequest(new { Message = "Campaign Name, Phone Column Name, and Template Name are required." });

            var extension = Path.GetExtension(file.FileName).ToLower();
            if (extension != ".xlsx" && extension != ".xls")
                return BadRequest(new { Message = "Only Excel files (.xlsx, .xls) are allowed." });

            // 1. Process Excel File
            using var stream = file.OpenReadStream();
            var parseResult = await _excelParserService.ParseExcelAsync(stream, phoneColumnName);

            // 2. Create Campaign in DB
            var campaign = new Campaign
            {
                UserId = userId,
                Name = campaignName,
                Status = "Pending",
                TotalMessages = parseResult.ValidRows.Count,
                SuccessfullySent = 0,
                FailedToSend = 0
            };

            _context.Campaigns.Add(campaign);
            await _context.SaveChangesAsync(); // Save to get CampaignId

            // 3. Save Valid Messages to DB
            var messagesToInsert = new List<Message>();
            foreach (var row in parseResult.ValidRows)
            {
                messagesToInsert.Add(new Message
                {
                    CampaignId = campaign.Id,
                    PhoneNumber = row.PhoneNumber,
                    TemplateName = templateName,
                    VariablesJson = JsonConvert.SerializeObject(row.Variables),
                    Status = "Pending"
                });
            }

            if (messagesToInsert.Any())
            {
                await _context.Messages.AddRangeAsync(messagesToInsert);
                await _context.SaveChangesAsync();
                
                // 4. Trigger Background Processing Queue via Hangfire
                var backgroundJobClient = HttpContext.RequestServices.GetRequiredService<Hangfire.IBackgroundJobClient>();
                backgroundJobClient.Enqueue<ICampaignProcessorService>(service => service.ProcessCampaignAsync(campaign.Id));
            }

            var result = new CampaignUploadResultDto
            {
                CampaignId = campaign.Id,
                TotalRows = parseResult.ValidRows.Count + parseResult.Errors.Count,
                ValidRows = parseResult.ValidRows.Count,
                InvalidRows = parseResult.Errors.Count,
                Errors = parseResult.Errors.Take(100).ToList() // Return max 100 errors to frontend
            };

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading campaign");
            return StatusCode(500, new { Message = "An error occurred while processing the file." });
        }
    }

    [HttpPost("create-manual")]
    public async Task<IActionResult> CreateManualCampaign([FromBody] CreateManualCampaignDto request)
    {
        try
        {
            var userId = 1; // Auth removed — default user

            if (string.IsNullOrEmpty(request.CampaignName) || string.IsNullOrEmpty(request.TemplateName))
                return BadRequest(new { Message = "Campaign Name and Template Name are required." });

            if (request.Numbers == null || !request.Numbers.Any())
                return BadRequest(new { Message = "At least one phone number must be provided." });

            // 1. Process Manual Numbers
            var validRows = new List<ExcelDataRow>();
            var errors = new List<string>();

            foreach (var rawPhone in request.Numbers)
            {
                if (string.IsNullOrWhiteSpace(rawPhone)) continue;

                var cleanPhone = System.Text.RegularExpressions.Regex.Replace(rawPhone.Trim(), @"[^\d]", "");
                if (cleanPhone.Length < 10 || cleanPhone.Length > 15)
                {
                    errors.Add($"Number '{rawPhone}' is invalid. Must be 10-15 digits.");
                }
                else
                {
                    validRows.Add(new ExcelDataRow { PhoneNumber = cleanPhone });
                }
            }

            if (!validRows.Any())
                return BadRequest(new { Message = "No valid phone numbers found.", Errors = errors });

            // 2. Create Campaign in DB
            var campaign = new Campaign
            {
                UserId = userId,
                Name = request.CampaignName,
                Status = "Pending",
                TotalMessages = validRows.Count,
                SuccessfullySent = 0,
                FailedToSend = 0
            };

            _context.Campaigns.Add(campaign);
            await _context.SaveChangesAsync();

            // 3. Save Valid Messages to DB
            var messagesToInsert = validRows.Select(row => new Message
            {
                CampaignId = campaign.Id,
                PhoneNumber = row.PhoneNumber,
                TemplateName = request.TemplateName,
                VariablesJson = JsonConvert.SerializeObject(row.Variables),
                Status = "Pending"
            }).ToList();

            await _context.Messages.AddRangeAsync(messagesToInsert);
            await _context.SaveChangesAsync();

            // 4. Trigger Background Processing Queue via Hangfire (immediate or scheduled)
            var backgroundJobClient = HttpContext.RequestServices.GetRequiredService<Hangfire.IBackgroundJobClient>();
            if (request.ScheduledAt.HasValue && request.ScheduledAt.Value > DateTime.UtcNow)
            {
                var delay = request.ScheduledAt.Value.ToUniversalTime() - DateTime.UtcNow;
                backgroundJobClient.Schedule<ICampaignProcessorService>(service => service.ProcessCampaignAsync(campaign.Id), delay);
                campaign.Status = "Scheduled";
                await _context.SaveChangesAsync();
            }
            else
            {
                backgroundJobClient.Enqueue<ICampaignProcessorService>(service => service.ProcessCampaignAsync(campaign.Id));
            }

            var result = new CampaignUploadResultDto
            {
                CampaignId = campaign.Id,
                TotalRows = request.Numbers.Count,
                ValidRows = validRows.Count,
                InvalidRows = errors.Count,
                Errors = errors.Take(100).ToList()
            };

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating manual campaign");
            return StatusCode(500, new { Message = "An error occurred while creating the campaign." });
        }
    }

    [HttpGet("demo-excel")]
    public IActionResult DownloadDemoExcel()
    {
        using var package = new OfficeOpenXml.ExcelPackage();
        OfficeOpenXml.ExcelPackage.License.SetNonCommercialPersonal("WhatsApp Bulk Sender");

        var sheet = package.Workbook.Worksheets.Add("Contacts");

        // Headers
        sheet.Cells[1, 1].Value = "PhoneNumber";
        sheet.Cells[1, 2].Value = "Name";
        sheet.Cells[1, 3].Value = "Custom1";
        sheet.Cells[1, 4].Value = "Custom2";

        // Sample rows
        sheet.Cells[2, 1].Value = "919876543210";
        sheet.Cells[2, 2].Value = "Raj Sharma";
        sheet.Cells[2, 3].Value = "VIP Customer";
        sheet.Cells[2, 4].Value = "Promo Code: SAVE20";

        sheet.Cells[3, 1].Value = "911234567890";
        sheet.Cells[3, 2].Value = "Priya Mehta";
        sheet.Cells[3, 3].Value = "Regular";
        sheet.Cells[3, 4].Value = "";

        // Auto-fit columns
        sheet.Cells.AutoFitColumns();

        var fileBytes = package.GetAsByteArray();
        return File(fileBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "demo_contacts.xlsx");
    }
}
