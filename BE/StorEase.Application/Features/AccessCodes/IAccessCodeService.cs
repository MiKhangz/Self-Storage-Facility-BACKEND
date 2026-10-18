using StorEase.Application.Common;
using StorEase.Application.Features.AccessCodes.Dtos;
using StorEase.Domain.Entities;

namespace StorEase.Application.Features.AccessCodes;

public interface IAccessCodeService
{
    /// <summary>Staff, or the owning customer, issue a code for an Active contract; the previous active code is revoked.</summary>
    Task<Result<AccessCodeDto>> IssueAsync(int contractId, CancellationToken ct);

    /// <summary>Deactivates one code (lockout, move-out, reported lost). 409 when it is already inactive.</summary>
    Task<Result<AccessCodeSummaryDto>> RevokeAsync(int accessCodeId, CancellationToken ct);

    /// <summary>The contract's current valid code, without the plaintext. 404 when none, or not the caller's contract.</summary>
    Task<Result<AccessCodeSummaryDto>> GetActiveAsync(int contractId, CancellationToken ct);

    /// <summary>
    /// For services already inside a transaction (check-in): revokes the contract's active codes and adds a new one.
    /// Does not save. <paramref name="contract"/> must have <c>AccessCodes</c> loaded. Returns the plaintext once.
    /// </summary>
    (AccessCode Entity, string Code) Issue(Contract contract);
}
