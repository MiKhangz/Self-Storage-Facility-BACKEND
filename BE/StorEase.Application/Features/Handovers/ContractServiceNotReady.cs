using StorEase.Application.Common;
using StorEase.Application.Features.Contracts;

namespace StorEase.Application.Features.Handovers;

// ponytail: placeholder so handover compiles and answers 501 (rolling back) until Nhân's ContractService exists.
// Delete this file and swap its registration in DependencyInjection.cs when his implementation lands.
public class ContractServiceNotReady : IContractService
{
    public Task<Result<string>> ActivateAsync(int contractId, CancellationToken ct) =>
        Task.FromResult<Result<string>>(Error.NotImplemented("Contract activation is not implemented yet."));
}
