using System.Security.Claims;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using AcxiomCRM.Api;
using AcxiomCRM.Data;
using AcxiomCRM.Models;
using AcxiomCRM.Security;

namespace AcxiomCRM.Services;

public interface ICrmApiService
{
    Task<PagedResponse<CustomerResponse>> GetCustomersAsync(ClaimsPrincipal user, string? search, string? status, int page, int pageSize, CancellationToken ct);
    Task<CustomerResponse?> GetCustomerAsync(ClaimsPrincipal user, int id, CancellationToken ct);
    Task<CustomerResponse> CreateCustomerAsync(ClaimsPrincipal user, CreateCustomerRequest request, string? ipAddress, CancellationToken ct);
    Task<CustomerResponse?> UpdateCustomerAsync(ClaimsPrincipal user, int id, UpdateCustomerRequest request, string? ipAddress, CancellationToken ct);
    Task<bool> DeleteCustomerAsync(ClaimsPrincipal user, int id, string? ipAddress, CancellationToken ct);
    Task<PagedResponse<LeadResponse>> GetLeadsAsync(ClaimsPrincipal user, string? search, string? status, int page, int pageSize, CancellationToken ct);
    Task<LeadResponse?> GetLeadAsync(ClaimsPrincipal user, int id, CancellationToken ct);
    Task<LeadResponse> CreateLeadAsync(ClaimsPrincipal user, CreateLeadRequest request, string? ipAddress, CancellationToken ct);
    Task<LeadResponse?> UpdateLeadAsync(ClaimsPrincipal user, int id, UpdateLeadRequest request, string? ipAddress, CancellationToken ct);
    Task<bool> DeleteLeadAsync(ClaimsPrincipal user, int id, string? ipAddress, CancellationToken ct);
    Task<PagedResponse<OpportunityResponse>> GetOpportunitiesAsync(ClaimsPrincipal user, string? search, string? stage, string? status, int page, int pageSize, CancellationToken ct);
    Task<OpportunityResponse?> GetOpportunityAsync(ClaimsPrincipal user, int id, CancellationToken ct);
    Task<OpportunityResponse> CreateOpportunityAsync(ClaimsPrincipal user, CreateOpportunityRequest request, string? ipAddress, CancellationToken ct);
    Task<OpportunityResponse?> UpdateOpportunityAsync(ClaimsPrincipal user, int id, UpdateOpportunityRequest request, string? ipAddress, CancellationToken ct);
    Task<bool> DeleteOpportunityAsync(ClaimsPrincipal user, int id, string? ipAddress, CancellationToken ct);
    Task<PagedResponse<FollowUpResponse>> GetFollowUpsAsync(ClaimsPrincipal user, string? status, int page, int pageSize, CancellationToken ct);
    Task<FollowUpResponse?> GetFollowUpAsync(ClaimsPrincipal user, int id, CancellationToken ct);
    Task<FollowUpResponse> CreateFollowUpAsync(ClaimsPrincipal user, CreateFollowUpRequest request, string? ipAddress, CancellationToken ct);
    Task<FollowUpResponse?> UpdateFollowUpAsync(ClaimsPrincipal user, int id, UpdateFollowUpRequest request, string? ipAddress, CancellationToken ct);
    Task<bool> DeleteFollowUpAsync(ClaimsPrincipal user, int id, string? ipAddress, CancellationToken ct);
}

public sealed class ApiServiceException(int statusCode, string message) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
}

public sealed class CrmApiService(ApplicationDbContext db) : ICrmApiService
{
    private static readonly string[] LeadStatuses = ["New", "Contacted", "Qualified", "Unqualified", "Converted", "Lost"];
    private static readonly string[] CustomerStatuses = ["Active", "Inactive", "Prospect"];
    private static readonly string[] Stages = ["Qualification", "Proposal", "Negotiation", "Won", "Lost"];
    private static readonly string[] OpportunityStatuses = ["Open", "Won", "Lost"];
    private static readonly string[] FollowUpStatuses = ["Planned", "Completed", "Missed", "Cancelled"];
    private static readonly string[] FollowUpTypes = ["Call", "Meeting", "Email", "Task"];
    private const int DefaultPageSize = 20;

