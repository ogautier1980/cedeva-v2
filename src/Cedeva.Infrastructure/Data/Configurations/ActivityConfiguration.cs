using Cedeva.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cedeva.Infrastructure.Data.Configurations;

public class ActivityConfiguration : IEntityTypeConfiguration<Activity>
{
    public void Configure(EntityTypeBuilder<Activity> builder)
    {
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(a => a.Description)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(a => a.PricePerDay)
            .HasPrecision(10, 2);

        builder.HasMany(a => a.Days)
            .WithOne(d => d.Activity)
            .HasForeignKey(d => d.ActivityId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(a => a.Groups)
            .WithOne(g => g.Activity)
            .HasForeignKey(g => g.ActivityId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(a => a.AdditionalQuestions)
            .WithOne(q => q.Activity)
            .HasForeignKey(q => q.ActivityId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(a => a.Bookings)
            .WithOne(b => b.Activity)
            .HasForeignKey(b => b.ActivityId)
            .OnDelete(DeleteBehavior.Restrict);

        // Many-to-Many with Children
        builder.HasMany(a => a.Children)
            .WithMany(c => c.Activities)
            .UsingEntity(j => j.ToTable("ActivityChildren"));

        // Many-to-Many with TeamMembers, through ActivityTeamMember (status and daily rate per
        // activity). Same table and key columns as the former anonymous join table.
        builder.HasMany(a => a.TeamMembers)
            .WithMany(t => t.Activities)
            .UsingEntity<ActivityTeamMember>(
                j => j.HasOne(x => x.TeamMember)
                      .WithMany(t => t.ActivityAssignments)
                      .HasForeignKey(x => x.TeamMemberId)
                      .OnDelete(DeleteBehavior.Restrict),
                j => j.HasOne(x => x.Activity)
                      .WithMany(a => a.TeamAssignments)
                      .HasForeignKey(x => x.ActivityId)
                      .OnDelete(DeleteBehavior.Cascade),
                j =>
                {
                    j.ToTable("ActivityTeamMembers");
                    j.HasKey(x => new { x.ActivityId, x.TeamMemberId });
                    j.Property(x => x.ActivityId).HasColumnName("ActivitiesId");
                    j.Property(x => x.TeamMemberId).HasColumnName("TeamMembersTeamMemberId");
                    j.Property(x => x.DailyCompensation).HasPrecision(10, 2);
                });
    }
}
