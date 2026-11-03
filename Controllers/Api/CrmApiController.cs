using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AcxiomCRM.Api;
using AcxiomCRM.Security;
using AcxiomCRM.Services;

namespace AcxiomCRM.Controllers.Api;

/// <summary>Secure JSON endpoints for the CRM resources.</summary>
[ApiController, Authorize(Policy = AuthorizationPolicies.CrmAccess), Route("api")]
[Produces("application/json")]
public sealed class CrmApiController(ICrmApiService service) : ControllerBase
{
    [HttpGet("customers"), ProducesResponseType(typeof(PagedResponse<CustomerResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<CustomerResponse>>> GetCustomers(string? search, string? status, int page = 1, int pageSize = 20, CancellationToken ct = default) => await Execute(() => service.GetCustomersAsync(User, search, status, page, pageSize, ct));

    [HttpGet("customers/{id:int}"), ProducesResponseType(typeof(CustomerResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<CustomerResponse>> GetCustomer(int id, CancellationToken ct = default) { var result = await service.GetCustomerAsync(User, id, ct); return result is null ? NotFoundProblem("Customer was not found.") : Ok(result); }

    [HttpPost("customers"), ProducesResponseType(typeof(CustomerResponse), StatusCodes.Status201Created)]
    public async Task<ActionResult<CustomerResponse>> CreateCustomer(CreateCustomerRequest request, CancellationToken ct = default) { try { var result = await service.CreateCustomerAsync(User, request, HttpContext.Connection.RemoteIpAddress?.ToString(), ct); return CreatedAtAction(nameof(GetCustomer), new { id = result.CustomerId }, result); } catch (ApiServiceException ex) { return Problem(statusCode: ex.StatusCode, title: ex.StatusCode == 409 ? "Conflict" : "Bad request", detail: ex.Message); } }

    [HttpPut("customers/{id:int}"), ProducesResponseType(typeof(CustomerResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<CustomerResponse>> UpdateCustomer(int id, UpdateCustomerRequest request, CancellationToken ct = default) { try { var result = await service.UpdateCustomerAsync(User, id, request, HttpContext.Connection.RemoteIpAddress?.ToString(), ct); return result is null ? NotFoundProblem("Customer was not found.") : Ok(result); } catch (ApiServiceException ex) { return Problem(statusCode: ex.StatusCode, title: ex.StatusCode == 409 ? "Conflict" : "Bad request", detail: ex.Message); } }

    [HttpDelete("customers/{id:int}"), ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteCustomer(int id, CancellationToken ct = default) { try { return await service.DeleteCustomerAsync(User, id, HttpContext.Connection.RemoteIpAddress?.ToString(), ct) ? NoContent() : NotFoundProblem("Customer was not found."); } catch (ApiServiceException ex) { return Problem(statusCode: ex.StatusCode, title: "Conflict", detail: ex.Message); } }

    [HttpGet("leads"), ProducesResponseType(typeof(PagedResponse<LeadResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<LeadResponse>>> GetLeads(string? search, string? status, int page = 1, int pageSize = 20, CancellationToken ct = default) => await Execute(() => service.GetLeadsAsync(User, search, status, page, pageSize, ct));
    [HttpGet("leads/{id:int}"), ProducesResponseType(typeof(LeadResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<LeadResponse>> GetLead(int id, CancellationToken ct = default) { var result = await service.GetLeadAsync(User, id, ct); return result is null ? NotFoundProblem("Lead was not found.") : Ok(result); }
    [HttpPost("leads"), ProducesResponseType(typeof(LeadResponse), StatusCodes.Status201Created)]
    public async Task<ActionResult<LeadResponse>> CreateLead(CreateLeadRequest request, CancellationToken ct = default) { try { var result = await service.CreateLeadAsync(User, request, Ip, ct); return CreatedAtAction(nameof(GetLead), new { id = result.LeadId }, result); } catch (ApiServiceException ex) { return Problem(statusCode: ex.StatusCode, title: ex.StatusCode == 409 ? "Conflict" : "Bad request", detail: ex.Message); } }
    [HttpPut("leads/{id:int}"), ProducesResponseType(typeof(LeadResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<LeadResponse>> UpdateLead(int id, UpdateLeadRequest request, CancellationToken ct = default) { try { var result = await service.UpdateLeadAsync(User, id, request, Ip, ct); return result is null ? NotFoundProblem("Lead was not found.") : Ok(result); } catch (ApiServiceException ex) { return Problem(statusCode: ex.StatusCode, title: ex.StatusCode == 409 ? "Conflict" : "Bad request", detail: ex.Message); } }
    [HttpDelete("leads/{id:int}"), ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteLead(int id, CancellationToken ct = default) { try { return await service.DeleteLeadAsync(User, id, Ip, ct) ? NoContent() : NotFoundProblem("Lead was not found."); } catch (ApiServiceException ex) { return Problem(statusCode: ex.StatusCode, title: "Conflict", detail: ex.Message); } }

    [HttpGet("opportunities"), ProducesResponseType(typeof(PagedResponse<OpportunityResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<OpportunityResponse>>> GetOpportunities(string? search, string? stage, string? status, int page = 1, int pageSize = 20, CancellationToken ct = default) => await Execute(() => service.GetOpportunitiesAsync(User, search, stage, status, page, pageSize, ct));
    [HttpGet("opportunities/{id:int}"), ProducesResponseType(typeof(OpportunityResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<OpportunityResponse>> GetOpportunity(int id, CancellationToken ct = default) { var result = await service.GetOpportunityAsync(User, id, ct); return result is null ? NotFoundProblem("Opportunity was not found.") : Ok(result); }
    [HttpPost("opportunities"), ProducesResponseType(typeof(OpportunityResponse), StatusCodes.Status201Created)]
    public async Task<ActionResult<OpportunityResponse>> CreateOpportunity(CreateOpportunityRequest request, CancellationToken ct = default) { try { var result = await service.CreateOpportunityAsync(User, request, Ip, ct); return CreatedAtAction(nameof(GetOpportunity), new { id = result.OpportunityId }, result); } catch (ApiServiceException ex) { return Problem(statusCode: ex.StatusCode, title: ex.StatusCode == 409 ? "Conflict" : "Bad request", detail: ex.Message); } }
    [HttpPut("opportunities/{id:int}"), ProducesResponseType(typeof(OpportunityResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<OpportunityResponse>> UpdateOpportunity(int id, UpdateOpportunityRequest request, CancellationToken ct = default) { try { var result = await service.UpdateOpportunityAsync(User, id, request, Ip, ct); return result is null ? NotFoundProblem("Opportunity was not found.") : Ok(result); } catch (ApiServiceException ex) { return Problem(statusCode: ex.StatusCode, title: ex.StatusCode == 409 ? "Conflict" : "Bad request", detail: ex.Message); } }
    [HttpDelete("opportunities/{id:int}"), ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteOpportunity(int id, CancellationToken ct = default) { try { return await service.DeleteOpportunityAsync(User, id, Ip, ct) ? NoContent() : NotFoundProblem("Opportunity was not found."); } catch (ApiServiceException ex) { return Problem(statusCode: ex.StatusCode, title: "Conflict", detail: ex.Message); } }

    [HttpGet("follow-ups"), ProducesResponseType(typeof(PagedResponse<FollowUpResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<FollowUpResponse>>> GetFollowUps(string? status, int page = 1, int pageSize = 20, CancellationToken ct = default) => await Execute(() => service.GetFollowUpsAsync(User, status, page, pageSize, ct));
    [HttpGet("follow-ups/{id:int}"), ProducesResponseType(typeof(FollowUpResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<FollowUpResponse>> GetFollowUp(int id, CancellationToken ct = default) { var result = await service.GetFollowUpAsync(User, id, ct); return result is null ? NotFoundProblem("Follow-up was not found.") : Ok(result); }
    [HttpPost("follow-ups"), ProducesResponseType(typeof(FollowUpResponse), StatusCodes.Status201Created)]
    public async Task<ActionResult<FollowUpResponse>> CreateFollowUp(CreateFollowUpRequest request, CancellationToken ct = default) { try { var result = await service.CreateFollowUpAsync(User, request, Ip, ct); return CreatedAtAction(nameof(GetFollowUp), new { id = result.FollowUpId }, result); } catch (ApiServiceException ex) { return Problem(statusCode: ex.StatusCode, title: ex.StatusCode == 409 ? "Conflict" : "Bad request", detail: ex.Message); } }
    [HttpPut("follow-ups/{id:int}"), ProducesResponseType(typeof(FollowUpResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<FollowUpResponse>> UpdateFollowUp(int id, UpdateFollowUpRequest request, CancellationToken ct = default) { try { var result = await service.UpdateFollowUpAsync(User, id, request, Ip, ct); return result is null ? NotFoundProblem("Follow-up was not found.") : Ok(result); } catch (ApiServiceException ex) { return Problem(statusCode: ex.StatusCode, title: ex.StatusCode == 409 ? "Conflict" : "Bad request", detail: ex.Message); } }
    [HttpDelete("follow-ups/{id:int}"), ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteFollowUp(int id, CancellationToken ct = default) { try { return await service.DeleteFollowUpAsync(User, id, Ip, ct) ? NoContent() : NotFoundProblem("Follow-up was not found."); } catch (ApiServiceException ex) { return Problem(statusCode: ex.StatusCode, title: "Conflict", detail: ex.Message); } }

    private string? Ip => HttpContext.Connection.RemoteIpAddress?.ToString();
    private async Task<ActionResult<T>> Execute<T>(Func<Task<T>> action) { try { return Ok(await action()); } catch (ApiServiceException ex) { return Problem(statusCode: ex.StatusCode, title: ex.StatusCode == 409 ? "Conflict" : "Bad request", detail: ex.Message); } }
    private NotFoundObjectResult NotFoundProblem(string detail) => NotFound(new ProblemDetails { Title = "Not found", Detail = detail, Status = StatusCodes.Status404NotFound, Instance = HttpContext.Request.Path });
}
