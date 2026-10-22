using System.Security.Cryptography;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using StorEase.Application.Common;
using StorEase.Application.Features.Invoices;
using StorEase.Application.Features.Invoices.Dtos;
using StorEase.Application.Features.Pricing;
using StorEase.Application.Features.Reservations.Dtos;
using StorEase.Application.Features.Reservations.Validators;
using StorEase.Application.Interfaces;
using StorEase.Domain.Entities;
using StorEase.Domain.Enums;

namespace StorEase.Application.Features.Reservations;

public sealed record CustomerSummaryDto(int UserId, string FullName, string Email, string PhoneNumber, string? CitizenId);

/// <summary>A customer created at the desk. They get no usable password and set one with "forgot password".</summary>
public sealed record WalkInCustomerRequest(string FullName, string Email, string PhoneNumber, string? CitizenId);

/// <summary>Exactly one of <see cref="CustomerId"/> (an existing customer) or <see cref="NewCustomer"/>.</summary>
public sealed record WalkInReservationRequest(
    int? CustomerId, WalkInCustomerRequest? NewCustomer, int FacilityId, int UnitTypeId, int StorageUnitId,
    DateOnly MoveInDate, int RentalMonths);

public class WalkInReservationRequestValidator : AbstractValidator<WalkInReservationRequest>
{
    public WalkInReservationRequestValidator()
    {
        RuleFor(x => x).Must(x => x.CustomerId is not null ^ x.NewCustomer is not null)
            .WithMessage("Give either an existing customer or a new customer, not both.");
        RuleFor(x => x.CustomerId).GreaterThan(0).When(x => x.CustomerId is not null);
        When(x => x.NewCustomer is not null, () =>
        {
            RuleFor(x => x.NewCustomer!.FullName).NotEmpty().MaximumLength(100);
            RuleFor(x => x.NewCustomer!.Email).NotEmpty().EmailAddress().MaximumLength(100);
            RuleFor(x => x.NewCustomer!.PhoneNumber).NotEmpty().MaximumLength(20).Matches(@"^\+?[0-9 ]{8,20}$");
            RuleFor(x => x.NewCustomer!.CitizenId).Matches(@"^[0-9]{9,12}$").When(x => !string.IsNullOrWhiteSpace(x.NewCustomer!.CitizenId));
        });
        RuleFor(x => x.FacilityId).GreaterThan(0);
        RuleFor(x => x.UnitTypeId).GreaterThan(0);
        RuleFor(x => x.StorageUnitId).GreaterThan(0);
        RuleFor(x => x.MoveInDate).GreaterThanOrEqualTo(_ => VietnamTime.Today).WithMessage("Move-in date cannot be in the past.");
        RuleFor(x => x.RentalMonths).InclusiveBetween(1, CreateReservationRequestValidator.MaxRentalMonths);
    }
}

public interface IWalkInService
{
    /// <summary>Customers matching a name, email or phone, for staff at the desk.</summary>
    Task<Result<PagedResult<CustomerSummaryDto>>> SearchCustomersAsync(string? search, int page, int pageSize, CancellationToken ct);

    /// <summary>One customer's contact details, for staff.</summary>
    Task<Result<CustomerSummaryDto>> GetCustomerAsync(int userId, CancellationToken ct);

    /// <summary>
    /// A booking made at the desk: the reservation is Confirmed with the chosen unit at once, and the deposit is
    /// recorded as paid in cash, so check-in can follow straight away.
    /// </summary>
    Task<Result<ReservationDto>> BookAsync(WalkInReservationRequest request, CancellationToken ct);
}

