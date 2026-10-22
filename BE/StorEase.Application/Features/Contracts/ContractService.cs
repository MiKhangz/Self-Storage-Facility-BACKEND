using Microsoft.EntityFrameworkCore;
using StorEase.Application.Common;
using StorEase.Application.Features.AccessCodes;
using StorEase.Application.Features.Contracts.Dtos;
using StorEase.Application.Features.Pricing;
using StorEase.Application.Interfaces;
using StorEase.Domain.Entities;
using StorEase.Domain.Enums;

namespace StorEase.Application.Features.Contracts;

public class ContractService(IUnitOfWork uow, ICurrentUser currentUser, IAccessCodeService accessCodes) : IContractService
{
    public async Task<Result<ContractDto>> GetAsync(int contractId, CancellationToken ct)
    {
        var dto = await Visible().Where(c => c.ContractId == contractId).ToDto().FirstOrDefaultAsync(ct);
        // Someone else's contract is a 404, not a 403, so ids cannot be probed.
        return dto is null ? Error.NotFound("Contract not found.") : dto;
    }

    public async Task<Result<PagedResult<ContractDto>>> ListAsync(
        ContractStatus? status, int page, int pageSize, CancellationToken ct)
    {
        var query = Visible();
        if (status is not null)
            query = query.Where(c => c.Status == status);

        return await query.OrderByDescending(c => c.ContractId).ToDto().ToPagedResultAsync(page, pageSize, ct);
    }

    public async Task<Result<ContractDto>> CreateAsync(CreateContractRequest request, CancellationToken ct)
    {
        var created = await uow.InTransactionAsync<int>(async () =>
        {
            var reservation = await uow.Repository<Reservation>().FindAsync(request.ReservationId, ct);
            if (reservation is null || !await InScopeAsync(reservation.FacilityId, ct))
                return Error.NotFound("Reservation not found.");
            if (reservation.Status != ReservationStatus.Confirmed || reservation.StorageUnitId is not { } unitId)
                return Error.Conflict($"A contract is drafted for a Confirmed reservation with a unit assigned; this one is {reservation.Status}.");

            // Business rule 1: one Active/Draft contract per unit (also a filtered unique index).
            if (await uow.Repository<Contract>().Query().AnyAsync(c => c.StorageUnitId == unitId
                    && (c.Status == ContractStatus.Active || c.Status == ContractStatus.Draft), ct))
                return Error.Conflict("The unit already has an Active or Draft contract.");

            // The rent and deposit the customer was quoted when booking, even if a new price was published since.
            var terms = await RentCalculator.TermsAsync(uow, reservation.UnitTypeId, reservation.MoveInDate, reservation.RentalMonths, ct,
                reservation.PricingPolicyId);
            if (!terms.IsSuccess)
                return terms.Error;

            var number = $"CT{reservation.ReservationCode[2..]}";
            var drafts = await uow.Repository<Contract>().Query().CountAsync(c => c.ReservationId == reservation.ReservationId, ct);
            var contract = new Contract
            {
                ReservationId = reservation.ReservationId,
                CustomerId = reservation.CustomerId,
                StorageUnitId = unitId,
                // A redraft after a terminated draft keeps the reservation's number with a suffix.
                ContractNumber = drafts == 0 ? number : $"{number}-{drafts + 1}",
                StartDate = reservation.MoveInDate,
                EndDate = reservation.MoveInDate.AddMonths(reservation.RentalMonths).AddDays(-1),
                MonthlyRent = terms.Value.MonthlyRent,
                DepositAmount = terms.Value.Deposit,
                PricingPolicyId = terms.Value.Policy.PricingPolicyId,
                PaymentDay = request.PaymentDay,
                Status = ContractStatus.Draft,
            };
            uow.Repository<Contract>().Add(contract);
            await uow.SaveChangesAsync(ct);
            return contract.ContractId;
        }, ct);

        return created.IsSuccess ? await GetAsync(created.Value, ct) : created.Error;
    }

    public async Task<Result<ContractDto>> SignAsync(int contractId, CancellationToken ct)
    {
        var signed = await uow.InTransactionAsync<int>(async () =>
        {
            var contract = await Visible().FirstOrDefaultAsync(c => c.ContractId == contractId, ct);
            if (contract is null)
                return Error.NotFound("Contract not found.");
            if (contract.Status != ContractStatus.Draft)
                return Error.Conflict($"Only a Draft contract can be signed; this one is {contract.Status}.");
            if (contract.SignedAt is not null)
                return Error.Conflict("The contract is already signed.");

            // Signing does not activate: the check-in does (business rule 3).
            contract.SignedAt = DateTime.UtcNow;
            return contract.ContractId;
        }, ct);

        return signed.IsSuccess ? await GetAsync(signed.Value, ct) : signed.Error;
    }

