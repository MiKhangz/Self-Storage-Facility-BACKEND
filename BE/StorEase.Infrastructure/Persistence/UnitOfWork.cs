using StorEase.Application.Common;
using StorEase.Application.Interfaces;
using StorEase.Infrastructure.Persistence.Repositories;

namespace StorEase.Infrastructure.Persistence;

public class UnitOfWork(AppDbContext db) : IUnitOfWork
{
    public IRepository<T> Repository<T>() where T : class => new Repository<T>(db);

    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);

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
