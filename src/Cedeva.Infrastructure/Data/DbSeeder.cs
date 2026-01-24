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
            throw;
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
        if (await _context.Organisations.AnyAsync())
            return;

        var address = new Address
        {
            Street = "Rue de la Plaine 1",
            City = "Gembloux",
            PostalCode = 5030,
            Country = Country.Belgium
        };

        var organisation = new Organisation
        {
            Name = "Plaine de Bossière",
            Description = "Organisation de stages pour enfants à Bossière",
            Address = address
        };

        _context.Organisations.Add(organisation);
        await _context.SaveChangesAsync();

        // Create a coordinator user for this organisation
        const string coordEmail = "coordinator@cedeva.be";
        const string coordPassword = "Coord@123456";

        var existingCoord = await _userManager.FindByEmailAsync(coordEmail);
        if (existingCoord == null)
        {
            var coordUser = new CedevaUser
            {
                UserName = coordEmail,
                Email = coordEmail,
                EmailConfirmed = true,
                FirstName = "Coordinateur",
                LastName = "Demo",
                Role = Role.Coordinator,
                OrganisationId = organisation.Id
            };

            var result = await _userManager.CreateAsync(coordUser, coordPassword);
            if (result.Succeeded)
            {
                await _userManager.AddToRoleAsync(coordUser, "Coordinator");
                _logger.LogInformation("Created coordinator user: {Email} for organisation {Org}",
                    coordEmail, organisation.Name);
            }
        }

        _logger.LogInformation("Created demo organisation: {Name}", organisation.Name);
    }

    private async Task SeedBelgianMunicipalitiesAsync()
    {
        if (await _context.BelgianMunicipalities.AnyAsync())
            return;

        var municipalities = GetBelgianMunicipalities();

        await _context.BelgianMunicipalities.AddRangeAsync(municipalities);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Seeded {Count} Belgian municipalities", municipalities.Count);
    }

    private static List<BelgianMunicipality> GetBelgianMunicipalities()
    {
        // Common Belgian municipalities - this is a subset, full list can be imported from CSV
        return new List<BelgianMunicipality>
        {
            // Bruxelles
            new() { PostalCode = 1000, City = "Bruxelles" },
            new() { PostalCode = 1020, City = "Laeken" },
            new() { PostalCode = 1030, City = "Schaerbeek" },
            new() { PostalCode = 1040, City = "Etterbeek" },
            new() { PostalCode = 1050, City = "Ixelles" },
            new() { PostalCode = 1060, City = "Saint-Gilles" },
            new() { PostalCode = 1070, City = "Anderlecht" },
            new() { PostalCode = 1080, City = "Molenbeek-Saint-Jean" },
            new() { PostalCode = 1081, City = "Koekelberg" },
            new() { PostalCode = 1082, City = "Berchem-Sainte-Agathe" },
            new() { PostalCode = 1083, City = "Ganshoren" },
            new() { PostalCode = 1090, City = "Jette" },
            new() { PostalCode = 1140, City = "Evere" },
            new() { PostalCode = 1150, City = "Woluwe-Saint-Pierre" },
            new() { PostalCode = 1160, City = "Auderghem" },
            new() { PostalCode = 1170, City = "Watermael-Boitsfort" },
            new() { PostalCode = 1180, City = "Uccle" },
            new() { PostalCode = 1190, City = "Forest" },
            new() { PostalCode = 1200, City = "Woluwe-Saint-Lambert" },
            new() { PostalCode = 1210, City = "Saint-Josse-ten-Noode" },

            // Brabant Wallon
            new() { PostalCode = 1300, City = "Wavre" },
            new() { PostalCode = 1310, City = "La Hulpe" },
            new() { PostalCode = 1315, City = "Incourt" },
            new() { PostalCode = 1320, City = "Beauvechain" },
            new() { PostalCode = 1325, City = "Chaumont-Gistoux" },
            new() { PostalCode = 1330, City = "Rixensart" },
            new() { PostalCode = 1340, City = "Ottignies-Louvain-la-Neuve" },
            new() { PostalCode = 1348, City = "Louvain-la-Neuve" },
            new() { PostalCode = 1350, City = "Orp-Jauche" },
            new() { PostalCode = 1360, City = "Perwez" },
            new() { PostalCode = 1370, City = "Jodoigne" },
            new() { PostalCode = 1380, City = "Lasne" },
            new() { PostalCode = 1390, City = "Grez-Doiceau" },
            new() { PostalCode = 1400, City = "Nivelles" },
            new() { PostalCode = 1410, City = "Waterloo" },
            new() { PostalCode = 1420, City = "Braine-l'Alleud" },
            new() { PostalCode = 1430, City = "Rebecq" },
            new() { PostalCode = 1440, City = "Braine-le-Château" },
            new() { PostalCode = 1450, City = "Chastre" },
            new() { PostalCode = 1457, City = "Walhain" },
            new() { PostalCode = 1460, City = "Ittre" },
            new() { PostalCode = 1470, City = "Genappe" },
            new() { PostalCode = 1480, City = "Tubize" },
            new() { PostalCode = 1490, City = "Court-Saint-Etienne" },
            new() { PostalCode = 1495, City = "Villers-la-Ville" },

            // Namur
            new() { PostalCode = 5000, City = "Namur" },
            new() { PostalCode = 5001, City = "Belgrade" },
            new() { PostalCode = 5002, City = "Saint-Servais" },
            new() { PostalCode = 5003, City = "Saint-Marc" },
            new() { PostalCode = 5004, City = "Bouge" },
            new() { PostalCode = 5020, City = "Champion" },
            new() { PostalCode = 5030, City = "Gembloux" },
            new() { PostalCode = 5031, City = "Grand-Manil" },
            new() { PostalCode = 5032, City = "Bossière" },
            new() { PostalCode = 5060, City = "Sambreville" },
            new() { PostalCode = 5070, City = "Fosses-la-Ville" },
            new() { PostalCode = 5080, City = "La Bruyère" },
            new() { PostalCode = 5100, City = "Jambes" },
            new() { PostalCode = 5101, City = "Erpent" },
            new() { PostalCode = 5140, City = "Sombreffe" },
            new() { PostalCode = 5150, City = "Floreffe" },
            new() { PostalCode = 5170, City = "Profondeville" },
            new() { PostalCode = 5190, City = "Jemeppe-sur-Sambre" },

            // Liège
            new() { PostalCode = 4000, City = "Liège" },
            new() { PostalCode = 4020, City = "Liège" },
            new() { PostalCode = 4030, City = "Grivegnée" },
            new() { PostalCode = 4040, City = "Herstal" },
            new() { PostalCode = 4050, City = "Chaudfontaine" },
            new() { PostalCode = 4100, City = "Seraing" },
            new() { PostalCode = 4120, City = "Neupré" },
            new() { PostalCode = 4130, City = "Esneux" },
            new() { PostalCode = 4140, City = "Sprimont" },
            new() { PostalCode = 4150, City = "Nandrin" },

            // Hainaut
            new() { PostalCode = 6000, City = "Charleroi" },
            new() { PostalCode = 6001, City = "Marcinelle" },
            new() { PostalCode = 6010, City = "Couillet" },
            new() { PostalCode = 6020, City = "Dampremy" },
            new() { PostalCode = 6030, City = "Marchienne-au-Pont" },
            new() { PostalCode = 6040, City = "Jumet" },
            new() { PostalCode = 6041, City = "Gosselies" },
            new() { PostalCode = 6042, City = "Lodelinsart" },
            new() { PostalCode = 6043, City = "Ransart" },
            new() { PostalCode = 6044, City = "Roux" },
            new() { PostalCode = 6060, City = "Gilly" },
            new() { PostalCode = 6061, City = "Montignies-sur-Sambre" },
            new() { PostalCode = 7000, City = "Mons" },
            new() { PostalCode = 7010, City = "Shape" },
            new() { PostalCode = 7011, City = "Ghlin" },
            new() { PostalCode = 7012, City = "Jemappes" },
            new() { PostalCode = 7020, City = "Nimy" },
            new() { PostalCode = 7030, City = "Saint-Symphorien" },
            new() { PostalCode = 7050, City = "Jurbise" },
            new() { PostalCode = 7060, City = "Soignies" },
            new() { PostalCode = 7100, City = "La Louvière" },
            new() { PostalCode = 7500, City = "Tournai" },

            // Luxembourg
            new() { PostalCode = 6600, City = "Bastogne" },
            new() { PostalCode = 6700, City = "Arlon" },
            new() { PostalCode = 6720, City = "Habay" },
            new() { PostalCode = 6800, City = "Libramont-Chevigny" },
            new() { PostalCode = 6820, City = "Florenville" },
            new() { PostalCode = 6830, City = "Bouillon" },
            new() { PostalCode = 6840, City = "Neufchâteau" },
            new() { PostalCode = 6850, City = "Paliseul" },
            new() { PostalCode = 6860, City = "Léglise" },
            new() { PostalCode = 6870, City = "Saint-Hubert" },
            new() { PostalCode = 6880, City = "Bertrix" },
            new() { PostalCode = 6890, City = "Libin" },
            new() { PostalCode = 6900, City = "Marche-en-Famenne" },
            new() { PostalCode = 6920, City = "Wellin" },
            new() { PostalCode = 6940, City = "Durbuy" },
            new() { PostalCode = 6950, City = "Nassogne" },
            new() { PostalCode = 6960, City = "Manhay" },
            new() { PostalCode = 6970, City = "Tenneville" },
            new() { PostalCode = 6980, City = "La Roche-en-Ardenne" },
            new() { PostalCode = 6990, City = "Hotton" },

            // Flandre (quelques villes principales)
            new() { PostalCode = 2000, City = "Antwerpen" },
            new() { PostalCode = 2018, City = "Antwerpen" },
            new() { PostalCode = 2020, City = "Antwerpen" },
            new() { PostalCode = 2100, City = "Deurne" },
            new() { PostalCode = 2140, City = "Borgerhout" },
            new() { PostalCode = 2600, City = "Berchem" },
            new() { PostalCode = 2610, City = "Wilrijk" },
            new() { PostalCode = 2800, City = "Mechelen" },
            new() { PostalCode = 3000, City = "Leuven" },
            new() { PostalCode = 3001, City = "Heverlee" },
            new() { PostalCode = 3010, City = "Kessel-Lo" },
            new() { PostalCode = 3500, City = "Hasselt" },
            new() { PostalCode = 3600, City = "Genk" },
            new() { PostalCode = 8000, City = "Brugge" },
            new() { PostalCode = 8200, City = "Sint-Andries" },
            new() { PostalCode = 8310, City = "Assebroek" },
            new() { PostalCode = 8400, City = "Oostende" },
            new() { PostalCode = 8500, City = "Kortrijk" },
            new() { PostalCode = 9000, City = "Gent" },
            new() { PostalCode = 9030, City = "Mariakerke" },
            new() { PostalCode = 9040, City = "Sint-Amandsberg" },
            new() { PostalCode = 9050, City = "Gentbrugge" },
            new() { PostalCode = 9100, City = "Sint-Niklaas" },
            new() { PostalCode = 9200, City = "Dendermonde" },
            new() { PostalCode = 9300, City = "Aalst" },
        };
    }
}
