using StorEase.Application.Interfaces;

namespace StorEase.Infrastructure.Persistence.Repositories;

public class Repository<T>(AppDbContext db) : IRepository<T> where T : class
{
    public IQueryable<T> Query() => db.Set<T>();

    public Task<T?> FindAsync(object id, CancellationToken ct) => db.Set<T>().FindAsync([id], ct).AsTask();

    public void Add(T entity) => db.Set<T>().Add(entity);

    public void Remove(T entity) => db.Set<T>().Remove(entity);
}
