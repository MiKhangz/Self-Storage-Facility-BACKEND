using Microsoft.EntityFrameworkCore;
using StorEase.Application.Common;
using StorEase.Application.Features.Reservations.Dtos;
using StorEase.Application.Interfaces;
using StorEase.Domain.Entities;
using StorEase.Domain.Enums;

namespace StorEase.Application.Features.Reservations;

public interface IReservationService
{
    Task<Result<ReservationDto>> CreateAsync(CreateReservationRequest request, CancellationToken ct);
    Task<Result<ReservationDto>> GetAsync(int reservationId, CancellationToken ct);
    Task<Result<PagedResult<ReservationDto>>> ListAsync(ReservationStatus? status, int page, int pageSize, CancellationToken ct);
    Task<Result<ReservationDto>> AssignUnitAsync(int reservationId, AssignUnitRequest request, CancellationToken ct);
}

public class ReservationService(IUnitOfWork uow, ICurrentUser currentUser) : IReservationService
{
    public async Task<Result<ReservationDto>> CreateAsync(CreateReservationRequest request, CancellationToken ct)
    {
        var facilityOpen = await uow.Repository<Facility>().Query()
            .AnyAsync(f => f.FacilityId == request.FacilityId && f.Status == FacilityStatus.Active, ct);
        if (!facilityOpen)
            return Error.NotFound("Facility not found.");

        if (!await uow.Repository<UnitType>().Query().AnyAsync(t => t.UnitTypeId == request.UnitTypeId, ct))
            return Error.NotFound("Unit type not found.");

        // Reserving does not hold a unit (staff assigns one later), but refuse a reservation that could never be served.
        var anyAvailable = await uow.Repository<StorageUnit>().Query().AnyAsync(u =>
            u.FacilityId == request.FacilityId &&
            u.UnitTypeId == request.UnitTypeId &&
            u.Status == StorageUnitStatus.Available, ct);
        if (!anyAvailable)
            return Error.Conflict("No unit of this type is available at this facility.");

        var reservation = new Reservation
        {
            CustomerId = currentUser.UserId,
            FacilityId = request.FacilityId,
            UnitTypeId = request.UnitTypeId,
            ReservationCode = NewReservationCode(),
            MoveInDate = request.MoveInDate,
            RentalMonths = request.RentalMonths,
            Status = ReservationStatus.Pending,
            CreatedAt = DateTime.UtcNow,
        };
        uow.Repository<Reservation>().Add(reservation);
        await uow.SaveChangesAsync(ct);

        return await GetAsync(reservation.ReservationId, ct);
    }

    public async Task<Result<ReservationDto>> GetAsync(int reservationId, CancellationToken ct)
    {
        var dto = await Project(Visible().Where(r => r.ReservationId == reservationId)).FirstOrDefaultAsync(ct);
        // A customer asking for someone else's reservation gets 404, not 403, so ids cannot be probed.
        return dto is null ? Error.NotFound("Reservation not found.") : dto;
    }

    public async Task<Result<PagedResult<ReservationDto>>> ListAsync(
        ReservationStatus? status, int page, int pageSize, CancellationToken ct)
    {
        var query = Visible();
        if (status is not null)
            query = query.Where(r => r.Status == status);

        return await Project(query.OrderByDescending(r => r.CreatedAt)).ToPagedResultAsync(page, pageSize, ct);
    }

    public async Task<Result<ReservationDto>> AssignUnitAsync(
        int reservationId, AssignUnitRequest request, CancellationToken ct)
    {
        var assigned = await uow.InTransactionAsync<int>(async () =>
        {
            var reservation = await uow.Repository<Reservation>().FindAsync(reservationId, ct);
            if (reservation is null)
                return Error.NotFound("Reservation not found.");
            if (reservation.Status != ReservationStatus.Pending)
                return Error.Conflict($"Only a Pending reservation can be assigned a unit; this one is {reservation.Status}.");

            var unit = await uow.Repository<StorageUnit>().FindAsync(request.StorageUnitId, ct);
            if (unit is null)
                return Error.NotFound("Storage unit not found.");
            if (unit.FacilityId != reservation.FacilityId || unit.UnitTypeId != reservation.UnitTypeId)
                return Error.Conflict("The unit does not match the reservation's facility and unit type.");
            if (unit.Status != StorageUnitStatus.Available)
                return Error.Conflict($"Unit {unit.UnitCode} is {unit.Status}, not Available.");

            // Business rule 1: a Draft contract already claims its unit.
            var unitTaken = await uow.Repository<Contract>().Query().AnyAsync(c =>
                c.StorageUnitId == unit.StorageUnitId &&
                (c.Status == ContractStatus.Active || c.Status == ContractStatus.Draft), ct);
            if (unitTaken)
                return Error.Conflict($"Unit {unit.UnitCode} already has an Active or Draft contract.");

            // ponytail: no row-version check, so two staff assigning the same unit at the same instant could both pass; add a concurrency token on StorageUnit if that ever matters.
            reservation.StorageUnitId = unit.StorageUnitId;
            reservation.Status = ReservationStatus.Confirmed;
            unit.Status = StorageUnitStatus.Reserved;
            return reservation.ReservationId;
        }, ct);

        return assigned.IsSuccess ? await GetAsync(assigned.Value, ct) : assigned.Error;
    }

    private IQueryable<Reservation> Visible()
    {
        var query = uow.Repository<Reservation>().Query();
        if (!currentUser.IsCustomer)
            return query;

        var customerId = currentUser.UserId;
        return query.Where(r => r.CustomerId == customerId);
    }

    private IQueryable<ReservationDto> Project(IQueryable<Reservation> source)
    {
        var contracts = uow.Repository<Contract>().Query();
        return source.Select(r => new ReservationDto(
            r.ReservationId,
            r.ReservationCode,
            r.CustomerId,
            r.Customer.FullName,
            r.FacilityId,
            r.Facility.Name,
            r.UnitTypeId,
            r.UnitType.Name,
            r.StorageUnitId,
            r.StorageUnit != null ? r.StorageUnit.UnitCode : null,
            r.MoveInDate,
            r.RentalMonths,
            r.Status,
            r.CreatedAt,
            contracts.Where(c => c.ReservationId == r.ReservationId)
                .OrderByDescending(c => c.ContractId)
                .Select(c => (int?)c.ContractId)
                .FirstOrDefault()));
    }

    // ponytail: random suffix, ~1-in-a-million clash per day; the unique index turns a clash into a 500. Switch to a sequence if volume grows.
    private static string NewReservationCode() => $"RS{DateTime.UtcNow:yyMMdd}{Random.Shared.Next(1_000_000):D6}";
}
