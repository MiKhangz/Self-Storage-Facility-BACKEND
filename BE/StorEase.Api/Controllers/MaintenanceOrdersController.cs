using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StorEase.Application.Common;
using StorEase.Application.Features.Maintenance;
using StorEase.Application.Features.Maintenance.Dtos;
using StorEase.Domain.Enums;

namespace StorEase.Api.Controllers;

[Route("api/maintenance-orders")]
public class MaintenanceOrdersController(IMaintenanceService orders) : ApiControllerBase
{
    private const string Readers = Roles.Staff + "," + Roles.BusinessOperationsManager + "," + Roles.SystemAdministrator;

    [HttpPost]
    [Authorize(Roles = Roles.Staff)]
    public async Task<ActionResult<MaintenanceOrderDto>> Create(CreateMaintenanceOrderRequest request, CancellationToken ct)
    {
        var result = await orders.CreateAsync(request, ct);
        return result.IsSuccess
            ? CreatedAtAction(nameof(Get), new { id = result.Value.MaintenanceOrderId }, result.Value)
            : Failure(result.Error);
    }

    [HttpGet]
    [Authorize(Roles = Readers)]
    public async Task<ActionResult<PagedResult<MaintenanceOrderDto>>> List(
        [FromQuery] MaintenanceOrderStatus? status, [FromQuery] int? storageUnitId,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var result = await orders.ListAsync(status, storageUnitId, page, pageSize, ct);
        return result.IsSuccess ? Ok(result.Value) : Failure(result.Error);
    }

    [HttpGet("{id:int}")]
    [Authorize(Roles = Readers)]
    public async Task<ActionResult<MaintenanceOrderDto>> Get(int id, CancellationToken ct)
    {
        var result = await orders.GetAsync(id, ct);
        return result.IsSuccess ? Ok(result.Value) : Failure(result.Error);
    }

    [HttpPost("{id:int}/status")]
    [Authorize(Roles = Roles.Staff)]
    public async Task<ActionResult<MaintenanceOrderDto>> ChangeStatus(
        int id, ChangeMaintenanceStatusRequest request, CancellationToken ct)
    {
        var result = await orders.ChangeStatusAsync(id, request, ct);
        return result.IsSuccess ? Ok(result.Value) : Failure(result.Error);
    }
}
