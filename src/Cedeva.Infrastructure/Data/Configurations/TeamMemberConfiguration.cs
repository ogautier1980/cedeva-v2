using Cedeva.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cedeva.Infrastructure.Data.Configurations;

public class TeamMemberConfiguration : IEntityTypeConfiguration<TeamMember>
{
    public void Configure(EntityTypeBuilder<TeamMember> builder)
    {
        builder.HasKey(t => t.TeamMemberId);

        builder.Property(t => t.FirstName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(t => t.LastName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(t => t.Email)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(t => t.MobilePhoneNumber)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(t => t.NationalRegisterNumber)
            .IsRequired()
            .HasMaxLength(15);

        builder.Property(t => t.LicenseUrl)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(t => t.DailyCompensation)
            .HasPrecision(10, 2);

        builder.HasOne(t => t.Address)
            .WithMany()
            .HasForeignKey(t => t.AddressId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(t => t.Expenses)
            .WithOne(e => e.TeamMember)
            .HasForeignKey(e => e.TeamMemberId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Ignore(t => t.FullName);
    }
}
