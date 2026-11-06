using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Westalife.Domain.Constants;
using Westalife.Domain.Entities;

namespace Westalife.Infrastructure.Persistence.Configurations;

public class BudgetTransferConfiguration : IEntityTypeConfiguration<BudgetTransfer>
{
    public void Configure(EntityTypeBuilder<BudgetTransfer> builder)
    {
        builder.ToTable("BudgetTransfers");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Amount)
            .HasPrecision(DomainConstants.MoneyPrecision, DomainConstants.MoneyScale);
        builder.Property(t => t.Note)
            .HasMaxLength(DomainConstants.Budget.TransferNoteMaxLength);
        builder.HasOne(t => t.FromBudgetItem)
            .WithMany()
            .HasForeignKey(t => t.FromBudgetItemId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(t => t.ToBudgetItem)
            .WithMany()
            .HasForeignKey(t => t.ToBudgetItemId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(t => t.TransferDate)
            .HasDatabaseName("IX_BudgetTransfers_TransferDate");
    }
}
