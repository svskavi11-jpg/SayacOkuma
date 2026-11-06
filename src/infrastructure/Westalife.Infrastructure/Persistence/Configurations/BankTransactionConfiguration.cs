using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Westalife.Domain.Constants;
using Westalife.Domain.Entities;

namespace Westalife.Infrastructure.Persistence.Configurations;

public class BankTransactionConfiguration : IEntityTypeConfiguration<BankTransaction>
{
    public void Configure(EntityTypeBuilder<BankTransaction> builder)
    {
        builder.ToTable("BankTransactions");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Type).HasConversion<int>();
        builder.Property(t => t.Amount)
            .HasPrecision(DomainConstants.MoneyPrecision, DomainConstants.MoneyScale);
        builder.Property(t => t.Description)
            .HasMaxLength(DomainConstants.BankTransaction.DescriptionMaxLength);

        builder.HasOne(t => t.BankAccount)
            .WithMany()
            .HasForeignKey(t => t.BankAccountId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(t => t.TargetBankAccount)
            .WithMany()
            .HasForeignKey(t => t.TargetBankAccountId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(t => t.BankAccountId).HasDatabaseName("IX_BankTransactions_BankAccountId");
        builder.HasIndex(t => t.TargetBankAccountId).HasDatabaseName("IX_BankTransactions_TargetBankAccountId");
        builder.HasIndex(t => t.TransferGroupId).HasDatabaseName("IX_BankTransactions_TransferGroupId");
    }
}
