using System.ComponentModel.DataAnnotations;

namespace AcxiomCRM.ViewModels;

public sealed class ActivityFormViewModel
{
    public int? CustomerId { get; set; }
    public int? LeadId { get; set; }
    [Required, StringLength(50)] public string ActivityType { get; set; } = "Call";
    [Required, StringLength(150)] public string Subject { get; set; } = string.Empty;
    [StringLength(2000)] public string? Description { get; set; }
    [Required, DataType(DataType.DateTime)] public DateTime ActivityDate { get; set; } = DateTime.Now;
    [Required, StringLength(30)] public string Status { get; set; } = "Open";
    public IReadOnlyList<FollowUpCustomerOption> Customers { get; set; } = [];
    public IReadOnlyList<FollowUpLeadOption> Leads { get; set; } = [];
}
