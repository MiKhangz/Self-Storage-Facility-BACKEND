using StorEase.Application.Common;
using StorEase.Application.Features.Contracts.Dtos;
using StorEase.Domain.Enums;

namespace StorEase.Application.Features.Contracts;

/// <summary>Owned by Nhân. Khang's handover calls <see cref="ActivateAsync"/>; add the rest of the contract API here.</summary>
public interface IContractService
{
    /// <summary>Staff see any contract; a customer only their own (others get 404).</summary>
    Task<Result<ContractDto>> GetAsync(int contractId, CancellationToken ct);

    Task<Result<PagedResult<ContractDto>>> ListAsync(ContractStatus? status, int page, int pageSize, CancellationToken ct);

    /// <summary>Records the calling customer's e-signature on their own Draft contract.</summary>
    Task<Result<ContractDto>> SignAsync(int contractId, CancellationToken ct);

    /// <summary>
    /// Moves a signed <c>Draft</c> contract to <c>Active</c> and issues a new access code, revoking any previous one.
    /// Called inside the handover's transaction, so it must not commit on its own.
    /// Returns the plaintext access code; only its hash is stored.
    /// </summary>
    Task<Result<string>> ActivateAsync(int contractId, CancellationToken ct);
}
