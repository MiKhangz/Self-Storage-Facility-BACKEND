using Microsoft.EntityFrameworkCore;
using StorEase.Application.Common;
using StorEase.Application.Features.Contracts.Dtos;
using StorEase.Application.Interfaces;
using StorEase.Domain.Entities;
using StorEase.Domain.Enums;

namespace StorEase.Application.Features.Contracts;

public class ContractService(IUnitOfWork uow, ICurrentUser currentUser) : IContractService
{
    public async Task<Result<ContractDto>> GetAsync(int contractId, CancellationToken ct)
    {
        var dto = await Visible().Where(c => c.ContractId == contractId).ToDto().FirstOrDefaultAsync(ct);
        // Someone else's contract is a 404, not a 403, so ids cannot be probed.
        return dto is null ? Error.NotFound("Contract not found.") : dto;
    }

    public async Task<Result<PagedResult<ContractDto>>> ListAsync(
        ContractStatus? status, int page, int pageSize, CancellationToken ct)
    {
        var query = Visible();
        if (status is not null)
            query = query.Where(c => c.Status == status);

        return await query.OrderByDescending(c => c.ContractId).ToDto().ToPagedResultAsync(page, pageSize, ct);
    }

    public async Task<Result<ContractDto>> SignAsync(int contractId, CancellationToken ct)
    {
        var signed = await uow.InTransactionAsync<int>(async () =>
        {
            var contract = await Visible().FirstOrDefaultAsync(c => c.ContractId == contractId, ct);
            if (contract is null)
                return Error.NotFound("Contract not found.");
            if (contract.Status != ContractStatus.Draft)
                return Error.Conflict($"Only a Draft contract can be signed; this one is {contract.Status}.");
            if (contract.SignedAt is not null)
                return Error.Conflict("The contract is already signed.");

            // Signing does not activate: the handover does (business rule 3).
            contract.SignedAt = DateTime.UtcNow;
            return contract.ContractId;
        }, ct);

        return signed.IsSuccess ? await GetAsync(signed.Value, ct) : signed.Error;
    }

    public Task<Result<string>> ActivateAsync(int contractId, CancellationToken ct) =>
        Task.FromResult<Result<string>>(Error.NotImplemented("Contract activation is not implemented yet."));

    private IQueryable<Contract> Visible()
    {
        var query = uow.Repository<Contract>().Query();
        if (!currentUser.IsCustomer)
            return query;

        var customerId = currentUser.UserId;
        return query.Where(c => c.CustomerId == customerId);
    }
}
