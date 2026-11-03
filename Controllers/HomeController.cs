using DiagnosticsActivity = System.Diagnostics.Activity;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AcxiomCRM.Data;
using AcxiomCRM.Models;
using AcxiomCRM.Security;
using AcxiomCRM.Services;
using AcxiomCRM.ViewModels;

namespace AcxiomCRM.Controllers;

public class HomeController : Controller
{
    private static readonly string[] LeadStatuses = ["New", "Contacted", "Qualified", "Unqualified", "Converted", "Lost"];
    private static readonly string[] OpportunityStages = ["Qualification", "Proposal", "Negotiation", "Won", "Lost"];
    private readonly ApplicationDbContext _db;
    private readonly UserManager<IdentityUser> _userManager;
    private readonly IApplicationInfoService _applicationInfoService;

    public HomeController(ApplicationDbContext db, UserManager<IdentityUser> userManager, IApplicationInfoService applicationInfoService)
    {
        _db = db;
        _userManager = userManager;
        _applicationInfoService = applicationInfoService;
    }

    [Authorize(Policy = AuthorizationPolicies.CrmAccess)]
    public async Task<IActionResult> Index(DashboardDateFilter filter = DashboardDateFilter.Last30Days, DateTime? fromDate = null, DateTime? toDate = null)
    {
        var range = ResolveRange(filter, fromDate, toDate);
        if (range.Error is not null)
            return View(new HomeViewModel { ApplicationName = _applicationInfoService.GetApplicationName(), EnvironmentName = _applicationInfoService.GetEnvironmentName(), DateFilter = filter, FromDate = fromDate, ToDate = toDate, ValidationError = range.Error });

        var customerQuery = AccessibleCustomers().Where(item => item.CreatedDate >= range.From && item.CreatedDate < range.ToExclusive);
        var leadQuery = AccessibleLeads().Where(item => item.CreatedDate >= range.From && item.CreatedDate < range.ToExclusive);
        var opportunityQuery = AccessibleOpportunities().Where(item => item.ExpectedCloseDate >= range.From && item.ExpectedCloseDate < range.ToExclusive);
        var openLeadStatuses = new[] { "New", "Contacted", "Qualified" };
        var totalOpportunities = await opportunityQuery.CountAsync();
        var openOpportunities = await opportunityQuery.CountAsync(item => item.Status == "Open");
        var wonOpportunities = await opportunityQuery.CountAsync(item => item.Status == "Won" || item.Stage == "Won");
        var lostOpportunities = await opportunityQuery.CountAsync(item => item.Status == "Lost" || item.Stage == "Lost");
        var totalPipeline = await opportunityQuery.Where(item => item.Status != "Won" && item.Status != "Lost" && item.Stage != "Won" && item.Stage != "Lost").SumAsync(item => (decimal?)item.Amount) ?? 0;
        var weightedPipeline = await opportunityQuery.Where(item => item.Status == "Open" && item.Stage != "Won" && item.Stage != "Lost").SumAsync(item => (decimal?)item.Amount * item.Probability / 100m) ?? 0;
        return View(new HomeViewModel
        {
            ApplicationName = _applicationInfoService.GetApplicationName(), EnvironmentName = _applicationInfoService.GetEnvironmentName(), DateFilter = filter,
            FromDate = range.From, ToDate = range.ToInclusive,
            Kpis = new DashboardKpiViewModel { TotalCustomers = await customerQuery.CountAsync(), TotalLeads = await leadQuery.CountAsync(), OpenLeads = await leadQuery.CountAsync(item => openLeadStatuses.Contains(item.Status)), TotalOpportunities = totalOpportunities, OpenOpportunities = openOpportunities, WonOpportunities = wonOpportunities, LostOpportunities = lostOpportunities, TotalPipelineValue = totalPipeline, WeightedPipelineValue = weightedPipeline },
            LeadStatusChart = new DashboardChartViewModel { Labels = LeadStatuses, Values = await ChartCountsAsync(leadQuery, LeadStatuses, (query, label) => query.CountAsync(item => item.Status == label)) },
            OpportunityStageChart = new DashboardChartViewModel { Labels = OpportunityStages, Values = await ChartCountsAsync(opportunityQuery, OpportunityStages, (query, label) => query.CountAsync(item => item.Stage == label)) },
            MonthlySalesChart = await BuildMonthlySalesAsync(opportunityQuery, range.From, range.ToExclusive.AddDays(-1))
        });
    }

