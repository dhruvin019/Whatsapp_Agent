namespace backend.Models.DTOs;

public class CampaignUploadResultDto
{
    public int TotalRows { get; set; }
    public int ValidRows { get; set; }
    public int InvalidRows { get; set; }
    public List<string> Errors { get; set; } = new();
    public int CampaignId { get; set; }
}

public class ExcelDataRow
{
    public string PhoneNumber { get; set; } = string.Empty;
    public Dictionary<string, string> Variables { get; set; } = new();
}

public class CreateManualCampaignDto
{
    public string CampaignName { get; set; } = string.Empty;
    public string TemplateName { get; set; } = string.Empty;
    public List<string> Numbers { get; set; } = new();
    public DateTime? ScheduledAt { get; set; } // Optional: null = send immediately
}
