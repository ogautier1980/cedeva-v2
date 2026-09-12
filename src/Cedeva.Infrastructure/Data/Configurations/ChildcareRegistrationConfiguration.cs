using Cedeva.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cedeva.Infrastructure.Data.Configurations;

public class ChildcareRegistrationConfiguration : IEntityTypeConfiguration<ChildcareRegistration>
{
    public void Configure(EntityTypeBuilder<ChildcareRegistration> builder)
    {
        builder.HasIndex(r => new { r.BookingId, r.ActivityDayId }).IsUnique();

        builder.HasOne(r => r.Booking)
            .WithMany()
            .HasForeignKey(r => r.BookingId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(r => r.ActivityDay)
            .WithMany()
            .HasForeignKey(r => r.ActivityDayId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
