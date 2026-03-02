using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace backend.Models;

public class Message
{
    [Key]
    public int Id { get; set; }
    
    [Required]
    [ForeignKey("Campaign")]
    public int CampaignId { get; set; }
    public Campaign? Campaign { get; set; }
    
    [Required]
    [MaxLength(20)]
    public string PhoneNumber { get; set; } = string.Empty;
    
    public string? TemplateName { get; set; }
    
    public string? VariablesJson { get; set; } // JSON string of variables for the template
    
    public string Status { get; set; } = "Pending"; // Pending, Sent, Delivered, Read, Failed
    
    public string? ErrorMessage { get; set; }
    
    public string? WhatsAppMessageId { get; set; }
    
    public DateTime? SentAt { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
