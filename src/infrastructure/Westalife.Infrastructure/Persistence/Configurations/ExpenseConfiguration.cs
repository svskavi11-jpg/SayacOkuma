using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Westalife.Domain.Constants;
using Westalife.Domain.Entities;

namespace Westalife.Infrastructure.Persistence.Configurations;

public class ExpenseConfiguration : IEntityTypeConfiguration<Expense>
{
    public void Configure(EntityTypeBuilder<Expense> builder)
    {
        builder.ToTable("Expenses");

        builder.HasKey(e => e.Id);
        TenantOwnedConfiguration.Configure(builder);

        builder.Property(e => e.Amount)
            .HasPrecision(DomainConstants.MoneyPrecision, DomainConstants.MoneyScale);

        builder.Property(e => e.Description)
            .HasMaxLength(DomainConstants.Budget.DescriptionMaxLength);

        builder.Property(e => e.ExpenseType)
            .HasMaxLength(DomainConstants.Budget.ExpenseTypeMaxLength);

        builder.Property(e => e.DocumentNumber)
            .HasMaxLength(DomainConstants.Budget.DocumentNumberMaxLength);

        builder.HasOne(e => e.BudgetItem)
            .WithMany()
            .HasForeignKey(e => e.BudgetItemId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<AidatInvoice>()
            .WithMany()
            .HasForeignKey(e => e.FundAidatInvoiceId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => e.ExpenseDate)
            .HasDatabaseName("IX_Expenses_ExpenseDate");

        builder.HasIndex(e => e.BudgetItemId)
            .HasDatabaseName("IX_Expenses_BudgetItemId");

        builder.HasOne(e => e.BankAccount)
            .WithMany()
            .HasForeignKey(e => e.BankAccountId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => e.BankAccountId)
            .HasDatabaseName("IX_Expenses_BankAccountId");
    }
}