public class WalkInService(
    IUnitOfWork uow, ICurrentUser currentUser, IPasswordHasher hasher, IInvoiceService invoices, IReservationService reservations)
    : IWalkInService
{
    public async Task<Result<PagedResult<CustomerSummaryDto>>> SearchCustomersAsync(string? search, int page, int pageSize, CancellationToken ct)
    {
        var query = Customers();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(u => u.FullName.Contains(term) || u.Email.Contains(term) || u.PhoneNumber.Contains(term));
        }
        return await query.OrderBy(u => u.FullName).Select(ToSummary).ToPagedResultAsync(page, pageSize, ct);
    }

    public async Task<Result<CustomerSummaryDto>> GetCustomerAsync(int userId, CancellationToken ct)
    {
        var dto = await Customers().Where(u => u.UserId == userId).Select(ToSummary).FirstOrDefaultAsync(ct);
        return dto is null ? Error.NotFound("Customer not found.") : dto;
    }

    private IQueryable<User> Customers() => uow.Repository<User>().Query().Where(u => u.Role.RoleName == Roles.StorageCustomer);

    private static readonly System.Linq.Expressions.Expression<Func<User, CustomerSummaryDto>> ToSummary =
        u => new CustomerSummaryDto(u.UserId, u.FullName, u.Email, u.PhoneNumber, u.CitizenId);

    public async Task<Result<ReservationDto>> BookAsync(WalkInReservationRequest request, CancellationToken ct)
    {
        var booked = await uow.InTransactionAsync<int>(async () =>
        {
            if (!await StaffScope.FacilityIdsOf(uow, currentUser.UserId).AnyAsync(id => id == request.FacilityId, ct))
                return Error.NotFound("Facility not found.");

            var unit = await uow.Repository<StorageUnit>().FindAsync(request.StorageUnitId, ct);
            if (unit is null || unit.FacilityId != request.FacilityId || unit.UnitTypeId != request.UnitTypeId)
                return Error.Conflict("The unit does not match the chosen facility and size.");
            if (unit.Status != StorageUnitStatus.Available)
                return Error.Conflict($"Unit {unit.UnitCode} is {unit.Status}, not Available.");
            // Business rule 1: a Draft contract already claims its unit.
            if (await uow.Repository<Contract>().Query().AnyAsync(c => c.StorageUnitId == unit.StorageUnitId
                    && (c.Status == ContractStatus.Active || c.Status == ContractStatus.Draft), ct))
                return Error.Conflict($"Unit {unit.UnitCode} already has an Active or Draft contract.");

            var customer = await ResolveCustomerAsync(request, ct);
            if (!customer.IsSuccess)
                return customer.Error;

            var terms = await RentCalculator.TermsAsync(uow, request.UnitTypeId, request.MoveInDate, request.RentalMonths, ct);
            if (!terms.IsSuccess)
                return terms.Error;

            var reservation = new Reservation
            {
                CustomerId = customer.Value,
                FacilityId = request.FacilityId,
                UnitTypeId = request.UnitTypeId,
                StorageUnitId = unit.StorageUnitId,
                CreatedBy = currentUser.UserId,
                ReservationCode = ReservationService.NewReservationCode(),
                MoveInDate = request.MoveInDate,
                RentalMonths = request.RentalMonths,
                PricingPolicyId = terms.Value.Policy.PricingPolicyId,
                Status = ReservationStatus.Confirmed,
                CreatedAt = DateTime.UtcNow,
            };
            uow.Repository<Reservation>().Add(reservation);
            unit.Status = StorageUnitStatus.Reserved;
            await uow.SaveChangesAsync(ct);

            var deposit = ReservationService.AddDepositInvoice(uow, reservation, terms.Value.Deposit);
            if (deposit is not null)
            {
                await uow.SaveChangesAsync(ct);
                // Paid in cash at the desk, recorded by the same staff member, inside this transaction.
                var paid = await invoices.RecordManualPaymentAsync(deposit.InvoiceId,
                    new PaymentRequest(deposit.TotalAmount, $"WALKIN-{reservation.ReservationCode}"), ct);
                if (!paid.IsSuccess)
                    return paid.Error;
            }
            return reservation.ReservationId;
        }, ct);

        return booked.IsSuccess ? await reservations.GetAsync(booked.Value, ct) : booked.Error;
    }

    private async Task<Result<int>> ResolveCustomerAsync(WalkInReservationRequest request, CancellationToken ct)
    {
        if (request.CustomerId is { } customerId)
        {
            var exists = await uow.Repository<User>().Query().AnyAsync(u => u.UserId == customerId
                && u.Role.RoleName == Roles.StorageCustomer && u.Status == UserStatus.Active, ct);
            return exists ? customerId : Error.NotFound("Customer not found.");
        }

        var newCustomer = request.NewCustomer!;
        var email = newCustomer.Email.Trim().ToLowerInvariant();
        if (await uow.Repository<User>().Query().AnyAsync(u => u.Email == email, ct))
            return Error.Conflict("An account with this email already exists; search for the customer instead.");
        var role = await uow.Repository<Role>().Query().FirstAsync(r => r.RoleName == Roles.StorageCustomer, ct);
        var user = new User
        {
            RoleId = role.RoleId,
            FullName = newCustomer.FullName.Trim(),
            Email = email,
            PhoneNumber = newCustomer.PhoneNumber.Trim(),
            CitizenId = string.IsNullOrWhiteSpace(newCustomer.CitizenId) ? null : newCustomer.CitizenId.Trim(),
            // Nobody knows this password; the customer sets their own through "forgot password".
            PasswordHash = hasher.Hash(Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))),
            Status = UserStatus.Active,
            CreatedAt = DateTime.UtcNow,
        };
        uow.Repository<User>().Add(user);
        await uow.SaveChangesAsync(ct);
        return user.UserId;
    }
}