    public async Task<PagedResponse<CustomerResponse>> GetCustomersAsync(ClaimsPrincipal user, string? search, string? status, int page, int pageSize, CancellationToken ct)
    {
        var query = Customers(user).AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search)) { var p = $"%{search.Trim()}%"; query = query.Where(x => EF.Functions.Like(x.CustomerName, p) || EF.Functions.Like(x.Email, p) || EF.Functions.Like(x.Phone!, p) || EF.Functions.Like(x.CompanyName!, p)); }
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(x => x.Status == status);
        var total = await query.CountAsync(ct); var (skip, take, pages, current) = Paging(page, pageSize, total);
        var items = (await query.OrderBy(x => x.CustomerName).Skip(skip).Take(take).ToListAsync(ct)).Select(ToCustomerResponse).ToList();
        return new(items, current, take, total, pages);
    }

    public async Task<CustomerResponse?> GetCustomerAsync(ClaimsPrincipal user, int id, CancellationToken ct) { var entity = await Customers(user).AsNoTracking().SingleOrDefaultAsync(x => x.CustomerId == id, ct); return entity is null ? null : ToCustomerResponse(entity); }

    public async Task<CustomerResponse> CreateCustomerAsync(ClaimsPrincipal user, CreateCustomerRequest request, string? ipAddress, CancellationToken ct)
    {
        ValidateCustomer(request.CustomerName, request.Status);
        var code = request.CustomerCode.Trim(); var email = request.Email.Trim();
        if (await db.Customers.AnyAsync(x => EF.Functions.Collate(x.CustomerCode, "NOCASE") == code || EF.Functions.Collate(x.Email, "NOCASE") == email, ct)) throw Conflict("Customer code or email already exists.");
        var entity = new Customer { CustomerCode = code, CustomerName = request.CustomerName.Trim(), Email = email, Phone = Normalize(request.Phone), CompanyName = Normalize(request.CompanyName), Address = Normalize(request.Address), City = Normalize(request.City), State = Normalize(request.State), Status = Canonical(request.Status, CustomerStatuses), CreatedBy = UserId(user), CreatedDate = DateTime.UtcNow };
        db.Customers.Add(entity); await db.SaveChangesAsync(ct); await AuditAsync(user, "Create", nameof(Customer), entity.CustomerId, null, Serialize(entity), ipAddress, ct); return ToCustomerResponse(entity);
    }

    public async Task<CustomerResponse?> UpdateCustomerAsync(ClaimsPrincipal user, int id, UpdateCustomerRequest request, string? ipAddress, CancellationToken ct)
    {
        var entity = await Customers(user).SingleOrDefaultAsync(x => x.CustomerId == id, ct); if (entity is null) return null;
        ValidateCustomer(request.CustomerName, request.Status); var code = request.CustomerCode.Trim(); var email = request.Email.Trim();
        if (await db.Customers.AnyAsync(x => x.CustomerId != id && (EF.Functions.Collate(x.CustomerCode, "NOCASE") == code || EF.Functions.Collate(x.Email, "NOCASE") == email), ct)) throw Conflict("Customer code or email already exists.");
        var old = Serialize(entity); entity.CustomerCode = code; entity.CustomerName = request.CustomerName.Trim(); entity.Email = email; entity.Phone = Normalize(request.Phone); entity.CompanyName = Normalize(request.CompanyName); entity.Address = Normalize(request.Address); entity.City = Normalize(request.City); entity.State = Normalize(request.State); entity.Status = Canonical(request.Status, CustomerStatuses);
        await db.SaveChangesAsync(ct); await AuditAsync(user, "Update", nameof(Customer), id, old, Serialize(entity), ipAddress, ct); return ToCustomerResponse(entity);
    }

    public async Task<bool> DeleteCustomerAsync(ClaimsPrincipal user, int id, string? ipAddress, CancellationToken ct)
    {
        var entity = await Customers(user).SingleOrDefaultAsync(x => x.CustomerId == id, ct); if (entity is null) return false;
        var old = Serialize(entity); db.Customers.Remove(entity); db.AuditLogs.Add(NewAudit(user, "Delete", nameof(Customer), id, old, null, ipAddress));
        try { await db.SaveChangesAsync(ct); } catch (DbUpdateException) { throw Conflict("Customer cannot be deleted while related records exist."); } return true;
    }

    public async Task<PagedResponse<LeadResponse>> GetLeadsAsync(ClaimsPrincipal user, string? search, string? status, int page, int pageSize, CancellationToken ct)
    {
        var query = Leads(user).AsNoTracking(); if (!string.IsNullOrWhiteSpace(search)) { var p = $"%{search.Trim()}%"; query = query.Where(x => EF.Functions.Like(x.LeadName, p) || EF.Functions.Like(x.Email, p) || EF.Functions.Like(x.Phone!, p) || EF.Functions.Like(x.CompanyName!, p)); } if (!string.IsNullOrWhiteSpace(status)) query = query.Where(x => x.Status == status);
        var total = await query.CountAsync(ct); var (skip, take, pages, current) = Paging(page, pageSize, total); var items = (await query.OrderByDescending(x => x.CreatedDate).Skip(skip).Take(take).ToListAsync(ct)).Select(ToLeadResponse).ToList(); return new(items, current, take, total, pages);
    }
    public async Task<LeadResponse?> GetLeadAsync(ClaimsPrincipal user, int id, CancellationToken ct) { var entity = await Leads(user).AsNoTracking().SingleOrDefaultAsync(x => x.LeadId == id, ct); return entity is null ? null : ToLeadResponse(entity); }
    public async Task<LeadResponse> CreateLeadAsync(ClaimsPrincipal user, CreateLeadRequest request, string? ipAddress, CancellationToken ct)
    {
        ValidateLead(request.Status); if (await db.Leads.AnyAsync(x => EF.Functions.Collate(x.LeadCode, "NOCASE") == request.LeadCode.Trim() || EF.Functions.Collate(x.Email, "NOCASE") == request.Email.Trim(), ct)) throw Conflict("Lead code or email already exists.");
        var entity = new Lead { LeadCode = request.LeadCode.Trim(), LeadName = request.LeadName.Trim(), Email = request.Email.Trim(), Phone = Normalize(request.Phone), CompanyName = Normalize(request.CompanyName), Source = request.Source.Trim(), Status = Canonical(request.Status, LeadStatuses), ExpectedValue = request.ExpectedValue, AssignedTo = UserId(user), CreatedDate = DateTime.UtcNow }; db.Leads.Add(entity); await db.SaveChangesAsync(ct); await AuditAsync(user, "Create", nameof(Lead), entity.LeadId, null, Serialize(entity), ipAddress, ct); return ToLeadResponse(entity);
    }
    public async Task<LeadResponse?> UpdateLeadAsync(ClaimsPrincipal user, int id, UpdateLeadRequest request, string? ipAddress, CancellationToken ct)
    {
        var entity = await Leads(user).SingleOrDefaultAsync(x => x.LeadId == id, ct); if (entity is null) return null; ValidateLead(request.Status); var newStatus = Canonical(request.Status, LeadStatuses); if (entity.Status == "Converted" && newStatus != "Converted") throw Conflict("A converted lead cannot be reopened."); if (await db.Leads.AnyAsync(x => x.LeadId != id && (EF.Functions.Collate(x.LeadCode, "NOCASE") == request.LeadCode.Trim() || EF.Functions.Collate(x.Email, "NOCASE") == request.Email.Trim()), ct)) throw Conflict("Lead code or email already exists.");
        var old = Serialize(entity); var oldStatus = entity.Status; entity.LeadCode = request.LeadCode.Trim(); entity.LeadName = request.LeadName.Trim(); entity.Email = request.Email.Trim(); entity.Phone = Normalize(request.Phone); entity.CompanyName = Normalize(request.CompanyName); entity.Source = request.Source.Trim(); entity.Status = newStatus; entity.ExpectedValue = request.ExpectedValue; await db.SaveChangesAsync(ct); await AuditAsync(user, oldStatus == entity.Status ? "Update" : "StatusChange", nameof(Lead), id, old, Serialize(entity), ipAddress, ct); return ToLeadResponse(entity);
    }
    public async Task<bool> DeleteLeadAsync(ClaimsPrincipal user, int id, string? ipAddress, CancellationToken ct)
    { var entity = await Leads(user).SingleOrDefaultAsync(x => x.LeadId == id, ct); if (entity is null) return false; db.Leads.Remove(entity); db.AuditLogs.Add(NewAudit(user, "Delete", nameof(Lead), id, Serialize(entity), null, ipAddress)); try { await db.SaveChangesAsync(ct); } catch (DbUpdateException) { throw Conflict("Lead cannot be deleted while related records exist."); } return true; }

    public async Task<PagedResponse<OpportunityResponse>> GetOpportunitiesAsync(ClaimsPrincipal user, string? search, string? stage, string? status, int page, int pageSize, CancellationToken ct)
    {
        var query = Opportunities(user).AsNoTracking(); if (!string.IsNullOrWhiteSpace(search)) { var p = $"%{search.Trim()}%"; query = query.Where(x => EF.Functions.Like(x.OpportunityName, p) || EF.Functions.Like(x.Customer.CustomerName, p)); } if (!string.IsNullOrWhiteSpace(stage)) query = query.Where(x => x.Stage == stage); if (!string.IsNullOrWhiteSpace(status)) query = query.Where(x => x.Status == status);
        var total = await query.CountAsync(ct); var (skip, take, pages, current) = Paging(page, pageSize, total); var items = await query.Include(x => x.Customer).Include(x => x.Lead).OrderByDescending(x => x.ExpectedCloseDate).Skip(skip).Take(take).ToListAsync(ct); return new(items.Select(ToOpportunityResponse).ToList(), current, take, total, pages);
    }
    public async Task<OpportunityResponse?> GetOpportunityAsync(ClaimsPrincipal user, int id, CancellationToken ct) { var x = await Opportunities(user).AsNoTracking().Include(x => x.Customer).Include(x => x.Lead).SingleOrDefaultAsync(x => x.OpportunityId == id, ct); return x is null ? null : ToOpportunityResponse(x); }
    public async Task<OpportunityResponse> CreateOpportunityAsync(ClaimsPrincipal user, CreateOpportunityRequest request, string? ipAddress, CancellationToken ct)
    { var entity = new Opportunity(); await ApplyOpportunityAsync(entity, request, ct); entity.AssignedTo = UserId(user); db.Opportunities.Add(entity); await db.SaveChangesAsync(ct); await AuditAsync(user, "Create", nameof(Opportunity), entity.OpportunityId, null, Serialize(entity), ipAddress, ct); return await GetOpportunityAsync(user, entity.OpportunityId, ct) ?? throw new InvalidOperationException(); }
    public async Task<OpportunityResponse?> UpdateOpportunityAsync(ClaimsPrincipal user, int id, UpdateOpportunityRequest request, string? ipAddress, CancellationToken ct)
    { var entity = await Opportunities(user).SingleOrDefaultAsync(x => x.OpportunityId == id, ct); if (entity is null) return null; var old = Serialize(entity); await ApplyOpportunityAsync(entity, request, ct); await db.SaveChangesAsync(ct); await AuditAsync(user, entity.Stage == request.Stage ? "Update" : "StageChange", nameof(Opportunity), id, old, Serialize(entity), ipAddress, ct); return await GetOpportunityAsync(user, id, ct); }
    public async Task<bool> DeleteOpportunityAsync(ClaimsPrincipal user, int id, string? ipAddress, CancellationToken ct) { var entity = await Opportunities(user).SingleOrDefaultAsync(x => x.OpportunityId == id, ct); if (entity is null) return false; db.Opportunities.Remove(entity); db.AuditLogs.Add(NewAudit(user, "Delete", nameof(Opportunity), id, Serialize(entity), null, ipAddress)); try { await db.SaveChangesAsync(ct); } catch (DbUpdateException) { throw Conflict("Opportunity cannot be deleted while related records exist."); } return true; }

    public async Task<PagedResponse<FollowUpResponse>> GetFollowUpsAsync(ClaimsPrincipal user, string? status, int page, int pageSize, CancellationToken ct)
    { var query = FollowUps(user).AsNoTracking(); if (!string.IsNullOrWhiteSpace(status)) query = query.Where(x => x.Status == status); var total = await query.CountAsync(ct); var (skip, take, pages, current) = Paging(page, pageSize, total); var items = await query.Include(x => x.Customer).Include(x => x.Lead).OrderBy(x => x.FollowUpDate).Skip(skip).Take(take).ToListAsync(ct); return new(items.Select(ToFollowUpResponse).ToList(), current, take, total, pages); }
    public async Task<FollowUpResponse?> GetFollowUpAsync(ClaimsPrincipal user, int id, CancellationToken ct) { var x = await FollowUps(user).AsNoTracking().Include(x => x.Customer).Include(x => x.Lead).SingleOrDefaultAsync(x => x.FollowUpId == id, ct); return x is null ? null : ToFollowUpResponse(x); }
    public async Task<FollowUpResponse> CreateFollowUpAsync(ClaimsPrincipal user, CreateFollowUpRequest request, string? ipAddress, CancellationToken ct) { var entity = new FollowUp(); await ApplyFollowUpAsync(entity, request, ct); entity.AssignedTo = UserId(user); db.FollowUps.Add(entity); await db.SaveChangesAsync(ct); await AuditAsync(user, "Create", nameof(FollowUp), entity.FollowUpId, null, Serialize(entity), ipAddress, ct); return await GetFollowUpAsync(user, entity.FollowUpId, ct) ?? throw new InvalidOperationException(); }
    public async Task<FollowUpResponse?> UpdateFollowUpAsync(ClaimsPrincipal user, int id, UpdateFollowUpRequest request, string? ipAddress, CancellationToken ct) { var entity = await FollowUps(user).SingleOrDefaultAsync(x => x.FollowUpId == id, ct); if (entity is null) return null; var old = Serialize(entity); var oldDate = entity.FollowUpDate; var oldStatus = entity.Status; await ApplyFollowUpAsync(entity, request, ct); await db.SaveChangesAsync(ct); var action = oldDate != entity.FollowUpDate ? "Reschedule" : oldStatus != entity.Status ? entity.Status : "Update"; await AuditAsync(user, action, nameof(FollowUp), id, old, Serialize(entity), ipAddress, ct); return await GetFollowUpAsync(user, id, ct); }
    public async Task<bool> DeleteFollowUpAsync(ClaimsPrincipal user, int id, string? ipAddress, CancellationToken ct) { var entity = await FollowUps(user).SingleOrDefaultAsync(x => x.FollowUpId == id, ct); if (entity is null) return false; db.FollowUps.Remove(entity); db.AuditLogs.Add(NewAudit(user, "Delete", nameof(FollowUp), id, Serialize(entity), null, ipAddress)); try { await db.SaveChangesAsync(ct); } catch (DbUpdateException) { throw Conflict("Follow-up could not be deleted."); } return true; }

    private async Task ApplyOpportunityAsync(Opportunity entity, CreateOpportunityRequest request, CancellationToken ct)
    { if (!Stages.Contains(request.Stage, StringComparer.OrdinalIgnoreCase)) throw Bad("Invalid opportunity stage."); if (!OpportunityStatuses.Contains(request.Status, StringComparer.OrdinalIgnoreCase)) throw Bad("Invalid opportunity status."); var stage = Canonical(request.Stage, Stages); var status = Canonical(request.Status, OpportunityStatuses); if (request.Amount <= 0 && status == "Open") throw Bad("Active opportunities require an amount greater than zero."); if (request.ExpectedCloseDate.HasValue && request.ExpectedCloseDate.Value.UtcDateTime.Date < DateTime.UtcNow.Date && status == "Open") throw Bad("Active opportunity close dates cannot be in the past."); var customer = await db.Customers.FindAsync([request.CustomerId], ct); if (customer is null) throw Bad("Customer does not exist."); if (request.LeadId.HasValue && !await db.Leads.AnyAsync(x => x.LeadId == request.LeadId.Value, ct)) throw Bad("Lead does not exist."); entity.OpportunityName = request.OpportunityName.Trim(); entity.CustomerId = request.CustomerId; entity.LeadId = request.LeadId; entity.Amount = request.Amount; entity.Stage = stage; entity.Probability = request.Probability; entity.ExpectedCloseDate = request.ExpectedCloseDate?.UtcDateTime; entity.Status = status; }
    private async Task ApplyFollowUpAsync(FollowUp entity, CreateFollowUpRequest request, CancellationToken ct)
    { if (request.CustomerId is null && request.LeadId is null) throw Bad("A follow-up must be linked to a customer or lead."); if (!FollowUpStatuses.Contains(request.Status, StringComparer.OrdinalIgnoreCase)) throw Bad("Invalid follow-up status."); if (!FollowUpTypes.Contains(request.FollowUpType, StringComparer.OrdinalIgnoreCase)) throw Bad("Invalid follow-up type."); var status = Canonical(request.Status, FollowUpStatuses); if (status == "Planned" && request.FollowUpDate.UtcDateTime < DateTime.UtcNow) throw Bad("Planned follow-up dates cannot be in the past."); if (request.CustomerId.HasValue && !await db.Customers.AnyAsync(x => x.CustomerId == request.CustomerId.Value, ct)) throw Bad("Customer does not exist."); if (request.LeadId.HasValue && !await db.Leads.AnyAsync(x => x.LeadId == request.LeadId.Value, ct)) throw Bad("Lead does not exist."); entity.CustomerId = request.CustomerId; entity.LeadId = request.LeadId; entity.FollowUpDate = request.FollowUpDate.UtcDateTime; entity.FollowUpType = Canonical(request.FollowUpType, FollowUpTypes); entity.Remarks = Normalize(request.Remarks); entity.Status = status; }
    private Task ApplyOpportunityAsync(Opportunity entity, UpdateOpportunityRequest request, CancellationToken ct) => ApplyOpportunityAsync(entity, new CreateOpportunityRequest { OpportunityName = request.OpportunityName, CustomerId = request.CustomerId, LeadId = request.LeadId, Amount = request.Amount, Stage = request.Stage, Probability = request.Probability, ExpectedCloseDate = request.ExpectedCloseDate, Status = request.Status }, ct);
    private Task ApplyFollowUpAsync(FollowUp entity, UpdateFollowUpRequest request, CancellationToken ct) => ApplyFollowUpAsync(entity, new CreateFollowUpRequest { CustomerId = request.CustomerId, LeadId = request.LeadId, FollowUpDate = request.FollowUpDate, FollowUpType = request.FollowUpType, Remarks = request.Remarks, Status = request.Status }, ct);
    private void ValidateCustomer(string name, string status) { if (string.IsNullOrWhiteSpace(name)) throw Bad("Customer name is required."); if (!CustomerStatuses.Contains(status, StringComparer.OrdinalIgnoreCase)) throw Bad("Invalid customer status."); }
    private void ValidateLead(string status) { if (!LeadStatuses.Contains(status, StringComparer.OrdinalIgnoreCase)) throw Bad("Invalid lead status."); }
    private static string Canonical(string value, IReadOnlyList<string> allowed) => allowed.First(item => string.Equals(item, value, StringComparison.OrdinalIgnoreCase));
    private IQueryable<Customer> Customers(ClaimsPrincipal user) => IsBroad(user) ? db.Customers : db.Customers.Where(x => x.CreatedBy == UserId(user));
    private IQueryable<Lead> Leads(ClaimsPrincipal user) => IsBroad(user) ? db.Leads : db.Leads.Where(x => x.AssignedTo == UserId(user));
    private IQueryable<Opportunity> Opportunities(ClaimsPrincipal user) => IsBroad(user) ? db.Opportunities : db.Opportunities.Where(x => x.AssignedTo == UserId(user));
    private IQueryable<FollowUp> FollowUps(ClaimsPrincipal user) => IsBroad(user) ? db.FollowUps : db.FollowUps.Where(x => x.AssignedTo == UserId(user));
    private static bool IsBroad(ClaimsPrincipal user) => user.IsInRole(RoleNames.Admin) || user.IsInRole(RoleNames.Manager);
    private static string UserId(ClaimsPrincipal user) => user.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new ApiServiceException(401, "Authentication is required.");
    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static ApiServiceException Bad(string message) => new(400, message); private static ApiServiceException Conflict(string message) => new(409, message);
    private static (int Skip, int Take, int Pages, int Page) Paging(int page, int size, int total) { var take = Math.Clamp(size <= 0 ? DefaultPageSize : size, 1, 100); var pages = Math.Max(1, (int)Math.Ceiling(total / (double)take)); var current = Math.Clamp(page, 1, pages); return ((current - 1) * take, take, pages, current); }
    private static CustomerResponse ToCustomerResponse(Customer x) => new(x.CustomerId, x.CustomerCode, x.CustomerName, x.Email, x.Phone, x.CompanyName, x.Address, x.City, x.State, x.Status, new DateTimeOffset(DateTime.SpecifyKind(x.CreatedDate, DateTimeKind.Utc)), x.CreatedBy);
    private static LeadResponse ToLeadResponse(Lead x) => new(x.LeadId, x.LeadCode, x.LeadName, x.Email, x.Phone, x.CompanyName, x.Source, x.Status, x.ExpectedValue, new DateTimeOffset(DateTime.SpecifyKind(x.CreatedDate, DateTimeKind.Utc)), x.AssignedTo ?? string.Empty);
    private static OpportunityResponse ToOpportunityResponse(Opportunity x) => new(x.OpportunityId, x.OpportunityName, x.CustomerId, x.Customer.CustomerName, x.LeadId, x.Lead?.LeadName, x.Amount, x.Stage, x.Probability, x.Amount * x.Probability / 100m, x.ExpectedCloseDate.HasValue ? new DateTimeOffset(DateTime.SpecifyKind(x.ExpectedCloseDate.Value, DateTimeKind.Utc)) : null, x.Status, x.AssignedTo ?? string.Empty);
    private static FollowUpResponse ToFollowUpResponse(FollowUp x) => new(x.FollowUpId, x.CustomerId, x.Customer?.CustomerName, x.LeadId, x.Lead?.LeadName, new DateTimeOffset(DateTime.SpecifyKind(x.FollowUpDate, DateTimeKind.Utc)), x.FollowUpType, x.Remarks, x.Status, x.AssignedTo ?? string.Empty);
    private static string Serialize(object value) => JsonSerializer.Serialize(value);
    private async Task AuditAsync(ClaimsPrincipal user, string action, string entityName, int id, string? oldValue, string? newValue, string? ipAddress, CancellationToken ct) { db.AuditLogs.Add(NewAudit(user, action, entityName, id, oldValue, newValue, ipAddress)); await db.SaveChangesAsync(ct); }
    private static AuditLog NewAudit(ClaimsPrincipal user, string action, string entity, int id, string? oldValue, string? newValue, string? ip) => new() { UserId = UserId(user), Action = action, EntityName = entity, RecordId = id.ToString(), OldValue = oldValue, NewValue = newValue, CreatedDate = DateTime.UtcNow, IpAddress = ip };
}
