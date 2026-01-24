using Cedeva.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cedeva.Infrastructure.Data.Configurations;

public class BookingConfiguration : IEntityTypeConfiguration<Booking>
{
    public void Configure(EntityTypeBuilder<Booking> builder)
    {
        builder.HasKey(b => b.Id);

        builder.HasOne(b => b.Child)
            .WithMany(c => c.Bookings)
            .HasForeignKey(b => b.ChildId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(b => b.Group)
            .WithMany(g => g.Bookings)
            .HasForeignKey(b => b.GroupId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(b => b.Days)
            .WithOne(d => d.Booking)
            .HasForeignKey(d => d.BookingId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(b => b.QuestionAnswers)
            .WithOne(a => a.Booking)
            .HasForeignKey(a => a.BookingId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
