using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StorEase.Application.Common;
using StorEase.Application.Features.Staff;
using StorEase.Application.Features.Staff.Dtos;
using StorEase.Domain.Enums;

namespace StorEase.Api.Controllers;

/// <summary>
/// Staff accounts, roles and facility assignment. System administrators manage everything; facility managers see
/// facility staff and assign them to the facilities they manage.
/// </summary>
[Route("api/staff")]
public class StaffController(IStaffAccountService accounts) : ApiControllerBase
{
    private const string AdminOrManager = Roles.SystemAdministrator + "," + Roles.FacilityManager;

    [HttpGet]
    [Authorize(Roles = AdminOrManager)]
    public async Task<ActionResult<PagedResult<StaffAccountDto>>> List(
        string? role, UserStatus? status, int page = 1, int pageSize = 20, CancellationToken ct = default)
    {
        var result = await accounts.ListAsync(role, status, page, pageSize, ct);
        return result.IsSuccess ? Ok(result.Value) : Failure(result.Error);
    }

    [HttpGet("{id:int}")]
    [Authorize(Roles = AdminOrManager)]
    public async Task<ActionResult<StaffAccountDto>> Get(int id, CancellationToken ct)
    {
        var result = await accounts.GetAsync(id, ct);
        return result.IsSuccess ? Ok(result.Value) : Failure(result.Error);
    }

    [HttpPost]
    [Authorize(Roles = Roles.SystemAdministrator)]
    public async Task<ActionResult<StaffAccountDto>> Create(CreateStaffAccountRequest request, CancellationToken ct)
    {
        var result = await accounts.CreateAsync(request, ct);
        return result.IsSuccess
            ? CreatedAtAction(nameof(Get), new { id = result.Value.UserId }, result.Value)
            : Failure(result.Error);
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = Roles.SystemAdministrator)]
    public async Task<ActionResult<StaffAccountDto>> Update(int id, UpdateStaffAccountRequest request, CancellationToken ct)
    {
        var result = await accounts.UpdateAsync(id, request, ct);
        return result.IsSuccess ? Ok(result.Value) : Failure(result.Error);
    }

    [HttpPut("{id:int}/facilities")]
    [Authorize(Roles = AdminOrManager)]
    public async Task<ActionResult<StaffAccountDto>> SetFacilities(
        int id, SetStaffFacilitiesRequest request, CancellationToken ct)
    {
        var result = await accounts.SetFacilitiesAsync(id, request, ct);
        return result.IsSuccess ? Ok(result.Value) : Failure(result.Error);
    }
}
