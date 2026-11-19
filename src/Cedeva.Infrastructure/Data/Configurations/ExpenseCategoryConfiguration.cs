using Cedeva.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cedeva.Infrastructure.Data.Configurations;

public class ExpenseCategoryConfiguration : IEntityTypeConfiguration<ExpenseCategory>
{
    public void Configure(EntityTypeBuilder<ExpenseCategory> builder)
    {
        // Plain FK index, declared explicitly: otherwise EF treats the filtered index below as
        // covering the foreign key and drops it, though that one only holds the P.A.F. rows.
        builder.HasIndex(c => c.OrganisationId);

        // At most one P.A.F. category per organisation (RegistrationFeesCategory.EnsureAsync relies on it).
        builder.HasIndex(c => c.OrganisationId, "IX_ExpenseCategories_OrganisationId_RegistrationFees")
            .IsUnique()
            .HasFilter("\"IsRegistrationFees\"");
    }
}
