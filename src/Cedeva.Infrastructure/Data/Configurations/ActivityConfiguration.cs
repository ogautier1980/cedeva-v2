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

        // Many-to-Many with TeamMembers
        builder.HasMany(a => a.TeamMembers)
            .WithMany(t => t.Activities)
            .UsingEntity<Dictionary<string, object>>(
                "ActivityTeamMembers",
                j => j.HasOne<TeamMember>()
                      .WithMany()
                      .HasForeignKey("TeamMembersTeamMemberId")
                      .OnDelete(DeleteBehavior.Restrict),
                j => j.HasOne<Activity>()
                      .WithMany()
                      .HasForeignKey("ActivitiesId")
                      .OnDelete(DeleteBehavior.Cascade));
    }
}
