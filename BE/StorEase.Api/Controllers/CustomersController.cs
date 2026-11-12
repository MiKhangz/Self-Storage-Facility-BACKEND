using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StorEase.Application.Common;
using StorEase.Application.Features.Reservations;

namespace StorEase.Api.Controllers;

[Route("api/customers")]
public class CustomersController(IWalkInService walkIns) : ApiControllerBase
{
    /// <summary>Customer accounts matching a name, email or phone — used at the desk for walk-in bookings.</summary>
    [HttpGet]
    [Authorize(Roles = Roles.Staff)]
    public async Task<ActionResult<PagedResult<CustomerSummaryDto>>> Search(string? search, int page = 1, int pageSize = 20, CancellationToken ct = default)
    {
        var result = await walkIns.SearchCustomersAsync(search, page, pageSize, ct);
        return result.IsSuccess ? Ok(result.Value) : Failure(result.Error);
    }

    /// <summary>One customer's contact details; 404 for a staff account or an unknown id.</summary>
    [HttpGet("{id:int}")]
    [Authorize(Roles = Roles.Staff + "," + Roles.BusinessOperationsManager + "," + Roles.SystemAdministrator)]
    public async Task<ActionResult<CustomerSummaryDto>> Get(int id, CancellationToken ct)
    {
        var result = await walkIns.GetCustomerAsync(id, ct);
        return result.IsSuccess ? Ok(result.Value) : Failure(result.Error);
    }
}
