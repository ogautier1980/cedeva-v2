using Cedeva.Core.Entities;
using Cedeva.Core.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Cedeva.Infrastructure.Data;

public class DbSeeder
{
    private readonly CedevaDbContext _context;
    private readonly UserManager<CedevaUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly ILogger<DbSeeder> _logger;

    public DbSeeder(
        CedevaDbContext context,
        UserManager<CedevaUser> userManager,
        RoleManager<IdentityRole> roleManager,
        ILogger<DbSeeder> logger)
    {
        _context = context;
        _userManager = userManager;
        _roleManager = roleManager;
        _logger = logger;
    }

    public async Task SeedAsync()
    {
        try
        {
            await _context.Database.MigrateAsync();

            await SeedRolesAsync();
            await SeedBelgianMunicipalitiesAsync();
            await SeedAdminUserAsync();
            await SeedDemoOrganisationAsync();

            _logger.LogInformation("Database seeding completed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while seeding the database");
            throw new InvalidOperationException("Database seeding failed", ex);
        }
    }

    private async Task SeedRolesAsync()
    {
        string[] roles = { "Admin", "Coordinator" };

        foreach (var role in roles)
        {
            if (!await _roleManager.RoleExistsAsync(role))
            {
                await _roleManager.CreateAsync(new IdentityRole(role));
                _logger.LogInformation("Created role: {Role}", role);
            }
        }
    }

    private async Task SeedAdminUserAsync()
    {
        const string adminEmail = "admin@cedeva.be";
        const string adminPassword = "Admin@123456";

        var existingAdmin = await _userManager.FindByEmailAsync(adminEmail);
        if (existingAdmin != null)
            return;

        var adminUser = new CedevaUser
        {
            UserName = adminEmail,
            Email = adminEmail,
            EmailConfirmed = true,
            FirstName = "Admin",
            LastName = "Cedeva",
            Role = Role.Admin
        };

        var result = await _userManager.CreateAsync(adminUser, adminPassword);
        if (result.Succeeded)
        {
            await _userManager.AddToRoleAsync(adminUser, "Admin");
            _logger.LogInformation("Created admin user: {Email}", adminEmail);
        }
        else
        {
            _logger.LogError("Failed to create admin user: {Errors}",
                string.Join(", ", result.Errors.Select(e => e.Description)));
        }
    }

    private async Task SeedDemoOrganisationAsync()
    {
        var existingCount = await _context.Organisations.CountAsync();
        if (existingCount >= 2)
            return;

        // First organisation
        if (existingCount == 0)
        {
            var address1 = new Address
            {
                Street = "Rue de la Plaine 1",
                City = "Gembloux",
                PostalCode = 5030,
                Country = Country.Belgium
            };

            var organisation1 = new Organisation
            {
                Name = "Plaine de Bossière",
                Description = "Organisation de stages pour enfants à Bossière",
                Address = address1
            };

            _context.Organisations.Add(organisation1);
            await _context.SaveChangesAsync();

            // Create a coordinator user for this organisation
            const string coordEmail1 = "coordinator@cedeva.be";
            const string coordPassword1 = "Coord@123456";

            var existingCoord1 = await _userManager.FindByEmailAsync(coordEmail1);
            if (existingCoord1 == null)
            {
                var coordUser1 = new CedevaUser
                {
                    UserName = coordEmail1,
                    Email = coordEmail1,
                    EmailConfirmed = true,
                    FirstName = "Coordinateur",
                    LastName = "Bossière",
                    Role = Role.Coordinator,
                    OrganisationId = organisation1.Id
                };

                var result1 = await _userManager.CreateAsync(coordUser1, coordPassword1);
                if (result1.Succeeded)
                {
                    await _userManager.AddToRoleAsync(coordUser1, "Coordinator");
                    _logger.LogInformation("Created coordinator user: {Email} for organisation {Org}",
                        coordEmail1, organisation1.Name);
                }
            }

            _logger.LogInformation("Created demo organisation: {Name}", organisation1.Name);
        }

        // Second organisation
        if (existingCount <= 1)
        {
            var address2 = new Address
            {
                Street = "Avenue des Sports 25",
                City = "Liège",
                PostalCode = 4000,
                Country = Country.Belgium
            };

            var organisation2 = new Organisation
            {
                Name = "Centre Récréatif Les Aventuriers",
                Description = "Centre de loisirs et stages pour enfants à Liège",
                Address = address2
            };

            _context.Organisations.Add(organisation2);
            await _context.SaveChangesAsync();

            // Create a coordinator user for this organisation
            const string coordEmail2 = "coordinator.liege@cedeva.be";
            const string coordPassword2 = "Coord@123456";

            var existingCoord2 = await _userManager.FindByEmailAsync(coordEmail2);
            if (existingCoord2 == null)
            {
                var coordUser2 = new CedevaUser
                {
                    UserName = coordEmail2,
                    Email = coordEmail2,
                    EmailConfirmed = true,
                    FirstName = "Sophie",
                    LastName = "Dumont",
                    Role = Role.Coordinator,
                    OrganisationId = organisation2.Id
                };

                var result2 = await _userManager.CreateAsync(coordUser2, coordPassword2);
                if (result2.Succeeded)
                {
                    await _userManager.AddToRoleAsync(coordUser2, "Coordinator");
                    _logger.LogInformation("Created coordinator user: {Email} for organisation {Org}",
                        coordEmail2, organisation2.Name);
                }
            }

            _logger.LogInformation("Created demo organisation: {Name}", organisation2.Name);
        }
    }

    private async Task SeedBelgianMunicipalitiesAsync()
    {
        // Remove existing municipalities
        var existingMunicipalities = await _context.BelgianMunicipalities.ToListAsync();
        if (existingMunicipalities.Any())
        {
            _context.BelgianMunicipalities.RemoveRange(existingMunicipalities);
            await _context.SaveChangesAsync();
            _logger.LogInformation("Removed {Count} existing Belgian municipalities", existingMunicipalities.Count);
        }

        // Load municipalities from CSV
        var municipalities = await GetBelgianMunicipalitiesFromCsvAsync();

        await _context.BelgianMunicipalities.AddRangeAsync(municipalities);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Seeded {Count} Belgian municipalities from CSV", municipalities.Count);
    }

    private static async Task<List<BelgianMunicipality>> GetBelgianMunicipalitiesFromCsvAsync()
    {
        var municipalities = new List<BelgianMunicipality>();
        var csvPath = Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "municipalities.csv");

        if (!File.Exists(csvPath))
        {
            throw new FileNotFoundException($"CSV file not found at: {csvPath}");
        }

        var lines = await File.ReadAllLinesAsync(csvPath);

        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            var parts = line.Split(';');
            if (parts.Length != 2)
                continue;

            if (int.TryParse(parts[0].Trim(), out var postalCode))
            {
                municipalities.Add(new BelgianMunicipality
                {
                    PostalCode = postalCode,
                    City = parts[1].Trim()
                });
            }
        }

        return municipalities;
    }
}