    public IActionResult Privacy() => View();

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error() => View(new ErrorViewModel { RequestId = DiagnosticsActivity.Current?.Id ?? HttpContext.TraceIdentifier });

    private IQueryable<Customer> AccessibleCustomers() => User.IsInRole(RoleNames.Admin) || User.IsInRole(RoleNames.Manager) ? _db.Customers : _db.Customers.Where(item => item.CreatedBy == CurrentUserId);
    private IQueryable<Lead> AccessibleLeads() => User.IsInRole(RoleNames.Admin) || User.IsInRole(RoleNames.Manager) ? _db.Leads : _db.Leads.Where(item => item.AssignedTo == CurrentUserId);
    private IQueryable<Opportunity> AccessibleOpportunities() => User.IsInRole(RoleNames.Admin) || User.IsInRole(RoleNames.Manager) ? _db.Opportunities : _db.Opportunities.Where(item => item.AssignedTo == CurrentUserId);
    private string CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

    private static async Task<IReadOnlyList<decimal>> ChartCountsAsync<T>(IQueryable<T> query, IReadOnlyList<string> labels, Func<IQueryable<T>, string, Task<int>> count)
    {
        var values = new List<decimal>(labels.Count);
        foreach (var label in labels) values.Add(await count(query, label));
        return values;
    }

    private static async Task<DashboardChartViewModel> BuildMonthlySalesAsync(IQueryable<Opportunity> query, DateTime from, DateTime to)
    {
        var won = await query.Where(item => item.Status == "Won" || item.Stage == "Won").Where(item => item.ExpectedCloseDate.HasValue)
            .GroupBy(item => new { item.ExpectedCloseDate!.Value.Year, item.ExpectedCloseDate!.Value.Month }).Select(group => new { group.Key.Year, group.Key.Month, Amount = group.Sum(item => item.Amount) }).ToListAsync();
        var labels = new List<string>(); var values = new List<decimal>();
        for (var month = new DateTime(from.Year, from.Month, 1); month <= new DateTime(to.Year, to.Month, 1); month = month.AddMonths(1))
        { labels.Add(month.ToString("MMM yyyy")); values.Add(won.FirstOrDefault(item => item.Year == month.Year && item.Month == month.Month)?.Amount ?? 0); }
        return new DashboardChartViewModel { Labels = labels, Values = values };
    }

    private static DashboardDateRange ResolveRange(DashboardDateFilter filter, DateTime? customFrom, DateTime? customTo)
    {
        var today = DateTime.Today;
        return filter switch
        {
            DashboardDateFilter.Today => new(today, today.AddDays(1), today, today, null),
            DashboardDateFilter.Last7Days => new(today.AddDays(-6), today.AddDays(1), today.AddDays(-6), today, null),
            DashboardDateFilter.Last30Days => new(today.AddDays(-29), today.AddDays(1), today.AddDays(-29), today, null),
            DashboardDateFilter.ThisMonth => new(new DateTime(today.Year, today.Month, 1), new DateTime(today.Year, today.Month, 1).AddMonths(1), new DateTime(today.Year, today.Month, 1), today, null),
            DashboardDateFilter.ThisYear => new(new DateTime(today.Year, 1, 1), new DateTime(today.Year + 1, 1, 1), new DateTime(today.Year, 1, 1), today, null),
            DashboardDateFilter.Custom when !customFrom.HasValue || !customTo.HasValue => new(today, today.AddDays(1), customFrom, customTo, "Both custom dates are required."),
            DashboardDateFilter.Custom when customFrom.Value.Date > customTo.Value.Date => new(today, today.AddDays(1), customFrom, customTo, "The custom start date cannot be after the end date."),
            DashboardDateFilter.Custom => new(customFrom.Value.Date, customTo.Value.Date.AddDays(1), customFrom.Value.Date, customTo.Value.Date, null),
            _ => new(today.AddDays(-29), today.AddDays(1), today.AddDays(-29), today, null)
        };
    }

    private sealed record DashboardDateRange(DateTime From, DateTime ToExclusive, DateTime? FromInclusive, DateTime? ToInclusive, string? Error)
    {
        public DateTime? FromDate => FromInclusive;
    }
}
