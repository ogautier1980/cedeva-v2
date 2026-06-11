using Cedeva.Core.Interfaces;
using Cedeva.Infrastructure.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Cedeva.Tests.TestSupport;

/// <summary>
/// Spins up a real EF Core schema on a private SQLite in-memory database.
/// The connection is kept open for the lifetime of the instance so the database survives.
/// Use <see cref="NewContext"/> to get a fresh context over the same database (clears the
/// change tracker) so persisted state can be verified independently of the seeding context.
///
/// A current-user service is ALWAYS supplied (defaulting to admin) because the multi-tenancy
/// query filters dereference it during EF parameter extraction — a null service throws.
/// </summary>
public sealed class SqliteTestContext : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly ICurrentUserService _defaultUser;

    public CedevaDbContext Context { get; }

    public SqliteTestContext(ICurrentUserService? currentUser = null)
    {
        _defaultUser = currentUser ?? FakeCurrentUserService.Admin();
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        Context = Build(_defaultUser);
        Context.Database.EnsureCreated();
    }

    /// <summary>Fresh context over the same in-memory database (no shared change tracker).</summary>
    public CedevaDbContext NewContext(ICurrentUserService? currentUser = null) =>
        Build(currentUser ?? _defaultUser);

    private CedevaDbContext Build(ICurrentUserService currentUser)
    {
        var options = new DbContextOptionsBuilder<CedevaDbContext>()
            .UseSqlite(_connection)
            .Options;

        return new CedevaDbContext(options, currentUser);
    }

    public void Dispose()
    {
        Context.Dispose();
        _connection.Dispose();
    }
}
