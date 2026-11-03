using System.ComponentModel.DataAnnotations;

namespace AcxiomCRM.Models;

public class Activity
{
    public int ActivityId { get; set; }
    [Required, StringLength(50)] public string ActivityType { get; set; } = string.Empty;
    [Required, StringLength(150)] public string Subject { get; set; } = string.Empty;
    [StringLength(2000)] public string? Description { get; set; }
    public DateTime ActivityDate { get; set; }
    public int? CustomerId { get; set; }
    public int? LeadId { get; set; }
    [StringLength(450)] public string? AssignedTo { get; set; }
    [Required, StringLength(30)] public string Status { get; set; } = "Open";
    public Customer? Customer { get; set; }
    public Lead? Lead { get; set; }
}
