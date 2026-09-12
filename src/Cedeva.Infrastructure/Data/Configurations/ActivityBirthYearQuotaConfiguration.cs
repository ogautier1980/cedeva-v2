using Cedeva.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cedeva.Infrastructure.Data.Configurations;

public class ActivityBirthYearQuotaConfiguration : IEntityTypeConfiguration<ActivityBirthYearQuota>
{
    public void Configure(EntityTypeBuilder<ActivityBirthYearQuota> builder)
    {
        builder.HasIndex(q => new { q.ActivityId, q.BirthYear }).IsUnique();

        builder.HasOne(q => q.Activity)
            .WithMany(a => a.BirthYearQuotas)
            .HasForeignKey(q => q.ActivityId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
