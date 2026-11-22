using Microsoft.EntityFrameworkCore;
using StorEase.Application.Common;
using StorEase.Application.Interfaces;
using StorEase.Infrastructure.Persistence.Repositories;

namespace StorEase.Infrastructure.Persistence;

public class UnitOfWork(AppDbContext db) : IUnitOfWork
{
    public IRepository<T> Repository<T>() where T : class => new Repository<T>(db);

    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);

    // shortcut: one lock for every facility, held for one short transaction; lock per facility if bookings queue up.
    private const string TakeLock = """
        DECLARE @r int;
        EXEC @r = sp_getapplock @Resource = 'StorEase.Claims', @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 15000;
        IF @r < 0 THROW 51000, 'Timed out waiting for the claims lock.', 1;
        """;

    public Task<Result<T>> InExclusiveTransactionAsync<T>(Func<Task<Result<T>>> work, CancellationToken ct) =>
        InTransactionAsync(async () =>
        {
            // SQLite (tests) already runs one transaction at a time.
            if (db.Database.IsSqlServer())
                await db.Database.ExecuteSqlRawAsync(TakeLock, ct);
            return await work();
        }, ct);

    public async Task<Result<T>> InTransactionAsync<T>(Func<Task<Result<T>>> work, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction is not null)
            return await work();

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var result = await work();

        if (result.IsSuccess)
        {
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        else
        {
            // Disposing the transaction rolls the database back; also drop the half-applied entity changes
            // so a later SaveChanges in this request cannot write them.
            db.ChangeTracker.Clear();
        }

        return result;
    }
}
