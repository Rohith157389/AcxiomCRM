namespace AcxiomCRM.ViewModels;

public sealed class CustomerListViewModel
{
    public IReadOnlyList<CustomerListItemViewModel> Customers { get; init; } = [];
    public string? Search { get; init; }
    public string? Status { get; init; }
    public string? OwnerId { get; init; }
    public IReadOnlyList<CustomerOwnerViewModel> Owners { get; init; } = [];
    public IReadOnlyList<string> Statuses { get; init; } = [];
    public int Page { get; init; }
    public int TotalPages { get; init; }
    public bool CanFilterByOwner { get; init; }
}

public sealed class CustomerOwnerViewModel
{
    public string Id { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
}
