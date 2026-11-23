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
        // SQLite ignores column sizes such as varchar(200); SQL Server refuses the save. Refuse it here too.
        Db.SavingChanges += (_, _) => RefuseTooLong(Db);
        Uow = new UnitOfWork(Db);
    }

    private static void RefuseTooLong(AppDbContext db)
    {
        foreach (var entry in db.ChangeTracker.Entries().Where(e => e.State is EntityState.Added or EntityState.Modified))
            foreach (var property in entry.Properties)
                if (property.Metadata.GetMaxLength() is { } max && property.CurrentValue is string text && text.Length > max)
                    throw new DbUpdateException(
                        $"{entry.Metadata.ClrType.Name}.{property.Metadata.Name} is {text.Length} characters; the column takes {max}.");
    }

    public void Dispose()
    {
        Db.Dispose();
        _connection.Dispose();
    }
}
