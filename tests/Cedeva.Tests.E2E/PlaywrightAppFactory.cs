using Cedeva.Core.Entities;
using Cedeva.Core.Enums;
using Cedeva.Infrastructure.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Cedeva.Tests.E2E;

/// <summary>
/// Boots the real Cedeva app on a real Kestrel server (random localhost port) so a browser
/// can drive it, while swapping SQL Server for a private SQLite database. Follows the official
/// "integration tests with Playwright" pattern: build the TestServer host the base factory
/// expects, then build and start a second Kestrel host that shares the same service config.
///
/// Kestrel is bound to http only; with no https port configured, app.UseHttpsRedirection()
/// becomes a no-op, so navigations are not redirected to an unbound https port.
/// </summary>
public sealed class PlaywrightAppFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _connection;
    private IHost? _kestrelHost;
    private string? _serverAddress;

    public PlaywrightAppFactory()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
    }

    /// <summary>Base address of the live Kestrel server (e.g. http://127.0.0.1:5xxxx).</summary>
    public string ServerAddress
    {
        get
        {
            _ = Server; // force host creation (runs CreateHost below)
            return _serverAddress!;
        }
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("RunStartupSeeding", "false");
        builder.UseUrls("http://127.0.0.1:0"); // random free port, honored by Kestrel

        builder.ConfigureTestServices(services =>
        {
            var toRemove = services.Where(d =>
                d.ServiceType == typeof(DbContextOptions<CedevaDbContext>) ||
                d.ServiceType == typeof(CedevaDbContext) ||
                (d.ServiceType.IsGenericType &&
                 d.ServiceType.Name.Contains("DbContextOptionsConfiguration"))).ToList();
            foreach (var d in toRemove) services.Remove(d);

            services.AddDbContext<CedevaDbContext>(options => options.UseSqlite(_connection));
        });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        // The in-memory TestServer host the base WebApplicationFactory drives via Services.
        var testHost = builder.Build();

        // A second, network-facing host on Kestrel that the browser will actually hit.
        builder.ConfigureWebHost(b => b.UseKestrel());
        _kestrelHost = builder.Build();
        _kestrelHost.Start();

        var addresses = _kestrelHost.Services.GetRequiredService<IServer>()
            .Features.Get<IServerAddressesFeature>()!;
        _serverAddress = addresses.Addresses.Single();

        testHost.Start();
        return testHost;
    }

    /// <summary>Creates the schema once and seeds an active, future-dated activity. Returns its id.</summary>
    public int SeedFutureActivity()
    {
        using var scope = Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<CedevaDbContext>();
        ctx.Database.EnsureCreated();

        var organisation = new Organisation
        {
            Name = "E2E Org",
            Description = "Organisation E2E",
            Address = new Address { Street = "Rue E2E", City = "Bruxelles", PostalCode = "1000", Country = Country.Belgium },
            BankAccountNumber = "BE68539007547034",
            BankAccountName = "E2E Org"
        };
        var activity = new Activity
        {
            Name = "Stage E2E",
            Description = "Activité E2E",
            IsActive = true,
            PricePerDay = 20m,
            StartDate = DateTime.Now.AddMonths(2),
            EndDate = DateTime.Now.AddMonths(2).AddDays(4),
            Organisation = organisation
        };
        ctx.Add(activity);
        ctx.SaveChanges();
        return activity.Id;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            _kestrelHost?.Dispose();
            _connection.Dispose();
        }
    }
}
