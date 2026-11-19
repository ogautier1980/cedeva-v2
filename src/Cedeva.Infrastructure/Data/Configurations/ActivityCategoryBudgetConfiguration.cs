using Cedeva.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cedeva.Infrastructure.Data.Configurations;

public class ActivityCategoryBudgetConfiguration : IEntityTypeConfiguration<ActivityCategoryBudget>
{
    public void Configure(EntityTypeBuilder<ActivityCategoryBudget> builder)
    {
        builder.HasIndex(b => new { b.ActivityId, b.ExpenseCategoryId }).IsUnique();
        builder.Property(b => b.Amount).HasPrecision(10, 2);

        builder.HasOne(b => b.Activity)
            .WithMany()
            .HasForeignKey(b => b.ActivityId)
            .OnDelete(DeleteBehavior.Cascade);

        // Deleting a category drops its budget lines in every activity.
        builder.HasOne(b => b.ExpenseCategory)
            .WithMany()
            .HasForeignKey(b => b.ExpenseCategoryId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
