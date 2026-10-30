using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Westalife.Domain.Constants;
using Westalife.Domain.Entities;

namespace Westalife.Infrastructure.Persistence.Configurations;

public class AidatInvoiceConfiguration : IEntityTypeConfiguration<AidatInvoice>
{
    public void Configure(EntityTypeBuilder<AidatInvoice> builder)
    {
        builder.ToTable("AidatInvoices");

        builder.HasKey(i => i.Id);

        builder.Property(i => i.PeriodStart).IsRequired();
        builder.Property(i => i.PeriodEnd).IsRequired();
        builder.Property(i => i.DueDate).IsRequired();
        builder.Property(i => i.DistributionMethod).IsRequired();

        builder.Property(i => i.TotalAmount)
            .HasPrecision(DomainConstants.MoneyPrecision, DomainConstants.MoneyScale);

        builder.Property(i => i.Note)
            .HasMaxLength(DomainConstants.AidatInvoice.NoteMaxLength);

        // Dönem listesi sorguları için dönem indeksi; vade sıralaması için vade indeksi.
        builder.HasIndex(i => new { i.PeriodStart, i.PeriodEnd })
            .HasDatabaseName("IX_AidatInvoices_Period");

        builder.HasIndex(i => i.DueDate)
            .HasDatabaseName("IX_AidatInvoices_DueDate");
    }
}
