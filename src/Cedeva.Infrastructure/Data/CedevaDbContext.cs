using Cedeva.Core.Entities;
using Cedeva.Core.Interfaces;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Cedeva.Infrastructure.Data;

public class CedevaDbContext : IdentityDbContext<CedevaUser>, IUnitOfWork
{
    private readonly ICurrentUserService? _currentUserService;

    public CedevaDbContext(DbContextOptions<CedevaDbContext> options) : base(options)
    {
    }

    public CedevaDbContext(DbContextOptions<CedevaDbContext> options, ICurrentUserService currentUserService)
        : base(options)
    {
        _currentUserService = currentUserService;
    }

    public DbSet<Organisation> Organisations => Set<Organisation>();
    public DbSet<Activity> Activities => Set<Activity>();
    public DbSet<ActivityDay> ActivityDays => Set<ActivityDay>();
    public DbSet<ActivityGroup> ActivityGroups => Set<ActivityGroup>();
    public DbSet<ActivityQuestion> ActivityQuestions => Set<ActivityQuestion>();
    public DbSet<ActivityQuestionAnswer> ActivityQuestionAnswers => Set<ActivityQuestionAnswer>();
    public DbSet<Address> Addresses => Set<Address>();
    public DbSet<BelgianMunicipality> BelgianMunicipalities => Set<BelgianMunicipality>();
    public DbSet<Booking> Bookings => Set<Booking>();
    public DbSet<BookingDay> BookingDays => Set<BookingDay>();
    public DbSet<Child> Children => Set<Child>();
    public DbSet<Parent> Parents => Set<Parent>();
    public DbSet<TeamMember> TeamMembers => Set<TeamMember>();
    public DbSet<Contact> Contacts => Set<Contact>();
    public DbSet<ContactGroup> ContactGroups => Set<ContactGroup>();
    public DbSet<ContactGroupMember> ContactGroupMembers => Set<ContactGroupMember>();
    public DbSet<Expense> Expenses => Set<Expense>();
    public DbSet<ExpenseCategory> ExpenseCategories => Set<ExpenseCategory>();
    public DbSet<EmailSent> EmailsSent => Set<EmailSent>();
    public DbSet<EmailTemplate> EmailTemplates => Set<EmailTemplate>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<ActivityFinancialTransaction> ActivityFinancialTransactions => Set<ActivityFinancialTransaction>();
    public DbSet<Excursion> Excursions => Set<Excursion>();
    public DbSet<ExcursionRegistration> ExcursionRegistrations => Set<ExcursionRegistration>();
    public DbSet<ExcursionGroup> ExcursionGroups => Set<ExcursionGroup>();
    public DbSet<ExcursionTeamMember> ExcursionTeamMembers => Set<ExcursionTeamMember>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Apply all configurations from assembly
        builder.ApplyConfigurationsFromAssembly(typeof(CedevaDbContext).Assembly);

        // Multi-tenancy global query filters
        ConfigureMultiTenancyFilters(builder);
    }

    private void ConfigureMultiTenancyFilters(ModelBuilder builder)
    {
        // Standard org-scoped entities (IOrganisationScoped) share the same tenant filter.
        ApplyOrganisationFilter<Activity>(builder);
        ApplyOrganisationFilter<Parent>(builder);
        ApplyOrganisationFilter<TeamMember>(builder);
        ApplyOrganisationFilter<ExpenseCategory>(builder);
        ApplyOrganisationFilter<Contact>(builder);
        ApplyOrganisationFilter<ContactGroup>(builder);
        ApplyOrganisationFilter<EmailTemplate>(builder);

        // Entities scoped via a relationship keep their own filter expression.
        builder.Entity<Child>()
            .HasQueryFilter(c => _currentUserService == null ||
                                 _currentUserService.IsAdmin ||
                                 c.Parent.OrganisationId == _currentUserService.OrganisationId);
        builder.Entity<ContactGroupMember>()
            .HasQueryFilter(m => _currentUserService == null ||
                                 _currentUserService.IsAdmin ||
                                 m.ContactGroup.OrganisationId == _currentUserService.OrganisationId);

        // Activity-level templates are removed explicitly when their activity is deleted (see
        // ActivitiesController.DeleteConfirmed). The FK uses NO ACTION because a DB-level cascade
        // here would create multiple cascade paths to EmailTemplates (Organisation -> EmailTemplates
        // directly and Organisation -> Activity -> EmailTemplates), which SQL Server forbids.
        builder.Entity<EmailTemplate>()
            .HasOne(et => et.Activity)
            .WithMany()
            .HasForeignKey(et => et.ActivityId)
            .OnDelete(DeleteBehavior.NoAction);
    }

    /// <summary>Applies the standard multi-tenant filter to an org-scoped entity.</summary>
    private void ApplyOrganisationFilter<TEntity>(ModelBuilder builder)
        where TEntity : class, IOrganisationScoped
    {
        builder.Entity<TEntity>().HasQueryFilter(e =>
            _currentUserService == null ||
            _currentUserService.IsAdmin ||
            e.OrganisationId == _currentUserService.OrganisationId);
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var currentUser = _currentUserService?.UserId ?? "System";
        var timestamp = DateTime.UtcNow;

        foreach (var entry in ChangeTracker.Entries())
        {
            // Gérer AuditableEntity
            if (entry.Entity is AuditableEntity auditableEntity)
            {
                if (entry.State == EntityState.Added)
                {
                    auditableEntity.CreatedAt = timestamp;
                    auditableEntity.CreatedBy = currentUser;
                    auditableEntity.ModifiedAt = null;
                    auditableEntity.ModifiedBy = null;
                }
                else if (entry.State == EntityState.Modified)
                {
                    auditableEntity.ModifiedAt = timestamp;
                    auditableEntity.ModifiedBy = currentUser;

                    // Empêcher écrasement des champs de création
                    entry.Property(nameof(AuditableEntity.CreatedAt)).IsModified = false;
                    entry.Property(nameof(AuditableEntity.CreatedBy)).IsModified = false;
                }
            }
            // Gérer CedevaUser séparément (n'hérite pas de AuditableEntity)
            else if (entry.Entity is CedevaUser cedevaUser)
            {
                if (entry.State == EntityState.Added)
                {
                    cedevaUser.CreatedAt = timestamp;
                    cedevaUser.CreatedBy = currentUser;
                    cedevaUser.ModifiedAt = null;
                    cedevaUser.ModifiedBy = null;
                }
                else if (entry.State == EntityState.Modified)
                {
                    cedevaUser.ModifiedAt = timestamp;
                    cedevaUser.ModifiedBy = currentUser;

                    // Empêcher écrasement des champs de création
                    entry.Property(nameof(cedevaUser.CreatedAt)).IsModified = false;
                    entry.Property(nameof(cedevaUser.CreatedBy)).IsModified = false;
                }
            }
        }

        return await base.SaveChangesAsync(cancellationToken);
    }

    async Task<int> IUnitOfWork.SaveChangesAsync()
    {
        return await SaveChangesAsync();
    }
}
