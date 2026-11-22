using Microsoft.EntityFrameworkCore;
using StorEase.Application.Common;
using StorEase.Application.Features.DepositRefunds;
using StorEase.Application.Features.Invoices;
using StorEase.Application.Features.Notifications;
using StorEase.Application.Features.Pricing;
using StorEase.Application.Features.StorageUnits;
using StorEase.Application.Features.Reservations.Dtos;
using StorEase.Application.Interfaces;
using StorEase.Domain.Entities;
using StorEase.Domain.Enums;

namespace StorEase.Application.Features.Reservations;

public interface IReservationService
{
    Task<Result<ReservationDto>> CreateAsync(CreateReservationRequest request, CancellationToken ct);
    Task<Result<ReservationDto>> GetAsync(int reservationId, CancellationToken ct);

    /// <summary>
    /// The reservation queue. <paramref name="search"/> matches the code, customer name, phone or email; the
    /// move-in range bounds are inclusive.
    /// </summary>
    Task<Result<PagedResult<ReservationDto>>> ListAsync(
        ReservationStatus? status, string? search, int? facilityId, DateOnly? moveInFrom, DateOnly? moveInTo,
        int page, int pageSize, CancellationToken ct);

    Task<Result<ReservationDto>> AssignUnitAsync(int reservationId, AssignUnitRequest request, CancellationToken ct);

    /// <summary>At the handover the customer saw the unit and wants another of the same size.</summary>
    Task<Result<ReservationDto>> ChangeUnitAsync(int reservationId, AssignUnitRequest request, CancellationToken ct);

    /// <summary>Customer (own) or staff cancel a Pending/Confirmed reservation; the held unit is released.</summary>
    Task<Result<ReservationCancellationDto>> CancelAsync(int reservationId, CancelReservationRequest request, CancellationToken ct);

    /// <summary>Staff mark a reservation whose move-in date has passed as a no-show; the deposit is forfeited.</summary>
    Task<Result<ReservationCancellationDto>> MarkNoShowAsync(int reservationId, CancellationToken ct);

    Task<Result<HandoverChecklistDto>> GetHandoverChecklistAsync(int reservationId, CancellationToken ct);

    /// <summary>The customer agrees to the unit staff proposed instead of the expected one.</summary>
    Task<Result<ReservationDto>> AcceptProposedUnitAsync(int reservationId, CancellationToken ct);

    /// <summary>The customer turns the proposed unit down: the booking is cancelled and the whole deposit refunded.</summary>
    Task<Result<ReservationCancellationDto>> DeclineProposedUnitAsync(int reservationId, CancellationToken ct);

    /// <summary>Customer (own) or staff set the handover appointment; missing it forfeits the deposit.</summary>
    Task<Result<ReservationDto>> SetAppointmentAsync(int reservationId, SetAppointmentRequest request, CancellationToken ct);

    /// <summary>
    /// Daily job: bookings whose appointment day (or move-in day) has passed without a handover expire and the deposit is
    /// forfeited, unless StorEase had no unit ready: then an early appointment is cleared, and after move-in day the
    /// booking is cancelled with the whole deposit back. Bookings starting soon whose held unit is blocked are flagged
    /// for staff and offered another free unit. Returns (expired as no-show, proposed).
    /// </summary>
    Task<(int Expired, int Proposed)> RunDailyAsync(DateOnly today, int lookAheadDays, CancellationToken ct);
}

