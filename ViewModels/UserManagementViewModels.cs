using System.ComponentModel.DataAnnotations;

namespace AcxiomCRM.ViewModels;

public sealed class UserListViewModel
{
    public IReadOnlyList<UserListItemViewModel> Users { get; init; } = [];
    public string? Search { get; init; }
    public int Page { get; init; }
    public int TotalPages { get; init; }
}

public sealed class UserListItemViewModel
{
    public string Id { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string UserName { get; init; } = string.Empty;
    public string Role { get; init; } = string.Empty;
    public bool IsActive { get; init; }
}

public sealed class UserFormViewModel
{
    public string? Id { get; set; }
    [Required, EmailAddress, StringLength(256)] public string Email { get; set; } = string.Empty;
    [Required, StringLength(256)] public string UserName { get; set; } = string.Empty;
    [Required, StringLength(30)] public string Role { get; set; } = "SalesExecutive";
    [DataType(DataType.Password), StringLength(100, MinimumLength = 8)] public string? Password { get; set; }
    public bool IsActive { get; set; } = true;
    public string? CurrentRole { get; set; }
    public bool IsDetailsOnly { get; set; }
}

public sealed class AuditLogListViewModel
{
    public IReadOnlyList<AuditLogListItemViewModel> AuditLogs { get; init; } = [];
    public string? Search { get; init; }
    public string? Action { get; init; }
    public string? EntityName { get; init; }
    public DateTime? FromDate { get; init; }
    public DateTime? ToDate { get; init; }
    public IReadOnlyList<string> Actions { get; init; } = [];
    public IReadOnlyList<string> Entities { get; init; } = [];
    public int Page { get; init; }
    public int TotalPages { get; init; }
}

public sealed class AuditLogListItemViewModel
{
    public int AuditLogId { get; init; }
    public string UserName { get; init; } = string.Empty;
    public string Action { get; init; } = string.Empty;
    public string EntityName { get; init; } = string.Empty;
    public string RecordId { get; init; } = string.Empty;
    public string? OldValue { get; init; }
    public string? NewValue { get; init; }
    public DateTime CreatedDate { get; init; }
    public string? IpAddress { get; init; }
}
