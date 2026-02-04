using Cedeva.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cedeva.Infrastructure.Data.Configurations;

public class ExcursionRegistrationConfiguration : IEntityTypeConfiguration<ExcursionRegistration>
{
    public void Configure(EntityTypeBuilder<ExcursionRegistration> builder)
    {
        builder.HasKey(er => er.Id);

        builder.HasOne(er => er.Excursion)
            .WithMany(e => e.Registrations)
            .HasForeignKey(er => er.ExcursionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(er => er.Booking)
            .WithMany()
            .HasForeignKey(er => er.BookingId)
            .OnDelete(DeleteBehavior.Restrict);

        // Unique constraint: prevent duplicate registrations
        builder.HasIndex(er => new { er.ExcursionId, er.BookingId })
            .IsUnique();
    }
}
