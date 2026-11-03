namespace AcxiomCRM.ViewModels;

public sealed class HomeViewModel
{
    public string ApplicationName { get; init; } = string.Empty;

    public string EnvironmentName { get; init; } = string.Empty;

    public DashboardDateFilter DateFilter { get; init; } = DashboardDateFilter.Last30Days;
    public DateTime? FromDate { get; init; }
    public DateTime? ToDate { get; init; }
    public string? ValidationError { get; init; }
    public DashboardKpiViewModel Kpis { get; init; } = new();
    public DashboardChartViewModel LeadStatusChart { get; init; } = new();
    public DashboardChartViewModel OpportunityStageChart { get; init; } = new();
    public DashboardChartViewModel MonthlySalesChart { get; init; } = new();
}

public enum DashboardDateFilter { Today, Last7Days, Last30Days, ThisMonth, ThisYear, Custom }

public sealed class DashboardKpiViewModel
{
    public int TotalCustomers { get; init; }
    public int TotalLeads { get; init; }
    public int OpenLeads { get; init; }
    public int TotalOpportunities { get; init; }
    public int OpenOpportunities { get; init; }
    public int WonOpportunities { get; init; }
    public int LostOpportunities { get; init; }
    public decimal TotalPipelineValue { get; init; }
    public decimal WeightedPipelineValue { get; init; }
}

public sealed class DashboardChartViewModel
{
    public IReadOnlyList<string> Labels { get; init; } = [];
    public IReadOnlyList<decimal> Values { get; init; } = [];
}
