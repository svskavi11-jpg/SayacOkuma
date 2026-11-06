using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Westalife.Domain.Entities;

namespace Westalife.Infrastructure.Persistence.Configurations;

public class BudgetPeriodConfiguration : IEntityTypeConfiguration<BudgetPeriod>
{
    public void Configure(EntityTypeBuilder<BudgetPeriod> builder)
    {
        builder.ToTable("BudgetPeriods");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Year).IsRequired();
        builder.HasIndex(p => p.Year)
            .IsUnique()
            .HasDatabaseName("UX_BudgetPeriods_Year");
    }
}
