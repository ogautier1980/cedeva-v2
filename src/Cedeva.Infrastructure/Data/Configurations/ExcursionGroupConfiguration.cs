using Cedeva.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cedeva.Infrastructure.Data.Configurations;

public class ExcursionGroupConfiguration : IEntityTypeConfiguration<ExcursionGroup>
{
    public void Configure(EntityTypeBuilder<ExcursionGroup> builder)
    {
        // Composite primary key
        builder.HasKey(eg => new { eg.ExcursionId, eg.ActivityGroupId });

        builder.HasOne(eg => eg.Excursion)
            .WithMany(e => e.ExcursionGroups)
            .HasForeignKey(eg => eg.ExcursionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(eg => eg.ActivityGroup)
            .WithMany()
            .HasForeignKey(eg => eg.ActivityGroupId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
