using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using StorEase.Application.Common;
using StorEase.Application.Features.AccessCodes.Dtos;
using StorEase.Application.Interfaces;
using StorEase.Domain.Entities;
using StorEase.Domain.Enums;

namespace StorEase.Application.Features.AccessCodes;

public class AccessCodeService(IUnitOfWork uow, IPasswordHasher hasher) : IAccessCodeService
{
    public Task<Result<AccessCodeDto>> IssueAsync(int contractId, CancellationToken ct) =>
        uow.InTransactionAsync<AccessCodeDto>(async () =>
        {
            var contract = await uow.Repository<Contract>().Query()
                .Include(c => c.AccessCodes).FirstOrDefaultAsync(c => c.ContractId == contractId, ct);
            if (contract is null)
                return Error.NotFound("Contract not found.");
            if (contract.Status != ContractStatus.Active)
                return Error.Conflict($"Access codes are issued for an Active contract; this one is {contract.Status}.");

            var (entity, code) = Issue(contract);
            await uow.SaveChangesAsync(ct); // the id is only known after the insert
            return new AccessCodeDto(entity.AccessCodeId, contract.ContractId, code, entity.IssuedAt, entity.ExpiresAt);
        }, ct);

    /// <summary>
    /// Revokes the contract's active codes and adds a new one, valid until the contract ends (business rule 9).
    /// The caller owns the transaction and the save. <paramref name="contract"/> must have <c>AccessCodes</c> loaded.
    /// </summary>
    public (AccessCode Entity, string Code) Issue(Contract contract)
    {
        foreach (var old in contract.AccessCodes.Where(a => a.IsActive))
            old.IsActive = false;

        var code = RandomNumberGenerator.GetInt32(100_000, 1_000_000).ToString();
        var issuedAt = DateTime.UtcNow;
        var entity = new AccessCode
        {
            ContractId = contract.ContractId,
            CodeHash = hasher.Hash(code),
            IssuedAt = issuedAt,
            ExpiresAt = contract.EndDate.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
            IsActive = true,
        };
        contract.AccessCodes.Add(entity);
        return (entity, code);
    }
}
