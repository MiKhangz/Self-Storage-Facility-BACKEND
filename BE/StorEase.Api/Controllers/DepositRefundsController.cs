using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StorEase.Application.Common;
using StorEase.Application.Features.DepositRefunds;
using StorEase.Domain.Enums;

namespace StorEase.Api.Controllers;

/// <summary>Deposits owed back after a cancellation or a return, and when they were paid.</summary>
[Route("api/deposit-refunds")]
public class DepositRefundsController(IDepositRefundService refunds) : ApiControllerBase
{
    /// <summary>Customers: their own. Facility managers: their facilities. Business operations, admin: all.</summary>
    [HttpGet]
    [Authorize(Roles = Roles.StorageCustomer + "," + Roles.FacilityManager + "," + Roles.BusinessOperationsManager + "," + Roles.SystemAdministrator)]
    public async Task<ActionResult<PagedResult<DepositRefundDto>>> List(
        DepositRefundStatus? status, int page = 1, int pageSize = 20, CancellationToken ct = default)
    {
        var result = await refunds.ListAsync(status, page, pageSize, ct);
        return result.IsSuccess ? Ok(result.Value) : Failure(result.Error);
    }

    /// <summary>Business operations record that the customer has the money back; 409 if already paid.</summary>
    [HttpPost("{id:int}/paid")]
    [Authorize(Roles = Roles.BusinessOperationsManager)]
    public async Task<ActionResult<DepositRefundDto>> MarkPaid(int id, MarkDepositRefundPaidRequest request, CancellationToken ct)
    {
        var result = await refunds.MarkPaidAsync(id, request, ct);
        return result.IsSuccess ? Ok(result.Value) : Failure(result.Error);
    }
}
