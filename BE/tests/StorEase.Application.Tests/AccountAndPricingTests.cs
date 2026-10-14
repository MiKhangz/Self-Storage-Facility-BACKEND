using System.Text.RegularExpressions;
using FluentAssertions;
using StorEase.Application.Common;
using StorEase.Application.Features.AccessCodes;
using StorEase.Application.Features.ActivityLogs;
using StorEase.Application.Features.Auth;
using StorEase.Application.Features.Auth.Dtos;
using StorEase.Application.Features.Contracts;
using StorEase.Application.Features.Contracts.Dtos;
using StorEase.Application.Features.Pricing;
using StorEase.Application.Features.Pricing.Dtos;
using StorEase.Domain.Entities;
using StorEase.Domain.Enums;
using StorEase.Infrastructure.Services;
using static StorEase.Application.Tests.CoreFlowSeed;

namespace StorEase.Application.Tests;

public class AccountAndPricingTests
{
    private static readonly JwtService Jwt = new(new JwtOptions
    {
        Issuer = "StorEase", Audience = "StorEase.Frontend", Key = new string('k', 64), ExpiresMinutes = 60,
    });

    private static AuthService Auth(TestDb t, FakeEmailService email) =>
        new(t.Uow, new Pbkdf2PasswordHasher(), Jwt, email, new AppOptions(), new ActivityLogService(t.Uow));

    private static string TokenIn(FakeEmailService email) =>
        Uri.UnescapeDataString(Regex.Match(email.Sent[^1].Body, @"token=(\S+)").Groups[1].Value);

    [Fact]
    public async Task A_new_account_signs_in_only_after_verifying_and_a_reset_link_works_once()
    {
        using var t = Create();
        var email = new FakeEmailService();
        var auth = Auth(t, email);

        var registered = await auth.RegisterAsync(new RegisterRequest("Khách mới", "New@X.vn", "0901234567", "Strong@123"), default);
        var beforeVerify = await auth.LoginAsync(new LoginRequest("new@x.vn", "Strong@123"), null, default);
        (await auth.VerifyEmailAsync(new VerifyEmailRequest(TokenIn(email)), default)).IsSuccess.Should().BeTrue();
        var afterVerify = await auth.LoginAsync(new LoginRequest("new@x.vn", "Strong@123"), null, default);

        registered.Value!.Status.Should().Be(nameof(UserStatus.Pending));
        beforeVerify.Error!.Type.Should().Be(ErrorType.Forbidden);
        afterVerify.IsSuccess.Should().BeTrue(afterVerify.Error?.Message);

        await auth.ForgotPasswordAsync(new ForgotPasswordRequest("new@x.vn"), default);
        var token = TokenIn(email);
        var reset = await auth.ResetPasswordAsync(new ResetPasswordRequest(token, "Newer@456"), default);
        var reused = await auth.ResetPasswordAsync(new ResetPasswordRequest(token, "Third@789"), default);

        reset.IsSuccess.Should().BeTrue();
        reused.Error!.Type.Should().Be(ErrorType.Validation, "the stamp changes with the password");
        (await auth.LoginAsync(new LoginRequest("new@x.vn", "Strong@123"), null, default)).IsSuccess.Should().BeFalse();
        (await auth.LoginAsync(new LoginRequest("new@x.vn", "Newer@456"), null, default)).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Fee_types_are_editable_only_on_a_draft_policy_with_unique_codes()
    {
        using var t = Create();
        t.Db.Add(new UnitPrice
        {
            PricingPolicy = new PricingPolicy { CreatedBy = StaffId, Version = 1, EffectiveFrom = new DateOnly(2025, 1, 1), Status = PricingPolicyStatus.Draft },
            UnitTypeId = SmallTypeId, BasePricePerMonth = 900_000m,
        });
        t.Db.SaveChanges();
        var pricing = new PricingService(t.Uow, new FakeCurrentUser(StaffId, Roles.BusinessOperationsManager));

        var fee = await pricing.CreateFeeTypeAsync(1, new FeeTypeRequest("lock", "Ổ khóa", 150_000m, false), default);
        var duplicate = await pricing.CreateFeeTypeAsync(1, new FeeTypeRequest("LOCK", "Khóa 2", 1m, false), default);
        await pricing.ActivatePolicyAsync(1, default);
        var editActive = await pricing.UpdateFeeTypeAsync(fee.Value!.FeeTypeId, new FeeTypeRequest("LOCK", "Ổ khóa", 200_000m, false), default);

        fee.Value.Code.Should().Be("LOCK");
        duplicate.Error!.Type.Should().Be(ErrorType.Conflict);
        editActive.Error!.Type.Should().Be(ErrorType.Conflict);
    }

    [Fact]
    public async Task A_contract_is_drafted_from_a_confirmed_reservation_at_the_policy_rent_only_once()
    {
        using var t = Create();
        t.Db.Add(new UnitPrice
        {
            PricingPolicy = new PricingPolicy
            {
                CreatedBy = StaffId, Version = 1, EffectiveFrom = new DateOnly(2025, 1, 1), DepositMonths = 1, Status = PricingPolicyStatus.Active,
            },
            UnitTypeId = SmallTypeId, BasePricePerMonth = 1_000_000m, Discount3Month = 10,
        });
        var reservation = new Reservation
        {
            CustomerId = CustomerId, FacilityId = FacilityId, UnitTypeId = SmallTypeId, StorageUnitId = A101,
            ReservationCode = "RS-DRAFT", MoveInDate = MoveIn, RentalMonths = 3, Status = ReservationStatus.Confirmed, CreatedAt = DateTime.UtcNow,
        };
        t.Db.Add(reservation);
        t.Db.SaveChanges();
        var contracts = new ContractService(t.Uow, Staff, new AccessCodeService(t.Uow, new Pbkdf2PasswordHasher(), Staff));

        var drafted = await contracts.CreateAsync(new CreateContractRequest(reservation.ReservationId, 5), default);
        var again = await contracts.CreateAsync(new CreateContractRequest(reservation.ReservationId, 5), default);

        drafted.IsSuccess.Should().BeTrue(drafted.Error?.Message);
        drafted.Value!.Status.Should().Be(ContractStatus.Draft);
        drafted.Value.MonthlyRent.Should().Be(900_000m);
        drafted.Value.EndDate.Should().Be(MoveIn.AddMonths(3).AddDays(-1));
        again.Error!.Type.Should().Be(ErrorType.Conflict, "business rule 1: one Active or Draft contract per unit");
    }
}
