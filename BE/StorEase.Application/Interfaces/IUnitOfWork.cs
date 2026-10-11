using StorEase.Application.Common;

namespace StorEase.Application.Interfaces;

public interface IUnitOfWork
{
    IRepository<T> Repository<T>() where T : class;

    Task SaveChangesAsync(CancellationToken ct);

    /// <summary>
    /// Runs <paramref name="work"/> in one database transaction. Saves and commits only when it returns
    /// success; a failure or an exception rolls everything back. A nested call joins the outer
    /// transaction, so services can call each other freely.
    /// </summary>
    Task<Result<T>> InTransactionAsync<T>(Func<Task<Result<T>>> work, CancellationToken ct);
}
