using Cedeva.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cedeva.Infrastructure.Data.Configurations;

public class ActivityDayConfiguration : IEntityTypeConfiguration<ActivityDay>
{
    public void Configure(EntityTypeBuilder<ActivityDay> builder)
    {
        builder.HasKey(d => d.DayId);

        builder.Property(d => d.Label)
            .IsRequired()
            .HasMaxLength(100);

        builder.HasMany(d => d.BookingDays)
            .WithOne(bd => bd.ActivityDay)
            .HasForeignKey(bd => bd.ActivityDayId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
