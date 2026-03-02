using System.ComponentModel.DataAnnotations;

namespace backend.Models;

public class User
{
    [Key]
    public int Id { get; set; }
    
    [Required]
    [MaxLength(20)]
    public string WhatsAppNumber { get; set; } = string.Empty;
    
    public bool IsVerified { get; set; }
    
    public string? WhatsAppBusinessAccountId { get; set; }
    
    public string? WhatsAppAccessToken { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
