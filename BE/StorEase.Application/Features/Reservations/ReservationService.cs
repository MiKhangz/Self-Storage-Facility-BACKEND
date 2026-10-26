using Microsoft.EntityFrameworkCore;
using StorEase.Application.Common;
using StorEase.Application.Features.DepositRefunds;
using StorEase.Application.Features.Invoices;
using StorEase.Application.Features.Pricing;
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

        // Reserving does not hold a unit (staff assigns one later), but refuse a reservation that could never be served.
        var anyAvailable = await uow.Repository<StorageUnit>().Query().AnyAsync(u =>
            u.FacilityId == request.FacilityId &&
            u.UnitTypeId == request.UnitTypeId &&
            u.Status == StorageUnitStatus.Available, ct);
        if (!anyAvailable)
            return Error.Conflict("No unit of this type is available at this facility.");

        // Without a policy there is no price, so the deposit is skipped rather than blocking the booking.
        var terms = await RentCalculator.TermsAsync(uow, request.UnitTypeId, request.MoveInDate, request.RentalMonths, ct);

        var created = await uow.InTransactionAsync<int>(async () =>
        {
            var reservation = new Reservation
            {
                CustomerId = currentUser.UserId,
                FacilityId = request.FacilityId,
                UnitTypeId = request.UnitTypeId,
                ReservationCode = NewReservationCode(),
                MoveInDate = request.MoveInDate,
                RentalMonths = request.RentalMonths,
                // The version this quote came from; the contract is drafted from it even if prices change before move-in.
                PricingPolicyId = terms.IsSuccess ? terms.Value.Policy.PricingPolicyId : null,
                Status = ReservationStatus.Pending,
                CreatedAt = DateTime.UtcNow,
            };
            uow.Repository<Reservation>().Add(reservation);
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
                $"Chúng tôi đã nhận yêu cầu đặt kho {dto.Value.UnitTypeName} tại {facility.Name}, dọn vào ngày {dto.Value.MoveInDate:dd/MM/yyyy}." +
                (dto.Value.DepositAmount is { } amount ? $"\nVui lòng thanh toán tiền cọc {amount:N0} ₫ trước ngày dọn vào." : ""), ct);
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
        var assigned = await uow.InTransactionAsync<int>(async () =>
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

        if (!assigned.IsSuccess)
            return assigned.Error;
        var dto = await GetAsync(assigned.Value, ct);
        if (dto.IsSuccess)
            await NotifyAsync(dto.Value, "Đặt kho đã được xác nhận",
                $"Đơn {dto.Value.ReservationCode} đã được xác nhận với kho {dto.Value.UnitCode} tại {dto.Value.FacilityName}. " +
                $"Hẹn gặp bạn ngày {dto.Value.MoveInDate:dd/MM/yyyy}.", ct);
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
            if (unit.Status != StorageUnitStatus.Available)
                return Error.Conflict($"Unit {unit.UnitCode} is {unit.Status}, not Available.");
            if (await uow.Repository<Contract>().Query().AnyAsync(c => c.StorageUnitId == unit.StorageUnitId &&
                    (c.Status == ContractStatus.Active || c.Status == ContractStatus.Draft), ct))
                return Error.Conflict($"Unit {unit.UnitCode} already has an Active or Draft contract.");

            if (reservation.StorageUnit.Status == StorageUnitStatus.Reserved)
                reservation.StorageUnit.Status = StorageUnitStatus.Available;
            unit.Status = StorageUnitStatus.Reserved;
            reservation.StorageUnitId = unit.StorageUnitId;

            var drafts = await uow.Repository<Contract>().Query()
                .Where(c => c.ReservationId == reservationId && c.Status == ContractStatus.Draft).ToListAsync(ct);
            foreach (var draft in drafts)
            {
                draft.StorageUnitId = unit.StorageUnitId;
                draft.SignedAt = null;
            }
            return reservation.ReservationId;
        }, ct);
        return changed.IsSuccess ? await GetAsync(changed.Value, ct) : changed.Error;
    }

    public Task<Result<ReservationCancellationDto>> CancelAsync(
        int reservationId, CancelReservationRequest request, CancellationToken ct) =>
        CloseAsync(reservationId, noShow: false, ct);

    public Task<Result<ReservationCancellationDto>> MarkNoShowAsync(int reservationId, CancellationToken ct) =>
        CloseAsync(reservationId, noShow: true, ct);

    private async Task<Result<ReservationCancellationDto>> CloseAsync(int reservationId, bool noShow, CancellationToken ct)
    {
        var closed = await uow.InTransactionAsync<(int Id, decimal Paid, bool Refundable)>(async () =>
        {
            var reservation = await Visible().Include(r => r.StorageUnit)
                .FirstOrDefaultAsync(r => r.ReservationId == reservationId, ct);
            if (reservation is null)
                return Error.NotFound("Reservation not found.");
            if (reservation.Status is not (ReservationStatus.Pending or ReservationStatus.Confirmed))
                return Error.Conflict($"Only a Pending or Confirmed reservation can be closed; this one is {reservation.Status}.");
            if (noShow && reservation.MoveInDate >= VietnamTime.Today)
                return Error.Conflict("A reservation can only be marked as a no-show after its move-in date.");

            reservation.Status = noShow ? ReservationStatus.Expired : ReservationStatus.Cancelled;
            if (reservation.StorageUnit is { Status: StorageUnitStatus.Reserved } unit)
                unit.Status = StorageUnitStatus.Available;

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
                refundable = !currentUser.IsCustomer || DateTime.UtcNow <= freeUntil;
            }
            if (refundable)
                DepositRefundService.Owe(uow, reservation, paid);
            return (reservation.ReservationId, paid, refundable);
        }, ct);

        if (!closed.IsSuccess)
            return closed.Error;
        var dto = await GetAsync(closed.Value.Id, ct);
        if (!dto.IsSuccess)
            return dto.Error;
        await NotifyAsync(dto.Value, noShow ? "Đặt kho đã hết hạn" : "Đặt kho đã được huỷ",
            $"Đơn {dto.Value.ReservationCode} đã {(noShow ? "hết hạn do không đến nhận kho" : "được huỷ")}." +
            (closed.Value.Refundable ? " Tiền cọc sẽ được hoàn lại." : ""), ct);
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
    private IQueryable<Reservation> Visible()
    {
        var query = uow.Repository<Reservation>().Query();
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
            contracts.Where(c => c.ReservationId == r.ReservationId)
                .OrderByDescending(c => c.ContractId)
                .Select(c => (int?)c.ContractId)
                .FirstOrDefault(),
            invoices.Where(i => i.ReservationId == r.ReservationId && i.ContractId == null).Select(i => (int?)i.InvoiceId).FirstOrDefault(),
            invoices.Where(i => i.ReservationId == r.ReservationId && i.ContractId == null).Select(i => (decimal?)i.TotalAmount).FirstOrDefault(),
            invoices.Where(i => i.ReservationId == r.ReservationId && i.ContractId == null).Select(i => (InvoiceStatus?)i.Status).FirstOrDefault()));
    }

    private async Task NotifyAsync(ReservationDto reservation, string subject, string body, CancellationToken ct)
    {
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
