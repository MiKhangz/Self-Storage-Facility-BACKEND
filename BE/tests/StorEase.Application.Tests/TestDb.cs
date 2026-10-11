using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using StorEase.Application.Interfaces;
using StorEase.Infrastructure.Persistence;

namespace StorEase.Application.Tests;

/// <summary>
/// A throwaway SQLite in-memory database with the real EF model and the real <see cref="UnitOfWork"/>.
/// The connection stays open for the lifetime of the object, which is what keeps the in-memory db alive.
/// </summary>
public sealed class TestDb : IDisposable
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    public AppDbContext Db { get; }
    public IUnitOfWork Uow { get; }

    public TestDb()
    {
        _connection.Open();
        Db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options);
        Db.Database.EnsureCreated();
        Uow = new UnitOfWork(Db);
    }

    public void Dispose()
    {
        Db.Dispose();
        _connection.Dispose();
    }
}
