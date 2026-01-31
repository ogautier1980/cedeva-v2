using Cedeva.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cedeva.Infrastructure.Data.Configurations;

public class CodaFileConfiguration : IEntityTypeConfiguration<CodaFile>
{
    public void Configure(EntityTypeBuilder<CodaFile> builder)
    {
        builder.HasKey(cf => cf.Id);

        builder.Property(cf => cf.FileName)
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(cf => cf.AccountNumber)
            .HasMaxLength(34) // IBAN max length
            .IsRequired();

        builder.Property(cf => cf.OldBalance)
            .HasPrecision(18, 2);

        builder.Property(cf => cf.NewBalance)
            .HasPrecision(18, 2);

        builder.HasOne(cf => cf.Organisation)
            .WithMany(o => o.CodaFiles)
            .HasForeignKey(cf => cf.OrganisationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(cf => cf.Transactions)
            .WithOne(bt => bt.CodaFile)
            .HasForeignKey(bt => bt.CodaFileId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(cf => cf.OrganisationId);
        builder.HasIndex(cf => cf.ImportDate);
    }
}
