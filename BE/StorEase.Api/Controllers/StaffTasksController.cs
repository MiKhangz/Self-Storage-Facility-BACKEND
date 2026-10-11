using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StorEase.Application.Common;
using StorEase.Application.Features.Staff;
using StorEase.Application.Features.Staff.Dtos;
using StorEase.Domain.Enums;

namespace StorEase.Api.Controllers;

[Route("api/staff-tasks")]
public class StaffTasksController(IStaffTaskService tasks) : ApiControllerBase
{
    private const string Readers = Roles.Staff + "," + Roles.BusinessOperationsManager + "," + Roles.SystemAdministrator;

    [HttpPost]
    [Authorize(Roles = Roles.FacilityManager)]
    public async Task<ActionResult<StaffTaskDto>> Create(CreateStaffTaskRequest request, CancellationToken ct)
    {
        var result = await tasks.CreateAsync(request, ct);
        return result.IsSuccess
            ? CreatedAtAction(nameof(Get), new { id = result.Value.TaskId }, result.Value)
            : Failure(result.Error);
    }

    [HttpGet]
    [Authorize(Roles = Readers)]
    public async Task<ActionResult<PagedResult<StaffTaskDto>>> List(
        [FromQuery] int? shiftId, [FromQuery] int? assignedTo, [FromQuery] StaffTaskStatus? status,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var result = await tasks.ListAsync(shiftId, assignedTo, status, page, pageSize, ct);
        return result.IsSuccess ? Ok(result.Value) : Failure(result.Error);
    }

    [HttpGet("{id:int}")]
    [Authorize(Roles = Readers)]
    public async Task<ActionResult<StaffTaskDto>> Get(int id, CancellationToken ct)
    {
        var result = await tasks.GetAsync(id, ct);
        return result.IsSuccess ? Ok(result.Value) : Failure(result.Error);
    }

    [HttpPost("{id:int}/status")]
    [Authorize(Roles = Roles.Staff)]
    public async Task<ActionResult<StaffTaskDto>> ChangeStatus(
        int id, ChangeStaffTaskStatusRequest request, CancellationToken ct)
    {
        var result = await tasks.ChangeStatusAsync(id, request, ct);
        return result.IsSuccess ? Ok(result.Value) : Failure(result.Error);
    }
}
