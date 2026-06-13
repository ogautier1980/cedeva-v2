using Cedeva.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cedeva.Infrastructure.Data.Configurations;

public class ExpenseConfiguration : IEntityTypeConfiguration<Expense>
{
    public void Configure(EntityTypeBuilder<Expense> builder)
    {
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Label)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(e => e.Amount)
            .IsRequired()
            .HasPrecision(10, 2);

        // ActivityId is required (non-nullable), so SET NULL is invalid — deleting an activity that
        // has expenses would fail at runtime. Restrict, consistent with the other financial
        // relationships hanging off Activity (bookings, excursions, transactions).
        builder.HasOne(e => e.Activity)
            .WithMany()
            .HasForeignKey(e => e.ActivityId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.Excursion)
            .WithMany(ex => ex.Expenses)
            .HasForeignKey(e => e.ExcursionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
