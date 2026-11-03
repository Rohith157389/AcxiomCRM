using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AcxiomCRM.Data;
using AcxiomCRM.Models;
using AcxiomCRM.Security;
using AcxiomCRM.ViewModels;

namespace AcxiomCRM.Controllers;

[Authorize(Policy = AuthorizationPolicies.CrmAccess)]
public class CrmController : Controller
{
    private static readonly string[] CustomerStatuses = ["Active", "Inactive", "Prospect"];
    private static readonly string[] LeadStatuses = ["New", "Contacted", "Qualified", "Unqualified", "Converted", "Lost"];
    private static readonly string[] OpportunityStages = ["Qualification", "Proposal", "Negotiation", "Won", "Lost"];
    private static readonly string[] OpportunityStatuses = ["Open", "Won", "Lost"];
    private static readonly string[] FollowUpStatuses = ["Planned", "Completed", "Missed", "Cancelled"];
    private static readonly string[] FollowUpTypes = ["Call", "Meeting", "Email", "Task"];
    private const int PageSize = 10;

    private readonly ApplicationDbContext _db;
    private readonly UserManager<IdentityUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;

    public CrmController(ApplicationDbContext db, UserManager<IdentityUser> userManager, RoleManager<IdentityRole> roleManager)
    {
        _db = db;
        _userManager = userManager;
        _roleManager = roleManager;
    }

    [HttpGet]
    public async Task<IActionResult> Customers(string? search, string? status, string? ownerId, int page = 1)
    {
        page = Math.Max(1, page);
        var query = AccessibleCustomers();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{search.Trim()}%";
            query = query.Where(customer =>
                EF.Functions.Like(customer.CustomerName, pattern) ||
                EF.Functions.Like(customer.Email, pattern) ||
                (customer.Phone != null && EF.Functions.Like(customer.Phone, pattern)) ||
                (customer.CompanyName != null && EF.Functions.Like(customer.CompanyName, pattern)));
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(customer => customer.Status == status);
        }

        if (CanFilterByOwner && !string.IsNullOrWhiteSpace(ownerId))
        {
            query = query.Where(customer => customer.CreatedBy == ownerId);
        }

