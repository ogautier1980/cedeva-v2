using Cedeva.Core.Enums;
using Cedeva.Core.Interfaces;
using Cedeva.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Testcontainers.MsSql;

namespace Cedeva.Tests.Sql;

/// <summary>
/// Starts a real SQL Server 2022 in a throwaway Docker container (Testcontainers) and creates the
/// Cedeva schema on it. Shared across the "Sql" collection. Gives the same provider and default
/// CI collation as production — so behaviour SQLite cannot reproduce (case-insensitive equality,
/// LINQ translation) is exercised faithfully.
/// </summary>
public sealed class SqlServerFixture : IAsyncLifetime
{
    private readonly MsSqlContainer _container =
        new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();

    public CedevaDbContext NewContext() =>
        new(new DbContextOptionsBuilder<CedevaDbContext>()
                .UseSqlServer(_container.GetConnectionString())
                .Options,
            new StubCurrentUser());

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        await using var ctx = NewContext();
        // Apply the real migrations (not EnsureCreated) so the schema matches production exactly.
        await ctx.Database.MigrateAsync();
    }

    public async Task DisposeAsync() => await _container.DisposeAsync();

    private sealed class StubCurrentUser : ICurrentUserService
    {
        public string? UserId => "sql-test";
        public int? OrganisationId => null;
        public Role? Role => Cedeva.Core.Enums.Role.Admin;
        public bool IsAdmin => true;
    }
}

[CollectionDefinition("Sql")]
public class SqlCollection : ICollectionFixture<SqlServerFixture>;
