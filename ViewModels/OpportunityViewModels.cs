using System.ComponentModel.DataAnnotations;

namespace AcxiomCRM.ViewModels;

public sealed class OpportunityFormViewModel
{
    public int OpportunityId { get; set; }
    [Required, StringLength(150)] public string OpportunityName { get; set; } = string.Empty;
    [Required, Range(1, int.MaxValue)] public int CustomerId { get; set; }
    public int? LeadId { get; set; }
    [Range(typeof(decimal), "0.01", "79228162514264337593543950335")] public decimal Amount { get; set; }
    [Required, StringLength(50)] public string Stage { get; set; } = "Qualification";
    [Range(typeof(decimal), "0", "100")] public decimal Probability { get; set; }
    [DataType(DataType.Date)] public DateTime? ExpectedCloseDate { get; set; }
    [Required, StringLength(30)] public string Status { get; set; } = "Open";
    public string? AssignedTo { get; set; }
    public decimal WeightedValue { get; set; }
    public string? CustomerName { get; set; }
    public string? LeadName { get; set; }
    public string? AssignedToName { get; set; }
    public IReadOnlyList<OpportunityCustomerOption> Customers { get; set; } = [];
    public IReadOnlyList<OpportunityLeadOption> Leads { get; set; } = [];
    public IReadOnlyList<OpportunityAssigneeOption> Assignees { get; set; } = [];
}

public sealed class OpportunityListViewModel
{
    public IReadOnlyList<OpportunityListItemViewModel> Opportunities { get; init; } = [];
    public string? Search { get; init; }
    public string? Stage { get; init; }
    public string? Status { get; init; }
    public string? AssignedTo { get; init; }
    public IReadOnlyList<string> Stages { get; init; } = [];
    public IReadOnlyList<string> Statuses { get; init; } = [];
    public IReadOnlyList<OpportunityAssigneeOption> Assignees { get; init; } = [];
    public int Page { get; init; }
    public int TotalPages { get; init; }
    public decimal TotalAmount { get; init; }
    public decimal WeightedPipeline { get; init; }
    public bool CanFilterByAssignee { get; init; }
}

public sealed class OpportunityListItemViewModel
{
    public int OpportunityId { get; init; }
    public string OpportunityName { get; init; } = string.Empty;
    public string CustomerName { get; init; } = string.Empty;
    public string? LeadName { get; init; }
    public decimal Amount { get; init; }
    public string Stage { get; init; } = string.Empty;
    public decimal Probability { get; init; }
    public DateTime? ExpectedCloseDate { get; init; }
    public string Status { get; init; } = string.Empty;
    public string AssignedToName { get; init; } = string.Empty;
    public decimal WeightedValue { get; init; }
    public bool IsActivePipeline { get; init; }
}

public sealed class OpportunityCustomerOption
{
    public int Id { get; init; }
    public string DisplayName { get; init; } = string.Empty;
}

public sealed class OpportunityLeadOption
{
    public int Id { get; init; }
    public string DisplayName { get; init; } = string.Empty;
}

public sealed class OpportunityAssigneeOption
{
    public string Id { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
}
