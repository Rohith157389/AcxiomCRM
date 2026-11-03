namespace AcxiomCRM.ViewModels;

public sealed class ReportsViewModel
{
    public string ReportType { get; init; } = "Customer";
    public string? Search { get; init; }
    public string? Status { get; init; }
    public DateTime? FromDate { get; init; }
    public DateTime? ToDate { get; init; }
    public string Sort { get; init; } = "date_desc";
    public int Page { get; init; } = 1;
    public int TotalPages { get; init; } = 1;
    public bool IsAdmin { get; init; }
    public IReadOnlyList<string> ReportTypes { get; init; } = [];
    public IReadOnlyList<string> StatusOptions { get; init; } = [];
    public IReadOnlyList<string> Columns { get; init; } = [];
    public IReadOnlyList<ReportRowViewModel> Rows { get; init; } = [];
}

public sealed class ReportRowViewModel
{
    public IReadOnlyList<string> Values { get; init; } = [];
}
