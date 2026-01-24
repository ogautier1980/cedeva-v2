using Cedeva.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cedeva.Infrastructure.Data.Configurations;

public class OrganisationConfiguration : IEntityTypeConfiguration<Organisation>
{
    public void Configure(EntityTypeBuilder<Organisation> builder)
    {
        builder.HasKey(o => o.Id);

        builder.Property(o => o.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(o => o.Description)
            .IsRequired()
            .HasMaxLength(500);

        builder.HasOne(o => o.Address)
            .WithMany()
            .HasForeignKey(o => o.AddressId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(o => o.Activities)
            .WithOne(a => a.Organisation)
            .HasForeignKey(a => a.OrganisationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(o => o.Parents)
            .WithOne(p => p.Organisation)
            .HasForeignKey(p => p.OrganisationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(o => o.TeamMembers)
            .WithOne(t => t.Organisation)
            .HasForeignKey(t => t.OrganisationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(o => o.Users)
            .WithOne(u => u.Organisation)
            .HasForeignKey(u => u.OrganisationId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
