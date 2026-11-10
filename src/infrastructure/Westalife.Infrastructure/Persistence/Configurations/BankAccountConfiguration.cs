using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Westalife.Domain.Constants;
using Westalife.Domain.Entities;

namespace Westalife.Infrastructure.Persistence.Configurations;

public class BankAccountConfiguration : IEntityTypeConfiguration<BankAccount>
{
    public void Configure(EntityTypeBuilder<BankAccount> builder)
    {
        builder.ToTable("BankAccounts");
        builder.HasKey(a => a.Id);
        TenantOwnedConfiguration.Configure(builder);

        builder.Property(a => a.BankName)
            .IsRequired()
            .HasMaxLength(DomainConstants.BankAccount.BankNameMaxLength);

        builder.Property(a => a.AccountName)
            .IsRequired()
            .HasMaxLength(DomainConstants.BankAccount.AccountNameMaxLength);

        builder.Property(a => a.AccountNumber)
            .IsRequired()
            .HasMaxLength(DomainConstants.BankAccount.AccountNumberMaxLength);

        builder.Property(a => a.Iban)
            .HasMaxLength(DomainConstants.BankAccount.IbanLength);

        builder.Property(a => a.OpeningBalance)
            .HasPrecision(DomainConstants.MoneyPrecision, DomainConstants.MoneyScale);

        builder.HasIndex(a => a.AccountNumber)
            .IsUnique()
            .HasDatabaseName("IX_BankAccounts_AccountNumber");

        builder.HasIndex(a => a.Iban)
            .IsUnique()
            .HasFilter("\"Iban\" IS NOT NULL")
            .HasDatabaseName("IX_BankAccounts_Iban");
    }
}