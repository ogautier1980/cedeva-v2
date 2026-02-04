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
    private int _structuredCommCounter = 100;
    private List<(string PostalCode, string City)> _municipalities = new();

    private const string ExampleLicenseUrl = "https://example.com/license.pdf";

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
            var organisations = await _context.Organisations.IgnoreQueryFilters().ToListAsync();
            if (!organisations.Any())
            {
                _logger.LogWarning("No organisations found. Cannot seed test data.");
                return;
            }

            // Load real municipalities for postal codes
            _municipalities = (await _context.BelgianMunicipalities
                .Select(m => new { m.PostalCode, m.City })
                .Distinct()
                .ToListAsync())
                .Select(m => (m.PostalCode, m.City))
                .ToList();

            _logger.LogInformation("Starting test data seeding for {Count} organisation(s), {Muni} municipalities loaded",
                organisations.Count, _municipalities.Count);

            foreach (var organisation in organisations)
            {
                await EnsureOrganisationBankAccountAsync(organisation);

                // Core entities
                var parentCount = await _context.Parents.IgnoreQueryFilters().CountAsync(p => p.OrganisationId == organisation.Id);
                if (parentCount < 10)
                    await SeedParentsAndChildrenAsync(organisation.Id, 25);

                var teamMemberCount = await _context.TeamMembers.IgnoreQueryFilters().CountAsync(t => t.OrganisationId == organisation.Id);
                if (teamMemberCount < 10)
                    await SeedTeamMembersAsync(organisation.Id, 12);

                var activityCount = await _context.Activities.IgnoreQueryFilters().CountAsync(a => a.OrganisationId == organisation.Id);
                if (activityCount < 3)
                    await SeedActivitiesAsync(organisation.Id, 4);

                // Activity ↔ team member assignments
                await SeedActivityTeamMembersAsync(organisation.Id);

                // Bookings with financial fields
                var bookingCount = await _context.Bookings.IgnoreQueryFilters()
                    .Include(b => b.Child).ThenInclude(c => c.Parent)
                    .CountAsync(b => b.Child.Parent.OrganisationId == organisation.Id);
                if (bookingCount < 5)
                    await SeedBookingsAsync(organisation.Id);

                // Payments
                var activityIds = await _context.Activities.IgnoreQueryFilters()
                    .Where(a => a.OrganisationId == organisation.Id).Select(a => a.Id).ToListAsync();
                var paymentCount = await _context.Payments.IgnoreQueryFilters()
                    .Where(p => _context.Bookings.IgnoreQueryFilters().Any(b => b.Id == p.BookingId && activityIds.Contains(b.ActivityId)))
                    .CountAsync();
                if (paymentCount == 0)
                    await SeedPaymentsAsync(organisation.Id);

                // CODA + bank transactions
                var codaCount = await _context.CodaFiles.IgnoreQueryFilters()
                    .CountAsync(c => c.OrganisationId == organisation.Id);
                if (codaCount == 0)
                    await SeedCodaAndBankTransactionsAsync(organisation.Id);

                // Expenses (per activity, skips if already present)
                await SeedExpensesAsync(organisation.Id);

                // Excursions (after bookings + payments so we can update TotalAmount)
                await SeedExcursionsAsync(organisation.Id);

                // Email templates
                var templateCount = await _context.EmailTemplates.IgnoreQueryFilters()
                    .CountAsync(t => t.OrganisationId == organisation.Id);
                if (templateCount == 0)
                    await SeedEmailTemplatesAsync(organisation.Id);

                await _context.SaveChangesAsync();
            }

            _logger.LogInformation("Test data seeding completed successfully for all organisations");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while seeding test data");
            throw new InvalidOperationException("Test data seeding failed", ex);
        }
    }

    // =========================================================================
    // Organisation bank account
    // =========================================================================
    private async Task EnsureOrganisationBankAccountAsync(Organisation organisation)
    {
        if (!string.IsNullOrEmpty(organisation.BankAccountNumber)) return;

        organisation.BankAccountNumber = organisation.Id == 1 ? "BE68539007547034" : "BE76539007547035";
        organisation.BankAccountName = organisation.Id == 1 ? "Plaine de Bossière ASBL" : "Les Aventuriers ASBL";
        await _context.SaveChangesAsync();
        _logger.LogInformation("Updated bank account for organisation {Id}", organisation.Id);
    }

    private (string PostalCode, string City) GetRandomMunicipality()
    {
        if (_municipalities.Any())
            return _municipalities[_random.Next(_municipalities.Count)];
        return (_random.Next(1000, 10000).ToString(), BelgianCities[_random.Next(BelgianCities.Length)]);
    }

    // =========================================================================
    // Parents & Children
    // =========================================================================
    private async Task SeedParentsAndChildrenAsync(int organisationId, int count)
    {
        _logger.LogInformation("Seeding {Count} parents with children...", count);

        var attempts = 0;
        var maxAttempts = count * 3;
        var created = 0;

        while (created < count && attempts < maxAttempts)
        {
            attempts++;

            var isFemale = _random.Next(2) == 0;
            var isFrench = _random.Next(2) == 0;
            var firstName = GetRandomFirstName(isFemale, isFrench);
            var lastName = BelgianLastNames[_random.Next(BelgianLastNames.Length)];
            var birthDate = GenerateRandomBirthDate(1975, 1995);
            var nationalRegisterNumber = GenerateNationalRegisterNumber(birthDate, isFemale);
            var email = $"{firstName.ToLower()}.{lastName.ToLower()}.{created}@example.be";

            var emailExists = await _context.Parents.IgnoreQueryFilters().AnyAsync(p => p.Email == email);
            var nrnExists = await _context.Parents.IgnoreQueryFilters().AnyAsync(p => p.NationalRegisterNumber == nationalRegisterNumber);
            if (emailExists || nrnExists) continue;

            var municipality = GetRandomMunicipality();
            var parent = new Parent
            {
                FirstName = firstName,
                LastName = lastName,
                Email = email,
                PhoneNumber = GenerateBelgianPhoneNumber(false),
                MobilePhoneNumber = GenerateBelgianPhoneNumber(true),
                NationalRegisterNumber = nationalRegisterNumber,
                Address = new Address
                {
                    Street = $"{BelgianStreets[_random.Next(BelgianStreets.Length)]} {_random.Next(1, 200)}",
                    PostalCode = municipality.PostalCode,
                    City = municipality.City,
                    Country = Country.Belgium
                },
                OrganisationId = organisationId
            };

            _context.Parents.Add(parent);
            await _context.SaveChangesAsync();
            created++;

            // 1-3 children per parent
            var childCount = _random.Next(1, 4);
            for (int j = 0; j < childCount; j++)
            {
                var childAttempts = 0;
                var childCreated = false;

                while (!childCreated && childAttempts < 10)
                {
                    childAttempts++;

                    var childIsFemale = _random.Next(2) == 0;
                    var childFirstName = GetRandomFirstName(childIsFemale, isFrench);
                    var childBirthDate = GenerateRandomBirthDate(2010, 2020);
                    var childNrn = GenerateNationalRegisterNumber(childBirthDate, childIsFemale);

                    var childNrnExists = await _context.Children.IgnoreQueryFilters().AnyAsync(c => c.NationalRegisterNumber == childNrn);
                    if (childNrnExists) continue;

                    _context.Children.Add(new Child
                    {
                        FirstName = childFirstName,
                        LastName = lastName,
                        BirthDate = childBirthDate,
                        ParentId = parent.Id,
                        NationalRegisterNumber = childNrn,
                        IsDisadvantagedEnvironment = _random.Next(5) == 0,
                        IsMildDisability = _random.Next(10) == 0,
                        IsSevereDisability = _random.Next(20) == 0
                    });
                    childCreated = true;
                }
            }
        }

        await _context.SaveChangesAsync();
        _logger.LogInformation("Successfully seeded {Created} parents with children", created);
    }

    // =========================================================================
    // Team Members
    // =========================================================================
    private async Task SeedTeamMembersAsync(int organisationId, int count)
    {
        _logger.LogInformation("Seeding {Count} team members...", count);

        var attempts = 0;
        var maxAttempts = count * 3;
        var created = 0;

        while (created < count && attempts < maxAttempts)
        {
            attempts++;

            var isFemale = _random.Next(2) == 0;
            var isFrench = _random.Next(2) == 0;
            var firstName = GetRandomFirstName(isFemale, isFrench);
            var lastName = BelgianLastNames[_random.Next(BelgianLastNames.Length)];
            var birthDate = GenerateRandomBirthDate(1990, 2005);
            var nationalRegisterNumber = GenerateNationalRegisterNumber(birthDate, isFemale);
            var email = $"{firstName.ToLower()}.{lastName.ToLower()}.team.{created}@example.be";

            var emailExists = await _context.TeamMembers.IgnoreQueryFilters().AnyAsync(t => t.Email == email);
            var nrnExists = await _context.TeamMembers.IgnoreQueryFilters().AnyAsync(t => t.NationalRegisterNumber == nationalRegisterNumber);
            if (emailExists || nrnExists) continue;

            var municipality = GetRandomMunicipality();
            _context.TeamMembers.Add(new TeamMember
            {
                FirstName = firstName,
                LastName = lastName,
                Email = email,
                MobilePhoneNumber = GenerateBelgianPhoneNumber(true),
                BirthDate = birthDate,
                NationalRegisterNumber = nationalRegisterNumber,
                TeamRole = (TeamRole)_random.Next(0, 2),
                Status = (Status)_random.Next(0, 2),
                License = (License)_random.Next(0, 5),
                LicenseUrl = ExampleLicenseUrl,
                DailyCompensation = _random.Next(3) > 0 ? (decimal)_random.Next(30, 100) : null, // ~67% have compensation
                Address = new Address
                {
                    Street = $"{BelgianStreets[_random.Next(BelgianStreets.Length)]} {_random.Next(1, 200)}",
                    PostalCode = municipality.PostalCode,
                    City = municipality.City,
                    Country = Country.Belgium
                },
                OrganisationId = organisationId
            });
            created++;
        }

        await _context.SaveChangesAsync();
        _logger.LogInformation("Successfully seeded {Created} team members", created);
    }

    // =========================================================================
    // Activities (with days, groups, questions)
    // =========================================================================
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

        var dayLabels = new[] { "Lundi", "Mardi", "Mercredi", "Jeudi", "Vendredi", "Samedi", "Dimanche" };

        for (int i = 0; i < count && i < activityNames.Length; i++)
        {
            var startDate = DateTime.Today.AddDays(_random.Next(30, 180));
            var duration = _random.Next(4, 8); // 4-7 days
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
            await _context.SaveChangesAsync();

            // Activity days with labels and week numbers
            int week = 1;
            for (var date = startDate; date <= endDate; date = date.AddDays(1))
            {
                var dayOfWeek = (int)date.DayOfWeek;
                if (dayOfWeek == 0) dayOfWeek = 7; // Sunday = 7
                var label = dayLabels[dayOfWeek - 1] + " " + date.ToString("dd/MM");

                _context.ActivityDays.Add(new ActivityDay
                {
                    ActivityId = activity.Id,
                    Label = label,
                    DayDate = date,
                    Week = week,
                    IsActive = true
                });

                if (date.DayOfWeek == DayOfWeek.Sunday || date == endDate)
                    week++;
            }

            // Groups
            var groupNames = new[] { "Groupe Rouge", "Groupe Bleu", "Groupe Vert", "Groupe Jaune" };
            var groupCount = _random.Next(2, 4);
            for (int g = 0; g < groupCount; g++)
            {
                _context.ActivityGroups.Add(new ActivityGroup
                {
                    Label = groupNames[g],
                    Capacity = _random.Next(10, 20),
                    ActivityId = activity.Id
                });
            }

            // Questions (first 2 activities per org)
            if (i < 2)
            {
                _context.ActivityQuestions.Add(new ActivityQuestion
                {
                    ActivityId = activity.Id,
                    QuestionText = "Avez-vous des allergies alimentaires ? Si oui, précisez.",
                    QuestionType = QuestionType.Text,
                    IsRequired = false
                });

                if (i == 0)
                {
                    _context.ActivityQuestions.Add(new ActivityQuestion
                    {
                        ActivityId = activity.Id,
                        QuestionText = "Souhaitez-vous participer à la sortie optionnelle du dernier jour ?",
                        QuestionType = QuestionType.Radio,
                        Options = "Oui,Non",
                        IsRequired = true
                    });
                }
            }
        }

        await _context.SaveChangesAsync();
        _logger.LogInformation("Successfully seeded activities with days, groups and questions");
    }

    // =========================================================================
    // Activity ↔ TeamMember assignments
    // =========================================================================
    private async Task SeedActivityTeamMembersAsync(int organisationId)
    {
        var activities = await _context.Activities.IgnoreQueryFilters()
            .Include(a => a.TeamMembers)
            .Where(a => a.OrganisationId == organisationId)
            .ToListAsync();

        var teamMembers = await _context.TeamMembers.IgnoreQueryFilters()
            .Where(t => t.OrganisationId == organisationId)
            .ToListAsync();

        if (!teamMembers.Any()) return;

        foreach (var activity in activities)
        {
            if (activity.TeamMembers.Any()) continue; // Already assigned

            var count = _random.Next(3, Math.Min(6, teamMembers.Count + 1));
            foreach (var tm in teamMembers.OrderBy(_ => _random.Next()).Take(count))
                activity.TeamMembers.Add(tm);
        }

        await _context.SaveChangesAsync();
        _logger.LogInformation("Seeded activity-team member assignments for org {OrgId}", organisationId);
    }

    // =========================================================================
    // Bookings (with TotalAmount, StructuredCommunication, PaymentStatus)
    // =========================================================================
    private async Task SeedBookingsAsync(int organisationId)
    {
        _logger.LogInformation("Seeding bookings for org {OrgId}...", organisationId);

        var children = await _context.Children.IgnoreQueryFilters()
            .Include(c => c.Parent)
            .Where(c => c.Parent.OrganisationId == organisationId)
            .ToListAsync();

        var activities = await _context.Activities.IgnoreQueryFilters()
            .Include(a => a.Days)
            .Where(a => a.OrganisationId == organisationId && a.PricePerDay.HasValue)
            .ToListAsync();

        var activityIds = activities.Select(a => a.Id).ToList();

        var groups = await _context.ActivityGroups.IgnoreQueryFilters()
            .Where(g => g.ActivityId.HasValue && activityIds.Contains(g.ActivityId.Value))
            .ToListAsync();

        var questions = await _context.ActivityQuestions.IgnoreQueryFilters()
            .Where(q => activityIds.Contains(q.ActivityId))
            .ToListAsync();

        var childrenToBook = children.OrderBy(_ => _random.Next())
            .Take((int)(children.Count * 0.6)).ToList();

        foreach (var child in childrenToBook)
        {
            var activitiesToBook = activities.OrderBy(_ => _random.Next())
                .Take(_random.Next(1, 3)).ToList();

            foreach (var activity in activitiesToBook)
            {
                var activityGroups = groups.Where(g => g.ActivityId == activity.Id).ToList();
                var group = activityGroups.Any() ? activityGroups[_random.Next(activityGroups.Count)] : null;

                // Payment status distribution:
                // 40% Paid, 25% PartiallyPaid, 20% NotPaid, 10% Paid(confirmed late), 5% Overpaid
                var roll = _random.Next(100);
                var isConfirmed = roll < 85;
                PaymentStatus paymentStatus;
                if (!isConfirmed)
                    paymentStatus = PaymentStatus.NotPaid;
                else if (roll < 40)
                    paymentStatus = PaymentStatus.Paid;
                else if (roll < 60)
                    paymentStatus = PaymentStatus.PartiallyPaid;
                else if (roll < 80)
                    paymentStatus = PaymentStatus.NotPaid;
                else if (roll < 84)
                    paymentStatus = PaymentStatus.Overpaid;
                else
                    paymentStatus = PaymentStatus.Paid;

                var booking = new Booking
                {
                    ChildId = child.Id,
                    ActivityId = activity.Id,
                    GroupId = group?.Id,
                    BookingDate = DateTime.Now.AddDays(-_random.Next(1, 60)),
                    IsConfirmed = isConfirmed,
                    IsMedicalSheet = _random.Next(10) > 3,
                    StructuredCommunication = GenerateStructuredCommunication(),
                    PaymentStatus = paymentStatus
                };

                _context.Bookings.Add(booking);
                await _context.SaveChangesAsync();

                // Booking days (50-100% of activity days)
                var daysToBook = activity.Days
                    .OrderBy(_ => _random.Next())
                    .Take(_random.Next(Math.Max(1, activity.Days.Count / 2), activity.Days.Count + 1))
                    .ToList();

                foreach (var activityDay in daysToBook)
                {
                    _context.BookingDays.Add(new BookingDay
                    {
                        BookingId = booking.Id,
                        ActivityDayId = activityDay.DayId,
                        IsReserved = true,
                        IsPresent = _random.Next(10) > 1
                    });
                }
                await _context.SaveChangesAsync();

                // Calculate TotalAmount from booked days × price
                booking.TotalAmount = daysToBook.Count * (activity.PricePerDay ?? 0);
                await _context.SaveChangesAsync();

                // Question answers
                var activityQuestions = questions.Where(q => q.ActivityId == activity.Id).ToList();
                foreach (var question in activityQuestions)
                {
                    var answers = question.QuestionType switch
                    {
                        QuestionType.Text => new[] { "Aucune allergie connue", "Allergie aux noix", "Intolérance au gluten", "" },
                        QuestionType.Radio => question.Options?.Split(',') ?? new[] { "Oui", "Non" },
                        QuestionType.Checkbox => new[] { "Natation", "Randonnée", "Arts", "" },
                        _ => new[] { "" }
                    };
                    var answer = answers[_random.Next(answers.Length)];
                    if (!string.IsNullOrEmpty(answer))
                    {
                        _context.ActivityQuestionAnswers.Add(new ActivityQuestionAnswer
                        {
                            BookingId = booking.Id,
                            ActivityQuestionId = question.Id,
                            AnswerText = answer
                        });
                    }
                }
            }
        }

        await _context.SaveChangesAsync();
        _logger.LogInformation("Successfully seeded bookings for org {OrgId}", organisationId);
    }

    // =========================================================================
    // Payments
    // =========================================================================
    private async Task SeedPaymentsAsync(int organisationId)
    {
        _logger.LogInformation("Seeding payments for org {OrgId}...", organisationId);

        var bookings = await _context.Bookings.IgnoreQueryFilters()
            .Include(b => b.Child).ThenInclude(c => c.Parent)
            .Where(b => b.Child.Parent.OrganisationId == organisationId
                     && b.TotalAmount > 0
                     && b.IsConfirmed
                     && b.PaymentStatus != PaymentStatus.NotPaid)
            .ToListAsync();

        foreach (var booking in bookings)
        {
            switch (booking.PaymentStatus)
            {
                case PaymentStatus.Paid:
                    _context.Payments.Add(new Payment
                    {
                        BookingId = booking.Id,
                        Amount = booking.TotalAmount,
                        PaymentDate = booking.BookingDate.AddDays(_random.Next(1, 15)),
                        PaymentMethod = PaymentMethod.BankTransfer,
                        Status = PaymentStatus.Paid,
                        StructuredCommunication = booking.StructuredCommunication
                    });
                    booking.PaidAmount = booking.TotalAmount;
                    break;

                case PaymentStatus.PartiallyPaid:
                    var partialAmount = Math.Round(booking.TotalAmount * (0.4m + (decimal)(_random.NextDouble() * 0.3)), 2);
                    _context.Payments.Add(new Payment
                    {
                        BookingId = booking.Id,
                        Amount = partialAmount,
                        PaymentDate = booking.BookingDate.AddDays(_random.Next(1, 10)),
                        PaymentMethod = PaymentMethod.Cash,
                        Status = PaymentStatus.Paid,
                        StructuredCommunication = booking.StructuredCommunication
                    });
                    booking.PaidAmount = partialAmount;
                    break;

                case PaymentStatus.Overpaid:
                    var overAmount = booking.TotalAmount + Math.Round((decimal)(_random.NextDouble() * 10) + 1m, 2);
                    _context.Payments.Add(new Payment
                    {
                        BookingId = booking.Id,
                        Amount = overAmount,
                        PaymentDate = booking.BookingDate.AddDays(_random.Next(1, 15)),
                        PaymentMethod = PaymentMethod.BankTransfer,
                        Status = PaymentStatus.Paid,
                        StructuredCommunication = booking.StructuredCommunication
                    });
                    booking.PaidAmount = overAmount;
                    break;
            }
        }

        await _context.SaveChangesAsync();
        _logger.LogInformation("Successfully seeded payments for org {OrgId}", organisationId);
    }

    // =========================================================================
    // CODA File + Bank Transactions
    // =========================================================================
    private async Task SeedCodaAndBankTransactionsAsync(int organisationId)
    {
        _logger.LogInformation("Seeding CODA file and bank transactions for org {OrgId}...", organisationId);

        var organisation = await _context.Organisations.IgnoreQueryFilters().FirstOrDefaultAsync(o => o.Id == organisationId);
        if (organisation == null || string.IsNullOrEmpty(organisation.BankAccountNumber)) return;

        // Load BankTransfer payments for this org (candidates for CODA reconciliation)
        var allBankTransferPayments = await _context.Payments.IgnoreQueryFilters()
            .Include(p => p.Booking).ThenInclude(b => b.Child).ThenInclude(c => c.Parent)
            .Where(p => p.PaymentMethod == PaymentMethod.BankTransfer)
            .ToListAsync();

        var bankTransferPayments = allBankTransferPayments
            .Where(p => p.Booking?.Child?.Parent?.OrganisationId == organisationId)
            .OrderBy(_ => _random.Next())
            .ToList();

        var statementDate = DateTime.Today.AddDays(-14);

        var codaFile = new CodaFile
        {
            OrganisationId = organisationId,
            FileName = $"cedeva_{statementDate:yyyy-MM-dd}.coda",
            ImportDate = DateTime.Now.AddDays(-13),
            StatementDate = statementDate,
            AccountNumber = organisation.BankAccountNumber,
            OldBalance = 5000m,
            NewBalance = 5000m, // Updated after transactions
            TransactionCount = 0, // Updated after transactions
            ImportedByUserId = 1
        };

        _context.CodaFiles.Add(codaFile);
        await _context.SaveChangesAsync();

        var allTransactions = new List<BankTransaction>();
        decimal balanceChange = 0;

        // Split into: already reconciled (30%), matchable not yet reconciled (40%), rest skipped
        var reconciledCount = Math.Min(bankTransferPayments.Count, Math.Max(2, bankTransferPayments.Count * 3 / 10));
        var matchableCount = Math.Min(bankTransferPayments.Count - reconciledCount, Math.Max(2, bankTransferPayments.Count * 4 / 10));

        var reconciledPayments = bankTransferPayments.Take(reconciledCount).ToList();
        var matchablePayments = bankTransferPayments.Skip(reconciledCount).Take(matchableCount).ToList();

        // Group 1: Already reconciled (history)
        foreach (var payment in reconciledPayments)
        {
            allTransactions.Add(new BankTransaction
            {
                OrganisationId = organisationId,
                TransactionDate = payment.PaymentDate,
                ValueDate = payment.PaymentDate.AddDays(_random.Next(0, 2)),
                Amount = payment.Amount,
                StructuredCommunication = payment.StructuredCommunication,
                CounterpartyName = $"{payment.Booking.Child.Parent.FirstName} {payment.Booking.Child.Parent.LastName}",
                CounterpartyAccount = $"BE{_random.Next(10000000, 99999999)}",
                TransactionCode = "05",
                CodaFileId = codaFile.Id,
                IsReconciled = true,
                PaymentId = payment.Id
            });
            balanceChange += payment.Amount;
        }

        // Group 2: Matchable — same StructuredCommunication, awaiting reconciliation
        foreach (var payment in matchablePayments)
        {
            allTransactions.Add(new BankTransaction
            {
                OrganisationId = organisationId,
                TransactionDate = payment.PaymentDate,
                ValueDate = payment.PaymentDate.AddDays(_random.Next(0, 3)),
                Amount = payment.Amount,
                StructuredCommunication = payment.StructuredCommunication,
                CounterpartyName = $"{payment.Booking.Child.Parent.FirstName} {payment.Booking.Child.Parent.LastName}",
                CounterpartyAccount = $"BE{_random.Next(10000000, 99999999)}",
                TransactionCode = "05",
                CodaFileId = codaFile.Id,
                IsReconciled = false
            });
            balanceChange += payment.Amount;
        }

        // Group 3: Unmatched credits — suggestion candidates
        for (int i = 0; i < 3; i++)
        {
            var amount = Math.Round((decimal)(_random.NextDouble() * 200) + 20m, 2);
            allTransactions.Add(new BankTransaction
            {
                OrganisationId = organisationId,
                TransactionDate = statementDate.AddDays(-_random.Next(1, 14)),
                ValueDate = statementDate.AddDays(-_random.Next(0, 13)),
                Amount = amount,
                FreeCommunication = $"Paiement divers ref.{_random.Next(10000, 99999)}",
                CounterpartyName = BelgianLastNames[_random.Next(BelgianLastNames.Length)],
                CounterpartyAccount = $"BE{_random.Next(10000000, 99999999)}",
                TransactionCode = "05",
                CodaFileId = codaFile.Id,
                IsReconciled = false
            });
            balanceChange += amount;
        }

        // Group 4: Unmatched debits
        var debitLabels = new[] { "Achat materiel activites", "Transport equipe" };
        var debitNames = new[] { "Fournisseur Materiel SA", "SNCB" };
        for (int i = 0; i < 2; i++)
        {
            var amount = -Math.Round((decimal)(_random.NextDouble() * 150) + 30m, 2);
            allTransactions.Add(new BankTransaction
            {
                OrganisationId = organisationId,
                TransactionDate = statementDate.AddDays(-_random.Next(1, 14)),
                ValueDate = statementDate.AddDays(-_random.Next(0, 13)),
                Amount = amount,
                FreeCommunication = debitLabels[i],
                CounterpartyName = debitNames[i],
                CounterpartyAccount = $"BE{_random.Next(10000000, 99999999)}",
                TransactionCode = "05",
                CodaFileId = codaFile.Id,
                IsReconciled = false
            });
            balanceChange += amount;
        }

        _context.BankTransactions.AddRange(allTransactions);
        await _context.SaveChangesAsync();

        // Link reconciled payments ↔ bank transactions (both directions)
        foreach (var payment in reconciledPayments)
        {
            var matchingBt = allTransactions.FirstOrDefault(bt => bt.IsReconciled && bt.PaymentId == payment.Id);
            if (matchingBt != null)
                payment.BankTransactionId = matchingBt.Id;
        }

        // Update CODA file summary
        codaFile.NewBalance = codaFile.OldBalance + balanceChange;
        codaFile.TransactionCount = allTransactions.Count;

        await _context.SaveChangesAsync();
        _logger.LogInformation("Successfully seeded CODA file with {Count} transactions for org {OrgId}",
            allTransactions.Count, organisationId);
    }

    // =========================================================================
    // Expenses (per activity)
    // =========================================================================
    private async Task SeedExpensesAsync(int organisationId)
    {
        var activities = await _context.Activities.IgnoreQueryFilters()
            .Where(a => a.OrganisationId == organisationId && a.PricePerDay.HasValue)
            .ToListAsync();

        var teamMembers = await _context.TeamMembers.IgnoreQueryFilters()
            .Where(t => t.OrganisationId == organisationId && t.DailyCompensation.HasValue)
            .ToListAsync();

        foreach (var activity in activities)
        {
            var existingCount = await _context.Expenses.IgnoreQueryFilters()
                .CountAsync(e => e.ActivityId == activity.Id);
            if (existingCount > 0) continue;

            if (!teamMembers.Any()) continue;

            var tm1 = teamMembers[_random.Next(teamMembers.Count)];
            var tm2 = teamMembers.Where(t => t.TeamMemberId != tm1.TeamMemberId)
                .OrderBy(_ => _random.Next()).FirstOrDefault();

            // Reimbursement: transport
            _context.Expenses.Add(new Expense
            {
                Label = "Frais de transport",
                Description = "Transport vers le lieu de l'activité",
                Amount = Math.Round((decimal)(_random.NextDouble() * 40) + 15m, 2),
                Category = "Transport",
                ExpenseType = ExpenseType.Reimbursement,
                TeamMemberId = tm1.TeamMemberId,
                ActivityId = activity.Id,
                ExpenseDate = activity.StartDate.AddDays(-2)
            });

            // Reimbursement: meals
            if (tm2 != null)
            {
                _context.Expenses.Add(new Expense
                {
                    Label = "Repas équipe",
                    Description = "Déjeuner collectif équipe",
                    Amount = Math.Round((decimal)(_random.NextDouble() * 25) + 10m, 2),
                    Category = "Repas",
                    ExpenseType = ExpenseType.Reimbursement,
                    TeamMemberId = tm2.TeamMemberId,
                    ActivityId = activity.Id,
                    ExpenseDate = activity.StartDate.AddDays(1)
                });
            }

            // Personal consumption (deducted from salary)
            _context.Expenses.Add(new Expense
            {
                Label = "Consommation personnelle",
                Description = "Café et collation",
                Amount = Math.Round((decimal)(_random.NextDouble() * 8) + 2m, 2),
                Category = "Consommation",
                ExpenseType = ExpenseType.PersonalConsumption,
                TeamMemberId = tm1.TeamMemberId,
                ActivityId = activity.Id,
                ExpenseDate = activity.StartDate.AddDays(1)
            });

            // Organisation expense: materials (card)
            _context.Expenses.Add(new Expense
            {
                Label = "Materiel activités",
                Description = "Achat materiel sportif et artistique",
                Amount = Math.Round((decimal)(_random.NextDouble() * 80) + 20m, 2),
                Category = "Materiel",
                OrganizationPaymentSource = "OrganizationCard",
                ActivityId = activity.Id,
                ExpenseDate = activity.StartDate.AddDays(-5)
            });

            // Organisation expense: transport (cash)
            _context.Expenses.Add(new Expense
            {
                Label = "Transport groupe",
                Description = "Location véhicule pour sortie",
                Amount = Math.Round((decimal)(_random.NextDouble() * 60) + 30m, 2),
                Category = "Transport",
                OrganizationPaymentSource = "OrganizationCash",
                ActivityId = activity.Id,
                ExpenseDate = activity.StartDate.AddDays(2)
            });
        }

        await _context.SaveChangesAsync();
        _logger.LogInformation("Seeded expenses for org {OrgId}", organisationId);
    }

    // =========================================================================
    // Excursions
    // =========================================================================
    private async Task SeedExcursionsAsync(int organisationId)
    {
        _logger.LogInformation("Seeding excursions for org {OrgId}...", organisationId);

        // Get activities for this organisation
        var activities = await _context.Activities.IgnoreQueryFilters()
            .Include(a => a.Groups)
            .Where(a => a.OrganisationId == organisationId)
            .ToListAsync();

        if (!activities.Any()) return;

        // Check if already seeded
        var activityIds = activities.Select(a => a.Id).ToList();
        var existingCount = await _context.Excursions
            .Where(e => activityIds.Contains(e.ActivityId))
            .CountAsync();
        if (existingCount > 0) return;

        // Placeholder CreatedByUserId for financial transactions (int field, not FK to Identity)
        const int createdByUserId = 1;

        // Get confirmed bookings with group info
        var bookings = await _context.Bookings.IgnoreQueryFilters()
            .Include(b => b.Child)
            .Where(b => activityIds.Contains(b.ActivityId) && b.IsConfirmed && b.GroupId.HasValue)
            .ToListAsync();

        // Get team members for this organisation
        var teamMembers = await _context.TeamMembers.IgnoreQueryFilters()
            .Where(t => t.OrganisationId == organisationId)
            .ToListAsync();

        var excursionTypes = new[] { ExcursionType.Pool, ExcursionType.AmusementPark, ExcursionType.CulturalVisit, ExcursionType.Nature, ExcursionType.Sports };

        foreach (var activity in activities)
        {
            if (activity.Groups.Count == 0) continue;

            // Create 1 or 2 excursions per activity
            int excursionCount = _random.Next(1, 3);

            for (int i = 0; i < excursionCount; i++)
            {
                // Pick 1-2 target groups
                var shuffledGroups = activity.Groups.OrderBy(_ => _random.Next()).ToList();
                int groupCount = Math.Min(_random.Next(1, 3), shuffledGroups.Count);
                var targetGroups = shuffledGroups.Take(groupCount).ToList();

                var excursionDate = activity.StartDate.AddDays(_random.Next(1, (activity.EndDate - activity.StartDate).Days));
                var cost = Math.Round((decimal)_random.Next(1500, 4500) / 100, 2); // €15–45

                var excursion = new Excursion
                {
                    Name = GetExcursionName(excursionTypes[_random.Next(excursionTypes.Length)], i),
                    Description = GetExcursionDescription(i),
                    ExcursionDate = excursionDate,
                    StartTime = _random.Next(2) == 0 ? new TimeSpan(_random.Next(8, 10), 0, 0) : null,
                    EndTime = _random.Next(2) == 0 ? new TimeSpan(_random.Next(14, 17), 0, 0) : null,
                    Cost = cost,
                    Type = excursionTypes[_random.Next(excursionTypes.Length)],
                    IsActive = true,
                    ActivityId = activity.Id
                };

                _context.Excursions.Add(excursion);
                await _context.SaveChangesAsync();

                // Link to target groups
                foreach (var group in targetGroups)
                {
                    _context.ExcursionGroups.Add(new ExcursionGroup
                    {
                        ExcursionId = excursion.Id,
                        ActivityGroupId = group.Id
                    });
                }

                // Register 30-70% of eligible confirmed bookings
                var eligibleGroupIds = targetGroups.Select(g => g.Id).ToList();
                var eligibleBookings = bookings
                    .Where(b => b.ActivityId == activity.Id && eligibleGroupIds.Contains(b.GroupId!.Value))
                    .ToList();

                var registrationRate = _random.Next(30, 71); // 30-70%
                var toRegister = eligibleBookings.OrderBy(_ => _random.Next())
                    .Take(eligibleBookings.Count * registrationRate / 100)
                    .ToList();

                foreach (var booking in toRegister)
                {
                    _context.ExcursionRegistrations.Add(new ExcursionRegistration
                    {
                        ExcursionId = excursion.Id,
                        BookingId = booking.Id,
                        RegistrationDate = excursionDate.AddDays(-_random.Next(1, 15)),
                        IsPresent = _random.Next(10) > 2 // 70% present
                    });

                    // Update booking total
                    booking.TotalAmount += cost;
                    if (booking.PaidAmount == 0)
                        booking.PaymentStatus = PaymentStatus.NotPaid;
                    else if (booking.PaidAmount < booking.TotalAmount)
                        booking.PaymentStatus = PaymentStatus.PartiallyPaid;
                    else if (booking.PaidAmount == booking.TotalAmount)
                        booking.PaymentStatus = PaymentStatus.Paid;
                    else
                        booking.PaymentStatus = PaymentStatus.Overpaid;

                    // Audit trail
                    _context.ActivityFinancialTransactions.Add(new ActivityFinancialTransaction
                    {
                        ActivityId = activity.Id,
                        TransactionDate = excursionDate.AddDays(-_random.Next(1, 10)),
                        Type = TransactionType.Income,
                        Category = TransactionCategory.ExcursionPayment,
                        Amount = cost,
                        Description = $"Inscription excursion: {excursion.Name} - {booking.Child.FirstName} {booking.Child.LastName}",
                        CreatedByUserId = createdByUserId
                    });
                }

                // Assign 1-2 team members
                if (teamMembers.Any())
                {
                    var assignedTeam = teamMembers.OrderBy(_ => _random.Next())
                        .Take(_random.Next(1, 3))
                        .ToList();

                    foreach (var tm in assignedTeam)
                    {
                        _context.ExcursionTeamMembers.Add(new ExcursionTeamMember
                        {
                            ExcursionId = excursion.Id,
                            TeamMemberId = tm.TeamMemberId,
                            IsAssigned = true,
                            IsPresent = _random.Next(10) > 2 // 70% present
                        });
                    }
                }

                // Add 1-3 expenses per excursion
                var expenseTemplates = new[] {
                    ("Location bus", "Transport", 120m, 200m),
                    ("Billets entrée", "Billets", 180m, 350m),
                    ("Sandwiches", "Repas", 40m, 80m),
                    ("Matériel artisanat", "Matériel", 25m, 60m),
                    ("Boissons", "Repas", 15m, 35m)
                };

                int expenseCount = _random.Next(1, 4);
                var usedExpenses = new HashSet<int>();

                for (int e = 0; e < expenseCount; e++)
                {
                    int idx;
                    do { idx = _random.Next(expenseTemplates.Length); } while (usedExpenses.Contains(idx));
                    usedExpenses.Add(idx);

                    var (label, category, minAmount, maxAmount) = expenseTemplates[idx];
                    var amount = Math.Round(minAmount + (decimal)_random.NextDouble() * (maxAmount - minAmount), 2);

                    _context.Expenses.Add(new Expense
                    {
                        Label = label,
                        Category = category,
                        Amount = amount,
                        ExpenseDate = excursionDate.AddDays(-_random.Next(0, 5)),
                        OrganizationPaymentSource = _random.Next(2) == 0 ? "OrganizationCard" : "OrganizationCash",
                        ActivityId = activity.Id,
                        ExcursionId = excursion.Id
                    });
                }

                await _context.SaveChangesAsync();
            }
        }

        _logger.LogInformation("Seeded excursions for org {OrgId}", organisationId);
    }

    private string GetExcursionName(ExcursionType type, int index)
    {
        var names = type switch
        {
            ExcursionType.Pool => new[] { "Journée piscine", "Après-midi piscine" },
            ExcursionType.AmusementPark => new[] { "Visite parc d'attractions", "Journée Walibi" },
            ExcursionType.CulturalVisit => new[] { "Visite musée", "Tournée historique" },
            ExcursionType.Nature => new[] { "Randonnée nature", "Journée forêt" },
            ExcursionType.Sports => new[] { "Tournoi sportif", "Journée sport" },
            _ => new[] { "Excursion" }
        };
        return names[index % names.Length];
    }

    private string GetExcursionDescription(int index)
    {
        var descriptions = new[]
        {
            "Excursion organisée pour les enfants du groupe. Prévoir un repas de midi.",
            "Activité en plein air. Les parents sont invités à accompagner si souhaitent.",
            "Journée culturelle pour découvrir de nouveaux horizons.",
            null
        };
        return descriptions[index % descriptions.Length]!;
    }

    // =========================================================================
    // Email Templates
    // =========================================================================
    private async Task SeedEmailTemplatesAsync(int organisationId)
    {
        _logger.LogInformation("Seeding email templates for org {OrgId}...", organisationId);

        var userId = await _context.Users.Select(u => u.Id).FirstOrDefaultAsync();
        if (userId == null) return;

        var now = DateTime.Now;

        _context.EmailTemplates.AddRange(
            new EmailTemplate
            {
                OrganisationId = organisationId,
                Name = "Confirmation d'inscription",
                TemplateType = EmailTemplateType.BookingConfirmation,
                Subject = "Confirmation inscription – %nom_activite%",
                HtmlContent =
                    "<h2 style=\"color:#007faf;\">Confirmation de votre inscription</h2>" +
                    "<p>Chère famille <strong>%nom_complet_parent%</strong>,</p>" +
                    "<p>Nous vous confirmons l'inscription de <strong>%nom_complet_enfant%</strong> " +
                    "à l'activité <strong>%nom_activite%</strong> " +
                    "(du %date_debut_activite% au %date_fin_activite%).</p>" +
                    "<p><strong>Montant total :</strong> %montant_total%<br>" +
                    "<strong>Communication structurée :</strong> %communication_structuree%</p>" +
                    "<p>En cas de question, n'hésitez pas à nous contacter.</p>" +
                    "<p>Cordialement,<br><strong>%nom_organisation%</strong></p>",
                IsDefault = true,
                IsShared = true,
                CreatedByUserId = userId,
                CreatedDate = now.AddDays(-10)
            },
            new EmailTemplate
            {
                OrganisationId = organisationId,
                Name = "Rappel de paiement",
                TemplateType = EmailTemplateType.PaymentReminder,
                Subject = "Rappel paiement – %nom_complet_enfant% – %nom_activite%",
                HtmlContent =
                    "<h2 style=\"color:#dc3545;\">Rappel de paiement</h2>" +
                    "<p>Chère famille <strong>%nom_complet_parent%</strong>,</p>" +
                    "<p>Le paiement pour l'inscription de <strong>%nom_complet_enfant%</strong> " +
                    "à <strong>%nom_activite%</strong> est en attente.</p>" +
                    "<p><strong>Montant restant :</strong> %montant_restant%<br>" +
                    "<strong>Communication structurée :</strong> %communication_structuree%</p>" +
                    "<p>Merci de procéder au paiement par virement bancaire dans les meilleurs délais.</p>" +
                    "<p>Cordialement,<br><strong>%nom_organisation%</strong></p>",
                IsDefault = true,
                IsShared = true,
                CreatedByUserId = userId,
                CreatedDate = now.AddDays(-8)
            },
            new EmailTemplate
            {
                OrganisationId = organisationId,
                Name = "Rappel fiche médicale",
                TemplateType = EmailTemplateType.MedicalSheetReminder,
                Subject = "Fiche médicale manquante – %nom_complet_enfant%",
                HtmlContent =
                    "<h2 style=\"color:#ffc107;\">Fiche médicale à compléter</h2>" +
                    "<p>Chère famille <strong>%nom_complet_parent%</strong>,</p>" +
                    "<p>La fiche médicale de <strong>%nom_complet_enfant%</strong> " +
                    "pour l'activité <strong>%nom_activite%</strong> n'a pas encore été reçue.</p>" +
                    "<p>Merci de nous la transmettre au plus tôt pour que votre enfant puisse " +
                    "bénéficier de tous les services proposés.</p>" +
                    "<p>Cordialement,<br><strong>%nom_organisation%</strong></p>",
                IsDefault = true,
                IsShared = true,
                CreatedByUserId = userId,
                CreatedDate = now.AddDays(-5)
            },
            new EmailTemplate
            {
                OrganisationId = organisationId,
                Name = "Message de bienvenue",
                TemplateType = EmailTemplateType.Custom,
                Subject = "Bienvenue chez %nom_organisation% !",
                HtmlContent =
                    "<h2 style=\"color:#28a745;\">Bienvenue !</h2>" +
                    "<p>Chère famille <strong>%nom_complet_parent%</strong>,</p>" +
                    "<p>Nous sommes ravis de vous accueillir parmi nous. " +
                    "<strong>%nom_complet_enfant%</strong> va passer un merveilleux séjour " +
                    "à <strong>%nom_activite%</strong> !</p>" +
                    "<p>N'hésitez pas à nous contacter si vous avez des questions.</p>" +
                    "<p>À bientôt !<br><strong>L'équipe de %nom_organisation%</strong></p>",
                IsDefault = false,
                IsShared = true,
                CreatedByUserId = userId,
                CreatedDate = now.AddDays(-3)
            }
        );

        await _context.SaveChangesAsync();
        _logger.LogInformation("Successfully seeded 4 email templates for org {OrgId}", organisationId);
    }

    // =========================================================================
    // Helpers
    // =========================================================================
    private string GenerateStructuredCommunication()
    {
        _structuredCommCounter++;
        var basePadded = _structuredCommCounter.ToString().PadLeft(10, '0');
        var baseNum = long.Parse(basePadded);
        var checksum = (int)(baseNum % 97);
        if (checksum == 0) checksum = 97;
        var full = basePadded + checksum.ToString("D2"); // 12 digits total
        return $"+++{full[..3]}/{full[3..7]}/{full[7..12]}+++";
    }

    private string GetRandomFirstName(bool isFemale, bool isFrench)
    {
        if (isFemale)
        {
            return isFrench
                ? FrenchFirstNamesFemale[_random.Next(FrenchFirstNamesFemale.Length)]
                : DutchFirstNamesFemale[_random.Next(DutchFirstNamesFemale.Length)];
        }
        else
        {
            return isFrench
                ? FrenchFirstNamesMale[_random.Next(FrenchFirstNamesMale.Length)]
                : DutchFirstNamesMale[_random.Next(DutchFirstNamesMale.Length)];
        }
    }

    private DateTime GenerateRandomBirthDate(int minYear, int maxYear)
    {
        var year = _random.Next(minYear, maxYear + 1);
        var month = _random.Next(1, 13);
        var day = _random.Next(1, DateTime.DaysInMonth(year, month) + 1);
        return new DateTime(year, month, day, 0, 0, 0, DateTimeKind.Unspecified);
    }

    private string GenerateNationalRegisterNumber(DateTime birthDate, bool isFemale)
    {
        var year = birthDate.Year % 100;
        var month = birthDate.Month;
        var day = birthDate.Day;

        var sequence = _random.Next(1, 499) * 2;
        if (!isFemale) sequence -= 1;

        var birthString = $"{year:D2}{month:D2}{day:D2}";
        var fullYear = birthDate.Year;

        var checksumBase = long.Parse(birthString + $"{sequence:D3}");
        if (fullYear >= 2000)
            checksumBase += 2000000000;

        var checksum = 97 - (checksumBase % 97);

        return $"{year:D2}.{month:D2}.{day:D2}-{sequence:D3}.{checksum:D2}";
    }

    private string GenerateBelgianPhoneNumber(bool mobile)
    {
        if (mobile)
        {
            var prefix = _random.Next(70, 100);
            var part1 = _random.Next(10, 100);
            var part2 = _random.Next(10, 100);
            var part3 = _random.Next(10, 100);
            return $"04{prefix:D2} {part1:D2} {part2:D2} {part3:D2}";
        }
        else
        {
            var areaCode = _random.Next(2, 10);
            var part1 = _random.Next(100, 1000);
            var part2 = _random.Next(10, 100);
            var part3 = _random.Next(10, 100);
            return $"0{areaCode} {part1:D3} {part2:D2} {part3:D2}";
        }
    }
}