public class ReservationService(IUnitOfWork uow, ICurrentUser currentUser, IEmailService email) : IReservationService
{
    public async Task<Result<ReservationDto>> CreateAsync(CreateReservationRequest request, CancellationToken ct)
    {
        var facility = await uow.Repository<Facility>().Query()
            .FirstOrDefaultAsync(f => f.FacilityId == request.FacilityId && f.Status == FacilityStatus.Active, ct);
        if (facility is null)
            return Error.NotFound("Facility not found.");
        // Nobody would assign the unit, hand it over or answer the customer there.
        if (!await StaffScope.StaffedFacilityIds(uow).AnyAsync(id => id == facility.FacilityId, ct))
            return Error.Conflict("This facility is not taking bookings yet: no staff work there.");

        if (!await uow.Repository<UnitType>().Query().AnyAsync(t => t.UnitTypeId == request.UnitTypeId, ct))
            return Error.NotFound("Unit type not found.");

        // The booking holds the first unit free for the whole stay (the "expected unit"); staff confirm it later.
        var lastDay = UnitSchedule.EndOf(request.MoveInDate, request.RentalMonths);
        var free = await UnitSchedule.FreeUnitsAsync(uow, request.FacilityId, request.UnitTypeId, request.MoveInDate, lastDay, ct);
        var unplaced = await UnitSchedule.UnplacedBookingsAsync(uow, request.FacilityId, request.UnitTypeId, request.MoveInDate, lastDay, ct);
        if (free.Count <= unplaced)
            return Error.Conflict("No unit of this type is available at this facility.");
        var expected = free[0];

        // Without a policy there is no price, so the deposit is skipped rather than blocking the booking.
        var terms = await RentCalculator.TermsAsync(uow, request.FacilityId, request.UnitTypeId, request.MoveInDate, request.RentalMonths, ct);

        var created = await uow.InTransactionAsync<int>(async () =>
        {
            var reservation = new Reservation
            {
                CustomerId = currentUser.UserId,
                FacilityId = request.FacilityId,
                UnitTypeId = request.UnitTypeId,
                StorageUnitId = expected.StorageUnitId,
                ReservationCode = NewReservationCode(),
                MoveInDate = request.MoveInDate,
                RentalMonths = request.RentalMonths,
                // The version this quote came from; the contract is drafted from it even if prices change before move-in.
                PricingPolicyId = terms.IsSuccess ? terms.Value.Policy.PricingPolicyId : null,
                FacilityPriceVersionId = terms.IsSuccess ? terms.Value.FacilityPriceVersionId : null,
                Status = ReservationStatus.Pending,
                CreatedAt = DateTime.UtcNow,
            };
            uow.Repository<Reservation>().Add(reservation);
            // A unit free today is held now; one still rented until before move-in is held when its renter leaves.
            if (expected.Status == StorageUnitStatus.Available)
                expected.Status = StorageUnitStatus.Reserved;
            await uow.SaveChangesAsync(ct);

            if (terms.IsSuccess)
                AddDepositInvoice(uow, reservation, terms.Value.Deposit);
            return reservation.ReservationId;
        }, ct);
        if (!created.IsSuccess)
            return created.Error;

        var dto = await GetAsync(created.Value, ct);
        if (dto.IsSuccess)
            await NotifyAsync(dto.Value, "Đã nhận yêu cầu đặt kho",
                $"Chúng tôi đã nhận yêu cầu đặt kho {dto.Value.UnitTypeName} tại {facility.Name}, dọn vào ngày {dto.Value.MoveInDate:dd/MM/yyyy}. " +
                $"Kho dự kiến: {expected.UnitCode} (nhân viên xác nhận trước ngày dọn vào)." +
                (dto.Value.DepositAmount is { } amount ? $"\nVui lòng thanh toán tiền cọc {Money.Text(amount)} ₫ trước ngày dọn vào." : ""), ct);
        return dto;
    }

    public async Task<Result<ReservationDto>> GetAsync(int reservationId, CancellationToken ct)
    {
        var dto = await Project(Visible().Where(r => r.ReservationId == reservationId)).FirstOrDefaultAsync(ct);
        // A customer asking for someone else's reservation gets 404, not 403, so ids cannot be probed.
        return dto is null ? Error.NotFound("Reservation not found.") : dto;
    }

    public async Task<Result<PagedResult<ReservationDto>>> ListAsync(
        ReservationStatus? status, string? search, int? facilityId, DateOnly? moveInFrom, DateOnly? moveInTo,
        int page, int pageSize, CancellationToken ct)
    {
        var query = Visible();
        if (status is not null)
            query = query.Where(r => r.Status == status);
        if (facilityId is not null)
            query = query.Where(r => r.FacilityId == facilityId);
        if (moveInFrom is not null)
            query = query.Where(r => r.MoveInDate >= moveInFrom);
        if (moveInTo is not null)
            query = query.Where(r => r.MoveInDate <= moveInTo);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(r => r.ReservationCode.Contains(term) || r.Customer.FullName.Contains(term)
                || r.Customer.PhoneNumber.Contains(term) || r.Customer.Email.Contains(term));
        }

