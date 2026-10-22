using Microsoft.EntityFrameworkCore;
using StorEase.Application.Common;
using StorEase.Application.Features.Contracts;
using StorEase.Application.Features.Handovers.Dtos;
using StorEase.Application.Interfaces;
using StorEase.Domain.Entities;
using StorEase.Domain.Enums;

namespace StorEase.Application.Features.Handovers;

public interface IHandoverService
{
    Task<Result<HandoverDto>> CreateAsync(CreateHandoverRequest request, CancellationToken ct);

    /// <summary>
    /// SR-02: undo a handover. The contract is terminated and its codes revoked, the unit goes back to Available and
    /// the reservation to Confirmed without a unit, so staff assign a unit again and redraft the contract.
    /// </summary>
    Task<Result<bool>> CancelAsync(int handoverId, CancellationToken ct);
}

/// <summary>Business rule 3: handover, not signing, activates the contract and occupies the unit.</summary>
public class HandoverService(IUnitOfWork uow, ICurrentUser currentUser, IContractService contracts) : IHandoverService
{
    public Task<Result<bool>> CancelAsync(int handoverId, CancellationToken ct) =>
        uow.InTransactionAsync<bool>(async () =>
        {
            var facilityIds = StaffScope.FacilityIdsOf(uow, currentUser.UserId);
            var handover = await uow.Repository<Handover>().Query()
                .Include(c => c.Reservation).Include(c => c.StorageUnit)
                .FirstOrDefaultAsync(c => c.HandoverId == handoverId && facilityIds.Contains(c.Reservation.FacilityId), ct);
            if (handover is null)
                return Error.NotFound("Handover not found.");
            if (handover.Status == HandoverStatus.Cancelled)
                return Error.Conflict("The handover is already cancelled.");

            var contract = handover.ContractId is { } contractId
                ? await uow.Repository<Contract>().Query().Include(c => c.AccessCodes).FirstOrDefaultAsync(c => c.ContractId == contractId, ct)
                : null;
            // Once the rental has moved on (overdue, being handed back, closed) undoing the handover would rewrite history.
            if (contract is not null && contract.Status != ContractStatus.Active)
                return Error.Conflict($"The contract is {contract.Status}; only the handover of an Active contract can be cancelled.");

            handover.Status = HandoverStatus.Cancelled;
            if (contract is not null)
            {
                contract.Status = ContractStatus.Terminated;
                foreach (var code in contract.AccessCodes.Where(a => a.IsActive))
                    code.IsActive = false;
            }
            handover.StorageUnit.Status = StorageUnitStatus.Available;
            handover.Reservation.Status = ReservationStatus.Confirmed;
            handover.Reservation.StorageUnitId = null;
            return true;
        }, ct);

    public Task<Result<HandoverDto>> CreateAsync(CreateHandoverRequest request, CancellationToken ct) =>
        uow.InTransactionAsync<HandoverDto>(async () =>
        {
            var reservation = await uow.Repository<Reservation>().FindAsync(request.ReservationId, ct);
            // Staff hand over only at facilities they work at; elsewhere the reservation does not exist for them.
            if (reservation is null || !await StaffScope.FacilityIdsOf(uow, currentUser.UserId).AnyAsync(id => id == reservation.FacilityId, ct))
                return Error.NotFound("Reservation not found.");
            if (reservation.Status != ReservationStatus.Confirmed || reservation.StorageUnitId is null)
                return Error.Conflict($"The reservation must be Confirmed with a unit assigned; it is {reservation.Status}.");

            // The deposit comes before the keys (DEMO_PLAN step order); no deposit invoice means none was due.
            var depositOpen = await uow.Repository<Invoice>().Query().AnyAsync(i => i.ReservationId == reservation.ReservationId
                && i.ContractId == null && i.Status != InvoiceStatus.Paid && i.Status != InvoiceStatus.WrittenOff, ct);
            if (depositOpen)
                return Error.Conflict("The deposit has not been paid yet.");

            var contract = await uow.Repository<Contract>().FindAsync(request.ContractId, ct);
            if (contract is null)
                return Error.NotFound("Contract not found.");
            if (contract.ReservationId != reservation.ReservationId || contract.StorageUnitId != reservation.StorageUnitId)
                return Error.Conflict("The contract does not belong to this reservation and unit.");
            if (contract.Status != ContractStatus.Draft)
                return Error.Conflict($"Only a Draft contract can be handed over; this one is {contract.Status}.");
            if (contract.SignedAt is null)
                return Error.Conflict("The customer has not signed the contract yet.");

            var unit = (await uow.Repository<StorageUnit>().FindAsync(reservation.StorageUnitId.Value, ct))!;
            if (unit.Status != StorageUnitStatus.Reserved)
                return Error.Conflict($"Unit {unit.UnitCode} is {unit.Status}, expected Reserved.");

            var handover = new Handover
            {
                ReservationId = reservation.ReservationId,
                ContractId = contract.ContractId,
                StorageUnitId = unit.StorageUnitId,
                StaffId = currentUser.UserId,
                HandoverTime = DateTime.UtcNow,
                LockSerial = request.LockSerial,
                CardNumber = request.CardNumber,
                ConditionNote = request.ConditionNote,
                Status = HandoverStatus.Completed,
            };
            uow.Repository<Handover>().Add(handover);
            reservation.Status = ReservationStatus.HandedOver;
            unit.Status = StorageUnitStatus.Occupied;

            var accessCode = await contracts.ActivateAsync(contract.ContractId, ct);
            if (!accessCode.IsSuccess)
                return accessCode.Error;

            await uow.SaveChangesAsync(ct);
            return new HandoverDto(
                handover.HandoverId, handover.ReservationId, contract.ContractId, unit.StorageUnitId, unit.UnitCode,
                handover.HandoverTime, request.LockSerial, request.CardNumber, request.ConditionNote,
                handover.Status, accessCode.Value);
        }, ct);
}
