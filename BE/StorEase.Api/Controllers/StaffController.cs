using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StorEase.Application.Common;
using StorEase.Application.Features.Staff;
using StorEase.Application.Features.Staff.Dtos;
using StorEase.Domain.Enums;

namespace StorEase.Api.Controllers;

/// <summary>Staff accounts, roles and facility assignment. System administrators only.</summary>
[Route("api/staff")]
[Authorize(Roles = Roles.SystemAdministrator)]
public class StaffController(IStaffAccountService accounts) : ApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResult<StaffAccountDto>>> List(
        string? role, UserStatus? status, int page = 1, int pageSize = 20, CancellationToken ct = default)
    {
        var result = await accounts.ListAsync(role, status, page, pageSize, ct);
        return result.IsSuccess ? Ok(result.Value) : Failure(result.Error);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<StaffAccountDto>> Get(int id, CancellationToken ct)
    {
        var result = await accounts.GetAsync(id, ct);
        return result.IsSuccess ? Ok(result.Value) : Failure(result.Error);
    }

    [HttpPost]
    public async Task<ActionResult<StaffAccountDto>> Create(CreateStaffAccountRequest request, CancellationToken ct)
    {
        var result = await accounts.CreateAsync(request, ct);
        return result.IsSuccess
            ? CreatedAtAction(nameof(Get), new { id = result.Value.UserId }, result.Value)
            : Failure(result.Error);
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<StaffAccountDto>> Update(int id, UpdateStaffAccountRequest request, CancellationToken ct)
    {
        var result = await accounts.UpdateAsync(id, request, ct);
        return result.IsSuccess ? Ok(result.Value) : Failure(result.Error);
    }

    [HttpPut("{id:int}/facilities")]
    public async Task<ActionResult<StaffAccountDto>> SetFacilities(
        int id, SetStaffFacilitiesRequest request, CancellationToken ct)
    {
        var result = await accounts.SetFacilitiesAsync(id, request, ct);
        return result.IsSuccess ? Ok(result.Value) : Failure(result.Error);
    }
}
