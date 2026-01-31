using Cedeva.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cedeva.Infrastructure.Data.Configurations;

public class BankTransactionConfiguration : IEntityTypeConfiguration<BankTransaction>
{
    public void Configure(EntityTypeBuilder<BankTransaction> builder)
    {
        builder.HasKey(bt => bt.Id);

        builder.Property(bt => bt.Amount)
            .HasPrecision(10, 2)
            .IsRequired();

        builder.Property(bt => bt.StructuredCommunication)
            .HasMaxLength(20);

        builder.Property(bt => bt.FreeCommunication)
            .HasMaxLength(500);

        builder.Property(bt => bt.CounterpartyName)
            .HasMaxLength(200);

        builder.Property(bt => bt.CounterpartyAccount)
            .HasMaxLength(34); // IBAN max length

        builder.Property(bt => bt.TransactionCode)
            .HasMaxLength(10)
            .IsRequired();

        builder.HasOne(bt => bt.Organisation)
            .WithMany(o => o.BankTransactions)
            .HasForeignKey(bt => bt.OrganisationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(bt => bt.CodaFile)
            .WithMany(cf => cf.Transactions)
            .HasForeignKey(bt => bt.CodaFileId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(bt => bt.StructuredCommunication);
        builder.HasIndex(bt => bt.OrganisationId);
        builder.HasIndex(bt => bt.IsReconciled);
    }
}
