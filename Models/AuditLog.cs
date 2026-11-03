using System.ComponentModel.DataAnnotations;

namespace AcxiomCRM.Models;

public class AuditLog
{
    public int AuditLogId { get; set; }
    [Required, StringLength(450)] public string UserId { get; set; } = string.Empty;
    [Required, StringLength(50)] public string Action { get; set; } = string.Empty;
    [Required, StringLength(100)] public string EntityName { get; set; } = string.Empty;
    [Required, StringLength(100)] public string RecordId { get; set; } = string.Empty;
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    [StringLength(45)] public string? IpAddress { get; set; }
}
