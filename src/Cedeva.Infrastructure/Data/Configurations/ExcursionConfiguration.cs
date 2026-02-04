using Cedeva.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cedeva.Infrastructure.Data.Configurations;

public class ExcursionConfiguration : IEntityTypeConfiguration<Excursion>
{
    public void Configure(EntityTypeBuilder<Excursion> builder)
    {
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Cost)
            .HasPrecision(10, 2);

        builder.HasOne(e => e.Activity)
            .WithMany()
            .HasForeignKey(e => e.ActivityId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(e => e.Registrations)
            .WithOne(r => r.Excursion)
            .HasForeignKey(r => r.ExcursionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(e => e.Expenses)
            .WithOne(ex => ex.Excursion)
            .HasForeignKey(ex => ex.ExcursionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
