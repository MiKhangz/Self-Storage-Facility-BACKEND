namespace StorEase.Application.Interfaces;

public interface IRepository<T> where T : class
{
    /// <summary>Tracked query. Project with <c>Select</c> for reads; load and modify for writes.</summary>
    IQueryable<T> Query();

    Task<T?> FindAsync(object id, CancellationToken ct);

    void Add(T entity);
}
