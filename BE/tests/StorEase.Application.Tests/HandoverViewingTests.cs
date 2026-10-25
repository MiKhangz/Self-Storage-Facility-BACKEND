using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using StorEase.Application.Common;
using StorEase.Application.Features.Reservations;
using StorEase.Application.Features.Reservations.Dtos;
using StorEase.Domain.Enums;
using static StorEase.Application.Tests.CoreFlowSeed;

namespace StorEase.Application.Tests;

/// <summary>At the handover the customer sees the unit first; if they prefer another of the same size, staff swap it.</summary>
public class HandoverViewingTests
{
    [Fact]
    public async Task Swapping_the_unit_frees_the_old_one_holds_the_new_one_and_asks_for_a_new_signature()
    {
        using var t = Create();
        var (reservationId, contractId) = SeedReadyForHandover(t, signed: true); // A-101, signed draft
        var staff = new ReservationService(t.Uow, Staff, new FakeEmailService());

        var swapped = await staff.ChangeUnitAsync(reservationId, new AssignUnitRequest(A102), default);
        var otherSize = await staff.ChangeUnitAsync(reservationId, new AssignUnitRequest(A104), default);

        swapped.Value!.StorageUnitId.Should().Be(A102);
        otherSize.Error!.Type.Should().Be(ErrorType.Conflict, "A-104 is a different size");
        t.Db.ChangeTracker.Clear();
        (await t.Db.StorageUnits.SingleAsync(u => u.StorageUnitId == A101)).Status.Should().Be(StorageUnitStatus.Available);
        (await t.Db.StorageUnits.SingleAsync(u => u.StorageUnitId == A102)).Status.Should().Be(StorageUnitStatus.Reserved);
        var contract = await t.Db.Contracts.SingleAsync(c => c.ContractId == contractId);
        contract.StorageUnitId.Should().Be(A102);
        contract.SignedAt.Should().BeNull("the signature was for the old unit");
    }
}
