using FluentAssertions;
using StorEase.Application.Common;
using StorEase.Application.Features.AccessCodes;
using StorEase.Application.Features.Contracts;
using StorEase.Application.Features.Contracts.Dtos;
using StorEase.Application.Features.CheckIns;
using StorEase.Application.Features.CheckIns.Dtos;
using StorEase.Application.Interfaces;
using StorEase.Domain.Enums;
using static StorEase.Application.Tests.CoreFlowSeed;

namespace StorEase.Application.Tests;

/// <summary>Business rule 3: check-in, not signing, activates the contract. Runs on SQLite with real transactions.</summary>
public class CheckInServiceTests
{
    private sealed class FakeHasher : IPasswordHasher
    {
        public string Hash(string value) => "h:" + value;
        public bool Verify(string value, string hash) => hash == "h:" + value;
    }

    /// <summary>Stands in for contract activation failing after the check-in has already changed rows.</summary>
    private sealed class FailingActivation : IContractService
    {
        public Task<Result<string>> ActivateAsync(int contractId, CancellationToken ct) =>
            Task.FromResult<Result<string>>(Error.Conflict("activation failed"));

        // CheckIn only calls ActivateAsync.
        public Task<Result<ContractDto>> GetAsync(int contractId, CancellationToken ct) => throw new NotSupportedException();
        public Task<Result<PagedResult<ContractDto>>> ListAsync(ContractStatus? status, int page, int pageSize, CancellationToken ct) => throw new NotSupportedException();
        public Task<Result<ContractDto>> SignAsync(int contractId, CancellationToken ct) => throw new NotSupportedException();
        public Task<Result<ContractDto>> TerminateAsync(int contractId, CancellationToken ct) => throw new NotSupportedException();
        public Task<Result<string>> ExportCsvAsync(ContractStatus? status, CancellationToken ct) => throw new NotSupportedException();
        public Task<Result<ContractDto>> CreateAsync(CreateContractRequest request, CancellationToken ct) => throw new NotSupportedException();
    }

    private static CheckInService RealService(TestDb t)
    {
        var codes = new AccessCodeService(t.Uow, new FakeHasher(), Staff);
        return new CheckInService(t.Uow, Staff, new ContractService(t.Uow, Staff, codes));
    }

    private static CreateCheckInRequest Request(int reservationId, int contractId) =>
        new(reservationId, contractId, "LK-00291", "CARD-5512", "Clean, no damage");

    [Fact]
    public async Task CheckIn_activates_the_contract_occupies_the_unit_and_issues_one_access_code()
    {
        using var t = Create();
        var (reservationId, contractId) = SeedReadyForCheckIn(t, signed: true);

        var result = await RealService(t).CreateAsync(Request(reservationId, contractId), default);

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        result.Value!.Status.Should().Be(CheckInStatus.Completed);
        result.Value.UnitCode.Should().Be("A-101");
        result.Value.AccessCode.Should().MatchRegex("^[0-9]{6}$");

        t.Db.ChangeTracker.Clear();
        t.Db.Contracts.Find(contractId)!.Status.Should().Be(ContractStatus.Active);
        t.Db.StorageUnits.Find(A101)!.Status.Should().Be(StorageUnitStatus.Occupied);
        t.Db.Reservations.Find(reservationId)!.Status.Should().Be(ReservationStatus.CheckedIn);
        t.Db.CheckIns.Should().ContainSingle().Which.StaffId.Should().Be(StaffId);
        t.Db.AccessCodes.Where(c => c.IsActive).Should().ContainSingle()
            .Which.CodeHash.Should().Be("h:" + result.Value.AccessCode, "only the hash is stored");
    }

    [Fact]
    public async Task Signing_alone_does_not_hand_over_an_unsigned_contract_is_refused()
    {
        using var t = Create();
        var (reservationId, contractId) = SeedReadyForCheckIn(t, signed: false);

        var result = await RealService(t).CreateAsync(Request(reservationId, contractId), default);

        result.Error!.Type.Should().Be(ErrorType.Conflict);
        result.Error.Message.Should().Contain("not signed");
        t.Db.ChangeTracker.Clear();
        t.Db.Contracts.Find(contractId)!.Status.Should().Be(ContractStatus.Draft);
        t.Db.CheckIns.Should().BeEmpty();
    }

    [Fact]
    public async Task A_failure_during_activation_rolls_the_whole_check_in_back()
    {
        using var t = Create();
        var (reservationId, contractId) = SeedReadyForCheckIn(t, signed: true);
        var service = new CheckInService(t.Uow, Staff, new FailingActivation());

        var result = await service.CreateAsync(Request(reservationId, contractId), default);

        result.IsSuccess.Should().BeFalse();
        t.Db.ChangeTracker.Clear();
        t.Db.CheckIns.Should().BeEmpty("the checkIn row is rolled back");
        t.Db.StorageUnits.Find(A101)!.Status.Should().Be(StorageUnitStatus.Reserved);
        t.Db.Reservations.Find(reservationId)!.Status.Should().Be(ReservationStatus.Confirmed);
        t.Db.Contracts.Find(contractId)!.Status.Should().Be(ContractStatus.Draft);
    }

    [Fact]
    public async Task The_same_reservation_cannot_be_handed_over_twice()
    {
        using var t = Create();
        var (reservationId, contractId) = SeedReadyForCheckIn(t, signed: true);
        await RealService(t).CreateAsync(Request(reservationId, contractId), default);

        var again = await RealService(t).CreateAsync(Request(reservationId, contractId), default);

        again.Error!.Type.Should().Be(ErrorType.Conflict);
        t.Db.CheckIns.Should().ContainSingle();
    }

    [Fact]
    public async Task A_contract_from_another_reservation_is_refused()
    {
        using var t = Create();
        var (reservationId, _) = SeedReadyForCheckIn(t, signed: true);

        var result = await RealService(t).CreateAsync(Request(reservationId, contractId: 999), default);

        result.Error!.Type.Should().Be(ErrorType.NotFound);
    }
}
