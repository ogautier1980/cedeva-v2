using Cedeva.Core.Entities;
using Cedeva.Core.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Cedeva.Infrastructure.Data;

public class TestDataSeeder
{
    private readonly CedevaDbContext _context;
    private readonly ILogger<TestDataSeeder> _logger;
    private readonly Random _random = new Random(42); // Fixed seed for reproducibility

    private static readonly string[] FrenchFirstNamesMale = {
        "Antoine", "Thomas", "Lucas", "Louis", "Hugo", "Arthur", "Jules", "Gabriel", "Léo", "Nathan",
        "Mathis", "Alexandre", "Maxime", "Victor", "Noah", "Raphaël", "Théo", "Simon", "Julien", "Baptiste"
    };

    private static readonly string[] FrenchFirstNamesFemale = {
        "Emma", "Louise", "Alice", "Chloé", "Léa", "Manon", "Camille", "Sarah", "Marie", "Zoé",
        "Juliette", "Clara", "Laura", "Julie", "Charlotte", "Inès", "Lisa", "Pauline", "Elise", "Anaïs"
    };

    private static readonly string[] DutchFirstNamesMale = {
        "Lucas", "Milan", "Noah", "Liam", "Mathis", "Adam", "Louis", "Victor", "Arthur", "Jules",
        "Nathan", "Gabriel", "Maxime", "Raphaël", "Simon", "Hugo", "Thomas", "Alexandre", "Antoine", "Léon"
    };

    private static readonly string[] DutchFirstNamesFemale = {
        "Emma", "Louise", "Olivia", "Mila", "Alice", "Lina", "Nora", "Ella", "Anna", "Juliette",
        "Camille", "Léa", "Marie", "Clara", "Charlotte", "Luna", "Eva", "Nina", "Zoé", "Chloé"
    };

    private static readonly string[] BelgianLastNames = {
        "Dubois", "Lambert", "Dupont", "Martin", "Simon", "Bernard", "Leroy", "Robert", "Laurent", "Lefebvre",
        "Meunier", "Moreau", "Petit", "François", "Fontaine", "Claes", "Janssens", "Maes", "Jacobs", "Peeters",
        "Willems", "Hermans", "Goossens", "Wouters", "De Smet", "Vandenberghe", "Vermeulen", "Stevens", "Hendrickx", "Van Den Berg"
    };

    private static readonly string[] BelgianStreets = {
        "Rue de la Gare", "Avenue du Roi", "Rue des Fleurs", "Boulevard Leopold", "Rue Haute",
        "Avenue Louise", "Rue de Bruxelles", "Place Communale", "Rue du Marché", "Avenue de la Liberté",
        "Chaussée de Waterloo", "Rue Saint-Jean", "Boulevard Anspach", "Rue Neuve", "Avenue des Arts"
    };

    private static readonly string[] BelgianCities = {
        "Bruxelles", "Liège", "Charleroi", "Gand", "Anvers", "Schaerbeek", "Anderlecht", "Bruges",
        "Namur", "Louvain", "Mons", "Verviers", "Malines", "Aalst", "Tournai"
    };

