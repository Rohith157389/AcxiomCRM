using System.ComponentModel.DataAnnotations;

namespace AcxiomCRM.Api;

/// <summary>Paginated API response containing records and page metadata.</summary>
public sealed record PagedResponse<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount, int TotalPages);

/// <summary>Customer creation payload.</summary>
public sealed record CreateCustomerRequest
{
    [Required, StringLength(30)] public required string CustomerCode { get; init; }
    [Required, StringLength(150)] public required string CustomerName { get; init; }
    [Required, EmailAddress, StringLength(256)] public required string Email { get; init; }
    [StringLength(30), RegularExpression(@"^[0-9+()\-\.\s]{7,30}$")] public string? Phone { get; init; }
    [StringLength(150)] public string? CompanyName { get; init; }
    [StringLength(250)] public string? Address { get; init; }
    [StringLength(100)] public string? City { get; init; }
    [StringLength(100)] public string? State { get; init; }
    [Required, StringLength(30)] public string Status { get; init; } = "Active";
}

/// <summary>Customer update payload.</summary>
public sealed record UpdateCustomerRequest
{
    [Required, StringLength(30)] public required string CustomerCode { get; init; }
    [Required, StringLength(150)] public required string CustomerName { get; init; }
    [Required, EmailAddress, StringLength(256)] public required string Email { get; init; }
    [StringLength(30), RegularExpression(@"^[0-9+()\-\.\s]{7,30}$")] public string? Phone { get; init; }
    [StringLength(150)] public string? CompanyName { get; init; }
    [StringLength(250)] public string? Address { get; init; }
    [StringLength(100)] public string? City { get; init; }
    [StringLength(100)] public string? State { get; init; }
    [Required, StringLength(30)] public string Status { get; init; } = "Active";
}

/// <summary>Customer returned by the API.</summary>
public sealed record CustomerResponse(int CustomerId, string CustomerCode, string CustomerName, string Email, string? Phone, string? CompanyName, string? Address, string? City, string? State, string Status, DateTimeOffset CreatedDate, string CreatedBy);

/// <summary>Lead creation payload.</summary>
public sealed record CreateLeadRequest
{
    [Required, StringLength(30)] public required string LeadCode { get; init; }
    [Required, StringLength(150)] public required string LeadName { get; init; }
    [Required, EmailAddress, StringLength(256)] public required string Email { get; init; }
    [StringLength(30)] public string? Phone { get; init; }
    [StringLength(150)] public string? CompanyName { get; init; }
    [Required, StringLength(80)] public required string Source { get; init; }
    [Required, StringLength(30)] public string Status { get; init; } = "New";
    [Range(0, double.MaxValue)] public decimal ExpectedValue { get; init; }
}

/// <summary>Lead update payload.</summary>
public sealed record UpdateLeadRequest
{
    [Required, StringLength(30)] public required string LeadCode { get; init; }
    [Required, StringLength(150)] public required string LeadName { get; init; }
    [Required, EmailAddress, StringLength(256)] public required string Email { get; init; }
    [StringLength(30)] public string? Phone { get; init; }
    [StringLength(150)] public string? CompanyName { get; init; }
    [Required, StringLength(80)] public required string Source { get; init; }
    [Required, StringLength(30)] public string Status { get; init; } = "New";
    [Range(0, double.MaxValue)] public decimal ExpectedValue { get; init; }
}

/// <summary>Lead returned by the API.</summary>
public sealed record LeadResponse(int LeadId, string LeadCode, string LeadName, string Email, string? Phone, string? CompanyName, string Source, string Status, decimal ExpectedValue, DateTimeOffset CreatedDate, string AssignedTo);

/// <summary>Opportunity creation payload.</summary>
public sealed record CreateOpportunityRequest
{
    [Required, StringLength(150)] public required string OpportunityName { get; init; }
    [Range(1, int.MaxValue)] public int CustomerId { get; init; }
    public int? LeadId { get; init; }
    [Range(typeof(decimal), "0.01", "79228162514264337593543950335")] public decimal Amount { get; init; }
    [Required, StringLength(50)] public string Stage { get; init; } = "Qualification";
    [Range(0, 100)] public decimal Probability { get; init; }
    public DateTimeOffset? ExpectedCloseDate { get; init; }
    [Required, StringLength(30)] public string Status { get; init; } = "Open";
}

/// <summary>Opportunity update payload.</summary>
public sealed record UpdateOpportunityRequest
{
    [Required, StringLength(150)] public required string OpportunityName { get; init; }
    [Range(1, int.MaxValue)] public int CustomerId { get; init; }
    public int? LeadId { get; init; }
    [Range(typeof(decimal), "0.01", "79228162514264337593543950335")] public decimal Amount { get; init; }
    [Required, StringLength(50)] public string Stage { get; init; } = "Qualification";
    [Range(0, 100)] public decimal Probability { get; init; }
    public DateTimeOffset? ExpectedCloseDate { get; init; }
    [Required, StringLength(30)] public string Status { get; init; } = "Open";
}

/// <summary>Opportunity returned by the API.</summary>
public sealed record OpportunityResponse(int OpportunityId, string OpportunityName, int CustomerId, string CustomerName, int? LeadId, string? LeadName, decimal Amount, string Stage, decimal Probability, decimal WeightedValue, DateTimeOffset? ExpectedCloseDate, string Status, string AssignedTo);

/// <summary>Follow-up creation payload.</summary>
public sealed record CreateFollowUpRequest
{
    public int? CustomerId { get; init; }
    public int? LeadId { get; init; }
    public DateTimeOffset FollowUpDate { get; init; }
    [Required, StringLength(50)] public required string FollowUpType { get; init; }
    [StringLength(1000)] public string? Remarks { get; init; }
    [Required, StringLength(30)] public string Status { get; init; } = "Planned";
}

/// <summary>Follow-up update payload.</summary>
public sealed record UpdateFollowUpRequest
{
    public int? CustomerId { get; init; }
    public int? LeadId { get; init; }
    public DateTimeOffset FollowUpDate { get; init; }
    [Required, StringLength(50)] public required string FollowUpType { get; init; }
    [StringLength(1000)] public string? Remarks { get; init; }
    [Required, StringLength(30)] public string Status { get; init; } = "Planned";
}

/// <summary>Follow-up returned by the API.</summary>
public sealed record FollowUpResponse(int FollowUpId, int? CustomerId, string? CustomerName, int? LeadId, string? LeadName, DateTimeOffset FollowUpDate, string FollowUpType, string? Remarks, string Status, string AssignedTo);
