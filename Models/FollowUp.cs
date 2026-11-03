using System.ComponentModel.DataAnnotations;

namespace AcxiomCRM.Models;

public class FollowUp
{
    public int FollowUpId { get; set; }
    public int? CustomerId { get; set; }
    public int? LeadId { get; set; }
    public DateTime FollowUpDate { get; set; }
    [Required, StringLength(50)] public string FollowUpType { get; set; } = string.Empty;
    [StringLength(1000)] public string? Remarks { get; set; }
    [Required, StringLength(30)] public string Status { get; set; } = "Pending";
    [StringLength(450)] public string? AssignedTo { get; set; }
    public Customer? Customer { get; set; }
    public Lead? Lead { get; set; }
}
