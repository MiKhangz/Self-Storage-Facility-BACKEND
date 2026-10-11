using StorEase.Application.Common;
using StorEase.Application.Features.AccessCodes.Dtos;
using StorEase.Domain.Entities;

namespace StorEase.Application.Features.AccessCodes;

public interface IAccessCodeService
{
    /// <summary>Staff re-issue a code for an Active contract; the previous active code is revoked.</summary>
    Task<Result<AccessCodeDto>> IssueAsync(int contractId, CancellationToken ct);

    /// <summary>
    /// For services already inside a transaction (handover): revokes the contract's active codes and adds a new one.
    /// Does not save. <paramref name="contract"/> must have <c>AccessCodes</c> loaded. Returns the plaintext once.
    /// </summary>
    (AccessCode Entity, string Code) Issue(Contract contract);
}
