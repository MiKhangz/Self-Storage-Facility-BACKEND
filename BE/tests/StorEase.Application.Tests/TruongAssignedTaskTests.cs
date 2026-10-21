using System.Net;
using FluentAssertions;
using StorEase.Application.Common;
using StorEase.Application.Features.Invoices;
using StorEase.Application.Features.Pricing;
using StorEase.Application.Features.Pricing.Dtos;
using StorEase.Application.Features.Renewals;
using StorEase.Domain.Entities;
using StorEase.Domain.Enums;
using static StorEase.Application.Tests.CoreFlowSeed;

namespace StorEase.Application.Tests;

public sealed class TruongAssignedTaskTests
{
    [Fact]
    public async Task Facility_manager_can_approve_a_renewal_in_their_facility()
    {
        using var db = Create();
        var manager = AddManager(db, 10, FacilityId);
        var (_, contractId) = SeedReadyForCheckIn(db, signed: true);
        var contract = db.Db.Contracts.Single(c => c.ContractId == contractId);
        contract.Status = ContractStatus.Active;
        contract.EndDate = new DateOnly(2027, 1, 31);
        var renewal = new Renewal
        {
            ContractId = contractId, RequestedAt = DateTime.UtcNow, ExtendMonths = 3,
            NewEndDate = new DateOnly(2027, 4, 30), NewMonthlyRent = 810_000m,
            Status = RenewalStatus.Requested,
        };
        db.Db.Renewals.Add(renewal);
        db.Db.SaveChanges();

        var result = await new RenewalService(db.Uow, new FakeCurrentUser(manager.UserId, Roles.FacilityManager))
            .ApproveAsync(renewal.RenewalId, default);

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        result.Value!.Status.Should().Be(RenewalStatus.Approved);
        result.Value.ApprovedBy.Should().Be(manager.UserId);
        db.Db.Contracts.Single(c => c.ContractId == contractId).EndDate.Should().Be(new DateOnly(2027, 4, 30));
        db.Db.Contracts.Single(c => c.ContractId == contractId).MonthlyRent.Should().Be(810_000m);
    }

    [Fact]
    public async Task Renewal_decision_is_hidden_from_a_manager_of_another_facility()
    {
        using var db = Create();
        var (_, contractId) = SeedReadyForCheckIn(db, signed: true);
        var contract = db.Db.Contracts.Single(c => c.ContractId == contractId);
        contract.Status = ContractStatus.Active;
        var renewal = new Renewal
        {
            ContractId = contractId, RequestedAt = DateTime.UtcNow, ExtendMonths = 1,
            NewEndDate = contract.EndDate.AddMonths(1), NewMonthlyRent = contract.MonthlyRent,
            Status = RenewalStatus.Requested,
        };
        db.Db.Renewals.Add(renewal);
        db.Db.SaveChanges();

        var result = await new RenewalService(db.Uow, new FakeCurrentUser(99, Roles.FacilityManager))
            .ApproveAsync(renewal.RenewalId, default);

        result.Error!.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task Rejecting_a_renewal_does_not_change_the_contract()
    {
        using var db = Create();
        var manager = AddManager(db, 10, FacilityId);
        var (_, contractId) = SeedReadyForCheckIn(db, signed: true);
        var contract = db.Db.Contracts.Single(c => c.ContractId == contractId);
        contract.Status = ContractStatus.Active;
        var originalEnd = contract.EndDate;
        var originalRent = contract.MonthlyRent;
        var renewal = new Renewal
        {
            ContractId = contractId, RequestedAt = DateTime.UtcNow, ExtendMonths = 1,
            NewEndDate = originalEnd.AddMonths(1), NewMonthlyRent = originalRent - 1,
            Status = RenewalStatus.Requested,
        };
        db.Db.Renewals.Add(renewal);
        db.Db.SaveChanges();

        var result = await new RenewalService(db.Uow, new FakeCurrentUser(manager.UserId, Roles.FacilityManager))
            .RejectAsync(renewal.RenewalId, default);

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        result.Value!.Status.Should().Be(RenewalStatus.Rejected);
        db.Db.Contracts.Single(c => c.ContractId == contractId).EndDate.Should().Be(originalEnd);
        db.Db.Contracts.Single(c => c.ContractId == contractId).MonthlyRent.Should().Be(originalRent);
    }

    [Fact]
    public async Task Draft_pricing_policy_terms_can_be_updated_but_active_terms_cannot()
    {
        using var db = Create();
        var policy = new PricingPolicy
        {
            CreatedBy = StaffId, Version = 1, EffectiveFrom = new DateOnly(2026, 1, 1),
            DepositMonths = 1, GraceDays = 5, Status = PricingPolicyStatus.Draft,
        };
        db.Db.PricingPolicies.Add(policy);
        db.Db.SaveChanges();
        var service = new PricingService(db.Uow, new FakeCurrentUser(StaffId, Roles.BusinessOperationsManager));
        var request = new UpdatePricingPolicyRequest(
            new DateOnly(2026, 2, 1), null, 2, 7, 30, 3, 2, 14, 60, 24);

        var updated = await service.UpdatePolicyAsync(policy.PricingPolicyId, request, default);
        await service.UpsertUnitPriceAsync(policy.PricingPolicyId, SmallTypeId,
            new UpsertUnitPriceRequest(1_000_000m, 5m, 10m, 15m), default);
        var activated = await service.ActivatePolicyAsync(policy.PricingPolicyId, default);
        var blocked = await service.UpdatePolicyAsync(policy.PricingPolicyId, request, default);

        updated.IsSuccess.Should().BeTrue(updated.Error?.Message);
        updated.Value!.EarlyTerminationPercent.Should().Be(2m);
        activated.IsSuccess.Should().BeTrue(activated.Error?.Message);
        blocked.Error!.Type.Should().Be(ErrorType.Conflict);
    }

    private static User AddManager(TestDb db, int userId, int facilityId)
    {
        var manager = new User
        {
            UserId = userId, Role = new Role { RoleName = Roles.FacilityManager }, FullName = "Manager",
            Email = $"manager{userId}@x.vn", PhoneNumber = "0900000000", PasswordHash = "x",
            Status = UserStatus.Active, CreatedAt = DateTime.UtcNow,
        };
        db.Db.Users.Add(manager);
        db.Db.StaffFacilities.Add(new StaffFacility { UserId = userId, FacilityId = facilityId, AssignedDate = DateOnly.FromDateTime(DateTime.UtcNow) });
        db.Db.SaveChanges();
        return manager;
    }
}
