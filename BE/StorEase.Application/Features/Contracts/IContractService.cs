using StorEase.Application.Common;

namespace StorEase.Application.Features.Contracts;

/// <summary>Owned by Nhân. Khang's handover calls <see cref="ActivateAsync"/>; add the rest of the contract API here.</summary>
public interface IContractService
{
    /// <summary>
    /// Moves a signed <c>Draft</c> contract to <c>Active</c> and issues a new access code, revoking any previous one.
    /// Called inside the handover's transaction, so it must not commit on its own.
    /// Returns the plaintext access code; only its hash is stored.
    /// </summary>
    Task<Result<string>> ActivateAsync(int contractId, CancellationToken ct);
}
