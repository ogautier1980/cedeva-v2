using Cedeva.Infrastructure.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Cedeva.Tests.TestSupport;

/// <summary>
/// Boots the real Cedeva web app in-memory for integration tests, but:
/// - swaps SQL Server for a private SQLite in-memory database (shared open connection),
/// - disables the background startup seeding (SQL Server migrations don't run on SQLite),
/// - replaces cookie/Identity auth with <see cref="TestAuthHandler"/> (header-driven claims).
/// Seed data via <see cref="Seed{T}"/>, then call <see cref="CreateClientFor"/>.
/// </summary>
public class CedevaWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _connection;

    public CedevaWebApplicationFactory()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Development => LocalFileStorageService (no Azure Blob); disable startup seeding.
        builder.UseEnvironment("Development");
        builder.UseSetting("RunStartupSeeding", "false");

        builder.ConfigureTestServices(services =>
        {
            // Replace the SQL Server DbContext with SQLite over a shared open connection.
            var toRemove = services.Where(d =>
                d.ServiceType == typeof(DbContextOptions<CedevaDbContext>) ||
                d.ServiceType == typeof(CedevaDbContext) ||
                (d.ServiceType.IsGenericType &&
                 d.ServiceType.Name.Contains("DbContextOptionsConfiguration"))).ToList();
            foreach (var d in toRemove) services.Remove(d);

            services.AddDbContext<CedevaDbContext>(options => options.UseSqlite(_connection));

            // Force the Test authentication scheme as the default for [Authorize].
            services.AddAuthentication(options =>
            {
                options.DefaultScheme = TestAuthHandler.SchemeName;
                options.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                options.DefaultChallengeScheme = TestAuthHandler.SchemeName;
            }).AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });

            // Bypass antiforgery so tests can POST without round-tripping a token.
            services.AddSingleton<Microsoft.AspNetCore.Antiforgery.IAntiforgery, FakeAntiforgery>();
        });
    }

    /// <summary>Creates the schema (once) and seeds data. Returns the seeder's result with DB-assigned IDs.</summary>
    public T Seed<T>(Func<CedevaDbContext, T> seed)
    {
        using var scope = Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<CedevaDbContext>();
        ctx.Database.EnsureCreated();
        var result = seed(ctx);
        ctx.SaveChanges();
        return result;
    }

    /// <summary>Fresh DbContext over the same database for asserting persisted state.</summary>
    public CedevaDbContext NewDbContext()
    {
        var scope = Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<CedevaDbContext>();
    }

    /// <summary>An HttpClient that authenticates as the given user (no auto-redirect).</summary>
    public HttpClient CreateClientFor(string userId, int? organisationId, string? role)
    {
        var client = CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserHeader, $"{userId}|{organisationId}|{role}");
        return client;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing) _connection.Dispose();
    }
}