        var totalCount = await query.CountAsync();
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)PageSize));
        page = Math.Min(page, totalPages);

        var customers = await query
            .AsNoTracking()
            .OrderByDescending(customer => customer.CreatedDate)
            .ThenBy(customer => customer.CustomerName)
            .Skip((page - 1) * PageSize)
            .Take(PageSize)
            .ToListAsync();

        var ownerIds = customers.Select(customer => customer.CreatedBy).Distinct().ToList();
        var ownerNames = await _userManager.Users
            .Where(user => ownerIds.Contains(user.Id))
            .ToDictionaryAsync(user => user.Id, user => user.Email ?? user.UserName ?? user.Id);

        var owners = CanFilterByOwner
            ? await _userManager.Users
                .Where(user => _db.Customers.Select(customer => customer.CreatedBy).Distinct().Contains(user.Id))
                .OrderBy(user => user.Email)
                .Select(user => new CustomerOwnerViewModel
                {
                    Id = user.Id,
                    DisplayName = user.Email ?? user.UserName ?? user.Id
                })
                .ToListAsync()
            : [];

        var model = new CustomerListViewModel
        {
            Customers = customers.Select(customer => new CustomerListItemViewModel
            {
                CustomerId = customer.CustomerId,
                CustomerCode = customer.CustomerCode,
                CustomerName = customer.CustomerName,
                Email = customer.Email,
                Phone = customer.Phone ?? string.Empty,
                CompanyName = customer.CompanyName ?? string.Empty,
                Status = customer.Status,
                CreatedDate = customer.CreatedDate,
                OwnerName = ownerNames.GetValueOrDefault(customer.CreatedBy, customer.CreatedBy)
            }).ToList(),
            Search = search,
            Status = status,
            OwnerId = CanFilterByOwner ? ownerId : null,
            Owners = owners,
            Statuses = CustomerStatuses,
            Page = page,
            TotalPages = totalPages,
            CanFilterByOwner = CanFilterByOwner
        };

        return View(model);
    }

    [HttpGet]
    public IActionResult Create() => View(new CustomerFormViewModel { Status = CustomerStatuses[0] });

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CustomerFormViewModel model)
    {
        await ValidateUniqueFieldsAsync(model);
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var customer = new Customer
        {
            CustomerCode = model.CustomerCode.Trim(),
            CustomerName = model.CustomerName.Trim(),
            Email = model.Email.Trim(),
            Phone = Normalize(model.Phone),
            CompanyName = Normalize(model.CompanyName),
            Address = Normalize(model.Address),
            City = Normalize(model.City),
            State = Normalize(model.State),
            Status = model.Status,
            CreatedDate = DateTime.UtcNow,
            CreatedBy = CurrentUserId
        };

        _db.Customers.Add(customer);
        try
        {
            await _db.SaveChangesAsync();
            await AddAuditAsync("Create", customer, null, SerializeCustomer(customer));
            TempData["SuccessMessage"] = "Customer created successfully.";
            return RedirectToAction(nameof(Customers));
        }
        catch (DbUpdateException)
        {
            ModelState.AddModelError(string.Empty, "The customer could not be saved. Check for duplicate customer code or email.");
            return View(model);
        }
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var customer = await AccessibleCustomers().AsNoTracking().SingleOrDefaultAsync(item => item.CustomerId == id);
        if (customer is null)
        {
            return NotFound();
        }

        return View(await ToFormModelAsync(customer));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var customer = await AccessibleCustomers().AsNoTracking().SingleOrDefaultAsync(item => item.CustomerId == id);
        if (customer is null)
        {
            return NotFound();
        }

        return View(await ToFormModelAsync(customer));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, CustomerFormViewModel model)
    {
        if (id != model.CustomerId)
        {
            return BadRequest();
        }

        var customer = await AccessibleCustomers().SingleOrDefaultAsync(item => item.CustomerId == id);
        if (customer is null)
        {
            return NotFound();
        }

        await ValidateUniqueFieldsAsync(model, id);
        if (!ModelState.IsValid)
        {
            return View(await ToFormModelAsync(customer, model));
        }

        var oldValue = SerializeCustomer(customer);
        customer.CustomerCode = model.CustomerCode.Trim();
        customer.CustomerName = model.CustomerName.Trim();
        customer.Email = model.Email.Trim();
        customer.Phone = Normalize(model.Phone);
        customer.CompanyName = Normalize(model.CompanyName);
        customer.Address = Normalize(model.Address);
        customer.City = Normalize(model.City);
        customer.State = Normalize(model.State);
        customer.Status = model.Status;

        try
        {
            await _db.SaveChangesAsync();
            await AddAuditAsync("Update", customer, oldValue, SerializeCustomer(customer));
            TempData["SuccessMessage"] = "Customer updated successfully.";
            return RedirectToAction(nameof(Customers));
        }
        catch (DbUpdateException)
        {
            ModelState.AddModelError(string.Empty, "The customer could not be updated. Check for duplicate customer code or email.");
            return View(await ToFormModelAsync(customer, model));
        }
    }

    [HttpGet]
    public async Task<IActionResult> Delete(int id)
    {
        var customer = await AccessibleCustomers().AsNoTracking().SingleOrDefaultAsync(item => item.CustomerId == id);
        if (customer is null)
        {
            return NotFound();
        }

        return View(await ToFormModelAsync(customer));
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var customer = await AccessibleCustomers().SingleOrDefaultAsync(item => item.CustomerId == id);
        if (customer is null)
        {
            return NotFound();
        }

        var oldValue = SerializeCustomer(customer);
        _db.Customers.Remove(customer);
        _db.AuditLogs.Add(new AuditLog
        {
            UserId = CurrentUserId,
            Action = "Delete",
            EntityName = nameof(Customer),
            RecordId = id.ToString(),
            OldValue = oldValue,
            CreatedDate = DateTime.UtcNow,
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString()
        });

        try
        {
            await _db.SaveChangesAsync();
            TempData["SuccessMessage"] = "Customer deleted successfully.";
            return RedirectToAction(nameof(Customers));
        }
        catch (DbUpdateException)
        {
            TempData["ErrorMessage"] = "The customer could not be deleted because it is referenced by another record.";
            return RedirectToAction(nameof(Customers));
        }
    }

    [HttpGet]
    public async Task<IActionResult> Leads(string? search, string? status, string? source, string? assignedTo, int page = 1)
    {
        page = Math.Max(1, page);
        var query = AccessibleLeads();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{search.Trim()}%";
            query = query.Where(lead => EF.Functions.Like(lead.LeadName, pattern) || EF.Functions.Like(lead.Email, pattern) ||
                (lead.Phone != null && EF.Functions.Like(lead.Phone, pattern)) || (lead.CompanyName != null && EF.Functions.Like(lead.CompanyName, pattern)));
        }
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(lead => lead.Status == status);
        if (!string.IsNullOrWhiteSpace(source)) query = query.Where(lead => lead.Source == source);
        if (CanFilterByAssignee && !string.IsNullOrWhiteSpace(assignedTo)) query = query.Where(lead => lead.AssignedTo == assignedTo);

        var totalCount = await query.CountAsync();
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)PageSize));
        page = Math.Min(page, totalPages);
        var leads = await query.AsNoTracking().OrderByDescending(lead => lead.CreatedDate).ThenBy(lead => lead.LeadName)
            .Skip((page - 1) * PageSize).Take(PageSize).ToListAsync();
        var assigneeIds = leads.Where(lead => lead.AssignedTo != null).Select(lead => lead.AssignedTo!).Distinct().ToList();
        var names = await _userManager.Users.Where(user => assigneeIds.Contains(user.Id))
            .ToDictionaryAsync(user => user.Id, user => user.Email ?? user.UserName ?? user.Id);
        var assignees = CanFilterByAssignee ? await GetAssigneesAsync() : [];
        return View(new LeadListViewModel
        {
            Leads = leads.Select(lead => new LeadListItemViewModel { LeadId = lead.LeadId, LeadCode = lead.LeadCode, LeadName = lead.LeadName,
                Email = lead.Email, Phone = lead.Phone, CompanyName = lead.CompanyName, Source = lead.Source, Status = lead.Status,
                ExpectedValue = lead.ExpectedValue, CreatedDate = lead.CreatedDate, AssignedToName = lead.AssignedTo == null ? "Unassigned" : names.GetValueOrDefault(lead.AssignedTo, lead.AssignedTo) }).ToList(),
            Search = search, Status = status, Source = source, AssignedTo = CanFilterByAssignee ? assignedTo : null,
            Statuses = LeadStatuses, Sources = await _db.Leads.Select(lead => lead.Source).Distinct().OrderBy(item => item).ToListAsync(),
            Assignees = assignees, Page = page, TotalPages = totalPages, CanFilterByAssignee = CanFilterByAssignee
        });
    }

    [HttpGet]
    public async Task<IActionResult> CreateLead() => View(new LeadFormViewModel { Status = "New", Assignees = await GetAssigneesAsync() });

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateLead(LeadFormViewModel model)
    {
        ValidateLeadStatus(model.Status);
        var assignedTo = await ResolveAssignedToAsync(model.AssignedTo);
        if (!ModelState.IsValid) { model.Assignees = await GetAssigneesAsync(); return View(model); }
        var lead = new Lead { LeadCode = model.LeadCode.Trim(), LeadName = model.LeadName.Trim(), Email = model.Email.Trim(), Phone = Normalize(model.Phone),
            CompanyName = Normalize(model.CompanyName), Source = model.Source.Trim(), Status = model.Status, ExpectedValue = model.ExpectedValue,
            CreatedDate = DateTime.UtcNow, AssignedTo = assignedTo };
        _db.Leads.Add(lead);
        try { await _db.SaveChangesAsync(); await AddLeadAuditAsync("Create", lead, null, SerializeLead(lead)); TempData["SuccessMessage"] = "Lead created successfully."; return RedirectToAction(nameof(Leads)); }
        catch (DbUpdateException) { ModelState.AddModelError(string.Empty, "The lead could not be saved. Check for a duplicate lead code."); model.Assignees = await GetAssigneesAsync(); return View(model); }
    }

    [HttpGet]
    public async Task<IActionResult> LeadDetails(int id)
    {
        var lead = await AccessibleLeads().AsNoTracking().SingleOrDefaultAsync(item => item.LeadId == id);
        return lead is null ? NotFound() : View(await ToLeadFormModelAsync(lead));
    }

    [HttpGet]
    public async Task<IActionResult> EditLead(int id)
    {
        var lead = await AccessibleLeads().AsNoTracking().SingleOrDefaultAsync(item => item.LeadId == id);
        return lead is null ? NotFound() : View(await ToLeadFormModelAsync(lead));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> EditLead(int id, LeadFormViewModel model)
    {
        if (id != model.LeadId) return BadRequest();
        var lead = await AccessibleLeads().SingleOrDefaultAsync(item => item.LeadId == id);
        if (lead is null) return NotFound();
        ValidateLeadStatus(model.Status);
        if (string.Equals(model.Status, "Converted", StringComparison.OrdinalIgnoreCase) && !string.Equals(lead.Status, "Converted", StringComparison.OrdinalIgnoreCase))
            ModelState.AddModelError(nameof(LeadFormViewModel.Status), "Use the conversion workflow to convert a Qualified lead.");
        if (string.Equals(lead.Status, "Converted", StringComparison.OrdinalIgnoreCase) && !string.Equals(model.Status, lead.Status, StringComparison.OrdinalIgnoreCase))
            ModelState.AddModelError(nameof(LeadFormViewModel.Status), "A converted lead cannot be moved to another status.");
        var assignedTo = await ResolveAssignedToAsync(model.AssignedTo, lead.AssignedTo);
        if (!ModelState.IsValid) { model.Assignees = await GetAssigneesAsync(); return View(model); }
        var oldValue = SerializeLead(lead); var oldStatus = lead.Status;
        lead.LeadCode = model.LeadCode.Trim(); lead.LeadName = model.LeadName.Trim(); lead.Email = model.Email.Trim(); lead.Phone = Normalize(model.Phone);
        lead.CompanyName = Normalize(model.CompanyName); lead.Source = model.Source.Trim(); lead.Status = model.Status; lead.ExpectedValue = model.ExpectedValue; lead.AssignedTo = assignedTo;
        try
        {
            await _db.SaveChangesAsync(); await AddLeadAuditAsync("Update", lead, oldValue, SerializeLead(lead));
            if (!string.Equals(oldStatus, lead.Status, StringComparison.OrdinalIgnoreCase)) await AddLeadAuditAsync("StatusChange", lead, oldValue, SerializeLead(lead));
            TempData["SuccessMessage"] = "Lead updated successfully."; return RedirectToAction(nameof(Leads));
        }
        catch (DbUpdateException) { ModelState.AddModelError(string.Empty, "The lead could not be updated. Check for a duplicate lead code."); model.Assignees = await GetAssigneesAsync(); return View(model); }
    }

    [HttpGet]
    public async Task<IActionResult> DeleteLead(int id)
    {
        var lead = await AccessibleLeads().AsNoTracking().SingleOrDefaultAsync(item => item.LeadId == id);
        return lead is null ? NotFound() : View(await ToLeadFormModelAsync(lead));
    }

    [HttpPost, ActionName("DeleteLead"), ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteLeadConfirmed(int id)
    {
        var lead = await AccessibleLeads().SingleOrDefaultAsync(item => item.LeadId == id);
        if (lead is null) return NotFound();
        var oldValue = SerializeLead(lead); _db.Leads.Remove(lead);
        _db.AuditLogs.Add(NewAudit("Delete", nameof(Lead), id.ToString(), oldValue, null));
        try { await _db.SaveChangesAsync(); TempData["SuccessMessage"] = "Lead deleted successfully."; }
        catch (DbUpdateException) { TempData["ErrorMessage"] = "The lead could not be deleted because it is referenced by another record."; }
        return RedirectToAction(nameof(Leads));
    }

    [HttpGet]
    public async Task<IActionResult> ConvertLead(int id)
    {
        var lead = await AccessibleLeads().AsNoTracking().SingleOrDefaultAsync(item => item.LeadId == id);
        if (lead is null) return NotFound();
        if (lead.Status != "Qualified") { TempData["ErrorMessage"] = "Only Qualified leads can be converted."; return RedirectToAction(nameof(Leads)); }
        return View(await ToLeadFormModelAsync(lead));
    }

    [HttpPost, ActionName("ConvertLead"), ValidateAntiForgeryToken]
    public async Task<IActionResult> ConvertLeadConfirmed(int id)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync();
        var lead = await AccessibleLeads().SingleOrDefaultAsync(item => item.LeadId == id);
        if (lead is null) return NotFound();
        if (lead.Status != "Qualified") { await transaction.RollbackAsync(); TempData["ErrorMessage"] = "This lead is already converted or is not Qualified."; return RedirectToAction(nameof(Leads)); }
        var ownerId = lead.AssignedTo ?? CurrentUserId;
        try
        {
            var customer = new Customer { CustomerCode = await GenerateCustomerCodeAsync(lead), CustomerName = lead.LeadName, Email = lead.Email,
                Phone = lead.Phone, CompanyName = lead.CompanyName, Status = "Active", CreatedDate = DateTime.UtcNow, CreatedBy = ownerId };
            var opportunity = new Opportunity { OpportunityName = $"{lead.LeadName} Opportunity"[..Math.Min(150, $"{lead.LeadName} Opportunity".Length)],
                Customer = customer, LeadId = lead.LeadId, Amount = lead.ExpectedValue, Stage = "Qualification", Probability = 10, ExpectedCloseDate = DateTime.UtcNow.AddDays(30), Status = "Open", AssignedTo = ownerId };
            var oldValue = SerializeLead(lead); lead.Status = "Converted";
            _db.Customers.Add(customer); _db.Opportunities.Add(opportunity); await _db.SaveChangesAsync();
            _db.AuditLogs.Add(NewAudit("Conversion", nameof(Lead), lead.LeadId.ToString(), oldValue, SerializeLead(lead)));
            _db.AuditLogs.Add(NewAudit("Create", nameof(Customer), customer.CustomerId.ToString(), null, JsonSerializer.Serialize(new { customer.CustomerId, customer.CustomerCode, customer.CreatedBy })));
            _db.AuditLogs.Add(NewAudit("Create", nameof(Opportunity), opportunity.OpportunityId.ToString(), null, JsonSerializer.Serialize(new { opportunity.OpportunityId, opportunity.CustomerId, opportunity.LeadId, opportunity.AssignedTo })));
            await _db.SaveChangesAsync(); await transaction.CommitAsync(); TempData["SuccessMessage"] = "Lead converted to a customer and opportunity.";
        }
        catch (DbUpdateException) { await transaction.RollbackAsync(); TempData["ErrorMessage"] = "The lead conversion failed and no records were created."; }
        return RedirectToAction(nameof(Leads));
    }
    [HttpGet]
    public async Task<IActionResult> Opportunities(string? search, string? stage, string? status, string? assignedTo, int page = 1)
    {
        page = Math.Max(1, page);
        var query = AccessibleOpportunities();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{search.Trim()}%";
            query = query.Where(item => EF.Functions.Like(item.OpportunityName, pattern) || EF.Functions.Like(item.Customer.CustomerName, pattern));
        }
        if (!string.IsNullOrWhiteSpace(stage)) query = query.Where(item => item.Stage == stage);
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(item => item.Status == status);
        if (CanFilterByAssignee && !string.IsNullOrWhiteSpace(assignedTo)) query = query.Where(item => item.AssignedTo == assignedTo);

        var totalAmount = await query.Where(item => item.Status != "Won" && item.Status != "Lost").SumAsync(item => (decimal?)item.Amount) ?? 0;
        var weightedPipeline = await query.Where(item => item.Status != "Won" && item.Status != "Lost").SumAsync(item => (decimal?)(item.Amount * item.Probability / 100m)) ?? 0;
        var totalCount = await query.CountAsync();
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)PageSize)); page = Math.Min(page, totalPages);
        var opportunities = await query.AsNoTracking().Include(item => item.Customer).Include(item => item.Lead)
            .OrderByDescending(item => item.ExpectedCloseDate).ThenBy(item => item.OpportunityName)
            .Skip((page - 1) * PageSize).Take(PageSize).ToListAsync();
        var assigneeIds = opportunities.Where(item => item.AssignedTo != null).Select(item => item.AssignedTo!).Distinct().ToList();
        var names = await _userManager.Users.Where(user => assigneeIds.Contains(user.Id)).ToDictionaryAsync(user => user.Id, user => user.Email ?? user.UserName ?? user.Id);
        return View(new OpportunityListViewModel
        {
            Opportunities = opportunities.Select(item => ToOpportunityListItem(item, names)).ToList(), Search = search, Stage = stage, Status = status,
            AssignedTo = CanFilterByAssignee ? assignedTo : null, Stages = OpportunityStages, Statuses = OpportunityStatuses,
            Assignees = CanFilterByAssignee ? await GetOpportunityAssigneesAsync() : [], Page = page, TotalPages = totalPages,
            TotalAmount = totalAmount, WeightedPipeline = weightedPipeline, CanFilterByAssignee = CanFilterByAssignee
        });
    }

    [HttpGet]
    public async Task<IActionResult> CreateOpportunity() => View("CreateOpportunity", await BuildOpportunityFormAsync(new OpportunityFormViewModel { Stage = "Qualification", Status = "Open" }));

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateOpportunity(OpportunityFormViewModel model)
    {
        await ValidateOpportunityAsync(model);
        if (!ModelState.IsValid) return View("CreateOpportunity", await BuildOpportunityFormAsync(model));
        var opportunity = new Opportunity { OpportunityName = model.OpportunityName.Trim(), CustomerId = model.CustomerId, LeadId = model.LeadId,
            Amount = model.Amount, Stage = model.Stage, Probability = model.Probability, ExpectedCloseDate = model.ExpectedCloseDate,
            Status = model.Status, AssignedTo = await ResolveOpportunityAssigneeAsync(model.AssignedTo) };
        _db.Opportunities.Add(opportunity);
        try { await _db.SaveChangesAsync(); await AddOpportunityAuditAsync("Create", opportunity, null, SerializeOpportunity(opportunity)); TempData["SuccessMessage"] = "Opportunity created successfully."; return RedirectToAction(nameof(Opportunities)); }
        catch (DbUpdateException) { ModelState.AddModelError(string.Empty, "The opportunity could not be saved."); return View("CreateOpportunity", await BuildOpportunityFormAsync(model)); }
    }

    [HttpGet]
    public async Task<IActionResult> OpportunityDetails(int id)
    {
        var opportunity = await AccessibleOpportunities().Include(item => item.Customer).Include(item => item.Lead).AsNoTracking().SingleOrDefaultAsync(item => item.OpportunityId == id);
        return opportunity is null ? NotFound() : View("OpportunityDetails", await ToOpportunityFormAsync(opportunity));
    }

    [HttpGet]
    public async Task<IActionResult> EditOpportunity(int id)
    {
        var opportunity = await AccessibleOpportunities().Include(item => item.Customer).Include(item => item.Lead).AsNoTracking().SingleOrDefaultAsync(item => item.OpportunityId == id);
        return opportunity is null ? NotFound() : View("EditOpportunity", await ToOpportunityFormAsync(opportunity));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> EditOpportunity(int id, OpportunityFormViewModel model)
    {
        if (id != model.OpportunityId) return BadRequest();
        var opportunity = await AccessibleOpportunities().SingleOrDefaultAsync(item => item.OpportunityId == id);
        if (opportunity is null) return NotFound();
        await ValidateOpportunityAsync(model, opportunity);
        if (!ModelState.IsValid) return View("EditOpportunity", await BuildOpportunityFormAsync(model));
        var oldValue = SerializeOpportunity(opportunity); var oldStage = opportunity.Stage;
        opportunity.OpportunityName = model.OpportunityName.Trim(); opportunity.CustomerId = model.CustomerId; opportunity.LeadId = model.LeadId;
        opportunity.Amount = model.Amount; opportunity.Stage = model.Stage; opportunity.Probability = model.Probability; opportunity.ExpectedCloseDate = model.ExpectedCloseDate;
        opportunity.Status = model.Status; opportunity.AssignedTo = await ResolveOpportunityAssigneeAsync(model.AssignedTo, opportunity.AssignedTo);
        try
        {
            await _db.SaveChangesAsync(); await AddOpportunityAuditAsync("Update", opportunity, oldValue, SerializeOpportunity(opportunity));
            if (!string.Equals(oldStage, opportunity.Stage, StringComparison.OrdinalIgnoreCase)) await AddOpportunityAuditAsync("StageChange", opportunity, oldValue, SerializeOpportunity(opportunity));
            TempData["SuccessMessage"] = "Opportunity updated successfully."; return RedirectToAction(nameof(Opportunities));
        }
        catch (DbUpdateException) { ModelState.AddModelError(string.Empty, "The opportunity could not be updated."); return View("EditOpportunity", await BuildOpportunityFormAsync(model)); }
    }

    [HttpGet]
    public async Task<IActionResult> DeleteOpportunity(int id)
    {
        var opportunity = await AccessibleOpportunities().Include(item => item.Customer).Include(item => item.Lead).AsNoTracking().SingleOrDefaultAsync(item => item.OpportunityId == id);
        return opportunity is null ? NotFound() : View("DeleteOpportunity", await ToOpportunityFormAsync(opportunity));
    }

    [HttpPost, ActionName("DeleteOpportunity"), ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteOpportunityConfirmed(int id)
    {
        var opportunity = await AccessibleOpportunities().SingleOrDefaultAsync(item => item.OpportunityId == id);
        if (opportunity is null) return NotFound();
        _db.Opportunities.Remove(opportunity); _db.AuditLogs.Add(NewAudit("Delete", nameof(Opportunity), id.ToString(), SerializeOpportunity(opportunity), null));
        try { await _db.SaveChangesAsync(); TempData["SuccessMessage"] = "Opportunity deleted successfully."; }
        catch (DbUpdateException) { TempData["ErrorMessage"] = "The opportunity could not be deleted."; }
        return RedirectToAction(nameof(Opportunities));
    }
    [HttpGet]
    public async Task<IActionResult> FollowUps(string? search, string? status, string? followUpType, DateTime? fromDate, DateTime? toDate, string? assignedTo, int page = 1)
    {
        page = Math.Max(1, page); var query = AccessibleFollowUps();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{search.Trim()}%";
            query = query.Where(item => (item.Customer != null && EF.Functions.Like(item.Customer.CustomerName, pattern)) ||
                (item.Lead != null && EF.Functions.Like(item.Lead.LeadName, pattern)) || (item.Remarks != null && EF.Functions.Like(item.Remarks, pattern)));
        }
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(item => item.Status == status);
        if (!string.IsNullOrWhiteSpace(followUpType)) query = query.Where(item => item.FollowUpType == followUpType);
        if (fromDate.HasValue) query = query.Where(item => item.FollowUpDate >= fromDate.Value.Date);
        if (toDate.HasValue) query = query.Where(item => item.FollowUpDate < toDate.Value.Date.AddDays(1));
        if (CanFilterByAssignee && !string.IsNullOrWhiteSpace(assignedTo)) query = query.Where(item => item.AssignedTo == assignedTo);
        var totalCount = await query.CountAsync(); var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)PageSize)); page = Math.Min(page, totalPages);
        var followUps = await query.AsNoTracking().Include(item => item.Customer).Include(item => item.Lead).OrderBy(item => item.FollowUpDate)
            .Skip((page - 1) * PageSize).Take(PageSize).ToListAsync();
        var ids = followUps.Where(item => item.AssignedTo != null).Select(item => item.AssignedTo!).Distinct().ToList();
        var names = await _userManager.Users.Where(user => ids.Contains(user.Id)).ToDictionaryAsync(user => user.Id, user => user.Email ?? user.UserName ?? user.Id);
        return View(new FollowUpListViewModel { FollowUps = followUps.Select(item => new FollowUpListItemViewModel { FollowUpId = item.FollowUpId,
            CustomerName = item.Customer?.CustomerName ?? "", LeadName = item.Lead?.LeadName ?? "", FollowUpDate = item.FollowUpDate, FollowUpType = item.FollowUpType,
            Remarks = item.Remarks, Status = item.Status, AssignedToName = item.AssignedTo == null ? "Unassigned" : names.GetValueOrDefault(item.AssignedTo, item.AssignedTo),
            IsOverdue = item.Status == "Planned" && item.FollowUpDate < DateTime.Now }).ToList(), Search = search, Status = status, FollowUpType = followUpType,
            FromDate = fromDate, ToDate = toDate, AssignedTo = CanFilterByAssignee ? assignedTo : null, Statuses = FollowUpStatuses, FollowUpTypes = FollowUpTypes,
            Assignees = CanFilterByAssignee ? await GetFollowUpAssigneesAsync() : [], Page = page, TotalPages = totalPages, CanFilterByAssignee = CanFilterByAssignee });
    }

    [HttpGet]
    public async Task<IActionResult> CreateFollowUp() => View("CreateFollowUp", await BuildFollowUpFormAsync(new FollowUpFormViewModel { Status = "Planned", FollowUpDate = DateTime.Now.AddDays(1) }));

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateFollowUp(FollowUpFormViewModel model)
    {
        await ValidateFollowUpAsync(model);
        if (!ModelState.IsValid) return View("CreateFollowUp", await BuildFollowUpFormAsync(model));
        var followUp = new FollowUp { CustomerId = model.CustomerId, LeadId = model.LeadId, FollowUpDate = model.FollowUpDate, FollowUpType = model.FollowUpType.Trim(), Remarks = Normalize(model.Remarks), Status = model.Status, AssignedTo = await ResolveFollowUpAssigneeAsync(model.AssignedTo) };
        _db.FollowUps.Add(followUp);
        try { await _db.SaveChangesAsync(); await AddFollowUpAuditAsync("Create", followUp, null, SerializeFollowUp(followUp)); TempData["SuccessMessage"] = "Follow-up created successfully."; return RedirectToAction(nameof(FollowUps)); }
        catch (DbUpdateException) { ModelState.AddModelError(string.Empty, "The follow-up could not be saved."); return View("CreateFollowUp", await BuildFollowUpFormAsync(model)); }
    }

    [HttpGet]
    public async Task<IActionResult> FollowUpDetails(int id)
    {
        var followUp = await AccessibleFollowUps().Include(item => item.Customer).Include(item => item.Lead).AsNoTracking().SingleOrDefaultAsync(item => item.FollowUpId == id);
        return followUp is null ? NotFound() : View("FollowUpDetails", await ToFollowUpFormAsync(followUp));
    }

    [HttpGet]
    public async Task<IActionResult> EditFollowUp(int id)
    {
        var followUp = await AccessibleFollowUps().Include(item => item.Customer).Include(item => item.Lead).AsNoTracking().SingleOrDefaultAsync(item => item.FollowUpId == id);
        return followUp is null ? NotFound() : View("EditFollowUp", await ToFollowUpFormAsync(followUp));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> EditFollowUp(int id, FollowUpFormViewModel model)
    {
        if (id != model.FollowUpId) return BadRequest();
        var followUp = await AccessibleFollowUps().SingleOrDefaultAsync(item => item.FollowUpId == id);
        if (followUp is null) return NotFound();
        await ValidateFollowUpAsync(model, followUp);
        if (!ModelState.IsValid) return View("EditFollowUp", await BuildFollowUpFormAsync(model));
        var oldValue = SerializeFollowUp(followUp); var oldDate = followUp.FollowUpDate; var oldStatus = followUp.Status;
        followUp.CustomerId = model.CustomerId; followUp.LeadId = model.LeadId; followUp.FollowUpDate = model.FollowUpDate; followUp.FollowUpType = model.FollowUpType.Trim(); followUp.Remarks = Normalize(model.Remarks); followUp.Status = model.Status; followUp.AssignedTo = await ResolveFollowUpAssigneeAsync(model.AssignedTo, followUp.AssignedTo);
        try
        {
            await _db.SaveChangesAsync(); await AddFollowUpAuditAsync(string.Equals(oldDate, followUp.FollowUpDate) ? "Update" : "Reschedule", followUp, oldValue, SerializeFollowUp(followUp));
            if (!string.Equals(oldStatus, followUp.Status, StringComparison.OrdinalIgnoreCase)) await AddFollowUpAuditAsync(followUp.Status, followUp, oldValue, SerializeFollowUp(followUp));
            TempData["SuccessMessage"] = "Follow-up updated successfully."; return RedirectToAction(nameof(FollowUps));
        }
        catch (DbUpdateException) { ModelState.AddModelError(string.Empty, "The follow-up could not be updated."); return View("EditFollowUp", await BuildFollowUpFormAsync(model)); }
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CompleteFollowUp(int id)
    {
        return await ChangeFollowUpStatusAsync(id, "Completed", "Follow-up marked completed.");
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CancelFollowUp(int id)
    {
        return await ChangeFollowUpStatusAsync(id, "Cancelled", "Follow-up cancelled.");
    }

    [HttpGet]
    public async Task<IActionResult> DeleteFollowUp(int id)
    {
        var followUp = await AccessibleFollowUps().Include(item => item.Customer).Include(item => item.Lead).AsNoTracking().SingleOrDefaultAsync(item => item.FollowUpId == id);
        return followUp is null ? NotFound() : View("DeleteFollowUp", await ToFollowUpFormAsync(followUp));
    }

    [HttpPost, ActionName("DeleteFollowUp"), ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteFollowUpConfirmed(int id)
    {
        var followUp = await AccessibleFollowUps().SingleOrDefaultAsync(item => item.FollowUpId == id);
        if (followUp is null) return NotFound();
        _db.FollowUps.Remove(followUp); _db.AuditLogs.Add(NewAudit("Delete", nameof(FollowUp), id.ToString(), SerializeFollowUp(followUp), null));
        try { await _db.SaveChangesAsync(); TempData["SuccessMessage"] = "Follow-up deleted successfully."; } catch (DbUpdateException) { TempData["ErrorMessage"] = "The follow-up could not be deleted."; }
        return RedirectToAction(nameof(FollowUps));
    }
    [HttpGet]
    public async Task<IActionResult> CreateActivity(int? customerId, int? leadId)
    {
        return View("CreateActivity", await BuildActivityFormAsync(new ActivityFormViewModel { CustomerId = customerId, LeadId = leadId }));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateActivity(ActivityFormViewModel model)
    {
        if (model.CustomerId is null && model.LeadId is null) ModelState.AddModelError(string.Empty, "Select at least one customer or lead.");
        if (model.CustomerId.HasValue && !await _db.Customers.AnyAsync(item => item.CustomerId == model.CustomerId.Value)) ModelState.AddModelError(nameof(model.CustomerId), "Select an existing customer.");
        if (model.LeadId.HasValue && !await _db.Leads.AnyAsync(item => item.LeadId == model.LeadId.Value)) ModelState.AddModelError(nameof(model.LeadId), "Select an existing lead.");
        if (!new[] { "Call", "Meeting", "Email", "Task" }.Contains(model.ActivityType, StringComparer.OrdinalIgnoreCase)) ModelState.AddModelError(nameof(model.ActivityType), "Select a valid activity type.");
        if (!ModelState.IsValid) return View("CreateActivity", await BuildActivityFormAsync(model));
        var activity = new Activity { CustomerId = model.CustomerId, LeadId = model.LeadId, ActivityType = model.ActivityType.Trim(), Subject = model.Subject.Trim(), Description = Normalize(model.Description), ActivityDate = model.ActivityDate, Status = model.Status, AssignedTo = CurrentUserId };
        _db.Activities.Add(activity);
        try { await _db.SaveChangesAsync(); _db.AuditLogs.Add(NewAudit("Create", nameof(Activity), activity.ActivityId.ToString(), null, JsonSerializer.Serialize(new { activity.ActivityId, activity.CustomerId, activity.LeadId, activity.ActivityType, activity.Subject }))); await _db.SaveChangesAsync(); TempData["SuccessMessage"] = "Activity recorded successfully."; return RedirectToAction(nameof(FollowUps)); }
        catch (DbUpdateException) { ModelState.AddModelError(string.Empty, "The activity could not be saved."); return View("CreateActivity", await BuildActivityFormAsync(model)); }
    }

    public IActionResult Activities() => Module("Activities");

    [HttpGet]
    public async Task<IActionResult> Reports(string reportType = "Customer", string? search = null, string? status = null, DateTime? fromDate = null, DateTime? toDate = null, string sort = "date_desc", int page = 1)
    {
        if (string.Equals(reportType, "AuditLog", StringComparison.OrdinalIgnoreCase) && !User.IsInRole(RoleNames.Admin)) return Forbid();
        if (fromDate.HasValue && toDate.HasValue && fromDate.Value.Date > toDate.Value.Date)
        {
            ModelState.AddModelError(string.Empty, "The start date cannot be after the end date.");
            fromDate = toDate = null;
        }
        return View("Reports", await BuildReportAsync(reportType, search, status, fromDate, toDate, sort, page));
    }

    [HttpGet]
    public async Task<IActionResult> ExportReport(string reportType = "Customer", string? search = null, string? status = null, DateTime? fromDate = null, DateTime? toDate = null, string sort = "date_desc")
    {
        if (string.Equals(reportType, "AuditLog", StringComparison.OrdinalIgnoreCase) && !User.IsInRole(RoleNames.Admin)) return Forbid();
        if (fromDate.HasValue && toDate.HasValue && fromDate.Value.Date > toDate.Value.Date) return BadRequest("The start date cannot be after the end date.");
        var report = await BuildReportAsync(reportType, search, status, fromDate, toDate, sort, 1, int.MaxValue);
        var csv = new StringBuilder();
        csv.AppendLine(string.Join(",", report.Columns.Select(CsvEscape)));
        foreach (var row in report.Rows) csv.AppendLine(string.Join(",", row.Values.Select(CsvEscape)));
        return File(Encoding.UTF8.GetBytes(csv.ToString()), "text/csv", $"{report.ReportType.Replace(" ", "-").ToLowerInvariant()}-report.csv");
    }

    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [HttpGet]
    public async Task<IActionResult> Users(string? search, int page = 1)
    {
        page = Math.Max(1, page); var query = _userManager.Users.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search)) { var pattern = $"%{search.Trim()}%"; query = query.Where(user => EF.Functions.Like(user.Email!, pattern) || EF.Functions.Like(user.UserName!, pattern)); }
        var totalCount = await query.CountAsync(); var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)PageSize)); page = Math.Min(page, totalPages);
        var users = await query.OrderBy(user => user.Email).Skip((page - 1) * PageSize).Take(PageSize).ToListAsync();
        var items = new List<UserListItemViewModel>();
        foreach (var user in users) { var roles = await _userManager.GetRolesAsync(user); items.Add(new UserListItemViewModel { Id = user.Id, Email = user.Email ?? "", UserName = user.UserName ?? "", Role = roles.FirstOrDefault() ?? "No role", IsActive = IsActive(user) }); }
        return View(new UserListViewModel { Users = items, Search = search, Page = page, TotalPages = totalPages });
    }

    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [HttpGet]
    public async Task<IActionResult> CreateUser() => View("CreateUser", new UserFormViewModel { Role = RoleNames.SalesExecutive });

    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateUser(UserFormViewModel model)
    {
        ValidateRole(model.Role); if (string.IsNullOrWhiteSpace(model.Password)) ModelState.AddModelError(nameof(model.Password), "Password is required.");
        if (!ModelState.IsValid) return View("CreateUser", model);
        var user = new IdentityUser { UserName = model.UserName.Trim(), Email = model.Email.Trim(), EmailConfirmed = true, LockoutEnabled = true };
        var result = await _userManager.CreateAsync(user, model.Password!);
        if (!result.Succeeded) { AddIdentityErrors(result); return View("CreateUser", model); }
        var roleResult = await _userManager.AddToRoleAsync(user, model.Role);
        if (!roleResult.Succeeded) { await _userManager.DeleteAsync(user); AddIdentityErrors(roleResult); return View("CreateUser", model); }
        _db.AuditLogs.Add(NewAudit("Create", "User", user.Id, null, JsonSerializer.Serialize(new { user.Id, user.Email, user.UserName, Role = model.Role })));
        await _db.SaveChangesAsync(); TempData["SuccessMessage"] = "User created successfully."; return RedirectToAction(nameof(Users));
    }

    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [HttpGet]
    public async Task<IActionResult> UserDetails(string id)
    {
        var user = await _userManager.FindByIdAsync(id); if (user is null) return NotFound(); var roles = await _userManager.GetRolesAsync(user);
        return View("UserDetails", new UserFormViewModel { Id = user.Id, Email = user.Email ?? "", UserName = user.UserName ?? "", Role = roles.FirstOrDefault() ?? "No role", CurrentRole = roles.FirstOrDefault(), IsActive = IsActive(user), IsDetailsOnly = true });
    }

    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [HttpGet]
    public async Task<IActionResult> EditUser(string id)
    {
        var user = await _userManager.FindByIdAsync(id); if (user is null) return NotFound(); var roles = await _userManager.GetRolesAsync(user);
        return View("EditUser", new UserFormViewModel { Id = user.Id, Email = user.Email ?? "", UserName = user.UserName ?? "", Role = roles.FirstOrDefault() ?? RoleNames.SalesExecutive, CurrentRole = roles.FirstOrDefault(), IsActive = IsActive(user) });
    }

    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> EditUser(string id, UserFormViewModel model)
    {
        if (id != model.Id) return BadRequest(); var user = await _userManager.FindByIdAsync(id); if (user is null) return NotFound(); ValidateRole(model.Role);
        var currentRoles = await _userManager.GetRolesAsync(user); var currentRole = currentRoles.FirstOrDefault();
        if (currentRole == RoleNames.Admin && model.Role != RoleNames.Admin && await IsLastActiveAdminAsync(user)) ModelState.AddModelError(nameof(model.Role), "The last active Admin cannot be removed.");
        if (!model.IsActive && currentRole == RoleNames.Admin && await IsLastActiveAdminAsync(user)) ModelState.AddModelError(nameof(model.IsActive), "The last active Admin cannot be deactivated.");
        if (!ModelState.IsValid) { model.CurrentRole = currentRole; return View("EditUser", model); }
        var oldActive = IsActive(user); var oldValue = JsonSerializer.Serialize(new { user.Id, user.Email, user.UserName, Role = currentRole, Active = oldActive });
        user.Email = model.Email.Trim(); user.UserName = model.UserName.Trim();
        var updateResult = await _userManager.UpdateAsync(user); if (!updateResult.Succeeded) { AddIdentityErrors(updateResult); model.CurrentRole = currentRole; return View("EditUser", model); }
        if (currentRole != model.Role) { if (currentRoles.Count > 0) await _userManager.RemoveFromRolesAsync(user, currentRoles); var roleResult = await _userManager.AddToRoleAsync(user, model.Role); if (!roleResult.Succeeded) { AddIdentityErrors(roleResult); return View("EditUser", model); } }
        await _userManager.SetLockoutEnabledAsync(user, true); await _userManager.SetLockoutEndDateAsync(user, model.IsActive ? null : DateTimeOffset.UtcNow.AddYears(100));
        var action = currentRole == model.Role ? "Update" : "RoleChange"; _db.AuditLogs.Add(NewAudit(action, "User", user.Id, oldValue, JsonSerializer.Serialize(new { user.Id, user.Email, user.UserName, Role = model.Role, Active = model.IsActive })));
        if (oldActive != model.IsActive) _db.AuditLogs.Add(NewAudit("ActivationChange", "User", user.Id, oldValue, JsonSerializer.Serialize(new { Active = model.IsActive })));
        await _db.SaveChangesAsync(); TempData["SuccessMessage"] = "User updated successfully."; return RedirectToAction(nameof(Users));
    }

    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [HttpGet]
    public async Task<IActionResult> AuditLogs(string? search, string? auditAction, string? entityName, DateTime? fromDate, DateTime? toDate, int page = 1)
    {
        page = Math.Max(1, page); var query = _db.AuditLogs.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search)) { var pattern = $"%{search.Trim()}%"; var matchingUserIds = await _userManager.Users.Where(user => EF.Functions.Like(user.Email!, pattern) || EF.Functions.Like(user.UserName!, pattern)).Select(user => user.Id).ToListAsync(); query = query.Where(log => matchingUserIds.Contains(log.UserId) || EF.Functions.Like(log.Action, pattern) || EF.Functions.Like(log.EntityName, pattern)); }
        if (!string.IsNullOrWhiteSpace(auditAction)) query = query.Where(log => log.Action == auditAction); if (!string.IsNullOrWhiteSpace(entityName)) query = query.Where(log => log.EntityName == entityName); if (fromDate.HasValue) query = query.Where(log => log.CreatedDate >= fromDate.Value.Date); if (toDate.HasValue) query = query.Where(log => log.CreatedDate < toDate.Value.Date.AddDays(1));
        var totalCount = await query.CountAsync(); var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)PageSize)); page = Math.Min(page, totalPages); var logs = await query.OrderByDescending(log => log.CreatedDate).Skip((page - 1) * PageSize).Take(PageSize).ToListAsync();
        var ids = logs.Select(log => log.UserId).Distinct().ToList(); var names = await _userManager.Users.Where(user => ids.Contains(user.Id)).ToDictionaryAsync(user => user.Id, user => user.Email ?? user.UserName ?? user.Id);
        return View(new AuditLogListViewModel { AuditLogs = logs.Select(log => new AuditLogListItemViewModel { AuditLogId = log.AuditLogId, UserName = names.GetValueOrDefault(log.UserId, log.UserId), Action = log.Action, EntityName = log.EntityName, RecordId = log.RecordId, OldValue = log.OldValue, NewValue = log.NewValue, CreatedDate = log.CreatedDate, IpAddress = log.IpAddress }).ToList(), Search = search, Action = auditAction, EntityName = entityName, FromDate = fromDate, ToDate = toDate, Actions = await _db.AuditLogs.Select(log => log.Action).Distinct().OrderBy(item => item).ToListAsync(), Entities = await _db.AuditLogs.Select(log => log.EntityName).Distinct().OrderBy(item => item).ToListAsync(), Page = page, TotalPages = totalPages });
    }

    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [HttpGet]
    public async Task<IActionResult> AuditLogDetails(int id)
    {
        var log = await _db.AuditLogs.AsNoTracking().SingleOrDefaultAsync(item => item.AuditLogId == id); if (log is null) return NotFound(); var user = await _userManager.FindByIdAsync(log.UserId);
        return View("AuditLogDetails", new AuditLogListItemViewModel { AuditLogId = log.AuditLogId, UserName = user?.Email ?? user?.UserName ?? log.UserId, Action = log.Action, EntityName = log.EntityName, RecordId = log.RecordId, OldValue = log.OldValue, NewValue = log.NewValue, CreatedDate = log.CreatedDate, IpAddress = log.IpAddress });
    }

    private bool CanFilterByOwner => User.IsInRole(RoleNames.Admin) || User.IsInRole(RoleNames.Manager);
    private bool CanFilterByAssignee => User.IsInRole(RoleNames.Admin) || User.IsInRole(RoleNames.Manager);

    private static bool IsActive(IdentityUser user) => user.LockoutEnd is null || user.LockoutEnd <= DateTimeOffset.UtcNow;

    private void ValidateRole(string role)
    {
        if (!RoleNames.All.Contains(role, StringComparer.Ordinal)) ModelState.AddModelError(nameof(UserFormViewModel.Role), "Select a valid role.");
    }

    private void AddIdentityErrors(IdentityResult result)
    {
        foreach (var error in result.Errors) ModelState.AddModelError(string.Empty, error.Description);
    }

    private async Task<bool> IsLastActiveAdminAsync(IdentityUser target)
    {
        var admins = await _userManager.GetUsersInRoleAsync(RoleNames.Admin);
        return admins.Where(user => user.Id != target.Id && IsActive(user)).Count() == 0 && IsActive(target);
    }

    private string CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new InvalidOperationException("Authenticated user ID is missing.");

    private IQueryable<Customer> AccessibleCustomers()
    {
        var query = _db.Customers.AsQueryable();
        return User.IsInRole(RoleNames.Admin) || User.IsInRole(RoleNames.Manager)
            ? query
            : query.Where(customer => customer.CreatedBy == CurrentUserId);
    }

    private IQueryable<Lead> AccessibleLeads()
    {
        var query = _db.Leads.AsQueryable();
        return User.IsInRole(RoleNames.Admin) || User.IsInRole(RoleNames.Manager)
            ? query : query.Where(lead => lead.AssignedTo == CurrentUserId);
    }

    private IQueryable<Opportunity> AccessibleOpportunities()
    {
        var query = _db.Opportunities.AsQueryable();
        return User.IsInRole(RoleNames.Admin) || User.IsInRole(RoleNames.Manager)
            ? query : query.Where(item => item.AssignedTo == CurrentUserId);
    }

    private IQueryable<FollowUp> AccessibleFollowUps()
    {
        var query = _db.FollowUps.AsQueryable();
        return User.IsInRole(RoleNames.Admin) || User.IsInRole(RoleNames.Manager) ? query : query.Where(item => item.AssignedTo == CurrentUserId);
    }

    private async Task<ActivityFormViewModel> BuildActivityFormAsync(ActivityFormViewModel model)
    {
        model.Customers = await _db.Customers.AsNoTracking().OrderBy(item => item.CustomerName).Select(item => new FollowUpCustomerOption { Id = item.CustomerId, DisplayName = item.CustomerName }).ToListAsync();
        model.Leads = await _db.Leads.AsNoTracking().OrderBy(item => item.LeadName).Select(item => new FollowUpLeadOption { Id = item.LeadId, DisplayName = $"{item.LeadName} ({item.Status})" }).ToListAsync();
        return model;
    }

    private async Task ValidateFollowUpAsync(FollowUpFormViewModel model, FollowUp? existing = null)
    {
        if (!FollowUpStatuses.Contains(model.Status, StringComparer.OrdinalIgnoreCase)) ModelState.AddModelError(nameof(model.Status), "Select a valid follow-up status.");
        if (!FollowUpTypes.Contains(model.FollowUpType, StringComparer.OrdinalIgnoreCase)) ModelState.AddModelError(nameof(model.FollowUpType), "Select a valid follow-up type.");
        if (model.CustomerId is null && model.LeadId is null) ModelState.AddModelError(string.Empty, "Select at least one customer or lead.");
        if (model.CustomerId.HasValue && !await _db.Customers.AnyAsync(item => item.CustomerId == model.CustomerId.Value)) ModelState.AddModelError(nameof(model.CustomerId), "Select an existing customer.");
        if (model.LeadId.HasValue && !await _db.Leads.AnyAsync(item => item.LeadId == model.LeadId.Value)) ModelState.AddModelError(nameof(model.LeadId), "Select an existing lead.");
        if (model.Status == "Planned" && model.FollowUpDate < DateTime.Now) ModelState.AddModelError(nameof(model.FollowUpDate), "Planned follow-ups cannot be in the past.");
        if (existing is not null)
        {
            if (existing.Status == "Completed" && model.Status != "Completed") ModelState.AddModelError(nameof(model.Status), "Completed follow-ups cannot be reopened.");
            if (existing.Status == "Cancelled" && model.Status != "Cancelled") ModelState.AddModelError(nameof(model.Status), "Cancelled follow-ups cannot be reopened.");
            if (existing.Status == "Missed" && model.Status != "Missed" && model.Status != "Planned") ModelState.AddModelError(nameof(model.Status), "Missed follow-ups can only be rescheduled as Planned.");
            if (model.Status == "Planned" && model.FollowUpDate < DateTime.Now) ModelState.AddModelError(nameof(model.FollowUpDate), "Rescheduled dates cannot be in the past.");
        }
        await ResolveFollowUpAssigneeAsync(model.AssignedTo, existing?.AssignedTo);
    }

    private async Task<string> ResolveFollowUpAssigneeAsync(string? submitted, string? existing = null)
    {
        if (!CanFilterByAssignee) return existing ?? CurrentUserId;
        if (string.IsNullOrWhiteSpace(submitted)) return existing ?? CurrentUserId;
        var user = await _userManager.FindByIdAsync(submitted);
        if (user is null) ModelState.AddModelError(nameof(FollowUpFormViewModel.AssignedTo), "Select a valid assigned user.");
        return user?.Id ?? existing ?? CurrentUserId;
    }

    private async Task<FollowUpFormViewModel> BuildFollowUpFormAsync(FollowUpFormViewModel model)
    {
        model.Customers = await _db.Customers.AsNoTracking().OrderBy(item => item.CustomerName).Select(item => new FollowUpCustomerOption { Id = item.CustomerId, DisplayName = item.CustomerName }).ToListAsync();
        model.Leads = await _db.Leads.AsNoTracking().OrderBy(item => item.LeadName).Select(item => new FollowUpLeadOption { Id = item.LeadId, DisplayName = $"{item.LeadName} ({item.Status})" }).ToListAsync();
        model.Assignees = CanFilterByAssignee ? await GetFollowUpAssigneesAsync() : [];
        return model;
    }

    private async Task<FollowUpFormViewModel> ToFollowUpFormAsync(FollowUp followUp)
    {
        var assignee = followUp.AssignedTo is null ? null : await _userManager.FindByIdAsync(followUp.AssignedTo);
        return await BuildFollowUpFormAsync(new FollowUpFormViewModel { FollowUpId = followUp.FollowUpId, CustomerId = followUp.CustomerId, LeadId = followUp.LeadId,
            FollowUpDate = followUp.FollowUpDate, FollowUpType = followUp.FollowUpType, Remarks = followUp.Remarks, Status = followUp.Status, AssignedTo = followUp.AssignedTo,
            CustomerName = followUp.Customer?.CustomerName, LeadName = followUp.Lead?.LeadName, AssignedToName = assignee?.Email ?? assignee?.UserName ?? followUp.AssignedTo });
    }

    private async Task<IReadOnlyList<FollowUpAssigneeOption>> GetFollowUpAssigneesAsync() => await _userManager.Users.OrderBy(user => user.Email)
        .Select(user => new FollowUpAssigneeOption { Id = user.Id, DisplayName = user.Email ?? user.UserName ?? user.Id }).ToListAsync();

    private async Task<IActionResult> ChangeFollowUpStatusAsync(int id, string status, string message)
    {
        var followUp = await AccessibleFollowUps().SingleOrDefaultAsync(item => item.FollowUpId == id);
        if (followUp is null) return NotFound();
        if (followUp.Status == "Completed" || followUp.Status == "Cancelled") { TempData["ErrorMessage"] = "This follow-up cannot change status."; return RedirectToAction(nameof(FollowUps)); }
        var oldValue = SerializeFollowUp(followUp); followUp.Status = status;
        await _db.SaveChangesAsync(); await AddFollowUpAuditAsync(status, followUp, oldValue, SerializeFollowUp(followUp)); TempData["SuccessMessage"] = message;
        return RedirectToAction(nameof(FollowUps));
    }

    private async Task AddFollowUpAuditAsync(string action, FollowUp followUp, string? oldValue, string? newValue)
    {
        _db.AuditLogs.Add(NewAudit(action, nameof(FollowUp), followUp.FollowUpId.ToString(), oldValue, newValue)); await _db.SaveChangesAsync();
    }

    private static string SerializeFollowUp(FollowUp followUp) => JsonSerializer.Serialize(new { followUp.FollowUpId, followUp.CustomerId, followUp.LeadId, followUp.FollowUpDate, followUp.FollowUpType, followUp.Remarks, followUp.Status, followUp.AssignedTo });

    private async Task ValidateOpportunityAsync(OpportunityFormViewModel model, Opportunity? existing = null)
    {
        if (!OpportunityStages.Contains(model.Stage, StringComparer.OrdinalIgnoreCase)) ModelState.AddModelError(nameof(model.Stage), "Select a valid opportunity stage.");
        if (!OpportunityStatuses.Contains(model.Status, StringComparer.OrdinalIgnoreCase)) ModelState.AddModelError(nameof(model.Status), "Select a valid opportunity status.");
        if (model.Status == "Open" && model.Amount <= 0) ModelState.AddModelError(nameof(model.Amount), "Amount must be greater than zero for active opportunities.");
        if (model.Status == "Open" && model.ExpectedCloseDate.HasValue && model.ExpectedCloseDate.Value.Date < DateTime.UtcNow.Date) ModelState.AddModelError(nameof(model.ExpectedCloseDate), "Expected close date cannot be in the past for active opportunities.");
        if ((model.Stage == "Won" && model.Status != "Won") || (model.Stage == "Lost" && model.Status != "Lost")) ModelState.AddModelError(nameof(model.Status), "Won and Lost stages must use the matching status.");
        if ((model.Status == "Won" && model.Stage != "Won") || (model.Status == "Lost" && model.Stage != "Lost")) ModelState.AddModelError(nameof(model.Stage), "Won and Lost statuses must use the matching stage.");
        if (!await _db.Customers.AnyAsync(item => item.CustomerId == model.CustomerId)) ModelState.AddModelError(nameof(model.CustomerId), "Select an existing customer.");
        if (model.LeadId.HasValue && !await _db.Leads.AnyAsync(item => item.LeadId == model.LeadId.Value)) ModelState.AddModelError(nameof(model.LeadId), "Select an existing lead.");
        if (existing is not null && existing.Status == "Won" && model.Status != "Won") ModelState.AddModelError(nameof(model.Status), "Won opportunities cannot be reopened.");
        if (existing is not null && existing.Status == "Lost" && model.Status != "Lost") ModelState.AddModelError(nameof(model.Status), "Lost opportunities cannot be reopened.");
        await ResolveOpportunityAssigneeAsync(model.AssignedTo, existing?.AssignedTo);
    }

    private async Task<string> ResolveOpportunityAssigneeAsync(string? submitted, string? existing = null)
    {
        if (!CanFilterByAssignee) return existing ?? CurrentUserId;
        if (string.IsNullOrWhiteSpace(submitted)) return existing ?? CurrentUserId;
        var user = await _userManager.FindByIdAsync(submitted);
        if (user is null) ModelState.AddModelError(nameof(OpportunityFormViewModel.AssignedTo), "Select a valid assigned user.");
        return user?.Id ?? existing ?? CurrentUserId;
    }

    private async Task<OpportunityFormViewModel> BuildOpportunityFormAsync(OpportunityFormViewModel model)
    {
        model.Customers = await _db.Customers.AsNoTracking().OrderBy(item => item.CustomerName).Select(item => new OpportunityCustomerOption { Id = item.CustomerId, DisplayName = item.CustomerName }).ToListAsync();
        model.Leads = await _db.Leads.AsNoTracking().OrderBy(item => item.LeadName).Select(item => new OpportunityLeadOption { Id = item.LeadId, DisplayName = $"{item.LeadName} ({item.Status})" }).ToListAsync();
        model.Assignees = CanFilterByAssignee ? await GetOpportunityAssigneesAsync() : [];
        return model;
    }

    private async Task<OpportunityFormViewModel> ToOpportunityFormAsync(Opportunity opportunity)
    {
        var assignee = opportunity.AssignedTo is null ? null : await _userManager.FindByIdAsync(opportunity.AssignedTo);
        return await BuildOpportunityFormAsync(new OpportunityFormViewModel { OpportunityId = opportunity.OpportunityId, OpportunityName = opportunity.OpportunityName,
            CustomerId = opportunity.CustomerId, LeadId = opportunity.LeadId, Amount = opportunity.Amount, Stage = opportunity.Stage,
            Probability = opportunity.Probability, ExpectedCloseDate = opportunity.ExpectedCloseDate, Status = opportunity.Status, AssignedTo = opportunity.AssignedTo,
            AssignedToName = assignee?.Email ?? assignee?.UserName ?? opportunity.AssignedTo, CustomerName = opportunity.Customer.CustomerName, LeadName = opportunity.Lead?.LeadName,
            WeightedValue = opportunity.Amount * opportunity.Probability / 100m });
    }

    private async Task<IReadOnlyList<OpportunityAssigneeOption>> GetOpportunityAssigneesAsync() => await _userManager.Users.OrderBy(user => user.Email)
        .Select(user => new OpportunityAssigneeOption { Id = user.Id, DisplayName = user.Email ?? user.UserName ?? user.Id }).ToListAsync();

    private static OpportunityListItemViewModel ToOpportunityListItem(Opportunity item, IReadOnlyDictionary<string, string> names) => new()
    {
        OpportunityId = item.OpportunityId, OpportunityName = item.OpportunityName, CustomerName = item.Customer.CustomerName, LeadName = item.Lead?.LeadName,
        Amount = item.Amount, Stage = item.Stage, Probability = item.Probability, ExpectedCloseDate = item.ExpectedCloseDate, Status = item.Status,
        AssignedToName = item.AssignedTo == null ? "Unassigned" : names.GetValueOrDefault(item.AssignedTo, item.AssignedTo), WeightedValue = item.Amount * item.Probability / 100m,
        IsActivePipeline = item.Status != "Won" && item.Status != "Lost"
    };

    private async Task AddOpportunityAuditAsync(string action, Opportunity opportunity, string? oldValue, string? newValue)
    {
        _db.AuditLogs.Add(NewAudit(action, nameof(Opportunity), opportunity.OpportunityId.ToString(), oldValue, newValue)); await _db.SaveChangesAsync();
    }

    private static string SerializeOpportunity(Opportunity opportunity) => JsonSerializer.Serialize(new { opportunity.OpportunityId, opportunity.OpportunityName, opportunity.CustomerId, opportunity.LeadId, opportunity.Amount, opportunity.Stage, opportunity.Probability, opportunity.ExpectedCloseDate, opportunity.Status, opportunity.AssignedTo });

    private async Task<IReadOnlyList<LeadAssigneeViewModel>> GetAssigneesAsync() => await _userManager.Users
        .OrderBy(user => user.Email).Select(user => new LeadAssigneeViewModel { Id = user.Id, DisplayName = user.Email ?? user.UserName ?? user.Id }).ToListAsync();

    private async Task<string?> ResolveAssignedToAsync(string? submitted, string? existing = null)
    {
        if (!CanFilterByAssignee) return existing ?? CurrentUserId;
        if (string.IsNullOrWhiteSpace(submitted)) return CurrentUserId;
        var user = await _userManager.FindByIdAsync(submitted);
        if (user is null) ModelState.AddModelError(nameof(LeadFormViewModel.AssignedTo), "Select a valid assigned user.");
        return user?.Id ?? existing ?? CurrentUserId;
    }

    private void ValidateLeadStatus(string status)
    {
        if (!LeadStatuses.Contains(status, StringComparer.OrdinalIgnoreCase)) ModelState.AddModelError(nameof(LeadFormViewModel.Status), "Select a valid lead status.");
    }

    private async Task<LeadFormViewModel> ToLeadFormModelAsync(Lead lead, LeadFormViewModel? submitted = null)
    {
        var owner = lead.AssignedTo is null ? null : await _userManager.FindByIdAsync(lead.AssignedTo);
        return submitted ?? new LeadFormViewModel { LeadId = lead.LeadId, LeadCode = lead.LeadCode, LeadName = lead.LeadName, Email = lead.Email,
            Phone = lead.Phone, CompanyName = lead.CompanyName, Source = lead.Source, Status = lead.Status, ExpectedValue = lead.ExpectedValue,
            CreatedDate = lead.CreatedDate, AssignedTo = lead.AssignedTo, AssignedToName = owner?.Email ?? owner?.UserName ?? lead.AssignedTo, Assignees = await GetAssigneesAsync() };
    }

    private async Task<string> GenerateCustomerCodeAsync(Lead lead)
    {
        var baseCode = $"LEAD-{lead.LeadCode}"[..Math.Min(30, $"LEAD-{lead.LeadCode}".Length)];
        var code = baseCode; var suffix = 1;
        while (await _db.Customers.AnyAsync(customer => customer.CustomerCode == code))
        {
            var ending = $"-{suffix++}"; code = $"{baseCode[..Math.Min(baseCode.Length, 30 - ending.Length)]}{ending}";
        }
        return code;
    }

    private AuditLog NewAudit(string action, string entityName, string recordId, string? oldValue, string? newValue) => new()
    {
        UserId = CurrentUserId, Action = action, EntityName = entityName, RecordId = recordId, OldValue = oldValue, NewValue = newValue,
        CreatedDate = DateTime.UtcNow, IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString()
    };

    private async Task AddLeadAuditAsync(string action, Lead lead, string? oldValue, string? newValue)
    {
        _db.AuditLogs.Add(NewAudit(action, nameof(Lead), lead.LeadId.ToString(), oldValue, newValue)); await _db.SaveChangesAsync();
    }

    private static string SerializeLead(Lead lead) => JsonSerializer.Serialize(new { lead.LeadId, lead.LeadCode, lead.LeadName, lead.Email, lead.Phone, lead.CompanyName, lead.Source, lead.Status, lead.ExpectedValue, lead.CreatedDate, lead.AssignedTo });

    private async Task ValidateUniqueFieldsAsync(CustomerFormViewModel model, int? excludedId = null)
    {
        if (!CustomerStatuses.Contains(model.Status, StringComparer.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(nameof(model.Status), "Select a valid customer status.");
        }

        var code = model.CustomerCode.Trim();
        var email = model.Email.Trim();
        var codeExists = await _db.Customers.AnyAsync(customer =>
            (excludedId == null || customer.CustomerId != excludedId) &&
            EF.Functions.Collate(customer.CustomerCode, "NOCASE") == code);
        if (codeExists)
        {
            ModelState.AddModelError(nameof(model.CustomerCode), "Customer code must be unique.");
        }

        var emailExists = await _db.Customers.AnyAsync(customer =>
            (excludedId == null || customer.CustomerId != excludedId) &&
            EF.Functions.Collate(customer.Email, "NOCASE") == email);
        if (emailExists)
        {
            ModelState.AddModelError(nameof(model.Email), "Email is already associated with another customer.");
        }
    }

    private async Task AddAuditAsync(string action, Customer customer, string? oldValue, string? newValue)
    {
        _db.AuditLogs.Add(new AuditLog
        {
            UserId = CurrentUserId,
            Action = action,
            EntityName = nameof(Customer),
            RecordId = customer.CustomerId.ToString(),
            OldValue = oldValue,
            NewValue = newValue,
            CreatedDate = DateTime.UtcNow,
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString()
        });
        await _db.SaveChangesAsync();
    }

    private async Task<CustomerFormViewModel> ToFormModelAsync(Customer customer, CustomerFormViewModel? submitted = null)
    {
        var owner = await _userManager.FindByIdAsync(customer.CreatedBy);
        return submitted ?? new CustomerFormViewModel
        {
            CustomerId = customer.CustomerId,
            CustomerCode = customer.CustomerCode,
            CustomerName = customer.CustomerName,
            Email = customer.Email,
            Phone = customer.Phone,
            CompanyName = customer.CompanyName,
            Address = customer.Address,
            City = customer.City,
            State = customer.State,
            Status = customer.Status,
            CreatedDate = customer.CreatedDate,
            CreatedBy = customer.CreatedBy,
            CreatedByName = owner?.Email ?? owner?.UserName ?? customer.CreatedBy
        };
    }

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string SerializeCustomer(Customer customer) => JsonSerializer.Serialize(new
    {
        customer.CustomerId,
        customer.CustomerCode,
        customer.CustomerName,
        customer.Email,
        customer.Phone,
        customer.CompanyName,
        customer.Address,
        customer.City,
        customer.State,
        customer.Status,
        customer.CreatedDate,
        customer.CreatedBy
    });

    private async Task<ReportsViewModel> BuildReportAsync(string reportType, string? search, string? status, DateTime? fromDate, DateTime? toDate, string sort, int page, int pageSize = PageSize)
    {
        var types = new[] { "Customer", "Lead", "FollowUp", "Opportunity", "Pipeline", "LeadConversion", "UserActivity", "AuditLog" };
        var key = types.FirstOrDefault(item => string.Equals(item, reportType, StringComparison.OrdinalIgnoreCase)) ?? "Customer";
        var columns = new List<string>(); var rows = new List<ReportRowViewModel>(); var statusOptions = new List<string>();
        var from = fromDate?.Date; var to = toDate?.Date.AddDays(1);
        var pattern = string.IsNullOrWhiteSpace(search) ? null : $"%{search.Trim()}%";

        if (key == "Customer")
        {
            var query = AccessibleCustomers().AsNoTracking();
            if (pattern is not null) query = query.Where(item => EF.Functions.Like(item.CustomerName, pattern) || EF.Functions.Like(item.Email, pattern) || EF.Functions.Like(item.CompanyName!, pattern));
            if (!string.IsNullOrWhiteSpace(status)) query = query.Where(item => item.Status == status);
            if (from.HasValue) query = query.Where(item => item.CreatedDate >= from);
            if (to.HasValue) query = query.Where(item => item.CreatedDate < to);
            var data = sort == "name" ? await query.OrderBy(item => item.CustomerName).ToListAsync() : await query.OrderByDescending(item => item.CreatedDate).ToListAsync();
            columns.AddRange(["Customer", "Email", "Company", "Status", "Created"]); rows.AddRange(data.Select(item => Row(item.CustomerName, item.Email, item.CompanyName, item.Status, item.CreatedDate.ToString("yyyy-MM-dd")))); statusOptions.AddRange(CustomerStatuses);
        }
        else if (key is "Lead" or "LeadConversion")
        {
            var query = AccessibleLeads().AsNoTracking();
            if (pattern is not null) query = query.Where(item => EF.Functions.Like(item.LeadName, pattern) || EF.Functions.Like(item.Email, pattern) || EF.Functions.Like(item.CompanyName!, pattern));
            if (!string.IsNullOrWhiteSpace(status)) query = query.Where(item => item.Status == status);
            if (from.HasValue) query = query.Where(item => item.CreatedDate >= from);
            if (to.HasValue) query = query.Where(item => item.CreatedDate < to);
            if (key == "LeadConversion") query = query.Where(item => item.Status == "Converted");
            var data = sort == "name" ? await query.OrderBy(item => item.LeadName).ToListAsync() : await query.OrderByDescending(item => item.CreatedDate).ToListAsync();
            columns.AddRange(key == "LeadConversion" ? ["Lead", "Company", "Expected Value", "Status", "Converted/Created"] : ["Lead", "Email", "Company", "Source", "Status", "Created"]);
            rows.AddRange(data.Select(item => key == "LeadConversion" ? Row(item.LeadName, item.CompanyName, item.ExpectedValue.ToString("C"), item.Status, item.CreatedDate.ToString("yyyy-MM-dd")) : Row(item.LeadName, item.Email, item.CompanyName, item.Source, item.Status, item.CreatedDate.ToString("yyyy-MM-dd")))); statusOptions.AddRange(LeadStatuses);
        }
        else if (key == "FollowUp")
        {
            var query = AccessibleFollowUps().AsNoTracking();
            if (pattern is not null) query = query.Where(item => EF.Functions.Like(item.Remarks!, pattern) || EF.Functions.Like(item.Customer!.CustomerName, pattern) || EF.Functions.Like(item.Lead!.LeadName, pattern));
            if (!string.IsNullOrWhiteSpace(status)) query = query.Where(item => item.Status == status);
            if (from.HasValue) query = query.Where(item => item.FollowUpDate >= from);
            if (to.HasValue) query = query.Where(item => item.FollowUpDate < to);
            var data = await query.Include(item => item.Customer).Include(item => item.Lead).OrderByDescending(item => item.FollowUpDate).ToListAsync();
            columns.AddRange(["Date", "Customer", "Lead", "Type", "Status", "Remarks"]); rows.AddRange(data.Select(item => Row(item.FollowUpDate.ToString("yyyy-MM-dd HH:mm"), item.Customer?.CustomerName, item.Lead?.LeadName, item.FollowUpType, item.Status, item.Remarks))); statusOptions.AddRange(FollowUpStatuses);
        }
        else if (key is "Opportunity" or "Pipeline")
        {
            var query = AccessibleOpportunities().AsNoTracking();
            if (pattern is not null) query = query.Where(item => EF.Functions.Like(item.OpportunityName, pattern) || EF.Functions.Like(item.Customer.CustomerName, pattern));
            if (!string.IsNullOrWhiteSpace(status)) query = query.Where(item => item.Status == status);
            if (key == "Pipeline") query = query.Where(item => item.Status == "Open" && item.Stage != "Won" && item.Stage != "Lost");
            if (from.HasValue) query = query.Where(item => item.ExpectedCloseDate >= from);
            if (to.HasValue) query = query.Where(item => item.ExpectedCloseDate < to);
            var data = await query.Include(item => item.Customer).Include(item => item.Lead).OrderByDescending(item => item.ExpectedCloseDate).ToListAsync();
            columns.AddRange(["Opportunity", "Customer", "Lead", "Amount", "Probability", "Weighted Value", "Stage", "Status", "Expected Close"]);
            rows.AddRange(data.Select(item => Row(item.OpportunityName, item.Customer.CustomerName, item.Lead?.LeadName, item.Amount.ToString("C"), item.Probability.ToString("0.##"), (item.Amount * item.Probability / 100m).ToString("C"), item.Stage, item.Status, item.ExpectedCloseDate?.ToString("yyyy-MM-dd")))); statusOptions.AddRange(OpportunityStatuses);
        }
        else if (key == "UserActivity")
        {
            var query = AccessibleActivities().AsNoTracking();
            if (pattern is not null) query = query.Where(item => EF.Functions.Like(item.Subject, pattern) || EF.Functions.Like(item.Description!, pattern));
            if (!string.IsNullOrWhiteSpace(status)) query = query.Where(item => item.Status == status);
            if (from.HasValue) query = query.Where(item => item.ActivityDate >= from);
            if (to.HasValue) query = query.Where(item => item.ActivityDate < to);
            var data = await query.Include(item => item.Customer).Include(item => item.Lead).OrderByDescending(item => item.ActivityDate).ToListAsync();
            columns.AddRange(["Date", "Type", "Subject", "Customer", "Lead", "Status"]); rows.AddRange(data.Select(item => Row(item.ActivityDate.ToString("yyyy-MM-dd HH:mm"), item.ActivityType, item.Subject, item.Customer?.CustomerName, item.Lead?.LeadName, item.Status))); statusOptions.AddRange(["Open", "Completed", "Cancelled"]);
        }
        else
        {
            var query = _db.AuditLogs.AsNoTracking();
            if (pattern is not null) query = query.Where(item => EF.Functions.Like(item.Action, pattern) || EF.Functions.Like(item.EntityName, pattern) || EF.Functions.Like(item.UserId, pattern));
            if (!string.IsNullOrWhiteSpace(status)) query = query.Where(item => item.EntityName == status);
            if (from.HasValue) query = query.Where(item => item.CreatedDate >= from);
            if (to.HasValue) query = query.Where(item => item.CreatedDate < to);
            var data = await query.OrderByDescending(item => item.CreatedDate).ToListAsync();
            var ids = data.Select(item => item.UserId).Distinct().ToList(); var names = await _userManager.Users.Where(item => ids.Contains(item.Id)).ToDictionaryAsync(item => item.Id, item => item.Email ?? item.UserName ?? item.Id);
            columns.AddRange(["Timestamp", "User", "Action", "Entity", "Record", "IP Address"]); rows.AddRange(data.Select(item => Row(item.CreatedDate.ToString("yyyy-MM-dd HH:mm"), names.GetValueOrDefault(item.UserId, item.UserId), item.Action, item.EntityName, item.RecordId, item.IpAddress))); statusOptions.AddRange(await _db.AuditLogs.Select(item => item.EntityName).Distinct().OrderBy(item => item).ToListAsync());
        }

        page = Math.Max(1, page); var totalPages = Math.Max(1, (int)Math.Ceiling(rows.Count / (double)pageSize)); page = Math.Min(page, totalPages);
        return new ReportsViewModel { ReportType = key, Search = search, Status = status, FromDate = fromDate, ToDate = toDate, Sort = sort, Page = page, TotalPages = totalPages, IsAdmin = User.IsInRole(RoleNames.Admin), ReportTypes = ["Customer", "Lead", "FollowUp", "Opportunity", "Pipeline", "LeadConversion", "UserActivity", "AuditLog"], StatusOptions = statusOptions.Distinct().ToList(), Columns = columns, Rows = rows.Skip((page - 1) * pageSize).Take(pageSize).ToList() };
    }

    private IQueryable<Activity> AccessibleActivities() => User.IsInRole(RoleNames.Admin) || User.IsInRole(RoleNames.Manager) ? _db.Activities : _db.Activities.Where(item => item.AssignedTo == CurrentUserId);
    private static ReportRowViewModel Row(params string?[] values) => new() { Values = values.Select(value => value ?? string.Empty).ToList() };
    private static string CsvEscape(string value) => $"\"{value.Replace("\"", "\"\"")}\"";

    private ViewResult Module(string title) => View("ComingSoon", new CrmModuleViewModel
    {
        Title = title,
        Description = $"The {title} module is reserved for the next implementation phase."
    });
}