        return await Project(query.OrderByDescending(r => r.CreatedAt)).ToPagedResultAsync(page, pageSize, ct);
    }

    public async Task<Result<ReservationDto>> AssignUnitAsync(
        int reservationId, AssignUnitRequest request, CancellationToken ct)
    {
        var assigned = await uow.InTransactionAsync<(int Id, bool Proposed)>(async () =>
        {
            var reservation = await Visible().FirstOrDefaultAsync(r => r.ReservationId == reservationId, ct);
            if (reservation is null)
                return Error.NotFound("Reservation not found.");
            // A Confirmed reservation without a unit is one whose handover was cancelled (SR-02); it is assigned again.
            var awaitingUnit = reservation.Status == ReservationStatus.Pending
                || (reservation.Status == ReservationStatus.Confirmed && reservation.StorageUnitId is null);
            if (!awaitingUnit)
                return Error.Conflict($"Only a reservation waiting for a unit can be assigned one; this one is {reservation.Status}.");

            var unit = await uow.Repository<StorageUnit>().FindAsync(request.StorageUnitId, ct);
            if (unit is null)
                return Error.NotFound("Storage unit not found.");
            if (unit.FacilityId != reservation.FacilityId || unit.UnitTypeId != reservation.UnitTypeId)
                return Error.Conflict("The unit does not match the reservation's facility and unit type.");
            if (unit.Status is StorageUnitStatus.Maintenance or StorageUnitStatus.Retired)
                return Error.Conflict($"Unit {unit.UnitCode} cannot be booked while it is {unit.Status}.");
            // Free for the whole stay: rented now is fine if the renter leaves before move-in.
            if (!await UnitSchedule.IsFreeAsync(uow, unit.StorageUnitId, reservation.MoveInDate,
                    UnitSchedule.EndOf(reservation.MoveInDate, reservation.RentalMonths), ct, exceptReservationId: reservation.ReservationId))
                return Error.Conflict($"Unit {unit.UnitCode} is booked or rented for part of this stay.");

            // The customer was told another unit when booking: they must agree to the change.
            if (reservation.StorageUnitId is { } held && held != unit.StorageUnitId)
            {
                reservation.ProposedStorageUnitId = unit.StorageUnitId;
                reservation.ProposedAt = DateTime.UtcNow;
                return (reservation.ReservationId, true);
            }

            // ponytail: no row-version check, so two staff assigning the same unit at the same instant could both pass; add a concurrency token on StorageUnit if that ever matters.
            if (reservation.StorageUnitId != unit.StorageUnitId)
                reservation.UnitBlocked = false;
            reservation.StorageUnitId = unit.StorageUnitId;
            reservation.Status = ReservationStatus.Confirmed;
            reservation.ProposedStorageUnitId = null;
            if (unit.Status == StorageUnitStatus.Available)
                unit.Status = StorageUnitStatus.Reserved;
            return (reservation.ReservationId, false);
        }, ct);

        if (!assigned.IsSuccess)
            return assigned.Error;
        var dto = await GetAsync(assigned.Value.Id, ct);
        if (dto.IsSuccess)
        {
            if (assigned.Value.Proposed)
                await NotifyProposalAsync(dto.Value, ct);
            else
                await NotifyAsync(dto.Value, "Đặt kho đã được xác nhận",
                    $"Đơn {dto.Value.ReservationCode} đã được xác nhận với kho {dto.Value.UnitCode} tại {dto.Value.FacilityName}. " +
                    $"Hẹn gặp bạn ngày {dto.Value.MoveInDate:dd/MM/yyyy}.", ct);
        }
        return dto;
    }

    /// <summary>
    /// The old unit goes back on sale and the new one is held. A drafted contract moves with the booking; if the
    /// customer had already signed it, the signature is cleared because it named the old unit.
    /// </summary>
    public async Task<Result<ReservationDto>> ChangeUnitAsync(
        int reservationId, AssignUnitRequest request, CancellationToken ct)
    {
        var changed = await uow.InTransactionAsync<int>(async () =>
        {
            var reservation = await Visible().Include(r => r.StorageUnit)
                .FirstOrDefaultAsync(r => r.ReservationId == reservationId, ct);
            if (reservation is null)
                return Error.NotFound("Reservation not found.");
            if (reservation.Status != ReservationStatus.Confirmed || reservation.StorageUnit is null)
                return Error.Conflict("Only a confirmed booking with a unit, not yet handed over, can change its unit.");
            if (reservation.StorageUnitId == request.StorageUnitId)
                return Error.Conflict("The booking already holds this unit.");

            var unit = await uow.Repository<StorageUnit>().FindAsync(request.StorageUnitId, ct);
            if (unit is null)
                return Error.NotFound("Storage unit not found.");
            if (unit.FacilityId != reservation.FacilityId || unit.UnitTypeId != reservation.UnitTypeId)
                return Error.Conflict("The new unit must be the same size, at the same facility.");
            // The customer is at the desk and moves in now, so the new unit must be free today and for the stay.
            if (unit.Status != StorageUnitStatus.Available)
                return Error.Conflict($"Unit {unit.UnitCode} is {unit.Status}, not Available.");
            if (!await UnitSchedule.IsFreeAsync(uow, unit.StorageUnitId, reservation.MoveInDate,
                    UnitSchedule.EndOf(reservation.MoveInDate, reservation.RentalMonths), ct, exceptReservationId: reservation.ReservationId))
                return Error.Conflict($"Unit {unit.UnitCode} is booked or rented for part of this stay.");

            var oldUnit = reservation.StorageUnit;
            unit.Status = StorageUnitStatus.Reserved;
            reservation.StorageUnitId = unit.StorageUnitId;
            // The customer chose at the desk, so an offer still open is out of date.
            reservation.ProposedStorageUnitId = null;
            reservation.ProposedAt = null;
            reservation.UnitBlocked = false;

            await MoveDraftAsync(reservationId, unit.StorageUnitId, ct);
            await uow.SaveChangesAsync(ct);
            if (oldUnit.Status == StorageUnitStatus.Reserved)
                await UnitSchedule.ReleaseAsync(uow, oldUnit, ct);
            return reservation.ReservationId;
        }, ct);
        return changed.IsSuccess ? await GetAsync(changed.Value, ct) : changed.Error;
    }

    public async Task<Result<ReservationDto>> AcceptProposedUnitAsync(int reservationId, CancellationToken ct)
    {
        var accepted = await uow.InTransactionAsync<int>(async () =>
        {
            var reservation = await Visible().Include(r => r.StorageUnit).Include(r => r.ProposedStorageUnit)
                .FirstOrDefaultAsync(r => r.ReservationId == reservationId, ct);
            if (reservation is null)
                return Error.NotFound("Reservation not found.");
            if (reservation.ProposedStorageUnit is not { } proposed)
                return Error.Conflict("No other unit is waiting for your answer.");
            if (reservation.Status is not (ReservationStatus.Pending or ReservationStatus.Confirmed))
                return Error.Conflict($"This booking is {reservation.Status}.");
            if (!await UnitSchedule.IsFreeAsync(uow, proposed.StorageUnitId, reservation.MoveInDate,
                    UnitSchedule.EndOf(reservation.MoveInDate, reservation.RentalMonths), ct, exceptReservationId: reservation.ReservationId))
                return Error.Conflict($"Unit {proposed.UnitCode} was taken meanwhile; staff will offer another.");

            var oldUnit = reservation.StorageUnit;
            reservation.StorageUnitId = proposed.StorageUnitId;
            reservation.ProposedStorageUnitId = null;
            reservation.ProposedAt = null;
            reservation.UnitBlocked = false;
            reservation.Status = ReservationStatus.Confirmed;
            if (proposed.Status == StorageUnitStatus.Available)
                proposed.Status = StorageUnitStatus.Reserved;
            await MoveDraftAsync(reservationId, proposed.StorageUnitId, ct);
            await uow.SaveChangesAsync(ct);
            if (oldUnit is { Status: StorageUnitStatus.Reserved })
                await UnitSchedule.ReleaseAsync(uow, oldUnit, ct);
            return reservation.ReservationId;
        }, ct);
        return accepted.IsSuccess ? await GetAsync(accepted.Value, ct) : accepted.Error;
    }

    /// <summary>
    /// The drafted contract follows the booking to its new unit, unsigned because it named the old one. When that unit
    /// still carries another contract until move-in (one contract at a time on a unit), the draft ends instead and
    /// staff draw it up again.
    /// </summary>
    private async Task MoveDraftAsync(int reservationId, int unitId, CancellationToken ct)
    {
        var contracts = uow.Repository<Contract>().Query();
        var draft = await contracts.FirstOrDefaultAsync(c => c.ReservationId == reservationId
            && c.Status == ContractStatus.Draft && c.StorageUnitId != unitId, ct);
        if (draft is null)
            return;
        if (await UnitSchedule.HoldingContracts(uow).AnyAsync(c => c.StorageUnitId == unitId, ct))
        {
            draft.Status = ContractStatus.Terminated;
            return;
        }
        draft.StorageUnitId = unitId;
        draft.SignedAt = null;
    }

    public async Task<Result<ReservationCancellationDto>> DeclineProposedUnitAsync(int reservationId, CancellationToken ct)
    {
        var reservation = await Visible().AsNoTracking().FirstOrDefaultAsync(r => r.ReservationId == reservationId, ct);
        if (reservation is null)
            return Error.NotFound("Reservation not found.");
        if (reservation.ProposedStorageUnitId is null)
            return Error.Conflict("No other unit is waiting for your answer.");
        return await CloseAsync(reservationId, noShow: false, ct, refundAnyway: true);
    }

    public async Task<Result<ReservationDto>> SetAppointmentAsync(int reservationId, SetAppointmentRequest request, CancellationToken ct)
    {
        var reservation = await Visible().FirstOrDefaultAsync(r => r.ReservationId == reservationId, ct);
        if (reservation is null)
            return Error.NotFound("Reservation not found.");
        if (reservation.Status is not (ReservationStatus.Pending or ReservationStatus.Confirmed))
            return Error.Conflict($"Only a booking not yet handed over can get an appointment; this one is {reservation.Status}.");
        if (await InvoiceBook.DepositOpenAsync(uow, reservationId, ct))
            return Error.Conflict("The deposit has not been paid yet.");
        // A missed appointment cannot be moved, or the no-show rule could be dodged.
        if (reservation.HandoverSlot < DateTime.UtcNow)
            return Error.Conflict("The appointment time has passed, so it can no longer be changed.");
        // Stored in UTC like every other moment; the rules read it in Vietnam time.
        var at = VietnamTime.ToUtc(request.At);
        var day = VietnamTime.DayOf(at);
        if (at < DateTime.UtcNow)
            return Error.Validation("The appointment time has already passed.");
        if (day < VietnamTime.Today || day > reservation.MoveInDate)
            return Error.Validation("The appointment must be between today and the move-in date.");
        if (at.AddHours(7).Hour is < 7 or >= 21)
            return Error.Validation("The appointment must be during opening hours (07:00–21:00).");
        reservation.HandoverSlot = at;
        await uow.SaveChangesAsync(ct);
        return await GetAsync(reservationId, ct);
    }

    public async Task<(int Expired, int Proposed)> RunDailyAsync(DateOnly today, int lookAheadDays, CancellationToken ct)
    {
        runningAsJob = true;
        // Missed appointments: the deposit is forfeited, as the customer was told when booking.
        var open = await uow.Repository<Reservation>().Query()
            .Where(r => r.Status == ReservationStatus.Pending || r.Status == ReservationStatus.Confirmed)
            .Select(r => new { r.ReservationId, r.MoveInDate, r.HandoverSlot })
            .ToListAsync(ct);
        var missed = open.Where(r => (r.HandoverSlot is { } s ? VietnamTime.DayOf(s) : r.MoveInDate) < today)
            .Select(r => r.ReservationId).ToList();
        var expired = 0;
        foreach (var id in missed)
        {
            var r = await uow.Repository<Reservation>().Query().Include(x => x.StorageUnit).FirstAsync(x => x.ReservationId == id, ct);
            if (!await MissIsOursAsync(r, ct))
            {
                if ((await CloseAsync(id, noShow: true, ct, today: today)).IsSuccess)
                    expired++;
            }
            else if (today <= r.MoveInDate)
                await ClearAppointmentAsync(r, ct); // an early appointment: the booking can still be served by move-in day
            else
                await CloseAsync(id, noShow: false, ct, refundAnyway: true);
        }

        // Bookings starting soon with no usable unit (its renter stays on, it is under repair): flag them for staff and
        // offer another free unit.
        var soon = await uow.Repository<Reservation>().Query()
            .Where(r => (r.Status == ReservationStatus.Pending || r.Status == ReservationStatus.Confirmed)
                && r.MoveInDate <= today.AddDays(lookAheadDays))
            .ToListAsync(ct);
        var proposed = 0;
        foreach (var r in soon)
        {
            var lastDay = UnitSchedule.EndOf(r.MoveInDate, r.RentalMonths);
            if (r.ProposedStorageUnitId is { } offered
                && !await UnitSchedule.IsFreeAsync(uow, offered, r.MoveInDate, lastDay, ct, exceptReservationId: r.ReservationId))
            {
                // That offer can no longer be accepted.
                r.ProposedStorageUnitId = null;
                r.ProposedAt = null;
            }
            var blocked = await IsBlockedAsync(r, ct);
            r.UnitBlocked = blocked || (r.UnitBlocked && HandoverDay(r) <= today);
            await uow.SaveChangesAsync(ct);
            // Without a held unit staff assign one; with an offer open the customer answers first.
            if (!blocked || r.StorageUnitId is null || r.ProposedStorageUnitId is not null)
                continue;
            var free = await UnitSchedule.FreeUnitsAsync(uow, r.FacilityId, r.UnitTypeId, r.MoveInDate, lastDay, ct, exceptReservationId: r.ReservationId);
            if (free.Count == 0)
                continue; // staff see the flag on Reservations and call the customer
            r.ProposedStorageUnitId = free[0].StorageUnitId;
            r.ProposedAt = DateTime.UtcNow;
            await uow.SaveChangesAsync(ct);
            proposed++;
            if (await GetAsync(r.ReservationId, ct) is { IsSuccess: true } dto)
                await NotifyProposalAsync(dto.Value, ct);
        }
        return (expired, proposed);
    }

    /// <summary>StorEase has no usable unit for the booking: none held, an offer unanswered, or the held one busy or unusable.</summary>
    private async Task<bool> IsBlockedAsync(Reservation r, CancellationToken ct) =>
        r.ProposedStorageUnitId is not null || r.StorageUnitId is not { } held
        || !await UnitSchedule.IsFreeAsync(uow, held, r.MoveInDate, UnitSchedule.EndOf(r.MoveInDate, r.RentalMonths), ct, exceptReservationId: r.ReservationId);

    /// <summary>A missed handover is no no-show when the unit was not ready or somebody still rents it.</summary>
    private async Task<bool> MissIsOursAsync(Reservation r, CancellationToken ct) =>
        r.UnitBlocked || r.StorageUnit is { Status: StorageUnitStatus.Occupied } || await IsBlockedAsync(r, ct);

    private async Task ClearAppointmentAsync(Reservation r, CancellationToken ct)
    {
        r.HandoverSlot = null;
        await uow.SaveChangesAsync(ct);
        if (await GetAsync(r.ReservationId, ct) is { IsSuccess: true } dto)
            await NotifyAsync(dto.Value, "Cần đặt lại lịch hẹn nhận kho",
                $"Kho của đơn {r.ReservationCode} chưa sẵn sàng vào lịch hẹn, nên lịch hẹn đã được bỏ và bạn không mất tiền cọc. " +
                $"Vui lòng đặt lịch mới, muộn nhất là ngày {r.MoveInDate:dd/MM/yyyy}.", ct);
    }

    /// <summary>Last day a no-show can still come: the appointment day, or the move-in day when none was set.</summary>
    private static DateOnly HandoverDay(Reservation r) => r.HandoverSlot is { } s ? VietnamTime.DayOf(s) : r.MoveInDate;

    private Task NotifyProposalAsync(ReservationDto dto, CancellationToken ct) =>
        NotifyAsync(dto, "Cần bạn xác nhận kho mới",
            $"Kho dự kiến {dto.UnitCode} của đơn {dto.ReservationCode} không còn trống cho thời gian thuê của bạn. " +
            $"Chúng tôi đề xuất kho {dto.ProposedUnitCode}. Vui lòng đồng ý trên trang đơn đặt kho, hoặc huỷ đơn để được hoàn toàn bộ tiền cọc.", ct);

    public Task<Result<ReservationCancellationDto>> CancelAsync(
        int reservationId, CancelReservationRequest request, CancellationToken ct) =>
        CloseAsync(reservationId, noShow: false, ct);

    public Task<Result<ReservationCancellationDto>> MarkNoShowAsync(int reservationId, CancellationToken ct) =>
        CloseAsync(reservationId, noShow: true, ct);

    /// <param name="refundAnyway">StorEase could not give the unit promised (offer turned down, or not ready by the handover day), so the deposit comes back in full.</param>
    private async Task<Result<ReservationCancellationDto>> CloseAsync(int reservationId, bool noShow, CancellationToken ct, bool refundAnyway = false, DateOnly? today = null)
    {
        var closed = await uow.InTransactionAsync<(int Id, decimal Paid, bool Refundable, bool OurFault)>(async () =>
        {
            var reservation = await Visible().Include(r => r.StorageUnit)
                .FirstOrDefaultAsync(r => r.ReservationId == reservationId, ct);
            if (reservation is null)
                return Error.NotFound("Reservation not found.");
            if (reservation.Status is not (ReservationStatus.Pending or ReservationStatus.Confirmed))
                return Error.Conflict($"Only a Pending or Confirmed reservation can be closed; this one is {reservation.Status}.");
            if (noShow && HandoverDay(reservation) >= (today ?? VietnamTime.Today))
                return Error.Conflict("A reservation can only be marked as a no-show after its handover appointment day.");
            if (noShow && await MissIsOursAsync(reservation, ct))
                return Error.Conflict("The unit was not ready for this customer, so this is not a no-show; cancel the booking to refund the deposit.");
            var ourFault = !noShow && (refundAnyway || reservation.UnitBlocked || await IsBlockedAsync(reservation, ct));

            reservation.UnitBlocked = false;
            reservation.Status = noShow ? ReservationStatus.Expired : ReservationStatus.Cancelled;
            reservation.ProposedStorageUnitId = null;
            await uow.SaveChangesAsync(ct);
            if (reservation.StorageUnit is { Status: StorageUnitStatus.Reserved } unit)
                await UnitSchedule.ReleaseAsync(uow, unit, ct);

            // A Draft contract still claims the unit (business rule 1), so it ends with the reservation.
            var drafts = await uow.Repository<Contract>().Query()
                .Where(c => c.ReservationId == reservationId && c.Status == ContractStatus.Draft).ToListAsync(ct);
            foreach (var draft in drafts)
                draft.Status = ContractStatus.Terminated;

            var deposit = await uow.Repository<Invoice>().Query()
                .FirstOrDefaultAsync(i => i.ReservationId == reservationId && i.ContractId == null, ct);
            var paid = deposit?.PaidAmount ?? 0m;
            if (deposit is not null && paid == 0 && deposit.Status != InvoiceStatus.Paid)
                deposit.Status = InvoiceStatus.WrittenOff;

            var refundable = false;
            if (paid > 0 && !noShow)
            {
                // The free-cancellation window the customer was quoted when booking.
                var policy = await RentCalculator.PolicyOfAsync(uow, reservation.PricingPolicyId, reservation.MoveInDate, ct);
                var freeUntil = reservation.CreatedAt.AddHours(policy?.CancelFreeHours ?? 0);
                refundable = ourFault || !currentUser.IsCustomer || DateTime.UtcNow <= freeUntil;
            }
            if (refundable)
                DepositRefundService.Owe(uow, reservation, paid);
            return (reservation.ReservationId, paid, refundable, ourFault);
        }, ct);

        if (!closed.IsSuccess)
            return closed.Error;
        var dto = await GetAsync(closed.Value.Id, ct);
        if (!dto.IsSuccess)
            return dto.Error;
        await NotifyAsync(dto.Value, noShow ? "Đặt kho đã hết hạn" : "Đặt kho đã được huỷ",
            $"Đơn {dto.Value.ReservationCode} đã {(noShow ? "hết hạn do không đến nhận kho" : "được huỷ")}" +
            (closed.Value.OurFault ? " vì StorEase chưa có kho sẵn sàng như đã hẹn." : ".") +
            (closed.Value.Refundable ? $" Tiền cọc {Money.Text(closed.Value.Paid)} ₫ sẽ được hoàn lại."
                : closed.Value.Paid > 0 ? $" Tiền cọc {Money.Text(closed.Value.Paid)} ₫ không được hoàn." : ""), ct);
        return new ReservationCancellationDto(dto.Value, closed.Value.Paid, closed.Value.Refundable);
    }

    public async Task<Result<HandoverChecklistDto>> GetHandoverChecklistAsync(int reservationId, CancellationToken ct)
    {
        var r = await Visible().AsNoTracking()
            .Include(x => x.Customer).Include(x => x.StorageUnit)
            .FirstOrDefaultAsync(x => x.ReservationId == reservationId, ct);
        if (r is null)
            return Error.NotFound("Reservation not found.");

        var contract = await uow.Repository<Contract>().Query().AsNoTracking()
            .Where(c => c.ReservationId == reservationId && c.Status != ContractStatus.Terminated)
            .OrderByDescending(c => c.ContractId).FirstOrDefaultAsync(ct);
        var deposit = await uow.Repository<Invoice>().Query().AsNoTracking()
            .FirstOrDefaultAsync(i => i.ReservationId == reservationId && i.ContractId == null, ct);

        var items = new List<HandoverItemDto>
        {
            new("identity", "Đã có CCCD/CMND của khách", !string.IsNullOrEmpty(r.Customer.CitizenId), r.Customer.CitizenId),
            new("deposit", "Đã thanh toán tiền cọc", deposit is null || deposit.Status == InvoiceStatus.Paid,
                deposit is null ? "Không yêu cầu cọc" : $"{deposit.PaidAmount:N0}/{deposit.TotalAmount:N0} ₫"),
            new("unit", "Đã gán kho và giữ chỗ", r.StorageUnit is { Status: StorageUnitStatus.Reserved }, r.StorageUnit?.UnitCode),
            new("contract", "Đã lập hợp đồng", contract is not null, contract?.ContractNumber),
            new("signed", "Khách đã ký hợp đồng", contract?.SignedAt is not null, contract?.SignedAt?.ToString("u")),
            new("moveIn", "Đến ngày dọn vào", r.MoveInDate <= VietnamTime.Today, r.MoveInDate.ToString("yyyy-MM-dd")),
        };
        // The move-in date is informative only: an early handover is the facility's call.
        var ready = r.Status == ReservationStatus.Confirmed && items.Where(i => i.Key != "moveIn").All(i => i.Done);
        return new HandoverChecklistDto(r.ReservationId, r.ReservationCode, r.Customer.FullName, r.Customer.PhoneNumber,
            r.Customer.CitizenId, r.StorageUnit?.UnitCode, contract?.ContractId, items, ready);
    }

    /// <summary>Customers see their own; facility staff and managers the facilities they work at; admin and business ops all.</summary>
    /// <summary>True while the daily job runs: it has no signed-in user and acts on every facility.</summary>
    private bool runningAsJob;

    private IQueryable<Reservation> Visible()
    {
        var query = uow.Repository<Reservation>().Query();
        if (runningAsJob)
            return query;
        var userId = currentUser.UserId;
        if (currentUser.IsCustomer)
            return query.Where(r => r.CustomerId == userId);
        if (StaffScope.SeesAllFacilities(currentUser.Role))
            return query;
        var facilityIds = StaffScope.FacilityIdsOf(uow, userId);
        return query.Where(r => facilityIds.Contains(r.FacilityId));
    }

    private IQueryable<ReservationDto> Project(IQueryable<Reservation> source)
    {
        var contracts = uow.Repository<Contract>().Query();
        var invoices = uow.Repository<Invoice>().Query();
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
            contracts.Where(c => c.ReservationId == r.ReservationId && c.Status != ContractStatus.Terminated)
                .OrderByDescending(c => c.ContractId)
                .Select(c => (int?)c.ContractId)
                .FirstOrDefault(),
            invoices.Where(i => i.ReservationId == r.ReservationId && i.ContractId == null).Select(i => (int?)i.InvoiceId).FirstOrDefault(),
            invoices.Where(i => i.ReservationId == r.ReservationId && i.ContractId == null).Select(i => (decimal?)i.TotalAmount).FirstOrDefault(),
            invoices.Where(i => i.ReservationId == r.ReservationId && i.ContractId == null).Select(i => (InvoiceStatus?)i.Status).FirstOrDefault(),
            r.ProposedStorageUnitId,
            r.ProposedStorageUnit != null ? r.ProposedStorageUnit.UnitCode : null,
            r.HandoverSlot,
            r.UnitBlocked));
    }

    /// <summary>The bell always; email as well.</summary>
    private async Task NotifyAsync(ReservationDto reservation, string subject, string body, CancellationToken ct)
    {
        NotificationService.Add(uow, reservation.CustomerId, subject, $"{body}\nMã đặt kho: {reservation.ReservationCode}",
            $"/booking/confirmed/{reservation.ReservationId}");
        await uow.SaveChangesAsync(ct);
        var to = await uow.Repository<User>().Query().Where(u => u.UserId == reservation.CustomerId)
            .Select(u => u.Email).FirstOrDefaultAsync(ct);
        if (to is not null)
            await email.SendAsync(to, subject, $"Xin chào {reservation.CustomerName},\n\n{body}\n\nMã đặt kho: {reservation.ReservationCode}", ct);
    }

    /// <summary>The deposit invoice raised with every reservation (online or walk-in); none when the deposit is zero.</summary>
    internal static Invoice? AddDepositInvoice(IUnitOfWork uow, Reservation reservation, decimal amount)
    {
        if (amount <= 0)
            return null;
        var deposit = new Invoice
        {
            ReservationId = reservation.ReservationId,
            CustomerId = reservation.CustomerId,
            InvoiceNumber = InvoiceBook.DepositNumber(reservation),
            IssueDate = VietnamTime.Today,
            // The deposit has to be settled before the keys are handed over.
            DueDate = reservation.MoveInDate,
            Status = InvoiceStatus.Issued,
        };
        deposit.InvoiceItems.Add(InvoiceBook.Line(InvoiceBook.DepositDescription, amount));
        InvoiceBook.Retotal(deposit, VietnamTime.Today);
        uow.Repository<Invoice>().Add(deposit);
        return deposit;
    }

    // ponytail: random suffix, ~1-in-a-million clash per day; the unique index turns a clash into a 500. Switch to a sequence if volume grows.
    internal static string NewReservationCode() => $"RS{DateTime.UtcNow:yyMMdd}{Random.Shared.Next(1_000_000):D6}";
}
