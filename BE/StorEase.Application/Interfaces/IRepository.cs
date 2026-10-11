namespace StorEase.Application.Interfaces;

public interface IRepository<T> where T : class
{
    /// <summary>Tracked query. Project with <c>Select</c> for reads; load and modify for writes.</summary>
    IQueryable<T> Query();

    Task<T?> FindAsync(object id, CancellationToken ct);

    void Add(T entity);

    /// <summary>Hard delete. Only for plans and link rows that carry no history (e.g. a future shift).</summary>
    void Remove(T entity);
}
