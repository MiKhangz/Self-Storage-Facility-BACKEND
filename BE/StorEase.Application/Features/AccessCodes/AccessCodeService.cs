using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using StorEase.Application.Common;
using StorEase.Application.Features.AccessCodes.Dtos;
using StorEase.Application.Interfaces;
using StorEase.Domain.Entities;
using StorEase.Domain.Enums;

namespace StorEase.Application.Features.AccessCodes;

public class AccessCodeService(IUnitOfWork uow, IPasswordHasher hasher, ICurrentUser currentUser, ICodeProtector protector) : IAccessCodeService
{
    // Staff may issue for any contract; a customer only for their own (someone else's is a 404).
    public Task<Result<AccessCodeDto>> IssueAsync(int contractId, CancellationToken ct) =>
        uow.InTransactionAsync<AccessCodeDto>(async () =>
        {
            var contract = await VisibleContracts().Include(c => c.AccessCodes)
                .FirstOrDefaultAsync(c => c.ContractId == contractId, ct);
            if (contract is null)
                return Error.NotFound("Contract not found.");
            if (contract.Status != ContractStatus.Active)
                return Error.Conflict($"Access codes are issued for an Active contract; this one is {contract.Status}.");

            var (entity, code) = Issue(contract);
            await uow.SaveChangesAsync(ct); // the id is only known after the insert
            return new AccessCodeDto(entity.AccessCodeId, contract.ContractId, code, entity.IssuedAt, entity.ExpiresAt);
        }, ct);

    public Task<Result<AccessCodeSummaryDto>> RevokeAsync(int accessCodeId, CancellationToken ct) =>
        uow.InTransactionAsync<AccessCodeSummaryDto>(async () =>
        {
            var code = await uow.Repository<AccessCode>().Query()
                .Include(a => a.Contract).ThenInclude(c => c.StorageUnit)
                .FirstOrDefaultAsync(a => a.AccessCodeId == accessCodeId, ct);
            if (code is null || !await CanSeeContractAsync(code.Contract, ct))
                return Error.NotFound("Access code not found.");
            if (!code.IsActive)
                return Error.Conflict("The access code is already revoked.");

            code.IsActive = false;
            return ToSummary(code);
        }, ct);

    public async Task<Result<AccessCodeSummaryDto>> GetActiveAsync(int contractId, CancellationToken ct)
    {
        var contract = await VisibleContracts().AsNoTracking()
            .Include(c => c.AccessCodes).FirstOrDefaultAsync(c => c.ContractId == contractId, ct);
        if (contract is null)
            return Error.NotFound("Contract not found.");

        // An expired code is no use to the customer even while its flag is still set.
        var active = contract.AccessCodes.Where(a => a.IsActive && a.ExpiresAt > DateTime.UtcNow)
            .OrderByDescending(a => a.IssuedAt).FirstOrDefault();
        if (active is null)
            return Error.NotFound("The contract has no active access code.");
        // Only the renter reads the digits back; staff find a renter by the code the customer says.
        return ToSummary(active) with { Code = currentUser.IsCustomer && active.CodeCipher is { } c ? protector.Unprotect(c) : null };
    }

    /// <summary>
    /// Revokes the contract's active codes and adds a new one, valid until the contract ends (business rule 9).
    /// The caller owns the transaction and the save. <paramref name="contract"/> must have <c>AccessCodes</c> loaded.
    /// </summary>
    public (AccessCode Entity, string Code) Issue(Contract contract)
    {
        foreach (var old in contract.AccessCodes.Where(a => a.IsActive))
            old.IsActive = false;

        // The desk finds a renter by code alone, so two renters at one facility must never share one.
        var facilityId = uow.Repository<StorageUnit>().Query()
            .Where(u => u.StorageUnitId == contract.StorageUnitId).Select(u => u.FacilityId).First();
        var taken = uow.Repository<AccessCode>().Query()
            .Where(a => a.IsActive && a.CodeCipher != null && a.ContractId != contract.ContractId
                && a.Contract.StorageUnit.FacilityId == facilityId)
            .Select(a => a.CodeCipher!).AsEnumerable().Select(protector.Unprotect).OfType<string>().ToHashSet();
        string code;
        do code = RandomNumberGenerator.GetInt32(100_000, 1_000_000).ToString();
        while (taken.Contains(code));
        var issuedAt = DateTime.UtcNow;
        var entity = new AccessCode
        {
            ContractId = contract.ContractId,
            CodeHash = hasher.Hash(code),
            CodeCipher = protector.Protect(code),
            IssuedAt = issuedAt,
            ExpiresAt = contract.EndDate.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
            IsActive = true,
        };
        contract.AccessCodes.Add(entity);
        return (entity, code);
    }

    private static AccessCodeSummaryDto ToSummary(AccessCode a) =>
        new(a.AccessCodeId, a.ContractId, a.IssuedAt, a.ExpiresAt, a.IsActive);

    private IQueryable<Contract> VisibleContracts()
    {
        var query = uow.Repository<Contract>().Query();
        if (currentUser.IsCustomer)
            return query.Where(c => c.CustomerId == currentUser.UserId);
        if (StaffScope.SeesAllFacilities(currentUser.Role))
            return query;
        var facilityIds = StaffScope.FacilityIdsOf(uow, currentUser.UserId);
        return query.Where(c => facilityIds.Contains(c.StorageUnit.FacilityId));
    }

    private async Task<bool> CanSeeContractAsync(Contract contract, CancellationToken ct) =>
        currentUser.IsCustomer
            ? contract.CustomerId == currentUser.UserId
            : StaffScope.SeesAllFacilities(currentUser.Role)
                || await StaffScope.FacilityIdsOf(uow, currentUser.UserId)
                    .AnyAsync(id => id == contract.StorageUnit.FacilityId, ct);
}
