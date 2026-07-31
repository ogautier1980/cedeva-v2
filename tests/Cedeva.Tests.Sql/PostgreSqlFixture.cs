using Cedeva.Core.Enums;
using Cedeva.Core.Interfaces;
using Cedeva.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace Cedeva.Tests.Sql;

/// <summary>
/// Starts a real PostgreSQL 17 in a throwaway Docker container (Testcontainers) and creates the
/// Cedeva schema on it. Shared across the "Sql" collection. Gives the same provider as production
/// — so behaviour SQLite cannot reproduce (case-sensitive equality/ILIKE translation, real
/// migration chain) is exercised faithfully.
/// </summary>
public sealed class PostgreSqlFixture : IAsyncLifetime
{
    static PostgreSqlFixture()
    {
        // Same rationale as Program.cs: the app's business DateTime fields don't track
        // DateTimeKind, so restore Npgsql's pre-6.0 lenient timestamptz write behaviour.
        AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
    }

    private readonly PostgreSqlContainer _container =
        new PostgreSqlBuilder("postgres:17-alpine").Build();

    public CedevaDbContext NewContext() =>
        new(new DbContextOptionsBuilder<CedevaDbContext>()
                .UseNpgsql(_container.GetConnectionString())
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
public class SqlCollection : ICollectionFixture<PostgreSqlFixture>;
