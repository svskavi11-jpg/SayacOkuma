using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Westalife.Domain.Constants;
using Westalife.Domain.Entities;

namespace Westalife.Infrastructure.Persistence.Configurations;

public class IncomeItemConfiguration : IEntityTypeConfiguration<IncomeItem>
{
    public void Configure(EntityTypeBuilder<IncomeItem> builder)
    {
        builder.ToTable("IncomeItems");
        builder.HasKey(i => i.Id);
        TenantOwnedConfiguration.Configure(builder);
        builder.Property(i => i.Year).IsRequired();
        builder.Property(i => i.Category)
            .IsRequired()
            .HasMaxLength(DomainConstants.Budget.CategoryMaxLength);
        builder.Property(i => i.EstimatedAmount)
            .HasPrecision(DomainConstants.MoneyPrecision, DomainConstants.MoneyScale);
        builder.HasIndex(i => new { i.Year, i.Category })
            .IsUnique()
            .HasDatabaseName("UX_IncomeItems_Year_Category");
    }
}
