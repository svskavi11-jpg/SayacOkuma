using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Westalife.Domain.Constants;
using Westalife.Domain.Entities;

namespace Westalife.Infrastructure.Persistence.Configurations;

public class BudgetItemConfiguration : IEntityTypeConfiguration<BudgetItem>
{
    public void Configure(EntityTypeBuilder<BudgetItem> builder)
    {
        builder.ToTable("BudgetItems");

        builder.HasKey(b => b.Id);
        TenantOwnedConfiguration.Configure(builder);

        builder.Property(b => b.Year).IsRequired();

        builder.Property(b => b.Category)
            .IsRequired()
            .HasMaxLength(DomainConstants.Budget.CategoryMaxLength);

        builder.Property(b => b.EstimatedAmount)
            .HasPrecision(DomainConstants.MoneyPrecision, DomainConstants.MoneyScale);

        builder.HasIndex(b => new { b.Year, b.Category })
            .IsUnique()
            .HasDatabaseName("UX_BudgetItems_Year_Category");
    }
}
