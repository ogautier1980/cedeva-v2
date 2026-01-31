using Cedeva.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cedeva.Infrastructure.Data.Configurations;

public class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Amount)
            .HasPrecision(10, 2)
            .IsRequired();

        builder.Property(p => p.StructuredCommunication)
            .HasMaxLength(20);

        builder.Property(p => p.Reference)
            .HasMaxLength(200);

        builder.HasOne(p => p.Booking)
            .WithMany(b => b.Payments)
            .HasForeignKey(p => p.BookingId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(p => p.BankTransaction)
            .WithOne(bt => bt.Payment)
            .HasForeignKey<Payment>(p => p.BankTransactionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(p => p.BookingId);
    }
}
