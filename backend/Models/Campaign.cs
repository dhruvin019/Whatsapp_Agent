using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace backend.Models;

public class Campaign
{
    [Key]
    public int Id { get; set; }
    
    [Required]
    [ForeignKey("User")]
    public int UserId { get; set; }
    public User? User { get; set; }
    
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;
    
    public string Status { get; set; } = "Pending"; // Pending, Processing, Completed, Failed
    
    public int TotalMessages { get; set; }
    public int SuccessfullySent { get; set; }
    public int FailedToSend { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