    public Task<Result<string>> ActivateAsync(int contractId, CancellationToken ct) =>
        uow.InTransactionAsync<string>(async () =>
        {
            var contract = await Visible().Include(c => c.AccessCodes)
                .FirstOrDefaultAsync(c => c.ContractId == contractId, ct);
            if (contract is null)
                return Error.NotFound("Contract not found.");
            if (contract.Status != ContractStatus.Draft)
                return Error.Conflict($"Only a Draft contract can be activated; this one is {contract.Status}.");
            if (contract.SignedAt is null)
                return Error.Conflict("The customer has not signed the contract yet.");

            // Business rule 1: one Active/Draft contract per unit. The DB has a filtered unique index too.
            var clash = await uow.Repository<Contract>().Query().AnyAsync(c =>
                c.StorageUnitId == contract.StorageUnitId && c.ContractId != contract.ContractId
                && (c.Status == ContractStatus.Active || c.Status == ContractStatus.Draft), ct);
            if (clash)
                return Error.Conflict("The unit already has another Active or Draft contract.");

            contract.Status = ContractStatus.Active;
            return accessCodes.Issue(contract).Code;
        }, ct);

    public async Task<Result<ContractDto>> TerminateAsync(int contractId, CancellationToken ct)
    {
        var done = await uow.InTransactionAsync<int>(async () =>
        {
            var contract = await Visible()
                .Include(c => c.AccessCodes).Include(c => c.StorageUnit)
                .FirstOrDefaultAsync(c => c.ContractId == contractId, ct);
            if (contract is null)
                return Error.NotFound("Contract not found.");
            if (contract.Status is ContractStatus.Draft or ContractStatus.Closed or ContractStatus.Terminated)
                return Error.Conflict($"A {contract.Status} contract cannot be terminated.");

            contract.Status = ContractStatus.Terminated;
            foreach (var code in contract.AccessCodes.Where(a => a.IsActive))
                code.IsActive = false;
            // Occupied is the only state this contract could have put the unit in.
            if (contract.StorageUnit.Status == StorageUnitStatus.Occupied)
                contract.StorageUnit.Status = StorageUnitStatus.Available;
            return contract.ContractId;
        }, ct);

        return done.IsSuccess ? await GetAsync(done.Value, ct) : done.Error;
    }

    public async Task<Result<string>> ExportCsvAsync(ContractStatus? status, CancellationToken ct)
    {
        var query = Visible();
        if (status is not null)
            query = query.Where(c => c.Status == status);

        var rows = await query.OrderBy(c => c.ContractId).ToDto().ToListAsync(ct);
        var sb = new System.Text.StringBuilder("ContractNumber,Customer,UnitCode,StartDate,EndDate,MonthlyRent,Status\n");
        foreach (var r in rows)
            sb.Append(string.Join(',', Csv(r.ContractNumber), Csv(r.CustomerName), Csv(r.UnitCode),
                r.StartDate.ToString("yyyy-MM-dd"), r.EndDate.ToString("yyyy-MM-dd"),
                r.MonthlyRent.ToString(System.Globalization.CultureInfo.InvariantCulture), r.Status)).Append('\n');
        return sb.ToString();
    }

    // Quote every text field and neutralise spreadsheet formulas (CSV injection).
    private static string Csv(string value)
    {
        if (value.Length > 0 && "=+-@".Contains(value[0]))
            value = "'" + value;
        return '"' + value.Replace("\"", "\"\"") + '"';
    }

    /// <summary>Customers see their own; facility staff and managers the facilities they work at; admin and business ops all.</summary>
    private IQueryable<Contract> Visible()
    {
        var query = uow.Repository<Contract>().Query();
        var userId = currentUser.UserId;
        if (currentUser.IsCustomer)
            return query.Where(c => c.CustomerId == userId);
        if (StaffScope.SeesAllFacilities(currentUser.Role))
            return query;
        var facilityIds = StaffScope.FacilityIdsOf(uow, userId);
        return query.Where(c => facilityIds.Contains(c.StorageUnit.FacilityId));
    }

    private async Task<bool> InScopeAsync(int facilityId, CancellationToken ct) =>
        StaffScope.SeesAllFacilities(currentUser.Role)
        || await StaffScope.FacilityIdsOf(uow, currentUser.UserId).AnyAsync(id => id == facilityId, ct);
}
