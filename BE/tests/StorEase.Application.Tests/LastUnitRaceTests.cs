using FluentAssertions;
using StorEase.Application.Common;
using StorEase.Application.Features.Reservations;
using StorEase.Application.Features.Reservations.Dtos;
using StorEase.Application.Interfaces;
using StorEase.Domain.Entities;
using StorEase.Domain.Enums;
using static StorEase.Application.Tests.CoreFlowSeed;

namespace StorEase.Application.Tests;

/// <summary>
/// Two requests for the last unit: the second waits for the lock, so whatever it checks must be read after that.
/// SQLite has one connection, so the first request is played just as the lock is given.
/// </summary>
public class LastUnitRaceTests
{
    private sealed class WaitedForLock(IUnitOfWork inner, Action firstRequest) : IUnitOfWork
    {
        public IRepository<T> Repository<T>() where T : class => inner.Repository<T>();
        public Task SaveChangesAsync(CancellationToken ct) => inner.SaveChangesAsync(ct);
        public Task<Result<T>> InTransactionAsync<T>(Func<Task<Result<T>>> work, CancellationToken ct) => inner.InTransactionAsync(work, ct);

        public Task<Result<T>> InExclusiveTransactionAsync<T>(Func<Task<Result<T>>> work, CancellationToken ct)
        {
            firstRequest();
            return inner.InExclusiveTransactionAsync(work, ct);
        }
    }

    [Fact]
    public async Task The_second_request_for_the_last_unit_is_refused()
    {
        using var t = Create();
        t.Db.StorageUnits.Find(A102)!.Status = StorageUnitStatus.Retired;
        t.Db.SaveChanges();
        var uow = new WaitedForLock(t.Uow, () =>
        {
            t.Db.Add(new Reservation
            {
                CustomerId = OtherCustomerId, FacilityId = FacilityId, UnitTypeId = SmallTypeId, StorageUnitId = A101, ReservationCode = "RS-FIRST",
                MoveInDate = MoveIn, RentalMonths = 3, Status = ReservationStatus.Pending, CreatedAt = DateTime.UtcNow,
            });
            t.Db.SaveChanges();
        });

        var second = await new ReservationService(uow, Customer, new FakeEmailService())
            .CreateAsync(new CreateReservationRequest(FacilityId, SmallTypeId, MoveIn, 3), default);

        second.Error!.Message.Should().Be("No unit of this type is available at this facility.");
    }
}
