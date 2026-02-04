using Cedeva.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cedeva.Infrastructure.Data.Configurations;

public class ExcursionTeamMemberConfiguration : IEntityTypeConfiguration<ExcursionTeamMember>
{
    public void Configure(EntityTypeBuilder<ExcursionTeamMember> builder)
    {
        builder.HasKey(etm => etm.Id);

        builder.Property(etm => etm.IsAssigned)
            .IsRequired();

        builder.Property(etm => etm.IsPresent)
            .IsRequired();

        builder.Property(etm => etm.Notes)
            .HasMaxLength(500);

        builder.HasOne(etm => etm.Excursion)
            .WithMany(e => e.TeamMembers)
            .HasForeignKey(etm => etm.ExcursionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(etm => etm.TeamMember)
            .WithMany()
            .HasForeignKey(etm => etm.TeamMemberId)
            .OnDelete(DeleteBehavior.Restrict);

        // Unique constraint: un membre d'équipe ne peut être assigné qu'une fois par excursion
        builder.HasIndex(etm => new { etm.ExcursionId, etm.TeamMemberId })
            .IsUnique();
    }
}
