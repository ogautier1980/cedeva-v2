using Cedeva.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cedeva.Infrastructure.Data.Configurations;

public class ChildConfiguration : IEntityTypeConfiguration<Child>
{
    public void Configure(EntityTypeBuilder<Child> builder)
    {
        builder.HasKey(c => c.Id);

        builder.Property(c => c.FirstName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(c => c.LastName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(c => c.NationalRegisterNumber)
            .IsRequired()
            .HasMaxLength(15);

        builder.HasOne(c => c.ActivityGroup)
            .WithMany(g => g.Children)
            .HasForeignKey(c => c.ActivityGroupId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.Ignore(c => c.FullName);
    }
}