    public TestDataSeeder(CedevaDbContext context, ILogger<TestDataSeeder> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task SeedTestDataAsync()
    {
        try
        {
            _logger.LogInformation("Starting test data seeding...");

            // Get all organisations
            var organisations = await _context.Organisations.IgnoreQueryFilters().ToListAsync();
            if (!organisations.Any())
            {
                _logger.LogWarning("No organisations found. Cannot seed test data.");
                return;
            }

            _logger.LogInformation("Found {Count} organisation(s) to seed data for", organisations.Count);

            // Loop through each organisation and seed data
            foreach (var organisation in organisations)
            {
                _logger.LogInformation("Seeding data for organisation: {OrganisationName} (ID: {OrganisationId})",
                    organisation.Name, organisation.Id);

                // Seed test data only if it doesn't exist
                var parentCount = await _context.Parents.IgnoreQueryFilters().CountAsync(p => p.OrganisationId == organisation.Id);
                if (parentCount < 10)
                {
                    await SeedParentsAndChildrenAsync(organisation.Id, 25);
                }
                else
                {
                    _logger.LogInformation("Organisation {OrganisationName} already has {Count} parents. Skipping parent seeding.",
                        organisation.Name, parentCount);
                }

                var teamMemberCount = await _context.TeamMembers.IgnoreQueryFilters().CountAsync(t => t.OrganisationId == organisation.Id);
                if (teamMemberCount < 10)
                {
                    await SeedTeamMembersAsync(organisation.Id, 12);
                }
                else
                {
                    _logger.LogInformation("Organisation {OrganisationName} already has {Count} team members. Skipping team member seeding.",
                        organisation.Name, teamMemberCount);
                }

                var activityCount = await _context.Activities.IgnoreQueryFilters().CountAsync(a => a.OrganisationId == organisation.Id);
                if (activityCount < 3)
                {
                    await SeedActivitiesAsync(organisation.Id, 4);
                }
                else
                {
                    _logger.LogInformation("Organisation {OrganisationName} already has {Count} activities. Skipping activity seeding.",
                        organisation.Name, activityCount);
                }

                var bookingCount = await _context.Bookings.IgnoreQueryFilters()
                    .Include(b => b.Child)
                        .ThenInclude(c => c.Parent)
                    .CountAsync(b => b.Child.Parent.OrganisationId == organisation.Id);
                if (bookingCount < 5)
                {
                    await SeedBookingsAsync(organisation.Id);
                }
                else
                {
                    _logger.LogInformation("Organisation {OrganisationName} already has {Count} bookings. Skipping booking seeding.",
                        organisation.Name, bookingCount);
                }

                await _context.SaveChangesAsync();

                _logger.LogInformation("Completed seeding data for organisation: {OrganisationName}", organisation.Name);
            }

            _logger.LogInformation("Test data seeding completed successfully for all organisations");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while seeding test data");
            throw;
        }
    }

    private async Task SeedParentsAndChildrenAsync(int organisationId, int count)
    {
        _logger.LogInformation("Seeding {Count} parents with children...", count);

        for (int i = 0; i < count; i++)
        {
            var isFemale = _random.Next(2) == 0;
            var isFrench = _random.Next(2) == 0;

            var firstName = isFemale
                ? (isFrench ? FrenchFirstNamesFemale[_random.Next(FrenchFirstNamesFemale.Length)]
                            : DutchFirstNamesFemale[_random.Next(DutchFirstNamesFemale.Length)])
                : (isFrench ? FrenchFirstNamesMale[_random.Next(FrenchFirstNamesMale.Length)]
                            : DutchFirstNamesMale[_random.Next(DutchFirstNamesMale.Length)]);

            var lastName = BelgianLastNames[_random.Next(BelgianLastNames.Length)];
            var birthDate = GenerateRandomBirthDate(1975, 1995);
            var nationalRegisterNumber = GenerateNationalRegisterNumber(birthDate, isFemale);

            var address = new Address
            {
                Street = $"{BelgianStreets[_random.Next(BelgianStreets.Length)]} {_random.Next(1, 200)}",
                PostalCode = _random.Next(1000, 10000),
                City = BelgianCities[_random.Next(BelgianCities.Length)],
                Country = Country.Belgium
            };

            var parent = new Parent
            {
                FirstName = firstName,
                LastName = lastName,
                Email = $"{firstName.ToLower()}.{lastName.ToLower()}@example.be",
                PhoneNumber = GenerateBelgianPhoneNumber(false),
                MobilePhoneNumber = GenerateBelgianPhoneNumber(true),
                NationalRegisterNumber = nationalRegisterNumber,
                Address = address,
                OrganisationId = organisationId
            };

            _context.Parents.Add(parent);
            await _context.SaveChangesAsync(); // Save to get parent ID

            // Add 1-3 children per parent
            var childCount = _random.Next(1, 4);
            for (int j = 0; j < childCount; j++)
            {
                var childIsFemale = _random.Next(2) == 0;
                var childFirstName = childIsFemale
                    ? (isFrench ? FrenchFirstNamesFemale[_random.Next(FrenchFirstNamesFemale.Length)]
                                : DutchFirstNamesFemale[_random.Next(DutchFirstNamesFemale.Length)])
                    : (isFrench ? FrenchFirstNamesMale[_random.Next(FrenchFirstNamesMale.Length)]
                                : DutchFirstNamesMale[_random.Next(DutchFirstNamesMale.Length)]);

                var childBirthDate = GenerateRandomBirthDate(2010, 2020);

                var child = new Child
                {
                    FirstName = childFirstName,
                    LastName = lastName,
                    BirthDate = childBirthDate,
                    ParentId = parent.Id,
                    NationalRegisterNumber = GenerateNationalRegisterNumber(childBirthDate, childIsFemale),
                    IsDisadvantagedEnvironment = _random.Next(5) == 0, // 20% chance
                    IsMildDisability = _random.Next(10) == 0, // 10% chance
                    IsSevereDisability = _random.Next(20) == 0 // 5% chance
                };

                _context.Children.Add(child);
            }
        }

        await _context.SaveChangesAsync();
        _logger.LogInformation("Successfully seeded parents and children");
    }

    private async Task SeedTeamMembersAsync(int organisationId, int count)
    {
        _logger.LogInformation("Seeding {Count} team members...", count);

        for (int i = 0; i < count; i++)
        {
            var isFemale = _random.Next(2) == 0;
            var isFrench = _random.Next(2) == 0;

            var firstName = isFemale
                ? (isFrench ? FrenchFirstNamesFemale[_random.Next(FrenchFirstNamesFemale.Length)]
                            : DutchFirstNamesFemale[_random.Next(DutchFirstNamesFemale.Length)])
                : (isFrench ? FrenchFirstNamesMale[_random.Next(FrenchFirstNamesMale.Length)]
                            : DutchFirstNamesMale[_random.Next(DutchFirstNamesMale.Length)]);

            var lastName = BelgianLastNames[_random.Next(BelgianLastNames.Length)];
            var birthDate = GenerateRandomBirthDate(1990, 2005);

            var address = new Address
            {
                Street = $"{BelgianStreets[_random.Next(BelgianStreets.Length)]} {_random.Next(1, 200)}",
                PostalCode = _random.Next(1000, 10000),
                City = BelgianCities[_random.Next(BelgianCities.Length)],
                Country = Country.Belgium
            };

            var role = (TeamRole)_random.Next(0, 4); // Random role
            var status = (Status)_random.Next(0, 3); // Random status
            var license = (License)_random.Next(0, 4); // Random license

            var teamMember = new TeamMember
            {
                FirstName = firstName,
                LastName = lastName,
                Email = $"{firstName.ToLower()}.{lastName.ToLower()}.team@example.be",
                MobilePhoneNumber = GenerateBelgianPhoneNumber(true),
                BirthDate = birthDate,
                NationalRegisterNumber = GenerateNationalRegisterNumber(birthDate, isFemale),
                TeamRole = role,
                Status = status,
                License = license,
                LicenseUrl = "https://example.com/license.pdf",
                DailyCompensation = _random.Next(2) == 0 ? _random.Next(30, 100) : null,
                Address = address,
                OrganisationId = organisationId
            };

            _context.TeamMembers.Add(teamMember);
        }

        await _context.SaveChangesAsync();
        _logger.LogInformation("Successfully seeded team members");
    }

    private async Task SeedActivitiesAsync(int organisationId, int count)
    {
        _logger.LogInformation("Seeding {Count} activities...", count);

        var activityNames = new[]
        {
            "Stage d'été - Juillet 2026",
            "Stage de Pâques 2026",
            "Stage de Carnaval 2026",
            "Stage d'hiver 2026",
            "Stage d'été - Août 2026"
        };

        for (int i = 0; i < count && i < activityNames.Length; i++)
        {
            var startDate = DateTime.Today.AddDays(_random.Next(30, 180));
            var duration = _random.Next(3, 8); // 3-7 days
            var endDate = startDate.AddDays(duration);

            var activity = new Activity
            {
                Name = activityNames[i],
                Description = $"Stage pour enfants de 6 à 12 ans avec activités ludiques et sportives à {BelgianCities[_random.Next(BelgianCities.Length)]}.",
                StartDate = startDate,
                EndDate = endDate,
                PricePerDay = _random.Next(15, 35),
                IsActive = true,
                OrganisationId = organisationId
            };

            _context.Activities.Add(activity);
            await _context.SaveChangesAsync(); // Save to get activity ID

            // Add activity days
            for (var date = startDate; date <= endDate; date = date.AddDays(1))
            {
                var activityDay = new ActivityDay
                {
                    ActivityId = activity.Id,
                    DayDate = date,
                    IsActive = true
                };
                _context.ActivityDays.Add(activityDay);
            }

            // Add 2-3 groups per activity
            var groupCount = _random.Next(2, 4);
            var groupNames = new[] { "Groupe Rouge", "Groupe Bleu", "Groupe Vert", "Groupe Jaune" };
            for (int g = 0; g < groupCount; g++)
            {
                var group = new ActivityGroup
                {
                    Label = groupNames[g],
                    Capacity = _random.Next(10, 20),
                    ActivityId = activity.Id
                };
                _context.ActivityGroups.Add(group);
            }
        }

        await _context.SaveChangesAsync();
        _logger.LogInformation("Successfully seeded activities with days and groups");
    }

    private async Task SeedBookingsAsync(int organisationId)
    {
        _logger.LogInformation("Seeding bookings...");

        var children = await _context.Children
            .IgnoreQueryFilters()
            .Include(c => c.Parent)
            .Where(c => c.Parent.OrganisationId == organisationId)
            .ToListAsync();

        var activities = await _context.Activities
            .IgnoreQueryFilters()
            .Include(a => a.Days)
            .Where(a => a.OrganisationId == organisationId)
            .ToListAsync();

        var groups = await _context.ActivityGroups.ToListAsync();

        // Create bookings for 60% of children
        var childrenToBook = children.OrderBy(x => _random.Next()).Take((int)(children.Count * 0.6)).ToList();

        foreach (var child in childrenToBook)
        {
            // Each child books 1-2 activities
            var activitiesToBook = activities.OrderBy(x => _random.Next()).Take(_random.Next(1, 3)).ToList();

            foreach (var activity in activitiesToBook)
            {
                var activityGroups = groups.Where(g => g.ActivityId == activity.Id).ToList();
                var group = activityGroups.Any() ? activityGroups[_random.Next(activityGroups.Count)] : null;

                var booking = new Booking
                {
                    ChildId = child.Id,
                    ActivityId = activity.Id,
                    GroupId = group?.Id,
                    BookingDate = DateTime.Now.AddDays(-_random.Next(1, 60)),
                    IsConfirmed = _random.Next(10) > 1, // 90% confirmed
                    IsMedicalSheet = _random.Next(10) > 3 // 70% have medical sheet
                };

                _context.Bookings.Add(booking);
                await _context.SaveChangesAsync(); // Save to get booking ID

                // Add booking days (book 50-100% of activity days)
                var daysToBook = activity.Days
                    .OrderBy(x => _random.Next())
                    .Take(_random.Next(activity.Days.Count / 2, activity.Days.Count + 1))
                    .ToList();

                foreach (var activityDay in daysToBook)
                {
                    var bookingDay = new BookingDay
                    {
                        BookingId = booking.Id,
                        ActivityDayId = activityDay.DayId,
                        IsReserved = true,
                        IsPresent = _random.Next(10) > 1 // 90% present
                    };
                    _context.BookingDays.Add(bookingDay);
                }
            }
        }

        await _context.SaveChangesAsync();
        _logger.LogInformation("Successfully seeded bookings and booking days");
    }

    private DateTime GenerateRandomBirthDate(int minYear, int maxYear)
    {
        var year = _random.Next(minYear, maxYear + 1);
        var month = _random.Next(1, 13);
        var day = _random.Next(1, DateTime.DaysInMonth(year, month) + 1);
        return new DateTime(year, month, day);
    }

    private string GenerateNationalRegisterNumber(DateTime birthDate, bool isFemale)
    {
        // Belgian National Register Number format: YY.MM.DD-XXX.YY
        // XXX is a sequence number (odd for males, even for females)
        // YY is a checksum

        var year = birthDate.Year % 100;
        var month = birthDate.Month;
        var day = birthDate.Day;

        // Generate sequence number (001-997 for males odd, 002-998 for females even)
        var sequence = _random.Next(1, 499) * 2;
        if (!isFemale) sequence -= 1; // Make odd for males

        // Calculate checksum
        var birthString = $"{year:D2}{month:D2}{day:D2}";
        var fullYear = birthDate.Year;

        // For people born after 2000, add 2000000000 to the number for checksum calculation
        var checksumBase = long.Parse(birthString + $"{sequence:D3}");
        if (fullYear >= 2000)
        {
            checksumBase += 2000000000;
        }

        var checksum = 97 - (checksumBase % 97);

        return $"{year:D2}.{month:D2}.{day:D2}-{sequence:D3}.{checksum:D2}";
    }

    private string GenerateBelgianPhoneNumber(bool mobile)
    {
        if (mobile)
        {
            // Belgian mobile: 04XX XX XX XX
            var prefix = _random.Next(70, 100); // 470-499
            var part1 = _random.Next(10, 100);
            var part2 = _random.Next(10, 100);
            var part3 = _random.Next(10, 100);
            return $"04{prefix:D2} {part1:D2} {part2:D2} {part3:D2}";
        }
        else
        {
            // Belgian landline: 0X XXX XX XX (X = 2-9 for area code)
            var areaCode = _random.Next(2, 10);
            var part1 = _random.Next(100, 1000);
            var part2 = _random.Next(10, 100);
            var part3 = _random.Next(10, 100);
            return $"0{areaCode} {part1:D3} {part2:D2} {part3:D2}";
        }
    }
}
