using Cedeva.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cedeva.Infrastructure.Data.Configurations;

public class ActivityFinancialTransactionConfiguration : IEntityTypeConfiguration<ActivityFinancialTransaction>
{
    public void Configure(EntityTypeBuilder<ActivityFinancialTransaction> builder)
    {
        builder.HasKey(aft => aft.Id);

        builder.Property(aft => aft.Amount)
            .HasPrecision(10, 2)
            .IsRequired();

        builder.Property(aft => aft.Description)
            .HasMaxLength(500)
            .IsRequired();

        builder.HasOne(aft => aft.Activity)
            .WithMany()
            .HasForeignKey(aft => aft.ActivityId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(aft => aft.Payment)
            .WithMany()
            .HasForeignKey(aft => aft.PaymentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(aft => aft.Expense)
            .WithMany()
            .HasForeignKey(aft => aft.ExpenseId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(aft => aft.ActivityId);
        builder.HasIndex(aft => aft.TransactionDate);
    }
}
