using System.ComponentModel.DataAnnotations;

namespace AcxiomCRM.ViewModels;

public sealed class LeadFormViewModel
{
    public int LeadId { get; set; }
    [Required, StringLength(30)] public string LeadCode { get; set; } = string.Empty;
    [Required, StringLength(150)] public string LeadName { get; set; } = string.Empty;
    [Required, EmailAddress, StringLength(256)] public string Email { get; set; } = string.Empty;
    [StringLength(30), RegularExpression(@"^[0-9+()\-\.\s]{7,30}$", ErrorMessage = "Enter a valid phone number.")] public string? Phone { get; set; }
    [StringLength(150)] public string? CompanyName { get; set; }
    [Required, StringLength(80)] public string Source { get; set; } = string.Empty;
    [Required, StringLength(30)] public string Status { get; set; } = "New";
    [Range(typeof(decimal), "0", "79228162514264337593543950335")] public decimal ExpectedValue { get; set; }
    public DateTime CreatedDate { get; set; }
    public string? AssignedTo { get; set; }
    public string? AssignedToName { get; set; }
    public IReadOnlyList<LeadAssigneeViewModel> Assignees { get; set; } = [];
}

public sealed class LeadListViewModel
{
    public IReadOnlyList<LeadListItemViewModel> Leads { get; init; } = [];
    public string? Search { get; init; }
    public string? Status { get; init; }
    public string? Source { get; init; }
    public string? AssignedTo { get; init; }
    public IReadOnlyList<string> Statuses { get; init; } = [];
    public IReadOnlyList<string> Sources { get; init; } = [];
    public IReadOnlyList<LeadAssigneeViewModel> Assignees { get; init; } = [];
    public int Page { get; init; }
    public int TotalPages { get; init; }
    public bool CanFilterByAssignee { get; init; }
}

public sealed class LeadListItemViewModel
{
    public int LeadId { get; init; }
    public string LeadCode { get; init; } = string.Empty;
    public string LeadName { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string? Phone { get; init; }
    public string? CompanyName { get; init; }
    public string Source { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public decimal ExpectedValue { get; init; }
    public DateTime CreatedDate { get; init; }
    public string AssignedToName { get; init; } = string.Empty;
}

public sealed class LeadAssigneeViewModel
{
    public string Id { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
}
