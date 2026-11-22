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

    /// <summary>
    /// The same, but one at a time across all requests, for "check, then claim" (a unit is free, so book it): read what
    /// is checked inside <paramref name="work"/>, because it may have changed while this waited. Never call it from
    /// inside an ordinary transaction, which could hold rows the one ahead is waiting for.
    /// </summary>
    Task<Result<T>> InExclusiveTransactionAsync<T>(Func<Task<Result<T>>> work, CancellationToken ct) => InTransactionAsync(work, ct);
}
