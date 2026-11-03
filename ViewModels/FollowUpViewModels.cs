using System.ComponentModel.DataAnnotations;

namespace AcxiomCRM.ViewModels;

public sealed class FollowUpFormViewModel
{
    public int FollowUpId { get; set; }
    public int? CustomerId { get; set; }
    public int? LeadId { get; set; }
    [Required, DataType(DataType.DateTime)] public DateTime FollowUpDate { get; set; } = DateTime.Now.AddDays(1);
    [Required, StringLength(50)] public string FollowUpType { get; set; } = "Call";
    [StringLength(1000)] public string? Remarks { get; set; }
    [Required, StringLength(30)] public string Status { get; set; } = "Planned";
    public string? AssignedTo { get; set; }
    public string? CustomerName { get; set; }
    public string? LeadName { get; set; }
    public string? AssignedToName { get; set; }
    public IReadOnlyList<FollowUpCustomerOption> Customers { get; set; } = [];
    public IReadOnlyList<FollowUpLeadOption> Leads { get; set; } = [];
    public IReadOnlyList<FollowUpAssigneeOption> Assignees { get; set; } = [];
}

public sealed class FollowUpListViewModel
{
    public IReadOnlyList<FollowUpListItemViewModel> FollowUps { get; init; } = [];
    public string? Search { get; init; }
    public string? Status { get; init; }
    public string? FollowUpType { get; init; }
    public string? AssignedTo { get; init; }
    public DateTime? FromDate { get; init; }
    public DateTime? ToDate { get; init; }
    public IReadOnlyList<string> Statuses { get; init; } = [];
    public IReadOnlyList<string> FollowUpTypes { get; init; } = [];
    public IReadOnlyList<FollowUpAssigneeOption> Assignees { get; init; } = [];
    public int Page { get; init; }
    public int TotalPages { get; init; }
    public bool CanFilterByAssignee { get; init; }
}

public sealed class FollowUpListItemViewModel
{
    public int FollowUpId { get; init; }
    public string CustomerName { get; init; } = string.Empty;
    public string LeadName { get; init; } = string.Empty;
    public DateTime FollowUpDate { get; init; }
    public string FollowUpType { get; init; } = string.Empty;
    public string? Remarks { get; init; }
    public string Status { get; init; } = string.Empty;
    public string AssignedToName { get; init; } = string.Empty;
    public bool IsOverdue { get; init; }
}

public sealed class FollowUpCustomerOption { public int Id { get; init; } public string DisplayName { get; init; } = string.Empty; }
public sealed class FollowUpLeadOption { public int Id { get; init; } public string DisplayName { get; init; } = string.Empty; }
public sealed class FollowUpAssigneeOption { public string Id { get; init; } = string.Empty; public string DisplayName { get; init; } = string.Empty; }
