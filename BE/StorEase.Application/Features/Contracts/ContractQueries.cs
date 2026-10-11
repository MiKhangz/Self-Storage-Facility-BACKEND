using StorEase.Application.Features.Contracts.Dtos;
using StorEase.Domain.Entities;

namespace StorEase.Application.Features.Contracts;

internal static class ContractQueries
{
    public static IQueryable<ContractDto> ToDto(this IQueryable<Contract> source) => source.Select(c => new ContractDto(
        c.ContractId, c.ContractNumber, c.ReservationId, c.CustomerId, c.Customer.FullName,
        c.StorageUnitId, c.StorageUnit.UnitCode, c.StartDate, c.EndDate,
        c.MonthlyRent, c.DepositAmount, c.PaymentDay, c.SignedAt, c.Status));
}
