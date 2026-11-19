using Cedeva.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cedeva.Infrastructure.Data.Configurations;

public class FiscalYearSettingConfiguration : IEntityTypeConfiguration<FiscalYearSetting>
{
    public void Configure(EntityTypeBuilder<FiscalYearSetting> builder)
    {
        builder.HasIndex(s => s.Year).IsUnique();
        builder.Property(s => s.MaxDailyAmount).HasPrecision(10, 2);

        // 2025 figures from the model attestation the client sent (19/11/2026).
        builder.HasData(new FiscalYearSetting
        {
            Id = 1,
            Year = 2025,
            MaxDailyAmount = 16.90m,
            TaxCode = "1384",
            CreatedAt = new DateTime(2026, 11, 19, 0, 0, 0, DateTimeKind.Utc),
            CreatedBy = "System"
        });
    }
}
